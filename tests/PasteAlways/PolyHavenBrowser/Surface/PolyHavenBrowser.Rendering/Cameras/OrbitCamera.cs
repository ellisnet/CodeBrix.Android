using System.Numerics;

namespace PolyHavenBrowser.Rendering;

/// <summary>
/// An orbit (turntable) camera for model viewing: the eye circles a target point at a
/// distance, with spin (<see cref="Orbit"/>), zoom (<see cref="Zoom"/>), and pan
/// (<see cref="Pan"/>). Produces standard view/projection matrices.
/// </summary>
public sealed class OrbitCamera
{
    private const float MinPitch = -89f;
    private const float MaxPitch = 89f;
    private const float MinFov = 10f;
    private const float MaxFov = 120f;

    private float _pitchDegrees = 15f;
    private float _distance = 3f;
    private float _fovDegrees = 45f;

    /// <summary>The heading of the eye around the target, in degrees. Wraps freely.</summary>
    public float YawDegrees { get; set; } = 30f;

    /// <summary>The elevation of the eye, in degrees, clamped to ±89.</summary>
    public float PitchDegrees
    {
        get => _pitchDegrees;
        set => _pitchDegrees = Math.Clamp(value, MinPitch, MaxPitch);
    }

    /// <summary>The distance from the eye to the target; never below <see cref="MinDistance"/>.</summary>
    public float Distance
    {
        get => _distance;
        set => _distance = Math.Max(value, MinDistance);
    }

    /// <summary>The smallest allowed <see cref="Distance"/>. Defaults to 0.001.</summary>
    public float MinDistance { get; set; } = 0.001f;

    /// <summary>
    /// The extra distance multiplier applied when framing a model, controlling how much of
    /// the view it fills. 1.1 fits it snugly; larger values leave more empty space around it
    /// (useful for a compact shape whose silhouette should stay fully visible while rotating);
    /// values below 1 zoom in past a snug fit (its extremities may leave the view).
    /// </summary>
    public float FitMargin { get; set; } = 1.1f;

    /// <summary>
    /// Raises the framing look-at point above the model's pivot by this fraction of the model
    /// radius, so the model sits lower in the view. 0 (the default) centers it vertically.
    /// </summary>
    public float VerticalFramingBias { get; set; }

    /// <summary>The point the camera looks at and orbits around.</summary>
    public Vector3 Target { get; set; }

    /// <summary>The vertical field of view in degrees, clamped to [10, 120]. Defaults to 45.</summary>
    public float FovDegrees
    {
        get => _fovDegrees;
        set => _fovDegrees = Math.Clamp(value, MinFov, MaxFov);
    }

    /// <summary>Spins the camera around the target (e.g. from pointer-drag deltas).</summary>
    public void Orbit(float deltaYawDegrees, float deltaPitchDegrees)
    {
        YawDegrees += deltaYawDegrees;
        PitchDegrees += deltaPitchDegrees;
    }

    /// <summary>
    /// Zooms by scaling the distance: factors below 1 move closer, above 1 move away
    /// (e.g. pass 0.9 / 1.1 per scroll-wheel notch).
    /// </summary>
    public void Zoom(float factor)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(factor);
        Distance *= factor;
    }

    /// <summary>
    /// Slides the target in the current view plane. Deltas are in fractions of the view
    /// height (e.g. pixel delta / viewport height), so panning feels the same at any zoom.
    /// </summary>
    public void Pan(float deltaX, float deltaY)
    {
        var (right, up) = GetViewPlaneAxes();
        var worldPerViewHeight = 2f * Distance * MathF.Tan(FovDegrees * MathF.PI / 360f);
        Target += (-deltaX * right + deltaY * up) * worldPerViewHeight;
    }

    /// <summary>The current eye position in world space.</summary>
    public Vector3 GetEyePosition()
    {
        var yaw = YawDegrees * MathF.PI / 180f;
        var pitch = PitchDegrees * MathF.PI / 180f;
        var direction = new Vector3(
            MathF.Cos(pitch) * MathF.Sin(yaw),
            MathF.Sin(pitch),
            MathF.Cos(pitch) * MathF.Cos(yaw));
        return Target + direction * Distance;
    }

    /// <summary>The view (world → camera) matrix.</summary>
    public Matrix4x4 GetViewMatrix() => Matrix4x4.CreateLookAt(GetEyePosition(), Target, Vector3.UnitY);

    /// <summary>The perspective projection matrix, with near/far planes scaled to the current distance.</summary>
    public Matrix4x4 GetProjectionMatrix(float aspectRatio)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(aspectRatio);
        var near = Math.Max(Distance * 0.01f, 0.0001f);
        var far = Math.Max(Distance * 100f, near * 10f);
        return Matrix4x4.CreatePerspectiveFieldOfView(FovDegrees * MathF.PI / 180f, aspectRatio, near, far);
    }

    /// <summary>Frames the camera on a model's bounding box, keeping the current yaw/pitch.</summary>
    public void FitToModel(LoadedModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        FitToBounds(model.BoundsMin, model.BoundsMax);
        //Orbit around the centroid when the model provides one, so a model with a sparse
        //extremity rotates in place instead of swinging around the bounding-box center. Raise
        //the look-at point by VerticalFramingBias so the model can sit lower in the view.
        var radius = MathF.Max((model.BoundsMax - model.BoundsMin).Length() * 0.5f, 0.001f);
        Target = (model.Pivot ?? model.BoundsCenter) + new Vector3(0f, radius * VerticalFramingBias, 0f);
    }

    /// <summary>Frames the camera on a bounding box, keeping the current yaw/pitch.</summary>
    public void FitToBounds(Vector3 boundsMin, Vector3 boundsMax)
    {
        Target = (boundsMin + boundsMax) * 0.5f;
        var radius = Math.Max((boundsMax - boundsMin).Length() * 0.5f, 0.001f);
        // Distance so the bounding sphere fits the vertical fov, with FitMargin of headroom.
        Distance = radius / MathF.Sin(FovDegrees * MathF.PI / 360f) * FitMargin;
    }

    private (Vector3 Right, Vector3 Up) GetViewPlaneAxes()
    {
        var forward = Vector3.Normalize(Target - GetEyePosition());
        var right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitY));
        var up = Vector3.Cross(right, forward);
        return (right, up);
    }
}
