namespace CodeBrix.Android.UI.Composition.Portable;

/// <summary>
/// The hit test of the inert composition platform: a point (already in the visual's own
/// coordinate space; Core applies the transforms) hits when it lies inside the visual's
/// size. Nothing is painted on Android, so there is no pixel-level hit testing.
/// </summary>
internal static class InertHitTest
{
    /// <summary>Returns true when (x, y) lies within [0, width] x [0, height].</summary>
    internal static bool IsInside(double x, double y, double width, double height) =>
        width > 0 && height > 0 && x >= 0 && y >= 0 && x <= width && y <= height;
}
