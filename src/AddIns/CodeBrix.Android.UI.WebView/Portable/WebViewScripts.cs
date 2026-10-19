using System.Text.Json;

namespace CodeBrix.Android.UI.WebView.Portable;

/// <summary>
/// The Android-specific scripts the web view runs (pure text, host-free tested): the document-start script of the two
/// message channels (page to host over the AndroidX WebMessageListener bridge object, and the host-to-page listener
/// surface), and the call InvokeScriptAsync makes of a function name and its arguments (the same text every
/// CodeBrix.Platform head builds: the arguments as a JSON array without its brackets).
/// </summary>
/// <remarks>
/// The host-to-page DELIVERY scripts are not here: AndroidNativeWebView.PostWebMessage evaluates the ones the framework
/// ships for every script-injecting head (CodeBrix.Platform.UI.Xaml.Controls.WebViewScripts.StringMessageDelivery /
/// JsonMessageDelivery in CodeBrix.Platform.UI.Core), so a message is escaped and dispatched identically on Linux, macOS and
/// Android. Those scripts call the page-side function named <see cref="DispatchFunctionName"/>, which the listener part
/// of <see cref="DocumentStartScript"/> installs; that part is the framework's MessageListenerScript text, carried here
/// because the Android document-start script also creates window.chrome.webview and its postMessage first (the
/// framework's script expects a page-to-host bridge to have created it). WebViewScriptsTests fences both against the
/// pinned framework assembly, so a change on either side fails a host test here instead of a page on a device.
/// </remarks>
internal static class WebViewScripts
{
    /// <summary>The name of the JavaScript object the page-to-host channel is exposed as (AndroidX WebMessageListener).</summary>
    internal const string BridgeObjectName = "codebrixWebViewHost";

    /// <summary>
    /// The name of the page-side function (a non-enumerable member of window.chrome.webview) that delivers a host message
    /// to the page's 'message' listeners and returns how many there were. Equal to the framework's
    /// WebViewScripts.DispatchFunctionName (fenced), because the framework's delivery scripts call it.
    /// </summary>
    internal const string DispatchFunctionName = "__codebrixDispatchMessage";

    /// <summary>
    /// The document-start script. Page to host: window.chrome.webview.postMessage (WebView2) and
    /// window.webkit.messageHandlers.codebrixWebView.postMessage (the Linux engine's name) both post the message as JSON
    /// (WebView2's WebMessageAsJson) through the bridge object. Host to page: window.chrome.webview.addEventListener /
    /// removeEventListener('message', listener) and the onmessage property, as WebView2 offers them; each listener is
    /// called with an event whose data is the message (the string of PostWebMessageAsString, the parsed value of
    /// PostWebMessageAsJson). A listener that throws does not stop the others (its error is reported asynchronously).
    /// </summary>
    internal const string DocumentStartScript =
        "(function(){var h=window." + BridgeObjectName + ";if(!h){return;}"
        + "function post(m){h.postMessage(JSON.stringify(m===undefined?null:m));}"
        + "window.chrome=window.chrome||{};window.chrome.webview=window.chrome.webview||{};window.chrome.webview.postMessage=post;"
        + "var v=window.chrome.webview,ls=[];"
        + "v.addEventListener=function(t,f){if(t==='message'&&typeof f==='function'&&ls.indexOf(f)<0){ls.push(f);}};"
        + "v.removeEventListener=function(t,f){if(t==='message'){var i=ls.indexOf(f);if(i>=0){ls.splice(i,1);}}};"
        + "if(!('onmessage' in v)){v.onmessage=null;}"
        + "function call(f,e){try{f.call(v,e);}catch(x){setTimeout(function(){throw x;},0);}}"
        + "Object.defineProperty(v,'" + DispatchFunctionName + "',{value:function(d){var e={type:'message',data:d,source:v},n=0;"
        + "if(typeof v.onmessage==='function'){n++;call(v.onmessage,e);}"
        + "ls.slice().forEach(function(f){n++;call(f,e);});return n;}});"
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
