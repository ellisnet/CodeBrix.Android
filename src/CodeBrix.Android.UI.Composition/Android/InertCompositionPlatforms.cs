using System;
using System.IO;
using System.Numerics;
using CodeBrix.Android.UI.Composition.Portable;
using CodeBrix.Platform.UI.Composition.Contracts;
using Microsoft.UI.Composition;
using Windows.Foundation;

namespace CodeBrix.Android.UI.Composition.Android;

/// <summary>
/// The per-visual platform object of the inert composition platform: nothing is painted
/// (native views do the drawing on Android), the hit test is a bounds test of the
/// visual's size.
/// </summary>
internal sealed class VisualAndroidPlatform : IVisualPlatform
{
    private readonly Visual _owner;

    internal VisualAndroidPlatform(Visual owner) => _owner = owner;

    /// <inheritdoc />
    public bool RequiresRepaintOnEveryFrame => false;

    /// <inheritdoc />
    public void DiscardPaintCache()
    {
    }

    /// <inheritdoc />
    public void DiscardChildrenCache()
    {
    }

    /// <inheritdoc />
    public bool CanPaint() => false;

    /// <inheritdoc />
    public bool HitTest(Point point)
    {
        var size = _owner?.Size ?? Vector2.Zero;
        return InertHitTest.IsInside(point.X, point.Y, size.X, size.Y);
    }
}

/// <summary>The per-brush platform object of the inert composition platform.</summary>
internal sealed class CompositionBrushAndroidPlatform : ICompositionBrushPlatform
{
    internal static readonly CompositionBrushAndroidPlatform Instance = new();

    /// <inheritdoc />
    public bool RequiresRepaintOnEveryFrame => false;

    /// <inheritdoc />
    public Vector2? Size => null;

    /// <inheritdoc />
    public bool CanPaint() => false;

    /// <inheritdoc />
    public void OnPropertyChanged(string propertyName)
    {
    }

    /// <inheritdoc />
    public void Dispose()
    {
    }
}

/// <summary>The per-clip platform object of the inert composition platform.</summary>
internal sealed class CompositionClipAndroidPlatform : ICompositionClipPlatform
{
    internal static readonly CompositionClipAndroidPlatform Instance = new();
}

/// <summary>The per-shape platform object of the inert composition platform.</summary>
internal sealed class CompositionShapeAndroidPlatform : ICompositionShapePlatform
{
    internal static readonly CompositionShapeAndroidPlatform Instance = new();

    /// <inheritdoc />
    public bool CanPaint() => false;

    /// <inheritdoc />
    public bool HitTest(Point point) => false;

    /// <inheritdoc />
    public void OnGeometryChanged()
    {
    }
}

/// <summary>
/// The per-surface platform object of the inert composition platform. Image decoding
/// for composition surfaces is not done here (images are shown by native views).
/// </summary>
internal sealed class CompositionSurfaceAndroidPlatform : ICompositionSurfacePlatform
{
    internal static readonly CompositionSurfaceAndroidPlatform Instance = new();

    /// <inheritdoc />
    public (bool success, object nativeResult) LoadFromStream(int? targetWidth, int? targetHeight, Stream imageStream) => (false, null);

    /// <inheritdoc />
    public void CopyPixels(int pixelWidth, int pixelHeight, ReadOnlyMemory<byte> data)
    {
    }

    /// <inheritdoc />
    public void Dispose()
    {
    }
}

/// <summary>
/// The Android implementation of <see cref="ICompositionPlatform"/>: NEUTRALISED (plan
/// decision D-P2). Core still builds its managed Visual tree - a Visual is created in
/// every UIElement's constructor - but every factory returns an inert platform object,
/// nothing paints, and effects are reported as unsupported. App-level Composition API
/// use is accepted and ignored.
/// </summary>
internal sealed class CompositionAndroidPlatform : ICompositionPlatform
{
    private readonly Func<PlatformCompositionSurface, ICompositionSurfacePlatform> _surfaceFactory;

    /// <summary>Creates the platform; every surface gets the inert surface platform.</summary>
    internal CompositionAndroidPlatform()
        : this(null)
    {
    }

    /// <summary>
    /// Creates the platform with a per-surface factory (the Android bootstrap passes the
    /// android.graphics.Bitmap-backed one, so decoded and rendered images keep their pixels).
    /// </summary>
    internal CompositionAndroidPlatform(Func<PlatformCompositionSurface, ICompositionSurfacePlatform> surfaceFactory)
    {
        _surfaceFactory = surfaceFactory ?? (_ => CompositionSurfaceAndroidPlatform.Instance);
    }

    /// <inheritdoc />
    public IVisualPlatform CreateVisualPlatform(Visual owner) => new VisualAndroidPlatform(owner);

    /// <inheritdoc />
    public ICompositionBrushPlatform CreateBrushPlatform(CompositionBrush owner) => CompositionBrushAndroidPlatform.Instance;

    /// <inheritdoc />
    public ICompositionClipPlatform CreateClipPlatform(CompositionClip owner) => CompositionClipAndroidPlatform.Instance;

    /// <inheritdoc />
    public ICompositionShapePlatform CreateShapePlatform(CompositionShape owner) => CompositionShapeAndroidPlatform.Instance;

    /// <inheritdoc />
    public ICompositionSurfacePlatform CreateSurfacePlatform(PlatformCompositionSurface owner) => _surfaceFactory(owner);

    /// <inheritdoc />
    public bool AreEffectsSupported(Compositor compositor) => false;

    /// <inheritdoc />
    public bool AreEffectsFast(Compositor compositor) => false;
}
