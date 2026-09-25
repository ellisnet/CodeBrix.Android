using PolyHavenBrowser.PolyHavenApiClient;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PolyHavenBrowser.Services;

/// <summary>The result of making a model available locally: where its glTF landed.</summary>
public sealed class DownloadedModel
{
    /// <summary>The Poly Haven asset slug (id).</summary>
    public string Slug { get; set; }

    /// <summary>The local path of the model's <c>.gltf</c> file (sidecar files sit alongside).</summary>
    public string GltfPath { get; set; }

    /// <summary>The model's subfolder inside the user's chosen download folder.</summary>
    public string ModelFolder { get; set; }

    /// <summary>Whether the files were already present and no network download was needed.</summary>
    public bool WasAlreadyDownloaded { get; set; }
}

/// <summary>
/// Downloads a Poly Haven model's glTF (at 2k texture resolution, the app's sensible default)
/// with all its sidecar files into a per-model subfolder of the user's chosen download folder,
/// reporting overall byte progress for the window's download bar. When the model's subfolder
/// already holds the glTF, the existing files are used and no network traffic happens.
/// </summary>
public sealed class ModelDownloadService
{
    //Texture resolutions to try for the glTF download, best first: 2k is the sweet spot for
    //the viewer, with graceful fallback when an asset doesn't offer it.
    private static readonly string[] PreferredResolutions = ["2k", "1k", "4k", "8k"];

    private readonly IPolyHavenApiClientFactory _factory;

    /// <summary>Creates the service over the Poly Haven API client factory.</summary>
    public ModelDownloadService(IPolyHavenApiClientFactory factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    /// <summary>
    /// Returns the model's already-downloaded glTF path inside the chosen download folder,
    /// or <see langword="null"/> when the model has not been downloaded there yet.
    /// </summary>
    public string TryFindExistingGltf(string downloadFolder, string slug)
    {
        if (string.IsNullOrWhiteSpace(downloadFolder) || string.IsNullOrWhiteSpace(slug)) { return null; }

        var modelFolder = Path.Combine(downloadFolder, slug);
        if (!Directory.Exists(modelFolder)) { return null; }

        return Directory.EnumerateFiles(modelFolder, "*.gltf", SearchOption.TopDirectoryOnly).FirstOrDefault();
    }

    /// <summary>
    /// Ensures the model is available inside the download folder, downloading its glTF and
    /// sidecar files when they are not already there. Progress is reported as a fraction in
    /// [0, 1] across all files (the API advertises every file's byte size up front).
    /// </summary>
    public async Task<DownloadedModel> EnsureDownloadedAsync(
        PolyHavenAsset asset, string downloadFolder, IProgress<double> progress, CancellationToken cancellationToken)
    {
        if (asset == null) { throw new ArgumentNullException(nameof(asset)); }
        if (string.IsNullOrWhiteSpace(downloadFolder))
        {
            throw new ArgumentException("A download folder is required.", nameof(downloadFolder));
        }

        var slug = asset.Id ?? throw new ArgumentException("The asset has no id (slug).", nameof(asset));
        var modelFolder = Path.Combine(downloadFolder, slug);

        var existing = TryFindExistingGltf(downloadFolder, slug);
        if (existing != null)
        {
            progress?.Report(1d);
            return new DownloadedModel
            {
                Slug = slug,
                GltfPath = existing,
                ModelFolder = modelFolder,
                WasAlreadyDownloaded = true,
            };
        }

        using var client = _factory.GetClient();
        var files = await client.GetAssetFilesAsync(slug, cancellationToken).ConfigureAwait(false);
        var gltf = PickGltf(files) ?? throw new InvalidOperationException(
            $"Poly Haven offers no glTF download for \"{asset.Name ?? slug}\".");

        //Every file advertises its size, so the bar can show true byte progress across the
        //main .gltf and all its sidecars (buffer .bin + texture images).
        var totalBytes = Math.Max(1L, gltf.Size + (gltf.Include?.Values.Sum(f => f.Size) ?? 0L));
        var completedBytes = 0L;

        Directory.CreateDirectory(modelFolder);

        var gltfPath = Path.Combine(modelFolder, FileNameFromUrl(gltf.Url));
        await DownloadOneAsync(client, gltf, gltfPath, totalBytes, completedBytes, progress, cancellationToken)
            .ConfigureAwait(false);
        completedBytes += gltf.Size;

        //A glTF references its sidecar files by relative path; the Include dictionary is
        //keyed by exactly those relative paths, so the files land where the glTF expects them.
        if (gltf.Include != null)
        {
            foreach (var (relativePath, sidecar) in gltf.Include)
            {
                var sidecarPath = Path.Combine(modelFolder, relativePath.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(sidecarPath)!);
                await DownloadOneAsync(client, sidecar, sidecarPath, totalBytes, completedBytes, progress, cancellationToken)
                    .ConfigureAwait(false);
                completedBytes += sidecar.Size;
            }
        }

        progress?.Report(1d);
        return new DownloadedModel
        {
            Slug = slug,
            GltfPath = gltfPath,
            ModelFolder = modelFolder,
            WasAlreadyDownloaded = false,
        };
    }

    private static async Task DownloadOneAsync(
        IPolyHavenApiClient client, PolyHavenFileRef file, string destinationPath,
        long totalBytes, long completedBytes, IProgress<double> progress, CancellationToken cancellationToken)
    {
        var fileProgress = progress == null
            ? null
            : new Progress<PolyHavenDownloadProgress>(p =>
                progress.Report(Math.Min(1d, (completedBytes + p.BytesReceived) / (double)totalBytes)));

        await client.DownloadFileAsync(file, destinationPath, fileProgress, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    private static PolyHavenFileRef PickGltf(PolyHavenFileTree files)
    {
        foreach (var resolution in PreferredResolutions)
        {
            var file = files.FindFile("gltf", resolution, "gltf");
            if (file != null) { return file; }
        }

        //Fall back to any .gltf the asset offers, whatever its tree shape.
        return files.EnumerateFiles()
            .FirstOrDefault(e => e.Path.EndsWith("gltf", StringComparison.OrdinalIgnoreCase))?.File;
    }

    private static string FileNameFromUrl(string url)
    {
        var name = Path.GetFileName(new Uri(url).AbsolutePath);
        return string.IsNullOrWhiteSpace(name) ? "model.gltf" : name;
    }
}
