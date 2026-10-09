using System.Runtime.CompilerServices;
using CodeBrix.Platform.Foundation.Extensibility;
using CodeBrix.Platform.UI.CommandBar.Contracts;
using SvgBootstrap = CodeBrix.Android.UI.Svg.Android.AndroidPlatformBootstrap;

namespace CodeBrix.Android.UI.CommandBar.Android;

/// <summary>
/// Registers the Android side of the CommandBar add-in: IIconRasterizationPlatform, after the Svg add-in that draws the
/// icons, and the overflow rule (an item click closes the overflow flyout, <see cref="OverflowItemCloser"/>). Idempotent; runs as the module initializer (the Core's PlatformContract loads this assembly BY NAME when the
/// first icon is made; the CodeBrix.Android.UI bootstrap also loads it at start-up).
/// </summary>
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

    /// <summary>Registers the add-in (once).</summary>
#pragma warning disable CA2255 // The module initializer is the platform bootstrap by design (twin loading by name).
    [ModuleInitializer]
#pragma warning restore CA2255
    internal static void EnsureRegistered()
    {
        SvgBootstrap.EnsureRegistered();

        lock (_gate)
        {
            if (_registered)
            {
                return;
            }

            var icons = new IconRasterizationAndroidPlatform();
            ApiExtensibility.Register(typeof(IIconRasterizationPlatform), _ => icons);

            // AP10-G: an item click closes the ToolBar's overflow flyout (PLATFORM-QUEUE workaround, OverflowItemCloser).
            OverflowItemCloser.Register();
            _registered = true;
        }
    }
}
