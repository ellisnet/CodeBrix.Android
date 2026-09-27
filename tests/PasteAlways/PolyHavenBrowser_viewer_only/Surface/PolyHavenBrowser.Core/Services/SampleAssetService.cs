using PolyHavenBrowser.PolyHavenApiClient;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

// ReSharper disable InconsistentNaming

namespace PolyHavenBrowser.Services;

/// <summary>The kind of sample asset the browser can display.</summary>
public enum SampleAssetKind
{
    /// <summary>A PBR texture set (its diffuse map is shown on a lit cube).</summary>
    Texture,

    /// <summary>An equirectangular HDRI panorama.</summary>
    Hdri,

    /// <summary>A 3D model (glTF).</summary>
    Model,
}

/// <summary>The result of ensuring a sample asset is available locally.</summary>
public sealed class SampleAsset
{
    /// <summary>The Poly Haven asset slug (id).</summary>
    public string Slug { get; set; }

    /// <summary>The human-readable asset name.</summary>
    public string Name { get; set; }

    /// <summary>
    /// The local path of the primary file to open: the diffuse map (texture), the <c>.hdr</c>
    /// (HDRI), or the <c>.gltf</c> (model, with its sidecar files alongside).
    /// </summary>
    public string PrimaryFilePath { get; set; }
}

/// <summary>
/// Picks a representative Poly Haven asset of each kind (the most-downloaded one), downloads
/// its 1k files into a per-user cache, and reuses the cached files on later runs so the app
/// only hits the network the first time it shows each kind.
/// </summary>
public sealed class SampleAssetService
{
    private readonly IPolyHavenApiClientFactory _factory;
    private readonly string _cacheRoot;
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>Creates the service over a Poly Haven API client factory.</summary>
    public SampleAssetService(IPolyHavenApiClientFactory factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _cacheRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PolyHavenBrowser", "cache");
    }

    /// <summary>The root folder where downloaded sample assets are cached.</summary>
    public string CacheRoot => _cacheRoot;

    /// <summary>
    /// Ensures the sample asset of the given kind is available locally, downloading it (once)
    /// if needed, and returns its local file paths.
    /// </summary>
    public async Task<SampleAsset> EnsureSampleAsync(
        SampleAssetKind kind, IProgress<string> status, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var kindDir = Path.Combine(_cacheRoot, kind.ToString().ToLowerInvariant());
            var markerPath = Path.Combine(kindDir, "sample.json");
            var slug = SlugFor(kind);

            //Reuse the cached asset only when it is the SAME curated slug. The marker is keyed by
            //kind, so changing the curated slug (SlugFor) must invalidate a stale marker and
            //re-download rather than keep serving the previously-cached asset.
            var cached = TryReadMarker(markerPath);
            if (cached != null && string.Equals(cached.Slug, slug, StringComparison.Ordinal)
                && File.Exists(cached.PrimaryFilePath))
            {
                status?.Report($"{Describe(kind)}: {cached.Name} (cached)");
                return cached;
            }

            using var client = _factory.GetClient();

            status?.Report($"Finding {slug} on Poly Haven…");
            var asset = await client.GetAssetAsync(slug, cancellationToken).ConfigureAwait(false);
            var name = string.IsNullOrWhiteSpace(asset.Name) ? slug : asset.Name;
            status?.Report($"Downloading {kind.ToString().ToLowerInvariant()}: {name}…");

            var files = await client.GetAssetFilesAsync(slug, cancellationToken).ConfigureAwait(false);
            var assetDir = Path.Combine(kindDir, slug);
            Directory.CreateDirectory(assetDir);

            var primaryPath = kind switch
            {
                SampleAssetKind.Hdri => await DownloadHdriAsync(client, files, assetDir, cancellationToken).ConfigureAwait(false),
                SampleAssetKind.Texture => await DownloadTextureAsync(client, files, assetDir, cancellationToken).ConfigureAwait(false),
                SampleAssetKind.Model => await DownloadModelAsync(client, files, assetDir, cancellationToken).ConfigureAwait(false),
                _ => throw new ArgumentOutOfRangeException(nameof(kind)),
            };

            var result = new SampleAsset { Slug = slug, Name = name, PrimaryFilePath = primaryPath };
            WriteMarker(markerPath, result);
            status?.Report($"{Describe(kind)}: {name}");
            return result;
        }
        finally
        {
            _gate.Release();
        }
    }

    //Curated Poly Haven assets, chosen to look good in each display mode.
    private static string SlugFor(SampleAssetKind kind) => kind switch
    {
        SampleAssetKind.Texture => "red_brick",
        SampleAssetKind.Hdri => "small_cathedral",
        SampleAssetKind.Model => "Camera_01",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static string Describe(SampleAssetKind kind) => kind switch
    {
        SampleAssetKind.Texture => "Texture",
        SampleAssetKind.Hdri => "HDRI",
        SampleAssetKind.Model => "Model",
        _ => kind.ToString(),
    };

    private static async Task<string> DownloadHdriAsync(
        IPolyHavenApiClient client, PolyHavenFileTree files, string assetDir, CancellationToken ct)
    {
        var file = files.FindFile("hdri", "1k", "hdr")
            ?? files.EnumerateFiles().FirstOrDefault(e =>
                e.Path.EndsWith("hdr", StringComparison.OrdinalIgnoreCase) && e.Path.Contains("1k")).File
            ?? files.EnumerateFiles().FirstOrDefault(e =>
                e.Path.EndsWith("hdr", StringComparison.OrdinalIgnoreCase)).File
            ?? throw new InvalidOperationException("The chosen HDRI has no .hdr file.");

        var path = Path.Combine(assetDir, FileNameFromUrl(file.Url));
        await client.DownloadFileAsync(file, path, cancellationToken: ct).ConfigureAwait(false);
        return path;
    }

    private static async Task<string> DownloadTextureAsync(
        IPolyHavenApiClient client, PolyHavenFileTree files, string assetDir, CancellationToken ct)
    {
        //Prefer a 1k diffuse/albedo map in a browser-friendly format.
        var entry = files.EnumerateFiles().FirstOrDefault(e => IsDiffuse(e.Path) && Is1kImage(e.Path))
            ?? files.EnumerateFiles().FirstOrDefault(e => Is1kImage(e.Path))
            ?? files.EnumerateFiles().FirstOrDefault(e =>
                e.Path.EndsWith("jpg", StringComparison.OrdinalIgnoreCase) ||
                e.Path.EndsWith("png", StringComparison.OrdinalIgnoreCase));
        if (entry == null)
        {
            throw new InvalidOperationException("The chosen texture has no displayable image file.");
        }

        var path = Path.Combine(assetDir, FileNameFromUrl(entry.File.Url));
        await client.DownloadFileAsync(entry.File, path, cancellationToken: ct).ConfigureAwait(false);
        return path;
    }

    private static async Task<string> DownloadModelAsync(
        IPolyHavenApiClient client, PolyHavenFileTree files, string assetDir, CancellationToken ct)
    {
        var file = files.FindFile("gltf", "1k", "gltf")
            ?? files.EnumerateFiles().FirstOrDefault(e =>
                e.Path.EndsWith("gltf", StringComparison.OrdinalIgnoreCase) && e.Path.Contains("1k")).File
            ?? files.EnumerateFiles().FirstOrDefault(e =>
                e.Path.EndsWith("gltf", StringComparison.OrdinalIgnoreCase)).File
            ?? throw new InvalidOperationException("The chosen model has no glTF file.");

        var gltfPath = Path.Combine(assetDir, FileNameFromUrl(file.Url));
        await client.DownloadFileAsync(file, gltfPath, cancellationToken: ct).ConfigureAwait(false);

        //A glTF references sidecar files (its .bin buffer and textures) by relative path; the
        //  Include dictionary is keyed by exactly those relative paths.
        if (file.Include != null)
        {
            foreach (var (relativePath, sidecar) in file.Include)
            {
                var sidecarPath = Path.Combine(assetDir, relativePath.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(sidecarPath)!);
                await client.DownloadFileAsync(sidecar, sidecarPath, cancellationToken: ct).ConfigureAwait(false);
            }
        }

        return gltfPath;
    }

    private static bool IsDiffuse(string path)
    {
        var name = path.Split('/')[0].ToLowerInvariant();
        return name.Contains("diff") || name.Contains("albedo") || name == "color" || name == "col";
    }

    private static bool Is1kImage(string path) =>
        path.Contains("/1k/") &&
        (path.EndsWith("jpg", StringComparison.OrdinalIgnoreCase) ||
         path.EndsWith("png", StringComparison.OrdinalIgnoreCase));

    private static string FileNameFromUrl(string url)
    {
        var name = Path.GetFileName(new Uri(url).AbsolutePath);
        return string.IsNullOrWhiteSpace(name) ? "download" : name;
    }

    private static SampleAsset TryReadMarker(string markerPath)
    {
        try
        {
            if (!File.Exists(markerPath)) { return null; }
            return JsonSerializer.Deserialize<SampleAsset>(File.ReadAllText(markerPath));
        }
        catch
        {
            return null;
        }
    }

    private static void WriteMarker(string markerPath, SampleAsset asset)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(markerPath)!);
        File.WriteAllText(markerPath, JsonSerializer.Serialize(asset));
    }
}
