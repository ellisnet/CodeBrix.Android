using System.Runtime.CompilerServices;
using CodeBrix.Android.UI.Handlers;
using CodeBrix.Android.UI.Hosting;
using CodeBrix.Platform.Foundation.Extensibility;
using CodeBrix.Platform.UI;
using CodeBrix.Platform.UI.Contracts;
using Microsoft.UI.Xaml;
using CompositionBootstrap = CodeBrix.Android.UI.Composition.Android.AndroidPlatformBootstrap;
using DispatchingBootstrap = CodeBrix.Android.UI.Dispatching.Android.AndroidPlatformBootstrap;
using ToolkitBootstrap = CodeBrix.Android.UI.Toolkit.Android.AndroidPlatformBootstrap;
using WinRTBootstrap = CodeBrix.Android.Android.AndroidPlatformBootstrap;

namespace CodeBrix.Android.UI.Android;

/// <summary>
/// The CodeBrix.Android platform bootstrap (the Android equivalent of the Skia
/// SkiaPlatformBootstrap chain): registers the Android implementation of every
/// CodeBrix.Platform.UI Core contract and FIRST runs the bootstraps of the other
/// CodeBrix.Android assemblies (dispatching, WinRT surface, composition, toolkit), so one
/// call registers everything. Idempotent. <see cref="CodeBrixApplication"/> calls it in
/// Application.OnCreate, before any XAML type is used; the module initializer covers other
/// entry points.
/// </summary>
/// <remarks>
/// IImagingPlatform names the Composition-internal type PlatformCompositionSurface; it is
/// implemented here since pin 1.0.266.1160, whose CodeBrix.Platform.UI.Composition.Core
/// grants InternalsVisibleTo to CodeBrix.Android.UI.
/// </remarks>
internal static class AndroidPlatformBootstrap
{
    private static readonly object _gate = new();
    private static bool _registered;

    /// <summary>Gets a value indicating whether the registrations have run.</summary>
    internal static bool IsRegistered
    {
        get
        {
            lock (_gate)
            {
                return _registered;
            }
        }
    }

    /// <summary>The registered font platform (the text measure's typeface resolver).</summary>
    internal static FontAndroidPlatform Fonts { get; private set; }

#pragma warning disable CA2255 // The module initializer is the platform bootstrap by design (B4 pattern).
    [ModuleInitializer]
#pragma warning restore CA2255
    internal static void EnsureRegistered()
    {
        lock (_gate)
        {
            if (_registered)
            {
                return;
            }

            // The chain (B4): the lower assemblies first - a UIElement constructor already needs
            // the composition platform, and the dispatcher pump drives Core's layout ticks.
            DispatchingBootstrap.EnsureRegistered();
            WinRTBootstrap.EnsureRegistered();
            CompositionBootstrap.EnsureRegistered();
            ToolkitBootstrap.EnsureRegistered();

            var fonts = new FontAndroidPlatform(
                () => global::Android.App.Application.Context?.Assets,
                HostLog.For("CodeBrix.Android.UI.Fonts"));
            Fonts = fonts;

            var application = new ApplicationAndroidPlatform();
            var focus = new FocusAndroidPlatform();
            var rendering = new RenderingAndroidPlatform();
            var geometry = new GeometryAndroidPlatform();
            var text = new TextAndroidPlatform(fonts, GetDefaultDensity);
            var imaging = new ImagingAndroidPlatform();

            ApiExtensibility.Register(typeof(IApplicationPlatform), _ => application);
            ApiExtensibility.Register(typeof(IFontPlatform), _ => fonts);
            ApiExtensibility.Register(typeof(IFocusPlatform), _ => focus);
            ApiExtensibility.Register(typeof(IRenderingPlatform), _ => rendering);
            ApiExtensibility.Register(typeof(IGeometryPlatform), _ => geometry);
            ApiExtensibility.Register(typeof(ITextPlatform), _ => text);
            ApiExtensibility.Register(typeof(IImagingPlatform), _ => imaging);
            ApiExtensibility.Register(typeof(global::Windows.UI.ViewManagement.IInputPaneExtension), _ => new InputPaneAndroidExtension());

            // AP7-B: the soft keyboard of the CUSTOM text-entry controls (SoftwareKeyboardFocus: TerminalView,
            // AdvancedTextEdit) - an input connection that types into Core as key presses (Input/TextInput).
            var textInput = CodeBrix.Android.UI.Input.TextInput.CoreTextInputController.Create();
            ApiExtensibility.Register(typeof(global::CodeBrix.Platform.UI.Xaml.Controls.Extensions.ITextInputFocusNotificationsSingleton), _ => textInput);

            // The system animation setting (C0c): UISettings.AnimationsEnabled follows the animator duration scale.
            // Registered before any XAML type is used; the refresh covers an entry point that read UISettings first.
            ApiExtensibility.Register(typeof(global::CodeBrix.Platform.Contracts.IAnimationSettingsPlatform), _ => new AnimationSettingsAndroidPlatform());
            global::Windows.UI.ViewManagement.UISettings.RefreshAnimationSettingsPlatform();

            // Decision D2: the device form (AnalyticsInfo.DeviceForm, the form part of DeviceFamily "Android.<form>")
            // is the width size class of the current window, read at query time (Compact -> Mobile, Medium -> Tablet,
            // Expanded -> Desktop), so a docked phone moving between size classes reports its current form.
            global::CodeBrix.Android.Android.AnalyticsInfoAndroidExtension.CurrentWindowWidthDp = Policy.WindowSizeClassMonitor.CurrentWindowWidthDp;

            // Input (AP2.5): one pointer and one keyboard source per window; Core's input manager creates
            // them with the window's XamlRoot host (XamlRootMap.Register -> ContentRoot.SetHost).
            ApiExtensibility.Register(typeof(global::Windows.UI.Core.ICodeBrixCorePointerInputSource), host => new CodeBrix.Android.UI.Input.AndroidCorePointerInputSource(host as AndroidXamlRootHost));
            ApiExtensibility.Register(typeof(global::Windows.UI.Core.ICodeBrixKeyboardInputSource), host => new CodeBrix.Android.UI.Input.AndroidKeyboardInputSource(host as AndroidXamlRootHost));

            // The per-control handler seam (WPH1): every element entering a live tree gets a
            // native handler (plan 2.5); Core re-reads the registration here in case anything
            // touched UIElement's statics before this bootstrap ran.
            ApiExtensibility.Register(typeof(IElementHandlerFactoryPlatform), _ => CodeBrixHandlers.Factory);

            // The overlay presenter (ContentDialog -> Material dialog, MenuFlyout -> Material menu), the activity
            // bridge of the WinRT services and the activity-lifecycle listener - registered BEFORE Core resolves its
            // element-handler services, so the presenter is seen on this refresh whatever order Core resolves in.
            Overlay.OverlayBootstrap.EnsureRegistered();
            UIElement.RefreshElementHandlerServices();

            // The platform defaults the Skia bootstrap sets (B6), with the Android values (D-P15).
            FeatureConfiguration.Popup.ConstrainByVisibleBoundsPlatformDefault = true;
            FeatureConfiguration.Frame.UseWinUIBehaviorPlatformDefault = true;
            FeatureConfiguration.ToolTip.UseToolTipsPlatformDefault = true;

            _registered = true;
        }

        // The add-ins the app ships (AP7): loaded by name once the framework registrations exist, outside the
        // lock (an add-in's module initializer calls back into this bootstrap).
        AddInLoader.EnsureLoaded();
    }

    private static double GetDefaultDensity() =>
        global::Android.App.Application.Context?.Resources?.DisplayMetrics?.Density ?? 1.0;
}
