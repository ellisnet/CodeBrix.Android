using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.Web.WebView2.Core;
using AUrlUtil = global::Android.Webkit.URLUtil;

namespace CodeBrix.Android.UI.WebView.Android;

/// <summary>
/// A download a page started (the system WebView only reports it): announced as CoreWebView2's DownloadStarting with
/// the result path every CodeBrix.Platform head proposes (the downloads folder, a collision-free file name); unless the
/// app cancels, the file is fetched into the (possibly changed) result path, with progress and the final state reported
/// on the CoreWebView2DownloadOperation - Completed, or Interrupted (UserCanceled / FileFailed / NetworkFailed) as the
/// Linux engine's downloads are.
/// </summary>
internal static class WebViewDownloads
{
    private static readonly HttpClient Http = new();

    /// <summary>Announces and, unless cancelled, runs one download.</summary>
    internal static void Start(CoreWebView2 core, DispatcherQueue queue, string url, string userAgent, string contentDisposition, string mimeType, long contentLength)
    {
        var suggested = SuggestedFileName(url, contentDisposition, mimeType);
        var resultPath = DownloadDefaults.GetCollisionFreePath(DownloadDefaults.GetDownloadsFolder(), suggested);
        var cancellation = new CancellationTokenSource();
        var operation = new CoreWebView2DownloadOperation(url, contentDisposition, mimeType, contentLength, resultPath, () => cancellation.Cancel());
        core.RaiseDownloadStarting(operation, args =>
        {
            if (args.Cancel)
            {
                cancellation.Cancel();
                return;
            }

            operation.SetResultFilePath(args.ResultFilePath);
            _ = RunAsync(operation, queue, url, userAgent, args.ResultFilePath, contentLength, cancellation.Token);
        });
    }

    /// <summary>The file name a download is proposed under (Content-Disposition, else the URL; a data: URL has none).</summary>
    private static string SuggestedFileName(string url, string contentDisposition, string mimeType)
    {
        if (url.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(contentDisposition))
        {
            var extension = global::Android.Webkit.MimeTypeMap.Singleton?.GetExtensionFromMimeType(mimeType);
            return "download." + (string.IsNullOrEmpty(extension) ? "bin" : extension);
        }

        return AUrlUtil.GuessFileName(url, contentDisposition, mimeType) ?? "download";
    }

    private static async Task RunAsync(CoreWebView2DownloadOperation operation, DispatcherQueue queue, string url, string userAgent, string path, long contentLength, CancellationToken token)
    {
        var destinationFailure = false;
        try
        {
            if (url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                var bytes = DecodeDataUri(url);
                destinationFailure = true;
                await File.WriteAllBytesAsync(path, bytes, token).ConfigureAwait(false);
                Report(queue, () => operation.ReportProgress(bytes.Length, bytes.Length));
            }
            else
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                if (!string.IsNullOrEmpty(userAgent))
                {
                    request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
                }

                using var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                var total = response.Content.Headers.ContentLength ?? (contentLength > 0 ? contentLength : (long?)null);
                await using var source = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
                destinationFailure = true;
                await using var target = File.Create(path);
                destinationFailure = false;
                var buffer = new byte[81920];
                long received = 0;
                int read;
                while ((read = await source.ReadAsync(buffer, token).ConfigureAwait(false)) > 0)
                {
                    destinationFailure = true;
                    await target.WriteAsync(buffer.AsMemory(0, read), token).ConfigureAwait(false);
                    destinationFailure = false;
                    received += read;
                    var progress = received;
                    Report(queue, () => operation.ReportProgress(progress, total));
                }
            }

            Report(queue, () => operation.ReportStateChanged(CoreWebView2DownloadState.Completed, CoreWebView2DownloadInterruptReason.None));
        }
        catch (Exception e)
        {
            var canceled = token.IsCancellationRequested;
            if (!canceled)
            {
                global::Android.Util.Log.Warn("CodeBrix.WebView", "WebView download failed: " + e.Message);
            }

            var reason = canceled ? CoreWebView2DownloadInterruptReason.UserCanceled
                : destinationFailure ? CoreWebView2DownloadInterruptReason.FileFailed
                : CoreWebView2DownloadInterruptReason.NetworkFailed;
            Report(queue, () => operation.ReportStateChanged(CoreWebView2DownloadState.Interrupted, reason));
        }
    }

    private static byte[] DecodeDataUri(string url)
    {
        var comma = url.IndexOf(',');
        var header = comma > 0 ? url.Substring(5, comma - 5) : string.Empty;
        var payload = comma >= 0 ? url.Substring(comma + 1) : string.Empty;
        return header.EndsWith(";base64", StringComparison.OrdinalIgnoreCase)
            ? Convert.FromBase64String(payload)
            : System.Text.Encoding.UTF8.GetBytes(Uri.UnescapeDataString(payload));
    }

    private static void Report(DispatcherQueue queue, Action action)
    {
        if (queue == null || queue.HasThreadAccess)
        {
            action();
        }
        else
        {
            queue.TryEnqueue(() => action());
        }
    }
}
