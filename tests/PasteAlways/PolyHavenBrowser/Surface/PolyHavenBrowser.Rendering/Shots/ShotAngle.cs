using System.Numerics;

namespace PolyHavenBrowser.Rendering;

/// <summary>How a product shot is lit.</summary>
public enum ShotLightMode
{
    /// <summary>
    /// A key light hung above and to the left of the shot's camera — the classic product
    /// photography setup. Gives solid shapes strong form; faces away from the key go dark.
    /// </summary>
    Key,

    /// <summary>
    /// A headlight from the camera itself (the interactive preview's default). Flat but
    /// safe: everything the camera sees is lit.
    /// </summary>
    Headlight,
}

/// <summary>
/// A named product-shot camera preset: where the orbit camera sits, how snugly the model
/// fills the frame, and how the shot is lit. The presets cover the one-sheet's shot list —
/// the hero three-quarter view plus the front/side/back/top gallery.
/// </summary>
public sealed class ShotAngle
{
    /// <summary>Creates a preset.</summary>
    public ShotAngle(string caption, float yawDegrees, float pitchDegrees, float fitMargin,
        ShotLightMode lightMode = ShotLightMode.Key)
    {
        Caption = caption;
        YawDegrees = yawDegrees;
        PitchDegrees = pitchDegrees;
        FitMargin = fitMargin;
        LightMode = lightMode;
    }

    /// <summary>The shot's caption on the sheet, e.g. <c>Front</c>.</summary>
    public string Caption { get; }

    /// <summary>The camera heading around the model (0 faces the model's front).</summary>
    public float YawDegrees { get; }

    /// <summary>The camera elevation.</summary>
    public float PitchDegrees { get; }

    /// <summary>
    /// The framing padding applied to the bounding-box fit: 1.0 touches the frame edges,
    /// larger values leave proportionally more air around the model.
    /// </summary>
    public float FitMargin { get; }

    /// <summary>How the shot is lit.</summary>
    public ShotLightMode LightMode { get; }

    /// <summary>The hero beauty shot: an elevated three-quarter view, framed generously.</summary>
    public static ShotAngle Hero { get; } = new("Hero", 32f, 15f, 1.10f);

    /// <summary>Straight-on front view.</summary>
    public static ShotAngle Front { get; } = new("Front", 0f, 10f, 1.14f);

    /// <summary>Side profile (the model's right side).</summary>
    public static ShotAngle Side { get; } = new("Side", 90f, 10f, 1.14f);

    /// <summary>Straight-on back view.</summary>
    public static ShotAngle Back { get; } = new("Back", 180f, 10f, 1.14f);

    /// <summary>The top-down view, kept a few degrees off vertical so shapes keep some form.</summary>
    public static ShotAngle Top { get; } = new("Top", 32f, 84f, 1.14f);

    /// <summary>
    /// The world-space direction toward this shot's key light: hung 40° around from the
    /// camera and 55° up, so every angle is lit like a product photo rather than sharing
    /// one fixed sun that would leave the back shot in darkness.
    /// </summary>
    public Vector3 KeyLightDirection()
    {
        var yaw = (YawDegrees + 40f) * MathF.PI / 180f;
        const float pitch = 55f * MathF.PI / 180f;
        return Vector3.Normalize(new Vector3(
            MathF.Cos(pitch) * MathF.Sin(yaw),
            MathF.Sin(pitch),
            MathF.Cos(pitch) * MathF.Cos(yaw)));
    }
}
