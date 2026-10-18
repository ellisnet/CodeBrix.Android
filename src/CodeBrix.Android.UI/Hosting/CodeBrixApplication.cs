using System;
using System.Threading;
using CodeBrix.Android.UI.Diagnostics;
using CodeBrix.Android.UI.Logging;
using CodeBrix.Android.UI.Portable;
using CodeBrix.Android.UI.Projection;
using CodeBrix.Platform.Extensions;
using CodeBrix.Platform.UI.Adapter.Microsoft.Extensions.Logging;
using CodeBrix.Platform.UI.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using AApplication = global::Android.App.Application;
using AIntent = global::Android.Content.Intent;
using JniHandleOwnership = global::Android.Runtime.JniHandleOwnership;
using MUX = Microsoft.UI.Xaml;

namespace CodeBrix.Android.UI.Hosting;

/// <summary>
/// The Android Application of a CodeBrix app. The app's head derives from it and creates its
/// pasted XAML application:
/// <code>
/// [Application]
/// public class MainApplication : CodeBrixApplication
/// {
///     public MainApplication(IntPtr handle, JniHandleOwnership transfer) : base(handle, transfer) { }
///     protected override Microsoft.UI.Xaml.Application CreateApp() =&gt; new App();
/// }
/// </code>
/// OnCreate configures logging (logcat) and runs the platform bootstrap (every CodeBrix
/// Core contract gets its Android implementation) BEFORE any XAML type is used; the first
/// <see cref="CodeBrixActivity"/> then starts the XAML application (Application.Start, the
/// app's OnLaunched). Later launches of the running app arrive in <see cref="OnLaunchIntent"/>.
/// </summary>
public abstract class CodeBrixApplication : AApplication
{
    private ILogger _log;
    private int _started;
    private ProjectionViewer _projectionViewer;

    /// <summary>The constructor Android calls.</summary>
    /// <param name="handle">The Java peer handle.</param>
    /// <param name="transfer">The handle ownership.</param>
    protected CodeBrixApplication(IntPtr handle, JniHandleOwnership transfer)
        : base(handle, transfer)
    {
    }

    /// <summary>
    /// When true, a text dump of the window's visual tree (element types, texts, layout
    /// slots) is written to logcat after each layout pass that changed it. Default false.
    /// </summary>
    protected virtual bool LogVisualTreeAfterLayout => false;

    /// <summary>
    /// When true, the AP1 projection viewer additionally mirrors the window's visual tree into
    /// the activity (text as text views, backgrounds as boxes, other elements as labelled
    /// placeholders) OVER the native element views. Diagnostic scaffolding only: since AP2 the
    /// element handlers show the tree natively, so the default is false.
    /// </summary>
    protected virtual bool UseProjectionViewer => false;

    /// <summary>
    /// When true, opts the whole application into direct GPU rendering: add-ins that produce GPU content present it
    /// directly where Android offers a way (the WebGpu add-in's WebGpuRenderView presents to a SurfaceView's window
    /// surface - the DirectSurface render path), instead of reading frames back to the processor. - EXPERIMENTAL
    /// </summary>
    /// <remarks>
    /// The Android equivalent of the desktop heads' <c>UseDirectGpuRenderingMode()</c> host-builder call, read once in
    /// <see cref="OnCreate"/>. It is a one-way, app-wide latch (<see cref="DirectGpuRenderingMode"/>): it cannot be turned
    /// off, and there is no per-element override - the whole app either runs in this mode or does not. An add-in that
    /// cannot use the direct path (or anything on it fails) falls back to its read-back path on its own. If this is not
    /// overridden, nothing changes. Default false.
    /// </remarks>
    protected virtual bool UseDirectGpuRenderingMode => false;

    /// <summary>
    /// When true, font resolution is confined to the fonts the application ships (its ms-appx fonts, the CodeBrix font
    /// packages it references, its FeatureConfiguration.Font.FallbackFontFamilies and the framework's symbols font):
    /// the device's /system/fonts are not consulted for glyphs the application's fonts lack - such a character shows the
    /// font's missing-glyph box. A parity-testing tool: Android's system fonts are stable, apps do not normally need it.
    /// </summary>
    /// <remarks>
    /// The Android equivalent of the desktop heads' <c>RestrictToEmbeddedFonts()</c> host-builder call, read once in
    /// <see cref="OnCreate"/> before any text is measured. It sets the ordinary flag
    /// <c>FeatureConfiguration.Font.RestrictToEmbeddedFonts</c> (not a latch: the app may also set that flag directly,
    /// before the first text is measured; resolved fonts are cached). It governs the TextLayout add-in's text engine,
    /// whose per-glyph fallback otherwise walks /system/fonts. Text that Android's own text stack draws (the native
    /// views of TextBlock, TextBox and the other controls) keeps Android's per-glyph fallback: a typeface loaded from the
    /// app's font file always carries the system fallback chain, and Android's public API offers no way to remove it.
    /// Default false.
    /// </remarks>
    protected virtual bool RestrictToEmbeddedFonts => false;

    /// <summary>
    /// When true, the Material 3 colour roles - the baseline Material palette, or the wallpaper's dynamic palette where
    /// the device offers one - are written into the framework's Fluent theme keys that the application does not define
    /// itself (tonal default Button, filled AccentButtonStyle, Material surface, text and outline colours), so an app
    /// that sets no control keys looks Material 3. Every Fluent key the application defines always wins. Default false:
    /// the app shows the framework's Fluent colours, the same look as on the desktop heads.
    /// </summary>
    /// <remarks>
    /// Read once, right after the XAML Application is created (its resources are where the roles go); a day/night flip
    /// re-derives the roles, and an explicit <c>Application.RequestedTheme</c> sets the AppCompat night mode either way.
    /// </remarks>
    protected virtual bool UseMaterialPalette => false;

    private SoftInputAdjust? _softInputAdjust;

    /// <summary>
    /// How the app's windows make room for the soft keyboard (MAUI's WindowSoftInputModeAdjust):
    /// <see cref="Hosting.SoftInputAdjust.Pan"/> (the default) pans the window so the focused text field stays visible;
    /// <see cref="Hosting.SoftInputAdjust.Resize"/> lays the page out again above the keyboard;
    /// <see cref="Hosting.SoftInputAdjust.Unspecified"/> leaves it to the system. Until the app sets it, an activity that
    /// declares its own adjust mode ([Activity(WindowSoftInputMode = ...)]) keeps that mode and every other activity
    /// pans; once set, it applies to every activity. Setting it while the app runs re-applies it at once (main thread).
    /// InputPane.OccludedRect reports the keyboard in every mode. Set it in the application's constructor or OnCreate to
    /// have it from the first frame.
    /// </summary>
    public SoftInputAdjust SoftInputAdjust
    {
        get => _softInputAdjust ?? SoftInputAdjust.Pan;
        set
        {
            _softInputAdjust = value;
            foreach (var activity in ActivityRegistry.All)
            {
                activity.ApplySoftInputAdjust();
            }
        }
    }

    /// <summary>Forgets the app-level setting (back to the default: declared modes kept, else Pan) and re-applies it.</summary>
    internal void ResetSoftInputAdjust()
    {
        _softInputAdjust = null;
        foreach (var activity in ActivityRegistry.All)
        {
            activity.ApplySoftInputAdjust();
        }
    }

    /// <summary>The app-level setting, or null while the app never set one (declared modes are then kept).</summary>
    internal SoftInputAdjust? ExplicitSoftInputAdjust => _softInputAdjust;

    /// <summary>The projection viewer, while <see cref="UseProjectionViewer"/> is on (null otherwise).</summary>
    internal ProjectionViewer ProjectionViewer => _projectionViewer;

    /// <summary>The logcat tag of the application log (default: the application's package name).</summary>
    protected virtual string LogTag => PackageName;

    /// <inheritdoc />
    public override void OnCreate()
    {
        base.OnCreate();

        ConfigureLoggingCore();
        _log = HostLog.For("CodeBrix.Android.UI.Hosting.Application");

        // Before any XAML type exists: the add-ins read the latch when their views get handlers and devices.
        if (DirectGpuRenderingOptIn.Apply(UseDirectGpuRenderingMode) && _log.IsEnabled(LogLevel.Information))
        {
            _log.LogInformation("Direct GPU rendering is enabled for this application (EXPERIMENTAL).");
        }

        // Before any text is measured: the font sources read the flag when they resolve a font.
        if (EmbeddedFontsOptIn.Apply(RestrictToEmbeddedFonts) && _log.IsEnabled(LogLevel.Information))
        {
            _log.LogInformation("Font resolution is restricted to the application's embedded fonts.");
        }

        global::CodeBrix.Android.UI.Android.AndroidPlatformBootstrap.EnsureRegistered();
        RegisterActivityLifecycleCallbacks(new ApplicationLifecycleTracker());

        if (_log.IsEnabled(LogLevel.Information))
        {
            _log.LogInformation("CodeBrix Android application created ({Package}); platform bootstrap registered.", PackageName);
        }
    }

    /// <summary>
    /// Called on the main thread when a launch reaches the RUNNING application: the launcher icon tapped after the app
    /// was started another way, a deep link, a notification tap, an <c>am start</c> - any intent Android delivers to
    /// the single-task main activity (its OnNewIntent), the intent of a second activity instance the host
    /// redirected to the running one, and the intent of a new main activity a launch created because the previous
    /// one was gone while the application lived (Back on the root page finishes it on API 36 and later; the app's own
    /// Window.Close finishes it too) - delivered once the window shows in that activity. The intent that started the
    /// app is not repeated here: it is the main activity's <c>Intent</c> when the XAML application starts; nor is the
    /// intent of an activity Android re-created on its own (with a saved state). The intent may be the launcher's
    /// plain MAIN intent; look at its action, data and extras. The default does nothing.
    /// </summary>
    /// <param name="intent">The launch intent (also the main activity's <c>Intent</c> from now on).</param>
    protected virtual void OnLaunchIntent(AIntent intent)
    {
    }

    /// <summary>Surfaces a launch intent of the running application (<see cref="OnLaunchIntent"/>).</summary>
    internal void DeliverLaunchIntent(AIntent intent)
    {
        if (_log?.IsEnabled(LogLevel.Information) == true)
        {
            _log.LogInformation("A launch reached the running application (action {Action}).", intent.Action ?? "none");
        }

        OnLaunchIntent(intent);
    }

    /// <summary>Creates the app's XAML Application (typically <c>new App()</c>).</summary>
    protected abstract MUX.Application CreateApp();

    /// <summary>
    /// Configures application logging. The default writes Information and above to logcat
    /// (with <see cref="LogTag"/>); override to change levels or add providers.
    /// </summary>
    /// <param name="builder">The logging builder (the logcat provider is already added).</param>
    protected virtual void ConfigureLogging(ILoggingBuilder builder)
    {
        builder.SetMinimumLevel(LogLevel.Information);
    }

    /// <summary>
    /// Starts the XAML application once (Application.Start -> the app's constructor ->
    /// OnLaunched). Called by the first <see cref="CodeBrixActivity"/>.
    /// </summary>
    internal void EnsureXamlApplicationStarted(CodeBrixActivity activity)
    {
        if (Interlocked.Exchange(ref _started, 1) == 1)
        {
            return;
        }

        if (_log.IsEnabled(LogLevel.Information))
        {
            _log.LogInformation("Starting the XAML application from {Activity}.", activity.GetType().Name);
        }

        MUX.Application.Start(_ =>
        {
            var app = CreateApp();
            if (_log.IsEnabled(LogLevel.Information))
            {
                _log.LogInformation("XAML application {App} created.", app?.GetType().FullName);
            }
        });

        // The theme bridge's posted installation ran before the application existed: complete it now, and write the
        // Material roles when the app opted in (UseMaterialPalette).
        Policy.ThemeBridge.OnXamlApplicationStarted(UseMaterialPalette);
        if (UseMaterialPalette && _log.IsEnabled(LogLevel.Information))
        {
            _log.LogInformation("Material palette opted in (UseMaterialPalette): the Material 3 roles colour the framework's Fluent keys the app does not define.");
        }

        if (UseProjectionViewer)
        {
            AttachProjectionViewer();
        }

        if (LogVisualTreeAfterLayout)
        {
            AttachVisualTreeLogging();
        }
    }

    private void ConfigureLoggingCore()
    {
        var provider = new LogcatLoggerProvider(LogTag);
        LogExtensionPoint.AmbientLoggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddProvider(provider);
            ConfigureLogging(builder);
        });
        LoggingAdapter.Initialize();
    }

    private void AttachProjectionViewer()
    {
        var wrapper = NativeWindowFactoryAndroidExtension.Instance.MainWindow;
        if (wrapper?.XamlRoot is { } xamlRoot && XamlRootMap.GetHostForRoot(xamlRoot) is AndroidXamlRootHost host)
        {
            _projectionViewer = new ProjectionViewer(host);
            if (_log.IsEnabled(LogLevel.Information))
            {
                _log.LogInformation("Projection viewer attached (the AP1 stand-in for element handlers).");
            }
        }
    }

    private void AttachVisualTreeLogging()
    {
        var wrapper = NativeWindowFactoryAndroidExtension.Instance.MainWindow;
        if (wrapper?.XamlRoot is not { } xamlRoot || XamlRootMap.GetHostForRoot(xamlRoot) is not AndroidXamlRootHost host)
        {
            return;
        }

        var treeLog = HostLog.For("CodeBrix.Android.UI.VisualTree");
        string lastDump = null;
        host.LayoutUpdated += (_, _) =>
        {
            var lines = VisualTreeDump.Dump(host.RootElement);
            var dump = string.Join("\n", lines);
            if (dump == lastDump)
            {
                return;
            }

            lastDump = dump;
            treeLog.LogInformation("Visual tree after layout ({Count} elements):", lines.Count);
            foreach (var line in lines)
            {
                treeLog.LogInformation("  {Line}", line);
            }

            // The native side, once Android has laid the views out (next frame).
            host.Wrapper.Activity?.RootLayout?.Post(() =>
            {
                var native = NativeViewDump.Dump(host.Wrapper.Activity?.RootLayout?.ContentLayer);
                treeLog.LogInformation("Native views after layout ({Count} views, {Handlers} handlers created):", native.Count, global::CodeBrix.Android.UI.Handlers.CodeBrixHandlers.Factory.CreatedCount);
                foreach (var line in native)
                {
                    treeLog.LogInformation("  {Line}", line);
                }
            });
        };
    }
}
