using System.Text.Json;
using System.Text.Json.Serialization;

namespace PolyHavenBrowser.PolyHavenApiClient;

/// <summary>
/// Metadata for a single Poly Haven asset (HDRI, texture, or model), as returned by the
/// <c>/assets</c> and <c>/info/{id}</c> endpoints. Fields that only apply to one asset
/// type (for example <see cref="EvsCap"/> for HDRIs or <see cref="Dimensions"/> for
/// textures) are <see langword="null"/> for other types. Any response fields without a
/// typed property are preserved in <see cref="ExtensionData"/>.
/// </summary>
public class PolyHavenAsset
{
    /// <summary>
    /// The unique ID (slug) of the asset, e.g. <c>abandoned_bakery</c>. This is not part of
    /// the asset JSON itself; it is populated by the client from the asset-list key or the
    /// requested ID.
    /// </summary>
    [JsonIgnore]
    public string? Id { get; set; }

    /// <summary>The human-readable display name of the asset.</summary>
    public string? Name { get; set; }

    /// <summary>The type of the asset (HDRI, texture, or model).</summary>
    public PolyHavenAssetType Type { get; set; }

    /// <summary>The categories this asset belongs to.</summary>
    public string[]? Categories { get; set; }

    /// <summary>Freeform search tags for the asset.</summary>
    public string[]? Tags { get; set; }

    /// <summary>
    /// The authors of the asset, keyed by author name with a value describing their
    /// contribution (e.g. <c>"All"</c>). Author names can be passed to
    /// <see cref="IPolyHavenApiClient.GetAuthorAsync"/>.
    /// </summary>
    public Dictionary<string, string>? Authors { get; set; }

    /// <summary>A short description of the asset, when available.</summary>
    public string? Description { get; set; }

    /// <summary>The publish date as a Unix timestamp in seconds.</summary>
    public long DatePublished { get; set; }

    /// <summary>The publish date as a UTC <see cref="DateTimeOffset"/>.</summary>
    [JsonIgnore]
    public DateTimeOffset DatePublishedUtc => DateTimeOffset.FromUnixTimeSeconds(DatePublished);

    /// <summary>For HDRIs, the capture date as a Unix timestamp in seconds.</summary>
    public long? DateTaken { get; set; }

    /// <summary>For HDRIs, the capture date as a UTC <see cref="DateTimeOffset"/>.</summary>
    [JsonIgnore]
    public DateTimeOffset? DateTakenUtc =>
        DateTaken is { } taken ? DateTimeOffset.FromUnixTimeSeconds(taken) : null;

    /// <summary>The total number of times the asset has been downloaded.</summary>
    public long DownloadCount { get; set; }

    /// <summary>A hash of the asset's file set; changes when any of its files change.</summary>
    public string? FilesHash { get; set; }

    /// <summary>
    /// The URL of the asset's thumbnail image on the Poly Haven CDN. The URL accepts
    /// <c>width</c> and <c>height</c> query parameters; see
    /// <see cref="IPolyHavenApiClient.GetThumbnailUrl"/>.
    /// </summary>
    public string? ThumbnailUrl { get; set; }

    /// <summary>The maximum available resolution as <c>[width, height]</c> in pixels.</summary>
    public int[]? MaxResolution { get; set; }

    /// <summary>Whether the asset was donated (not funded by Poly Haven).</summary>
    public bool? Donated { get; set; }

    /// <summary>For HDRIs, the capture location as <c>[latitude, longitude]</c>.</summary>
    public double[]? Coords { get; set; }

    /// <summary>For HDRIs, whether backplate photos are available for the asset.</summary>
    public bool? Backplates { get; set; }

    /// <summary>For HDRIs, the dynamic range captured, in EVs.</summary>
    public int? EvsCap { get; set; }

    /// <summary>For HDRIs, the white balance the HDRI was shot at, in Kelvin.</summary>
    public int? Whitebalance { get; set; }

    /// <summary>For textures, the real-world dimensions as <c>[x, y]</c> in millimeters.</summary>
    public double[]? Dimensions { get; set; }

    /// <summary>
    /// For models, whether level-of-detail (LOD) versions are available. (The API's swagger
    /// document describes this as an array, but the live API returns a boolean.)
    /// </summary>
    public bool? Lods { get; set; }

    /// <summary>
    /// Any additional response fields that do not map to a typed property (for example
    /// <c>attributes</c>, <c>category</c>, or <c>sponsors</c>), preserved as raw JSON.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}
