// Technique from .NET MAUI, src/Core/src/Platform/Android/MauiWebViewClient.cs, MauiWebChromeClient.cs and MauiWebView.cs
// @ 828569a864 (a WebViewClient that announces each navigation and can refuse it, reports the finished load and the
// back/forward state; a WebChromeClient for the title; the settings a WebView2-like control needs). Copyright (c) .NET
// Foundation and Contributors. Licensed under the MIT License. See THIRD-PARTY-NOTICES.txt.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CodeBrix.Android.UI.WebView.Hosting;
using CodeBrix.Android.UI.WebView.Portable;
using CodeBrix.Platform.UI.Xaml.Controls;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using AContext = global::Android.Content.Context;
using AWebView = global::Android.Webkit.WebView;

namespace CodeBrix.Android.UI.WebView.Android;

/// <summary>
/// The Android native web view behind one CoreWebView2 (<see cref="INativeWebView"/>): an android.webkit.WebView driven
/// the way every CodeBrix.Platform head drives its engine. Every navigation is ANNOUNCED first (CoreWebView2's
/// NavigationStarting, which may refuse it): the app's own (a URI, a page handed over as text, reload, back, forward)
/// before the load is started, a page's own (a link, a script) from ShouldOverrideUrlLoading; a finished load reports
/// the history state and NavigationCompleted; the document title, the page's messages to its host and downloads are
/// raised on the CoreWebView2. The android.webkit.WebView is created when the host element's handler connects (it needs
/// the activity); work asked for before that is queued.
/// </summary>
internal sealed class AndroidNativeWebView : ICleanableNativeWebView
{
    private readonly CoreWebView2 _coreWebView;
    private readonly ContentPresenter _presenter;
    private readonly WebViewHostElement _host;
    private readonly List<Action<AWebView>> _pending = new();
    private AWebView _view;
    private string _pendingHtml;
    private string _lastHtml;
    private string _currentUri;
    private string _userAgent;
    private bool _scrollingEnabled = true;
    private bool _loadFailed;

    /// <summary>Creates the native web view of a CoreWebView2 and puts its host element into the presenter.</summary>
    /// <param name="coreWebView">The CoreWebView2.</param>
    /// <param name="presenter">The WebView2 template's ContentPresenter.</param>
    internal AndroidNativeWebView(CoreWebView2 coreWebView, ContentPresenter presenter)
    {
        _coreWebView = coreWebView;
        _presenter = presenter;
        _host = new WebViewHostElement(this);
        presenter.Content = _host;
    }

    /// <summary>The android.webkit.WebView, or null before the host element's handler connected.</summary>
    internal AWebView View => _view;

    /// <inheritdoc />
    public string DocumentTitle => _view?.Title ?? string.Empty;

    /// <summary>Creates the android.webkit.WebView (once) with the activity context of the host element's handler.</summary>
    /// <param name="context">The activity context.</param>
    /// <returns>The view.</returns>
    internal AWebView GetOrCreateView(AContext context)
    {
        if (_view != null && _view.Handle != IntPtr.Zero)
        {
            return _view;
        }

        var view = new AWebView(context)
        {
            // MATCH_PARENT, not the view group's default WRAP_CONTENT: a WebView whose layout height is WRAP_CONTENT lays the
            // page out for a zero-height viewport (to measure its content), so height:100% is 0. The layout replay still
            // measures and places the view exactly at the host element's Core rectangle (MauiWebView does the same).
            LayoutParameters = new global::Android.Views.ViewGroup.LayoutParams(
                global::Android.Views.ViewGroup.LayoutParams.MatchParent, global::Android.Views.ViewGroup.LayoutParams.MatchParent),
        };
        var settings = view.Settings;
        settings.JavaScriptEnabled = true;
        settings.DomStorageEnabled = true;
        settings.AllowFileAccess = true;
        settings.LoadWithOverviewMode = false;
        if (_userAgent != null)
        {
            settings.UserAgentString = _userAgent;
        }

        view.SetWebViewClient(new CodeBrixWebViewClient(this));
        view.SetWebChromeClient(new CodeBrixWebChromeClient(this));
        view.SetDownloadListener(new CodeBrixDownloadListener(this));
        WebMessageBridge.Install(view, this);
        ApplyScrolling(view);
        _view = view;

        var queued = _pending.ToArray();
        _pending.Clear();
        foreach (var action in queued)
        {
            action(view);
        }

        return view;
    }

    /// <inheritdoc />
    public void GoBack() => Run(view =>
    {
        if (view.CanGoBack() && Announce(HistoryUri(view, -1)))
        {
            view.GoBack();
        }
    });

    /// <inheritdoc />
    public void GoForward() => Run(view =>
    {
        if (view.CanGoForward() && Announce(HistoryUri(view, 1)))
        {
            view.GoForward();
        }
    });

    /// <inheritdoc />
    public void Stop() => Run(view => view.StopLoading());

    /// <inheritdoc />
    public void Reload()
    {
        if (_lastHtml != null && (_currentUri == null || _currentUri.StartsWith("data:", StringComparison.Ordinal)
            || string.Equals(_currentUri, "about:blank", StringComparison.Ordinal)))
        {
            ProcessNavigation(_lastHtml);
            return;
        }

        Run(view =>
        {
            if (Announce(ToUri(view.Url)))
            {
                view.Reload();
            }
        });
    }

    /// <inheritdoc />
    public void ProcessNavigation(Uri uri)
    {
        _pendingHtml = null;
        _lastHtml = null;
        if (uri == null)
        {
            return;
        }

        var address = uri.ToString();
        if (_coreWebView.HostToFolderMap.TryGetValue(uri.Host.ToLowerInvariant(), out var folder))
        {
            // A virtual host mapped to an app folder: the app's Content is packaged as Android assets under its
            // ms-appx path (the consumer build), so the folder is served from file:///android_asset/.
            address = "file:///android_asset/" + folder.Trim('/', '\\').Replace('\\', '/') + uri.PathAndQuery;
        }

        if (!Announce(uri))
        {
            return;
        }

        Run(view => view.LoadUrl(address));
    }

    /// <inheritdoc />
    public void ProcessNavigation(string html)
    {
        _pendingHtml = html ?? string.Empty;
        var announced = Announce(_pendingHtml);
        var page = _pendingHtml;
        _pendingHtml = null;
        if (!announced)
        {
            return;
        }

        _lastHtml = page;

        // As WebView2 does: the document's address is about:blank (the page is not a URL the engine can fetch again).
        Run(view => view.LoadDataWithBaseURL(null, page, "text/html", "UTF-8", null));
    }

    /// <inheritdoc />
    public void ProcessNavigation(HttpRequestMessage httpRequestMessage)
    {
        _pendingHtml = null;
        _lastHtml = null;
        var uri = httpRequestMessage?.RequestUri;
        if (uri == null)
        {
            Log("ProcessNavigation received an HttpRequestMessage with a null uri.");
            return;
        }

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in httpRequestMessage.Headers)
        {
            headers[header.Key] = string.Join(",", header.Value);
        }

        if (!Announce(uri))
        {
            return;
        }

        Run(view => view.LoadUrl(uri.ToString(), headers));
    }

    /// <inheritdoc />
    public Task<string> ExecuteScriptAsync(string script, CancellationToken token)
    {
        var done = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        token.Register(() => done.TrySetCanceled(token));
        Run(view => view.EvaluateJavascript(script ?? string.Empty, new ScriptResultCallback(done)));
        return done.Task;
    }

    /// <inheritdoc />
    public Task<string> InvokeScriptAsync(string script, string[] arguments, CancellationToken token) =>
        ExecuteScriptAsync(WebViewScripts.Invocation(script, arguments), token);

    /// <inheritdoc />
    public void SetScrollingEnabled(bool isScrollingEnabled)
    {
        _scrollingEnabled = isScrollingEnabled;
        if (_view != null)
        {
            ApplyScrolling(_view);
        }
    }

    /// <inheritdoc />
    public void SetUserAgent(string userAgent)
    {
        _userAgent = userAgent;
        Run(view => view.Settings.UserAgentString = userAgent);
    }

    /// <inheritdoc />
    public void OnLoaded() => _view?.OnResume();

    /// <inheritdoc />
    public void OnUnloaded() => _view?.OnPause();

    /// <summary>A page's own navigation (a link, a script): announced; true refuses it (ShouldOverrideUrlLoading).</summary>
    /// <param name="url">The address the page is going to.</param>
    /// <returns>True when the navigation was refused.</returns>
    internal bool OnPageNavigating(string url) => !Announce(ToUri(url));

    /// <summary>A load started (the error state is reset).</summary>
    internal void OnLoadStarted() => _loadFailed = false;

    /// <summary>The main frame's load failed.</summary>
    internal void OnLoadFailed() => _loadFailed = true;

    /// <summary>A load finished: the history state and NavigationCompleted (as every CodeBrix.Platform head reports it).</summary>
    /// <param name="view">The web view.</param>
    /// <param name="url">The address that finished loading.</param>
    internal void OnLoadFinished(AWebView view, string url)
    {
        _currentUri = url;
        var success = !_loadFailed;
        _coreWebView.SetHistoryProperties(view.CanGoBack(), view.CanGoForward());
        _coreWebView.RaiseHistoryChanged();
        _coreWebView.RaiseNavigationCompleted(ToUri(url), success, success ? 200 : 0, CoreWebView2WebErrorStatus.Unknown, true);
    }

    /// <summary>The page's title changed.</summary>
    internal void OnTitleChanged() => _coreWebView.OnDocumentTitleChanged();

    /// <summary>The page posted a message to its host.</summary>
    /// <param name="message">The message.</param>
    internal void OnWebMessage(string message) => Dispatch(() => _coreWebView.RaiseWebMessageReceived(message));

    /// <summary>The page started a download: announced (DownloadStarting), then fetched into the result path.</summary>
    /// <param name="url">The address of the file.</param>
    /// <param name="userAgent">The page's user agent.</param>
    /// <param name="contentDisposition">The Content-Disposition header.</param>
    /// <param name="mimeType">The MIME type.</param>
    /// <param name="contentLength">The length, or -1.</param>
    internal void OnDownload(string url, string userAgent, string contentDisposition, string mimeType, long contentLength) =>
        Dispatch(() => WebViewDownloads.Start(_coreWebView, _presenter.DispatcherQueue, url, userAgent, contentDisposition, mimeType, contentLength));

    private bool Announce(object navigation)
    {
        if (navigation == null)
        {
            return true;
        }

        _coreWebView.RaiseNavigationStarting(navigation, out var cancel);
        return !cancel;
    }

    private void Run(Action<AWebView> action)
    {
        if (_view != null)
        {
            action(_view);
        }
        else
        {
            _pending.Add(action);
        }
    }

    private void Dispatch(Action action)
    {
        if (_presenter.DispatcherQueue is { } queue && !queue.HasThreadAccess)
        {
            queue.TryEnqueue(() => action());
        }
        else
        {
            action();
        }
    }

    private void ApplyScrolling(AWebView view)
    {
        view.VerticalScrollBarEnabled = _scrollingEnabled;
        view.HorizontalScrollBarEnabled = _scrollingEnabled;
        view.OverScrollMode = _scrollingEnabled ? global::Android.Views.OverScrollMode.IfContentScrolls : global::Android.Views.OverScrollMode.Never;
    }

    private static Uri HistoryUri(AWebView view, int offset)
    {
        var history = view.CopyBackForwardList();
        var index = history.CurrentIndex + offset;
        return index >= 0 && index < history.Size ? ToUri(history.GetItemAtIndex(index)?.Url) : null;
    }

    private static Uri ToUri(string url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return null;
        }

        try
        {
            return new Uri(url);
        }
        catch (UriFormatException)
        {
            // A data: document longer than System.Uri allows: reported by its kind.
            return url.StartsWith("data:", StringComparison.Ordinal) ? new Uri("data:text/html,") : null;
        }
    }

    private static void Log(string message) => global::Android.Util.Log.Warn("CodeBrix.WebView", message);

    /// <summary>The host element of this web view (in the WebView2 template's ContentPresenter).</summary>
    internal WebViewHostElement Host => _host;
}
