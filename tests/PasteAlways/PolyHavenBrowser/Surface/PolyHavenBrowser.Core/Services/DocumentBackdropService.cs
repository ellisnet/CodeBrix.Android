using PolyHavenBrowser.PolyHavenApiClient;
using PolyHavenBrowser.Rendering;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PolyHavenBrowser.Services;

/// <summary>
/// Provides the three photography stages the one-sheet's product shots are set on: the
/// warm <c>wood_table</c> tabletop for the hero, light <c>marble_01</c> and dark
/// <c>dark_wood</c> studio coves for the gallery — all free CC0 Poly Haven textures. The
/// 1k diffuse maps are downloaded once into a cache folder and reused; when a texture
/// cannot be fetched (offline, for instance) its stage falls back to a plain colored
/// floor, so document creation always proceeds.
/// </summary>
public sealed class DocumentBackdropService
{
    private const string HeroTextureSlug = "wood_table";
    private const string LightTextureSlug = "marble_01";
    private const string DarkTextureSlug = "dark_wood";

    private readonly IPolyHavenApiClientFactory _factory;

    /// <summary>Creates the service over the Poly Haven API client factory.</summary>
    public DocumentBackdropService(IPolyHavenApiClientFactory factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    /// <summary>The stages for one document run: hero tabletop, light cove, dark cove.</summary>
    public sealed class BackdropStages
    {
        /// <summary>The hero shot's warm wooden tabletop stage.</summary>
        public required ShotStage Tabletop { get; init; }

        /// <summary>The light studio stage (front/side gallery shots).</summary>
        public required ShotStage Light { get; init; }

        /// <summary>The dark studio stage (back/top gallery shots).</summary>
        public required ShotStage Dark { get; init; }
    }

    /// <summary>
    /// Builds the three stages, downloading (or reusing cached) floor textures under
    /// <paramref name="cacheFolder"/>.
    /// </summary>
    public async Task<BackdropStages> GetStagesAsync(string cacheFolder, CancellationToken cancellationToken)
    {
        var wood = await TryGetDiffuseRgbaAsync(HeroTextureSlug, cacheFolder, cancellationToken).ConfigureAwait(false);
        var marble = await TryGetDiffuseRgbaAsync(LightTextureSlug, cacheFolder, cancellationToken).ConfigureAwait(false);
        var dark = await TryGetDiffuseRgbaAsync(DarkTextureSlug, cacheFolder, cancellationToken).ConfigureAwait(false);

        return new BackdropStages
        {
            Tabletop = ShotStage.Tabletop(wood?.Rgba, wood?.Width ?? 0, wood?.Height ?? 0),
            Light = ShotStage.Light(marble?.Rgba, marble?.Width ?? 0, marble?.Height ?? 0),
            Dark = ShotStage.Dark(dark?.Rgba, dark?.Width ?? 0, dark?.Height ?? 0),
        };
    }

    private sealed record DecodedTexture(byte[] Rgba, int Width, int Height);

    //Returns the texture's decoded 1k diffuse map, from cache or the network — or null when
    //  it cannot be obtained, in which case the stage uses its plain floor color.
    private async Task<DecodedTexture> TryGetDiffuseRgbaAsync(
        string slug, string cacheFolder, CancellationToken cancellationToken)
    {
        try
        {
            var cachePath = Path.Combine(cacheFolder, $"{slug}_diff_1k.jpg");
            byte[] encoded;
            if (File.Exists(cachePath))
            {
                encoded = await File.ReadAllBytesAsync(cachePath, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                using var client = _factory.GetClient();
                var files = await client.GetAssetFilesAsync(slug, cancellationToken).ConfigureAwait(false);
                var diffuse = files.FindFile("Diffuse", "1k", "jpg")
                    ?? files.EnumerateFiles().FirstOrDefault(e =>
                        e.Path.Contains("diff", StringComparison.OrdinalIgnoreCase)
                        && e.Path.Contains("1k", StringComparison.OrdinalIgnoreCase)
                        && e.Path.EndsWith("jpg", StringComparison.OrdinalIgnoreCase))?.File;
                if (diffuse == null) { return null; }

                encoded = await client.DownloadFileAsync(diffuse, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);

                Directory.CreateDirectory(cacheFolder);
                await File.WriteAllBytesAsync(cachePath, encoded, cancellationToken).ConfigureAwait(false);
            }

            var (rgba, width, height) = LdrImageDecoder.DecodeToRgbaBytes(encoded);
            return new DecodedTexture(rgba, width, height);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            //Offline, or the texture asset changed shape: the stage's plain floor still works.
            return null;
        }
    }
}
