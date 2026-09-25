using Windows.Graphics;
using APath = global::Android.Graphics.Path;

namespace CodeBrix.Android.UI.Composition.Android;

/// <summary>
/// An <see cref="IGeometrySource2D"/> backed by an <see cref="APath"/>, in DIPs.
/// </summary>
internal sealed class AndroidGeometrySource2D : IGeometrySource2D
{
    internal AndroidGeometrySource2D(APath path) => Path = path;

    /// <summary>The Android path (coordinates in DIPs).</summary>
    internal APath Path { get; }
}
