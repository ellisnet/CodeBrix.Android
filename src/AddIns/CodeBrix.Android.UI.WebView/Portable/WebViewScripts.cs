using System;
using System.Text.Json;

namespace CodeBrix.Android.UI.WebView.Portable;

/// <summary>
/// The scripts the Android web view runs (pure text, host-free tested): the document-start script of the two message
/// channels (page to host and host to page), the call InvokeScriptAsync makes of a function name and its arguments (the
/// same text every CodeBrix.Platform head builds: the arguments as a JSON array without its brackets), and the script
/// that delivers a host message to the page's listeners.
/// </summary>
internal static class WebViewScripts
{
    /// <summary>The name of the JavaScript object the page-to-host channel is exposed as (AndroidX WebMessageListener).</summary>
    internal const string BridgeObjectName = "codebrixWebViewHost";

    /// <summary>
    /// The name of the page-side function (a non-enumerable member of window.chrome.webview) that delivers a host message
    /// to the page's 'message' listeners and returns how many there were.
    /// </summary>
    internal const string DispatchFunctionName = "__codebrixDispatchMessage";

    /// <summary>The value <see cref="Delivery"/> evaluates to when the page has no message channel (the document-start script did not run).</summary>
    internal const string NoChannelResult = "-1";

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

    /// <summary>
    /// The script that delivers PostWebMessageAsString(<paramref name="message"/>) to the page: event.data is the
    /// string. The text reaches the page only as a JSON string literal (quotes, line breaks, "&lt;/script&gt;" and every
    /// non-ASCII character escaped), never as script text. Evaluates to the number of listeners that received it, or
    /// <see cref="NoChannelResult"/> when the page has no channel.
    /// </summary>
    /// <param name="message">The message (null is sent as an empty string, as WebView2 marshals it).</param>
    /// <returns>The script.</returns>
    internal static string StringMessageDelivery(string message) => Delivery(JsonSerializer.Serialize(message ?? string.Empty));

    /// <summary>
    /// The script that delivers PostWebMessageAsJson(<paramref name="json"/>) to the page: event.data is the parsed JSON
    /// value. The JSON text is validated here and handed to the page as a JSON string literal that the page parses with
    /// JSON.parse - it is never evaluated as script.
    /// </summary>
    /// <param name="json">The message as JSON text.</param>
    /// <returns>The script.</returns>
    /// <exception cref="ArgumentException"><paramref name="json"/> is null or not valid JSON (WebView2 refuses it too).</exception>
    internal static string JsonMessageDelivery(string json)
    {
        if (json == null)
        {
            throw new ArgumentException("The message is not valid JSON: it is null.", nameof(json));
        }

        try
        {
            using var document = JsonDocument.Parse(json);
        }
        catch (JsonException exception)
        {
            throw new ArgumentException("The message is not valid JSON: " + exception.Message, nameof(json), exception);
        }

        return Delivery("JSON.parse(" + JsonSerializer.Serialize(json) + ")");
    }

    private static string Delivery(string dataExpression) =>
        "(function(){var v=window.chrome&&window.chrome.webview;"
        + "if(!v||typeof v." + DispatchFunctionName + "!=='function'){return " + NoChannelResult + ";}"
        + "return v." + DispatchFunctionName + "(" + dataExpression + ");})()";
}
