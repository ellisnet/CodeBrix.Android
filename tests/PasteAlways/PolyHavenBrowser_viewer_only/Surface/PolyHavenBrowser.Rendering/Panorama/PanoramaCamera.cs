namespace PolyHavenBrowser.Rendering;

/// <summary>
/// The view state for an equirectangular panorama: where the user is looking (yaw/pitch)
/// and how wide the view is (field of view). Drag input maps to <see cref="Rotate"/> and
/// scroll input to <see cref="Zoom"/>.
/// </summary>
public sealed class PanoramaCamera
{
    private const float MinPitch = -89f;
    private const float MaxPitch = 89f;
    private const float MinFov = 20f;
    private const float MaxFov = 120f;

    private float _pitchDegrees;
    private float _fovDegrees = 75f;

    /// <summary>
    /// The heading in degrees. 0 looks at the horizontal center of the panorama; positive
    /// values turn right. Wraps freely.
    /// </summary>
    public float YawDegrees { get; set; }

    /// <summary>The elevation in degrees, clamped to ±89 (positive looks up).</summary>
    public float PitchDegrees
    {
        get => _pitchDegrees;
        set => _pitchDegrees = Math.Clamp(value, MinPitch, MaxPitch);
    }

    /// <summary>The vertical field of view in degrees, clamped to [20, 120]. Defaults to 75.</summary>
    public float FovDegrees
    {
        get => _fovDegrees;
        set => _fovDegrees = Math.Clamp(value, MinFov, MaxFov);
    }

    /// <summary>Turns the view by the given deltas (e.g. from pointer-drag distance).</summary>
    public void Rotate(float deltaYawDegrees, float deltaPitchDegrees)
    {
        YawDegrees += deltaYawDegrees;
        PitchDegrees += deltaPitchDegrees;
    }

    /// <summary>Narrows (negative) or widens (positive) the field of view by the given degrees.</summary>
    public void Zoom(float deltaFovDegrees) => FovDegrees += deltaFovDegrees;
}
