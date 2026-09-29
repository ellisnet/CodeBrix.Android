using System;
using System.Threading;
using CodeBrix.Android.UI.Diagnostics;
using CodeBrix.Android.UI.Logging;
using CodeBrix.Android.UI.Projection;
using CodeBrix.Platform.Extensions;
using CodeBrix.Platform.UI.Adapter.Microsoft.Extensions.Logging;
using CodeBrix.Platform.UI.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using AApplication = global::Android.App.Application;
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
/// app's OnLaunched).
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

        global::CodeBrix.Android.UI.Android.AndroidPlatformBootstrap.EnsureRegistered();
        RegisterActivityLifecycleCallbacks(new ApplicationLifecycleTracker());

        if (_log.IsEnabled(LogLevel.Information))
        {
            _log.LogInformation("CodeBrix Android application created ({Package}); platform bootstrap registered.", PackageName);
        }
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
