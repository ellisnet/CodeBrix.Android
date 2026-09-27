using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace PolyHavenBrowser.PolyHavenApiClient;

/// <summary>
/// The default <see cref="IPolyHavenApiClient"/> implementation, backed by
/// <see cref="HttpClient"/>. Create instances via <see cref="DefaultPolyHavenClientFactory"/>
/// (or dependency injection with <see cref="PolyHavenServiceCollectionExtensions.AddPolyHavenApiClient"/>),
/// which manage HTTP connection pooling; disposing this client releases only the client's
/// own resources, never the shared connection pool.
/// </summary>
public sealed class RestPolyHavenApiClient : IPolyHavenApiClient
{
    private const int DownloadBufferSize = 81920;

    private readonly HttpClient _httpClient;
    private readonly PolyHavenClientOptions _options;
    private readonly bool _disposeHttpClient;
    private readonly Uri _baseUri;
    private bool _disposed;

    internal RestPolyHavenApiClient(HttpClient httpClient, PolyHavenClientOptions options, bool disposeHttpClient = true)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _disposeHttpClient = disposeHttpClient;

        var baseAddress = options.BaseAddress;
        if (string.IsNullOrWhiteSpace(baseAddress))
        {
            throw new ArgumentException("Options must specify a base address.", nameof(options));
        }

        _baseUri = new Uri(baseAddress.EndsWith('/') ? baseAddress : baseAddress + "/", UriKind.Absolute);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetAssetTypesAsync(CancellationToken cancellationToken = default)
    {
        return await GetJsonAsync(
            "types", "the asset type list", PolyHavenJsonContext.Default.StringArray, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, PolyHavenAsset>> GetAssetsAsync(
        PolyHavenAssetType? type = null,
        IEnumerable<string>? categories = null,
        CancellationToken cancellationToken = default)
    {
        var query = new List<string>(2);
        if (type is { } assetType)
        {
            query.Add("type=" + assetType.ToApiString());
        }

        var joinedCategories = JoinList(categories);
        if (joinedCategories is not null)
        {
            query.Add("categories=" + Uri.EscapeDataString(joinedCategories));
        }

        var relativeUrl = "assets" + (query.Count > 0 ? "?" + string.Join('&', query) : string.Empty);
        var assets = await GetJsonAsync(
            relativeUrl, "the asset list", PolyHavenJsonContext.Default.DictionaryStringPolyHavenAsset, cancellationToken)
            .ConfigureAwait(false);

        foreach (var (id, asset) in assets)
        {
            asset.Id = id;
        }

        return assets;
    }

    /// <inheritdoc />
    public async Task<PolyHavenAsset> GetAssetAsync(string assetId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetId);
        var asset = await GetJsonAsync(
            "info/" + Uri.EscapeDataString(assetId), $"asset '{assetId}'",
            PolyHavenJsonContext.Default.PolyHavenAsset, cancellationToken)
            .ConfigureAwait(false);
        asset.Id = assetId;
        return asset;
    }

    /// <inheritdoc />
    public async Task<PolyHavenFileTree> GetAssetFilesAsync(string assetId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetId);
        var json = await GetStringAsync(
            "files/" + Uri.EscapeDataString(assetId), $"the file list for asset '{assetId}'", cancellationToken)
            .ConfigureAwait(false);

        try
        {
            using var document = JsonDocument.Parse(json);
            return PolyHavenFileTree.Parse(document.RootElement);
        }
        catch (JsonException ex)
        {
            throw new PolyHavenApiException(
                $"The Poly Haven API returned an unparseable file list for asset '{assetId}'.", ex);
        }
    }

    /// <inheritdoc />
    public async Task<PolyHavenAuthor> GetAuthorAsync(string authorId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(authorId);
        return await GetJsonAsync(
            "author/" + Uri.EscapeDataString(authorId), $"author '{authorId}'",
            PolyHavenJsonContext.Default.PolyHavenAuthor, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, int>> GetCategoriesAsync(
        PolyHavenAssetType type,
        IEnumerable<string>? inCategories = null,
        CancellationToken cancellationToken = default)
    {
        var relativeUrl = "categories/" + type.ToApiString();
        var joined = JoinList(inCategories);
        if (joined is not null)
        {
            relativeUrl += "?in=" + Uri.EscapeDataString(joined);
        }

        return await GetJsonAsync(
            relativeUrl, $"the category list for type '{type.ToApiString()}'",
            PolyHavenJsonContext.Default.DictionaryStringInt32, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public string GetThumbnailUrl(PolyHavenAsset asset, int? width = null, int? height = null)
    {
        ArgumentNullException.ThrowIfNull(asset);
        var url = asset.ThumbnailUrl;
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new ArgumentException("The asset has no thumbnail URL.", nameof(asset));
        }

        if (width is null && height is null)
        {
            return url;
        }

        var baseUrl = url.Split('?')[0];
        var query = new List<string>(2);
        if (width is { } w)
        {
            query.Add("width=" + w);
        }

        if (height is { } h)
        {
            query.Add("height=" + h);
        }

        return baseUrl + "?" + string.Join('&', query);
    }

    /// <inheritdoc />
    public Task<byte[]> GetThumbnailAsync(
        PolyHavenAsset asset,
        int? width = null,
        int? height = null,
        CancellationToken cancellationToken = default)
    {
        return GetImageAsync(GetThumbnailUrl(asset, width, height), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<byte[]> GetImageAsync(string imageUrl, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(imageUrl);

        using var response = await _httpClient.GetAsync(
            new Uri(imageUrl, UriKind.Absolute), HttpCompletionOption.ResponseContentRead, cancellationToken)
            .ConfigureAwait(false);
        await EnsureSuccessAsync(response, $"image '{imageUrl}'", cancellationToken).ConfigureAwait(false);
        return await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task DownloadFileAsync(
        PolyHavenFileRef file,
        Stream destination,
        IProgress<PolyHavenDownloadProgress>? progress = null,
        bool verifyMd5 = false,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(destination);

        using var response = await _httpClient.GetAsync(
            new Uri(file.Url, UriKind.Absolute), HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        await EnsureSuccessAsync(response, $"file '{file.Url}'", cancellationToken).ConfigureAwait(false);

        var totalBytes = response.Content.Headers.ContentLength ?? (file.Size > 0 ? file.Size : (long?)null);
        using var md5 = verifyMd5 && !string.IsNullOrEmpty(file.Md5)
            ? IncrementalHash.CreateHash(HashAlgorithmName.MD5)
            : null;

        var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using (source.ConfigureAwait(false))
        {
            var buffer = new byte[DownloadBufferSize];
            long received = 0;
            int read;
            while ((read = await source.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false)) > 0)
            {
                await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                md5?.AppendData(buffer, 0, read);
                received += read;
                progress?.Report(new PolyHavenDownloadProgress(received, totalBytes));
            }
        }

        if (md5 is not null)
        {
            var actualMd5 = Convert.ToHexStringLower(md5.GetHashAndReset());
            if (!actualMd5.Equals(file.Md5, StringComparison.OrdinalIgnoreCase))
            {
                throw new PolyHavenIntegrityException(file.Md5!, actualMd5, file.Url);
            }
        }
    }

    /// <inheritdoc />
    public async Task DownloadFileAsync(
        PolyHavenFileRef file,
        string destinationFilePath,
        IProgress<PolyHavenDownloadProgress>? progress = null,
        bool verifyMd5 = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationFilePath);

        var fullPath = Path.GetFullPath(destinationFilePath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        try
        {
            var fileStream = new FileStream(
                fullPath, FileMode.Create, FileAccess.Write, FileShare.None, DownloadBufferSize, useAsync: true);
            await using (fileStream.ConfigureAwait(false))
            {
                await DownloadFileAsync(file, fileStream, progress, verifyMd5, cancellationToken).ConfigureAwait(false);
            }
        }
        catch
        {
            try
            {
                File.Delete(fullPath);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }

            throw;
        }
    }

    /// <inheritdoc />
    public async Task<byte[]> DownloadFileAsync(
        PolyHavenFileRef file,
        IProgress<PolyHavenDownloadProgress>? progress = null,
        bool verifyMd5 = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);
        using var memoryStream = new MemoryStream(
            file.Size is > 0 and <= int.MaxValue ? (int)file.Size : 0);
        await DownloadFileAsync(file, memoryStream, progress, verifyMd5, cancellationToken).ConfigureAwait(false);
        return memoryStream.ToArray();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_disposeHttpClient)
        {
            _httpClient.Dispose();
        }
    }

    private async Task<T> GetJsonAsync<T>(
        string relativeUrl, string resourceDescription, JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken)
    {
        var json = await GetStringAsync(relativeUrl, resourceDescription, cancellationToken).ConfigureAwait(false);
        try
        {
            return JsonSerializer.Deserialize(json, typeInfo)
                ?? throw new PolyHavenApiException(
                    $"The Poly Haven API returned an empty response for {resourceDescription}.");
        }
        catch (JsonException ex)
        {
            throw new PolyHavenApiException(
                $"The Poly Haven API returned an unparseable response for {resourceDescription}.", ex);
        }
    }

    private async Task<string> GetStringAsync(
        string relativeUrl, string resourceDescription, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (_options.MetadataRequestTimeout > TimeSpan.Zero)
        {
            timeoutCts.CancelAfter(_options.MetadataRequestTimeout);
        }

        using var response = await _httpClient.GetAsync(
            new Uri(_baseUri, relativeUrl), HttpCompletionOption.ResponseContentRead, timeoutCts.Token)
            .ConfigureAwait(false);
        await EnsureSuccessAsync(response, resourceDescription, timeoutCts.Token).ConfigureAwait(false);
        return await response.Content.ReadAsStringAsync(timeoutCts.Token).ConfigureAwait(false);
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response, string resourceDescription, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        string? body = null;
        try
        {
            body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException)
        {
        }

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new PolyHavenNotFoundException(resourceDescription, response.StatusCode, body);
        }

        throw new PolyHavenApiException(
            $"The Poly Haven API request for {resourceDescription} failed with status " +
            $"{(int)response.StatusCode} ({response.StatusCode}).",
            response.StatusCode, body);
    }

    private static string? JoinList(IEnumerable<string>? values)
    {
        if (values is null)
        {
            return null;
        }

        var joined = string.Join(',', values.Where(static v => !string.IsNullOrWhiteSpace(v)));
        return joined.Length > 0 ? joined : null;
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
