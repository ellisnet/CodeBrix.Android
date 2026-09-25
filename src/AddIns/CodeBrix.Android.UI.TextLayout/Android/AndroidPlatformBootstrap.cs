using System.Runtime.CompilerServices;
using CodeBrix.Android.UI.Hosting;
using CodeBrix.Platform.Foundation.Contracts;
using CodeBrix.Platform.Foundation.Extensibility;
using SkiaSharp;
using UIBootstrap = CodeBrix.Android.UI.Android.AndroidPlatformBootstrap;

namespace CodeBrix.Android.UI.TextLayout.Android;

/// <summary>
/// Registers the Android side of the TextLayout add-in: the text engine's font source
/// (IFontSourcePlatform&lt;SKTypeface&gt;, <see cref="FontSourceAndroidPlatform"/>), after the framework's own
/// registrations. Idempotent; runs as the module initializer (TextLayout.Core and PlotterView.Core load this assembly
/// by name, and the CodeBrix.Android.UI bootstrap loads it at start-up).
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

    /// <summary>The registered font source (null until registered).</summary>
    internal static FontSourceAndroidPlatform FontSource { get; private set; }

    /// <summary>Registers the add-in (once).</summary>
#pragma warning disable CA2255 // The module initializer is the platform bootstrap by design (twin loading by name).
    [ModuleInitializer]
#pragma warning restore CA2255
    internal static void EnsureRegistered()
    {
        UIBootstrap.EnsureRegistered();

        lock (_gate)
        {
            if (_registered)
            {
                return;
            }

            if (!ApiExtensibility.IsRegistered<IFontSourcePlatform<SKTypeface>>())
            {
                var source = new FontSourceAndroidPlatform(
                    () => global::Android.App.Application.Context?.Assets,
                    HostLog.For("CodeBrix.Android.UI.TextLayout"));
                FontSource = source;
                ApiExtensibility.Register(typeof(IFontSourcePlatform<SKTypeface>), _ => source);
            }

            _registered = true;
        }
    }
}
