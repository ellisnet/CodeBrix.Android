using Microsoft.UI.Xaml;

namespace CodeBrix.Android.UI.WebView.Hosting;

/// <summary>
/// The element the Android web view provider puts into the WebView2 template's ContentPresenter (as every
/// CodeBrix.Platform head puts its own host element there): Core lays it out and hit-tests it (its whole box - a
/// touch on the page is the page's); its handler shows the <see cref="Android.AndroidNativeWebView"/>'s
/// android.webkit.WebView at its rectangle.
/// </summary>
internal sealed class WebViewHostElement : FrameworkElement
{
    /// <summary>Creates the host of one native web view.</summary>
    /// <param name="nativeWebView">The native web view it shows.</param>
    internal WebViewHostElement(Android.AndroidNativeWebView nativeWebView)
    {
        NativeWebView = nativeWebView;
    }

    /// <summary>The native web view this element shows.</summary>
    internal Android.AndroidNativeWebView NativeWebView { get; }

    /// <inheritdoc />
    internal override bool IsViewHit() => true;
}
