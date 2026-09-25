using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PolyHavenBrowser.PolyHavenApiClient;

namespace PolyHavenBrowser.Services;

/// <summary>
/// The Browsing View's data source: fetches the complete Poly Haven <b>models</b> catalog
/// once per run (the API returns all model metadata in a single call), sorts and filters it
/// locally, and downloads cell thumbnails on demand with a small in-memory cache so scrolled
/// cells never re-hit the network.
/// </summary>
public sealed class ModelCatalogService
{
    //Cell thumbnails are requested at exactly the size the catalog cell displays them.
    private const int ThumbnailWidth = 512;
    private const int ThumbnailHeight = 288;

    private readonly IPolyHavenApiClientFactory _factory;
    private readonly SemaphoreSlim _catalogGate = new(1, 1);

    //At most a handful of thumbnail requests in flight at once - polite to the CDN, and
    //plenty to keep up with scrolling.
    private readonly SemaphoreSlim _thumbnailGate = new(4, 4);
    private readonly ConcurrentDictionary<string, byte[]> _thumbnailCache = new();

    private IReadOnlyList<PolyHavenAsset> _models;

    /// <summary>Creates the service over the Poly Haven API client factory.</summary>
    public ModelCatalogService(IPolyHavenApiClientFactory factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    /// <summary>
    /// Gets the full model catalog, fetching it from the API on the first call and
    /// serving it from memory afterwards. Every returned asset has its Id (slug) populated.
    /// </summary>
    public async Task<IReadOnlyList<PolyHavenAsset>> GetModelsAsync(CancellationToken cancellationToken)
    {
        if (_models != null) { return _models; }

        await _catalogGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_models != null) { return _models; }

            using var client = _factory.GetClient();
            var assets = await client.GetAssetsAsync(PolyHavenAssetType.Model, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            _models = assets.Values.ToList();
            return _models;
        }
        finally
        {
            _catalogGate.Release();
        }
    }

    /// <summary>
    /// Applies the search text and sort order to the catalog. The search matches the model's
    /// name, slug, categories and tags, case-insensitively; an empty search matches everything.
    /// </summary>
    public static IReadOnlyList<PolyHavenAsset> SortAndFilter(
        IReadOnlyList<PolyHavenAsset> models, CatalogSortOrder sortOrder, string searchText)
    {
        if (models == null) { throw new ArgumentNullException(nameof(models)); }

        IEnumerable<PolyHavenAsset> result = models;

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var term = searchText.Trim();
            result = result.Where(m => Matches(m, term));
        }

        result = sortOrder switch
        {
            CatalogSortOrder.Newest => result.OrderByDescending(m => m.DatePublished),
            CatalogSortOrder.NameAscending => result.OrderBy(m => m.Name ?? m.Id, StringComparer.OrdinalIgnoreCase),
            _ => result.OrderByDescending(m => m.DownloadCount),
        };

        return result.ToList();
    }

    /// <summary>
    /// Downloads (or serves from cache) a model's catalog-cell thumbnail as encoded image
    /// bytes, sized for the catalog cell.
    /// </summary>
    public async Task<byte[]> GetThumbnailAsync(PolyHavenAsset asset, CancellationToken cancellationToken)
    {
        if (asset == null) { throw new ArgumentNullException(nameof(asset)); }

        var key = asset.Id ?? asset.ThumbnailUrl ?? string.Empty;
        if (_thumbnailCache.TryGetValue(key, out var cached)) { return cached; }

        await _thumbnailGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_thumbnailCache.TryGetValue(key, out cached)) { return cached; }

            using var client = _factory.GetClient();
            var bytes = await client.GetThumbnailAsync(asset, ThumbnailWidth, ThumbnailHeight, cancellationToken)
                .ConfigureAwait(false);
            _thumbnailCache[key] = bytes;
            return bytes;
        }
        finally
        {
            _thumbnailGate.Release();
        }
    }

    private static bool Matches(PolyHavenAsset model, string term)
    {
        if (Contains(model.Name, term) || Contains(model.Id, term)) { return true; }

        if (model.Categories != null && model.Categories.Any(c => Contains(c, term))) { return true; }
        if (model.Tags != null && model.Tags.Any(t => Contains(t, term))) { return true; }

        return model.Authors != null && model.Authors.Keys.Any(a => Contains(a, term));
    }

    private static bool Contains(string text, string term) =>
        text != null && text.Contains(term, StringComparison.OrdinalIgnoreCase);
}
