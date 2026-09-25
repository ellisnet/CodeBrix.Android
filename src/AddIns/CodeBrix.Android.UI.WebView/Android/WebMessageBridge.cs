using System;
using System.Collections.Generic;
using AndroidX.WebKit;
using CodeBrix.Android.UI.WebView.Portable;
using AWebView = global::Android.Webkit.WebView;
using AJavaObject = global::Java.Lang.Object;

namespace CodeBrix.Android.UI.WebView.Android;

/// <summary>
/// The page-to-host message channel: an AndroidX WebKit WebMessageListener object ("codebrixWebViewHost") and a
/// document-start script that gives every page the two names a CodeBrix.Platform page posts through -
/// window.chrome.webview.postMessage (WebView2) and window.webkit.messageHandlers.codebrixWebView.postMessage (the
/// Linux engine's) - both forwarding to it, as JSON (WebView2's WebMessageAsJson: a posted string arrives as a JSON
/// string, which TryGetWebMessageAsString unwraps). A message is raised as CoreWebView2's WebMessageReceived.
/// </summary>
internal static class WebMessageBridge
{
    /// <summary>Installs the channel on a web view (when the system WebView supports it).</summary>
    /// <param name="view">The web view.</param>
    /// <param name="owner">The native web view the messages go to.</param>
    internal static void Install(AWebView view, AndroidNativeWebView owner)
    {
        var origins = new HashSet<string> { "*" };
        if (WebViewFeature.IsFeatureSupported(WebViewFeature.WebMessageListener))
        {
            WebViewCompat.AddWebMessageListener(view, WebViewScripts.BridgeObjectName, origins, new Listener(owner));
        }
        else
        {
            global::Android.Util.Log.Warn("CodeBrix.WebView", "This system WebView has no WebMessageListener: a page cannot post messages to its host.");
        }

        if (WebViewFeature.IsFeatureSupported(WebViewFeature.DocumentStartScript))
        {
            WebViewCompat.AddDocumentStartJavaScript(view, WebViewScripts.DocumentStartScript, origins);
        }
    }

    private sealed class Listener : AJavaObject, WebViewCompat.IWebMessageListener
    {
        private readonly WeakReference<AndroidNativeWebView> _owner;

        internal Listener(AndroidNativeWebView owner) => _owner = new WeakReference<AndroidNativeWebView>(owner);

        public void OnPostMessage(AWebView view, WebMessageCompat message, global::Android.Net.Uri sourceOrigin, bool isMainFrame, JavaScriptReplyProxy replyProxy)
        {
            if (_owner.TryGetTarget(out var owner) && message?.Data is { } data)
            {
                owner.OnWebMessage(data);
            }
        }
    }
}
