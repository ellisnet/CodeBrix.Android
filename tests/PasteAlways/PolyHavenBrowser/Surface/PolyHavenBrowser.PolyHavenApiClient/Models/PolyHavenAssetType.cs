namespace PolyHavenBrowser.PolyHavenApiClient;

/// <summary>
/// The type of a Poly Haven asset. The numeric values match the <c>type</c> field
/// returned by the Poly Haven API (0 = HDRI, 1 = texture, 2 = model).
/// </summary>
public enum PolyHavenAssetType
{
    /// <summary>A high-dynamic-range environment image (HDRI).</summary>
    Hdri = 0,

    /// <summary>A PBR texture set.</summary>
    Texture = 1,

    /// <summary>A 3D model.</summary>
    Model = 2,
}

/// <summary>
/// Helpers for converting <see cref="PolyHavenAssetType"/> values to and from the
/// string identifiers used by the Poly Haven API (<c>hdris</c>, <c>textures</c>, <c>models</c>).
/// </summary>
public static class PolyHavenAssetTypeExtensions
{
    /// <summary>
    /// Converts the asset type to the string identifier used by the Poly Haven API
    /// in URLs and query parameters.
    /// </summary>
    /// <param name="type">The asset type to convert.</param>
    /// <returns><c>hdris</c>, <c>textures</c>, or <c>models</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="type"/> is not a defined value.</exception>
    public static string ToApiString(this PolyHavenAssetType type) => type switch
    {
        PolyHavenAssetType.Hdri => "hdris",
        PolyHavenAssetType.Texture => "textures",
        PolyHavenAssetType.Model => "models",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown Poly Haven asset type."),
    };

    /// <summary>
    /// Attempts to parse an API string identifier (<c>hdris</c>, <c>textures</c>, or <c>models</c>,
    /// case-insensitive) into a <see cref="PolyHavenAssetType"/>.
    /// </summary>
    /// <param name="value">The API string identifier to parse.</param>
    /// <param name="type">The parsed asset type when the method returns <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when the value was recognized; otherwise <see langword="false"/>.</returns>
    public static bool TryParseApiString(string? value, out PolyHavenAssetType type)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case "hdris":
                type = PolyHavenAssetType.Hdri;
                return true;
            case "textures":
                type = PolyHavenAssetType.Texture;
                return true;
            case "models":
                type = PolyHavenAssetType.Model;
                return true;
            default:
                type = default;
                return false;
        }
    }
}
