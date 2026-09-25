namespace PolyHavenBrowser.Rendering;

/// <summary>
/// The set dressing behind a product shot: an optional floor texture (a Poly Haven CC0
/// texture's diffuse map, decoded to RGBA) and the backdrop gradient of the "infinity
/// cove" that curves up behind the model. The model itself is never re-textured — the
/// stage is only the surface it stands on and the wall behind it.
/// </summary>
public sealed class ShotStage
{
    /// <summary>The decoded floor texture (RGBA, 4 bytes per pixel), or <see langword="null"/> for a plain floor.</summary>
    public byte[]? FloorTextureRgba { get; init; }

    /// <summary>The floor texture's width in pixels.</summary>
    public int FloorTextureWidth { get; init; }

    /// <summary>The floor texture's height in pixels.</summary>
    public int FloorTextureHeight { get; init; }

    /// <summary>
    /// The floor texture's physical repeat size in world units (Poly Haven textures are
    /// authored around one meter). Larger values scale the pattern up.
    /// </summary>
    public float FloorTextureWorldSize { get; init; } = 1.0f;

    /// <summary>The plain floor color (sRGB) used when there is no floor texture.</summary>
    public (byte R, byte G, byte B) FloorColor { get; init; } = (0xE8, 0xE9, 0xEB);

    /// <summary>The backdrop gradient color (sRGB) at floor level.</summary>
    public (byte R, byte G, byte B) BackdropBottom { get; init; } = (0xE2, 0xE4, 0xE7);

    /// <summary>The backdrop gradient color (sRGB) at the top of the cove.</summary>
    public (byte R, byte G, byte B) BackdropTop { get; init; } = (0xC6, 0xC9, 0xCE);

    /// <summary>The contact shadow's maximum darkening in [0, 1].</summary>
    public float ShadowStrength { get; init; } = 0.45f;

    /// <summary>
    /// A light studio stage: a bright cove that makes mid and dark models pop. Pass a
    /// light floor texture (e.g. Poly Haven's <c>marble_01</c> diffuse) or none for a
    /// plain bright floor.
    /// </summary>
    public static ShotStage Light(byte[]? floorRgba = null, int width = 0, int height = 0) => new()
    {
        FloorTextureRgba = floorRgba,
        FloorTextureWidth = width,
        FloorTextureHeight = height,
        FloorColor = (0xEA, 0xEA, 0xEC),
        BackdropBottom = (0xEF, 0xF0, 0xF2),
        BackdropTop = (0xC9, 0xCC, 0xD1),
        ShadowStrength = 0.38f,
    };

    /// <summary>
    /// A dark studio stage: a moody cove that makes light models pop. Pass a dark floor
    /// texture (e.g. Poly Haven's <c>dark_wood</c> diffuse) or none for a plain dark floor.
    /// </summary>
    public static ShotStage Dark(byte[]? floorRgba = null, int width = 0, int height = 0) => new()
    {
        FloorTextureRgba = floorRgba,
        FloorTextureWidth = width,
        FloorTextureHeight = height,
        FloorColor = (0x24, 0x25, 0x29),
        BackdropBottom = (0x2E, 0x31, 0x38),
        BackdropTop = (0x14, 0x15, 0x18),
        ShadowStrength = 0.55f,
    };

    /// <summary>
    /// The hero tabletop stage: a warm dark backdrop over a wooden table floor (e.g. Poly
    /// Haven's <c>wood_table</c> diffuse) — the classic product photograph.
    /// </summary>
    public static ShotStage Tabletop(byte[]? floorRgba = null, int width = 0, int height = 0) => new()
    {
        FloorTextureRgba = floorRgba,
        FloorTextureWidth = width,
        FloorTextureHeight = height,
        FloorColor = (0x4E, 0x3D, 0x2B),
        BackdropBottom = (0x3D, 0x35, 0x2A),
        BackdropTop = (0x17, 0x14, 0x0F),
        ShadowStrength = 0.50f,
    };
}
