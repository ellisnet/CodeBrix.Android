// Technique from .NET MAUI, src/Core/src/Platform/Android/MauiWebViewClient.cs and MauiWebChromeClient.cs @ 828569a864
// (ShouldOverrideUrlLoading announces a page's own navigation and refuses it; OnPageStarted / OnReceivedError /
// OnPageFinished give the load result; OnReceivedTitle the title). Copyright (c) .NET Foundation and Contributors.
// Licensed under the MIT License. See THIRD-PARTY-NOTICES.txt.

using System;
using System.Threading.Tasks;
using ABitmap = global::Android.Graphics.Bitmap;
using AIValueCallback = global::Android.Webkit.IValueCallback;
using AIWebResourceRequest = global::Android.Webkit.IWebResourceRequest;
using AJavaObject = global::Java.Lang.Object;
using AWebChromeClient = global::Android.Webkit.WebChromeClient;
using AWebResourceError = global::Android.Webkit.WebResourceError;
using AWebView = global::Android.Webkit.WebView;
using AWebViewClient = global::Android.Webkit.WebViewClient;
using AIDownloadListener = global::Android.Webkit.IDownloadListener;

namespace CodeBrix.Android.UI.WebView.Android;

/// <summary>The WebViewClient of a native web view: a page's own navigations and the load results.</summary>
internal sealed class CodeBrixWebViewClient : AWebViewClient
{
    private readonly WeakReference<AndroidNativeWebView> _owner;

    /// <summary>Creates the client.</summary>
    /// <param name="owner">The native web view.</param>
    internal CodeBrixWebViewClient(AndroidNativeWebView owner) => _owner = new WeakReference<AndroidNativeWebView>(owner);

    /// <inheritdoc />
    public override bool ShouldOverrideUrlLoading(AWebView view, AIWebResourceRequest request)
    {
        if (request == null || !request.IsForMainFrame || !_owner.TryGetTarget(out var owner))
        {
            return false;
        }

        return owner.OnPageNavigating(request.Url?.ToString());
    }

    /// <inheritdoc />
    public override void OnPageStarted(AWebView view, string url, ABitmap favicon)
    {
        if (_owner.TryGetTarget(out var owner))
        {
            owner.OnLoadStarted();
        }

        base.OnPageStarted(view, url, favicon);
    }

    /// <inheritdoc />
    public override void OnReceivedError(AWebView view, AIWebResourceRequest request, AWebResourceError error)
    {
        if (request != null && request.IsForMainFrame && _owner.TryGetTarget(out var owner))
        {
            owner.OnLoadFailed();
        }

        base.OnReceivedError(view, request, error);
    }

    /// <inheritdoc />
    public override void OnPageFinished(AWebView view, string url)
    {
        if (_owner.TryGetTarget(out var owner) && view != null && !string.IsNullOrWhiteSpace(url))
        {
            owner.OnLoadFinished(view, url);
        }

        base.OnPageFinished(view, url);
    }
}

/// <summary>The WebChromeClient of a native web view: the document title.</summary>
internal sealed class CodeBrixWebChromeClient : AWebChromeClient
{
    private readonly WeakReference<AndroidNativeWebView> _owner;

    /// <summary>Creates the client.</summary>
    /// <param name="owner">The native web view.</param>
    internal CodeBrixWebChromeClient(AndroidNativeWebView owner) => _owner = new WeakReference<AndroidNativeWebView>(owner);

    /// <inheritdoc />
    public override void OnReceivedTitle(AWebView view, string title)
    {
        base.OnReceivedTitle(view, title);
        if (_owner.TryGetTarget(out var owner))
        {
            owner.OnTitleChanged();
        }
    }
}

/// <summary>The download listener of a native web view.</summary>
internal sealed class CodeBrixDownloadListener : AJavaObject, AIDownloadListener
{
    private readonly WeakReference<AndroidNativeWebView> _owner;

    /// <summary>Creates the listener.</summary>
    /// <param name="owner">The native web view.</param>
    internal CodeBrixDownloadListener(AndroidNativeWebView owner) => _owner = new WeakReference<AndroidNativeWebView>(owner);

    /// <inheritdoc />
    public void OnDownloadStart(string url, string userAgent, string contentDisposition, string mimetype, long contentLength)
    {
        if (_owner.TryGetTarget(out var owner))
        {
            owner.OnDownload(url, userAgent, contentDisposition, mimetype, contentLength);
        }
    }
}

/// <summary>Completes a task with the JSON result of evaluateJavascript.</summary>
internal sealed class ScriptResultCallback : AJavaObject, AIValueCallback
{
    private readonly TaskCompletionSource<string> _done;

    /// <summary>Creates the callback.</summary>
    /// <param name="done">The task to complete.</param>
    internal ScriptResultCallback(TaskCompletionSource<string> done) => _done = done;

    /// <inheritdoc />
    public void OnReceiveValue(AJavaObject value) => _done.TrySetResult(value?.ToString() ?? "null");
}
