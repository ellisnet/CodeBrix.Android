namespace PolyHavenBrowser.Rendering;

/// <summary>The tone-mapping operator used to compress HDR values into display range.</summary>
public enum ToneMapOperator
{
    /// <summary>The ACES filmic approximation (Narkowicz) — the recommended default for HDRIs.</summary>
    AcesFilmic,

    /// <summary>The classic Reinhard operator, <c>c / (1 + c)</c> — cheaper, flatter look.</summary>
    Reinhard,

    /// <summary>No compression; values are simply clamped to [0, 1]. Useful for inspecting data maps.</summary>
    Clamp,
}
