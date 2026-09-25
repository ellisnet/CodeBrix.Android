using System.Text.Json;

namespace CodeBrix.Android.UI.WebView.Portable;

/// <summary>
/// The scripts the Android web view runs (pure text, host-free tested): the document-start script of the page-to-host
/// channel, and the call InvokeScriptAsync makes of a function name and its arguments (the same text every
/// CodeBrix.Platform head builds: the arguments as a JSON array without its brackets).
/// </summary>
internal static class WebViewScripts
{
    /// <summary>The name of the JavaScript object the page-to-host channel is exposed as (AndroidX WebMessageListener).</summary>
    internal const string BridgeObjectName = "codebrixWebViewHost";

    /// <summary>
    /// The document-start script: window.chrome.webview.postMessage (WebView2) and
    /// window.webkit.messageHandlers.codebrixWebView.postMessage (the Linux engine's name) both post the message as JSON
    /// (WebView2's WebMessageAsJson) through the bridge object.
    /// </summary>
    internal const string DocumentStartScript =
        "(function(){var h=window." + BridgeObjectName + ";if(!h){return;}"
        + "function post(m){h.postMessage(JSON.stringify(m===undefined?null:m));}"
        + "window.chrome=window.chrome||{};window.chrome.webview=window.chrome.webview||{};window.chrome.webview.postMessage=post;"
        + "window.webkit=window.webkit||{};window.webkit.messageHandlers=window.webkit.messageHandlers||{};"
        + "window.webkit.messageHandlers.codebrixWebView={postMessage:post};})();";

    /// <summary>The script InvokeScriptAsync runs: <paramref name="function"/>(arguments as JSON strings).</summary>
    /// <param name="function">The function expression.</param>
    /// <param name="arguments">The arguments (null for none).</param>
    /// <returns>The call.</returns>
    internal static string Invocation(string function, string[] arguments)
    {
        var list = arguments == null ? string.Empty : JsonSerializer.Serialize(arguments);
        if (list.Length >= 2)
        {
            list = list.Substring(1, list.Length - 2);
        }

        return function + "(" + list + ")";
    }
}
