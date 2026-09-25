using System.Runtime.CompilerServices;
using CodeBrix.Android.UI.Handlers;
using CodeBrix.Android.UI.WebView.Handlers;
using CodeBrix.Android.UI.WebView.Hosting;
using CodeBrix.Platform.Foundation.Extensibility;
using Microsoft.Web.WebView2.Core;
using UIBootstrap = CodeBrix.Android.UI.Android.AndroidPlatformBootstrap;

namespace CodeBrix.Android.UI.WebView.Android;

/// <summary>
/// Registers the Android side of the WebView add-in: INativeWebViewProvider (per CoreWebView2) over the system
/// android.webkit.WebView and the handler of its host element. Idempotent; runs as the module initializer (the
/// CodeBrix.Android.UI bootstrap loads this assembly by name at start-up, before any CoreWebView2 asks for its
/// provider - the Linux head's provider is registered by the app's generated code from an assembly attribute; the
/// Android one needs no public type).
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
        UIBootstrap.EnsureRegistered();

        lock (_gate)
        {
            if (_registered)
            {
                return;
            }

            if (!ApiExtensibility.IsRegistered<INativeWebViewProvider>())
            {
                ApiExtensibility.Register<CoreWebView2>(typeof(INativeWebViewProvider), core => new AndroidNativeWebViewProvider(core));
            }

            CodeBrixHandlers.Register<WebViewHostElement>(_ => new WebViewHostHandler());
            _registered = true;
        }
    }
}
