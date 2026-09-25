using CodeBrix.Platform.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;

namespace CodeBrix.Android.UI.WebView.Android;

/// <summary>
/// The Android <see cref="INativeWebViewProvider"/> (one per CoreWebView2, created by Core through ApiExtensibility):
/// the native web view is an <see cref="AndroidNativeWebView"/> over the system android.webkit.WebView.
/// </summary>
internal sealed class AndroidNativeWebViewProvider : INativeWebViewProvider
{
    private readonly CoreWebView2 _coreWebView;

    /// <summary>Creates the provider of one CoreWebView2.</summary>
    /// <param name="coreWebView">The CoreWebView2.</param>
    internal AndroidNativeWebViewProvider(CoreWebView2 coreWebView) => _coreWebView = coreWebView;

    /// <inheritdoc />
    INativeWebView INativeWebViewProvider.CreateNativeWebView(ContentPresenter contentPresenter) =>
        new AndroidNativeWebView(_coreWebView, contentPresenter);
}
