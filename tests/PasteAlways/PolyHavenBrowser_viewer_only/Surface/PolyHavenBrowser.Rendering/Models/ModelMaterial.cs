using System.Numerics;

namespace PolyHavenBrowser.Rendering;

/// <summary>The glTF alpha-coverage mode of a material.</summary>
public enum ModelAlphaMode
{
    /// <summary>Fully opaque; the alpha channel is ignored (the glTF default).</summary>
    Opaque,

    /// <summary>Alpha-tested: fully opaque or fully transparent per-fragment at a cutoff.</summary>
    Mask,

    /// <summary>Alpha-blended (translucent) — glass and similar see-through surfaces.</summary>
    Blend,
}

/// <summary>
/// The display-relevant subset of a glTF material: base color (factor and optional
/// texture) plus the flags the preview renderer needs. Metallic/roughness factors are
/// carried for future PBR shading but are not used by the current preview shading.
/// </summary>
public sealed class ModelMaterial
{
    /// <summary>
    /// The preview opacity applied to <see cref="ModelAlphaMode.Blend"/> materials so glass and
    /// similar surfaces don't occlude what's behind them. glTF exports commonly mark glass as
    /// BLEND while leaving the base-color alpha opaque (relying on a transmission-capable viewer
    /// this preview doesn't implement); this constant instead gives such surfaces a fixed
    /// see-through look, multiplied onto any real base-color alpha.
    /// </summary>
    public const float BlendPreviewOpacity = 0.15f;

    /// <summary>The material name, when the source file provides one.</summary>
    public string? Name { get; init; }

    /// <summary>How the material's alpha is interpreted. Defaults to <see cref="ModelAlphaMode.Opaque"/>.</summary>
    public ModelAlphaMode AlphaMode { get; init; } = ModelAlphaMode.Opaque;

    /// <summary>The RGBA base color factor. Defaults to opaque white.</summary>
    public Vector4 BaseColorFactor { get; init; } = Vector4.One;

    /// <summary>The decoded base color texture as RGBA bytes (4 per pixel), or <see langword="null"/> when untextured.</summary>
    public byte[]? BaseColorTextureRgba { get; init; }

    /// <summary>The base color texture width in pixels (0 when untextured).</summary>
    public int BaseColorTextureWidth { get; init; }

    /// <summary>The base color texture height in pixels (0 when untextured).</summary>
    public int BaseColorTextureHeight { get; init; }

    /// <summary>The glTF metallic factor (carried for future use).</summary>
    public float MetallicFactor { get; init; } = 1f;

    /// <summary>The glTF roughness factor (carried for future use).</summary>
    public float RoughnessFactor { get; init; } = 1f;

    /// <summary>Whether the material renders both triangle faces.</summary>
    public bool DoubleSided { get; init; }
}
