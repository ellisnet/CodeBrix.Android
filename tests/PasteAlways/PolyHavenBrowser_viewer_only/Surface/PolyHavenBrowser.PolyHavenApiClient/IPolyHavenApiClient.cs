namespace PolyHavenBrowser.PolyHavenApiClient;

/// <summary>
/// A client for the Poly Haven API (<c>https://api.polyhaven.com</c>), covering the full
/// public API surface: browsing the asset catalog, reading asset/author/category metadata,
/// listing an asset's downloadable files, downloading those files, and fetching asset
/// thumbnail images. Obtain instances from an <see cref="IPolyHavenApiClientFactory"/>,
/// and dispose them when finished.
/// </summary>
public interface IPolyHavenApiClient : IDisposable
{
    /// <summary>
    /// Gets the list of asset types available on Poly Haven
    /// (currently <c>hdris</c>, <c>textures</c>, and <c>models</c>). Calls <c>GET /types</c>.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <exception cref="PolyHavenApiException">The request failed.</exception>
    Task<IReadOnlyList<string>> GetAssetTypesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the asset catalog, optionally filtered by type and/or categories, keyed by
    /// asset ID (slug). Each returned asset has <see cref="PolyHavenAsset.Id"/> populated.
    /// Calls <c>GET /assets</c>.
    /// </summary>
    /// <param name="type">The asset type to filter by, or <see langword="null"/> for all types.</param>
    /// <param name="categories">Category names the assets must belong to, or <see langword="null"/> for no category filter.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <exception cref="PolyHavenApiException">The request failed.</exception>
    Task<IReadOnlyDictionary<string, PolyHavenAsset>> GetAssetsAsync(
        PolyHavenAssetType? type = null,
        IEnumerable<string>? categories = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the full metadata of a single asset. The returned asset has
    /// <see cref="PolyHavenAsset.Id"/> populated. Calls <c>GET /info/{id}</c>.
    /// </summary>
    /// <param name="assetId">The asset's unique ID (slug), e.g. <c>abandoned_bakery</c>.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <exception cref="PolyHavenNotFoundException">No asset exists with the given ID.</exception>
    /// <exception cref="PolyHavenApiException">The request failed.</exception>
    Task<PolyHavenAsset> GetAssetAsync(string assetId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the catalog of downloadable files for an asset, as a traversable tree
    /// (for example <c>hdri/4k/hdr</c> or <c>Diffuse/1k/jpg</c>). Calls <c>GET /files/{id}</c>.
    /// </summary>
    /// <param name="assetId">The asset's unique ID (slug).</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <exception cref="PolyHavenNotFoundException">No asset exists with the given ID.</exception>
    /// <exception cref="PolyHavenApiException">The request failed.</exception>
    Task<PolyHavenFileTree> GetAssetFilesAsync(string assetId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets information about an asset author. Author IDs are the keys of
    /// <see cref="PolyHavenAsset.Authors"/>. Calls <c>GET /author/{id}</c>.
    /// </summary>
    /// <param name="authorId">The author's unique ID (their name), e.g. <c>Greg Zaal</c>.</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <exception cref="PolyHavenNotFoundException">No author exists with the given ID.</exception>
    /// <exception cref="PolyHavenApiException">The request failed.</exception>
    Task<PolyHavenAuthor> GetAuthorAsync(string authorId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the available categories for an asset type, with the number of assets in each.
    /// Calls <c>GET /categories/{type}</c>.
    /// </summary>
    /// <param name="type">The asset type to list categories for.</param>
    /// <param name="inCategories">When provided, only counts assets that are also in all of these categories (the API's <c>in</c> parameter).</param>
    /// <param name="cancellationToken">A token to cancel the request.</param>
    /// <exception cref="PolyHavenApiException">The request failed.</exception>
    Task<IReadOnlyDictionary<string, int>> GetCategoriesAsync(
        PolyHavenAssetType type,
        IEnumerable<string>? inCategories = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds the URL of an asset's thumbnail image at the requested size. When both
    /// <paramref name="width"/> and <paramref name="height"/> are <see langword="null"/>,
    /// the asset's original thumbnail URL is returned unchanged.
    /// </summary>
    /// <param name="asset">The asset whose thumbnail URL to build. Must have <see cref="PolyHavenAsset.ThumbnailUrl"/> set.</param>
    /// <param name="width">The requested image width in pixels, or <see langword="null"/>.</param>
    /// <param name="height">The requested image height in pixels, or <see langword="null"/>.</param>
    /// <exception cref="ArgumentException">The asset has no thumbnail URL.</exception>
    string GetThumbnailUrl(PolyHavenAsset asset, int? width = null, int? height = null);

    /// <summary>
    /// Downloads an asset's thumbnail image at the requested size and returns the raw
    /// image bytes (typically PNG or WebP).
    /// </summary>
    /// <param name="asset">The asset whose thumbnail to download. Must have <see cref="PolyHavenAsset.ThumbnailUrl"/> set.</param>
    /// <param name="width">The requested image width in pixels, or <see langword="null"/>.</param>
    /// <param name="height">The requested image height in pixels, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">A token to cancel the download.</param>
    /// <exception cref="ArgumentException">The asset has no thumbnail URL.</exception>
    /// <exception cref="PolyHavenApiException">The request failed.</exception>
    Task<byte[]> GetThumbnailAsync(
        PolyHavenAsset asset,
        int? width = null,
        int? height = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads any image by absolute URL (for example a thumbnail or render URL taken
    /// from asset metadata) and returns the raw image bytes.
    /// </summary>
    /// <param name="imageUrl">The absolute URL of the image.</param>
    /// <param name="cancellationToken">A token to cancel the download.</param>
    /// <exception cref="PolyHavenApiException">The request failed.</exception>
    Task<byte[]> GetImageAsync(string imageUrl, CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads a file to the given stream, optionally reporting progress and verifying
    /// the file's MD5 checksum.
    /// </summary>
    /// <param name="file">The file to download, taken from a <see cref="PolyHavenFileTree"/>.</param>
    /// <param name="destination">The writable stream to copy the file's bytes to.</param>
    /// <param name="progress">An optional progress sink, called as bytes arrive.</param>
    /// <param name="verifyMd5">When <see langword="true"/> and the file advertises an MD5 checksum, verifies the downloaded bytes against it.</param>
    /// <param name="cancellationToken">A token to cancel the download.</param>
    /// <exception cref="PolyHavenIntegrityException">MD5 verification was requested and the checksum did not match.</exception>
    /// <exception cref="PolyHavenApiException">The request failed.</exception>
    Task DownloadFileAsync(
        PolyHavenFileRef file,
        Stream destination,
        IProgress<PolyHavenDownloadProgress>? progress = null,
        bool verifyMd5 = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads a file to the given path, creating the containing directory when needed,
    /// optionally reporting progress and verifying the file's MD5 checksum. On failure,
    /// any partially written file is deleted.
    /// </summary>
    /// <param name="file">The file to download, taken from a <see cref="PolyHavenFileTree"/>.</param>
    /// <param name="destinationFilePath">The path to write the file to. An existing file is overwritten.</param>
    /// <param name="progress">An optional progress sink, called as bytes arrive.</param>
    /// <param name="verifyMd5">When <see langword="true"/> and the file advertises an MD5 checksum, verifies the downloaded bytes against it.</param>
    /// <param name="cancellationToken">A token to cancel the download.</param>
    /// <exception cref="PolyHavenIntegrityException">MD5 verification was requested and the checksum did not match.</exception>
    /// <exception cref="PolyHavenApiException">The request failed.</exception>
    Task DownloadFileAsync(
        PolyHavenFileRef file,
        string destinationFilePath,
        IProgress<PolyHavenDownloadProgress>? progress = null,
        bool verifyMd5 = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads a file into memory and returns its bytes, optionally reporting progress
    /// and verifying the file's MD5 checksum. Intended for small files; prefer the stream
    /// or path overloads for large downloads.
    /// </summary>
    /// <param name="file">The file to download, taken from a <see cref="PolyHavenFileTree"/>.</param>
    /// <param name="progress">An optional progress sink, called as bytes arrive.</param>
    /// <param name="verifyMd5">When <see langword="true"/> and the file advertises an MD5 checksum, verifies the downloaded bytes against it.</param>
    /// <param name="cancellationToken">A token to cancel the download.</param>
    /// <exception cref="PolyHavenIntegrityException">MD5 verification was requested and the checksum did not match.</exception>
    /// <exception cref="PolyHavenApiException">The request failed.</exception>
    Task<byte[]> DownloadFileAsync(
        PolyHavenFileRef file,
        IProgress<PolyHavenDownloadProgress>? progress = null,
        bool verifyMd5 = false,
        CancellationToken cancellationToken = default);
}
