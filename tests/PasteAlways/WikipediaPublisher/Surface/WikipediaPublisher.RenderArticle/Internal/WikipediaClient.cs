using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using WikipediaPublisher.RenderArticle.Models;

namespace WikipediaPublisher.RenderArticle.Internal;

/// <summary>
/// A small, polite HTTP client for Wikipedia: fetches article pages, runs
/// MediaWiki API searches, and downloads media files with rate limiting.
/// </summary>
internal sealed class WikipediaClient : IDisposable
{
    //Wikimedia's user-agent policy asks for an identifying UA string with contact info
    private const string UserAgent =
        "WikipediaPublisher/1.0 (https://github.com/ellisnet; jeremy@ellisnet.com) CodeBrix.MarkupParse";

    private const int MediaDownloadDelayMs = 250;
    private const string DefaultWikiHost = "en.wikipedia.org";

    private readonly HttpClient _httpClient;
    private DateTime _lastMediaDownloadUtc = DateTime.MinValue;
    private readonly SemaphoreSlim _mediaThrottle = new(1, 1);
    private bool _isDisposed;

    public WikipediaClient()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        _httpClient.Timeout = TimeSpan.FromSeconds(60);
    }

    /// <summary>Fetches the raw HTML of an article page.</summary>
    public Task<string> GetArticleHtmlAsync(string articleUrl, CancellationToken cancellationToken = default)
    {
        CheckIsDisposed();
        if (string.IsNullOrWhiteSpace(articleUrl))
        {
            throw new ArgumentException("Value cannot be null or blank.", nameof(articleUrl));
        }

        return _httpClient.GetStringAsync(articleUrl, cancellationToken);
    }

    /// <summary>
    /// Searches Wikipedia article titles/content via the MediaWiki API.
    /// </summary>
    public async Task<IList<ArticleSearchResult>> SearchAsync(
        string searchTerms,
        int maxResults = 20,
        string wikiHost = DefaultWikiHost,
        CancellationToken cancellationToken = default)
    {
        CheckIsDisposed();
        if (string.IsNullOrWhiteSpace(searchTerms)) { return []; }

        maxResults = Math.Clamp(maxResults, 1, 50);
        wikiHost = string.IsNullOrWhiteSpace(wikiHost) ? DefaultWikiHost : wikiHost.Trim();

        var apiUrl = $"https://{wikiHost}/w/api.php?action=query&list=search"
                     + $"&srsearch={Uri.EscapeDataString(searchTerms.Trim())}"
                     + $"&srlimit={maxResults}&format=json";

        var json = await _httpClient.GetStringAsync(apiUrl, cancellationToken);

        var results = new List<ArticleSearchResult>();
        using var document = JsonDocument.Parse(json);

        if (document.RootElement.TryGetProperty("query", out var query)
            && query.TryGetProperty("search", out var searchHits))
        {
            foreach (var hit in searchHits.EnumerateArray())
            {
                var title = hit.TryGetProperty("title", out var titleProp)
                    ? (titleProp.GetString() ?? "")
                    : "";
                if (title.Length == 0) { continue; }

                var snippet = hit.TryGetProperty("snippet", out var snippetProp)
                    ? StripHtmlTags(snippetProp.GetString() ?? "")
                    : "";

                results.Add(new ArticleSearchResult
                {
                    Title = title,
                    Snippet = snippet,
                    ArticleUrl = $"https://{wikiHost}/wiki/{Uri.EscapeDataString(title.Replace(' ', '_'))}"
                });
            }
        }

        return results;
    }

    //extmetadata fields worth fetching for an image credit line
    private static readonly string[] AttributionMetadataFields =
        ["Artist", "Attribution", "Credit", "LicenseShortName", "AttributionRequired", "Copyrighted"];

    private const int MaxTitlesPerMetadataQuery = 50;

    /// <summary>
    /// Looks up Wikimedia "extmetadata" (author, credit, license, …) for the given "File:" page
    /// titles via the MediaWiki imageinfo API, batching up to 50 titles per request. The local
    /// wiki's API transparently resolves files hosted on Wikimedia Commons. Titles that cannot be
    /// resolved are simply absent from the result; a failed request yields an empty dictionary
    /// (attribution is best-effort and never fails the render).
    /// </summary>
    /// <returns>A case-insensitive map from the (normalized) file title to its metadata
    /// field/value pairs.</returns>
    public async Task<IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>>
        GetImageMetadataAsync(
            IReadOnlyCollection<string> fileTitles,
            string wikiHost = DefaultWikiHost,
            CancellationToken cancellationToken = default)
    {
        CheckIsDisposed();

        var result = new Dictionary<string, IReadOnlyDictionary<string, string>>(
            StringComparer.OrdinalIgnoreCase);
        if (fileTitles is null || fileTitles.Count == 0) { return result; }

        wikiHost = string.IsNullOrWhiteSpace(wikiHost) ? DefaultWikiHost : wikiHost.Trim();

        var uniqueTitles = fileTitles
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        for (var offset = 0; offset < uniqueTitles.Count; offset += MaxTitlesPerMetadataQuery)
        {
            var batch = uniqueTitles.Skip(offset).Take(MaxTitlesPerMetadataQuery).ToList();
            var titlesParam = Uri.EscapeDataString(string.Join("|", batch));

            var apiUrl = $"https://{wikiHost}/w/api.php?action=query&prop=imageinfo"
                         + "&iiprop=extmetadata"
                         + $"&iiextmetadatafilter={string.Join("|", AttributionMetadataFields)}"
                         + $"&titles={titlesParam}&format=json&formatversion=2";

            try
            {
                var json = await _httpClient.GetStringAsync(apiUrl, cancellationToken);
                ParseImageMetadata(json, result);
            }
            catch (HttpRequestException) { /* best-effort; skip this batch */ }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested) { }
        }

        return result;
    }

    private static void ParseImageMetadata(
        string json, IDictionary<string, IReadOnlyDictionary<string, string>> into)
    {
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("query", out var query)
            || !query.TryGetProperty("pages", out var pages))
        {
            return;
        }

        //formatversion=2 renders pages as an array
        foreach (var page in pages.EnumerateArray())
        {
            if (!page.TryGetProperty("title", out var titleProp)) { continue; }
            var title = titleProp.GetString();
            if (string.IsNullOrWhiteSpace(title)) { continue; }

            if (!page.TryGetProperty("imageinfo", out var imageInfo)
                || imageInfo.ValueKind != JsonValueKind.Array
                || imageInfo.GetArrayLength() == 0)
            {
                continue;
            }

            var info = imageInfo[0];
            if (!info.TryGetProperty("extmetadata", out var extMetadata)) { continue; }

            var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var field in extMetadata.EnumerateObject())
            {
                if (field.Value.TryGetProperty("value", out var valueProp)
                    && valueProp.ValueKind == JsonValueKind.String)
                {
                    fields[field.Name] = valueProp.GetString() ?? "";
                }
            }

            if (fields.Count > 0) { into[title] = fields; }
        }
    }

    /// <summary>
    /// Downloads a media file (rate-limited to be polite to Wikimedia servers).
    /// Returns null when the download fails — callers treat missing images as non-fatal.
    /// </summary>
    public async Task<byte[]> TryDownloadMediaAsync(string url, CancellationToken cancellationToken = default)
    {
        CheckIsDisposed();
        if (string.IsNullOrWhiteSpace(url)) { return null; }

        await _mediaThrottle.WaitAsync(cancellationToken);
        try
        {
            var sinceLast = DateTime.UtcNow - _lastMediaDownloadUtc;
            var wait = TimeSpan.FromMilliseconds(MediaDownloadDelayMs) - sinceLast;
            if (wait > TimeSpan.Zero)
            {
                await Task.Delay(wait, cancellationToken);
            }

            _lastMediaDownloadUtc = DateTime.UtcNow;
            return await _httpClient.GetByteArrayAsync(url, cancellationToken);
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            //Request timeout (not user cancellation)
            return null;
        }
        finally
        {
            _mediaThrottle.Release();
        }
    }

    private static string StripHtmlTags(string html) =>
        Regex.Replace(html, "<[^>]+>", "").Replace("&quot;", "\"").Replace("&amp;", "&")
            .Replace("&lt;", "<").Replace("&gt;", ">").Replace("&#39;", "'");

    private void CheckIsDisposed()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
    }

    public void Dispose()
    {
        if (_isDisposed) { return; }
        _isDisposed = true;
        _httpClient.Dispose();
        _mediaThrottle.Dispose();
    }
}
