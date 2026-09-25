using System;

namespace CodeBrix.Android.UI.Portable.Projection;

/// <summary>Colour helpers of the projection viewer.</summary>
internal static class ProjectionColor
{
    /// <summary>
    /// Returns the Android ARGB int of a colour with an extra opacity factor (brush opacity
    /// times element opacity), clamped to 0..1.
    /// </summary>
    internal static int ToArgb(byte a, byte r, byte g, byte b, double opacity)
    {
        if (double.IsNaN(opacity))
        {
            opacity = 1;
        }

        var alpha = (int)Math.Round(a * Math.Clamp(opacity, 0, 1));
        return (alpha << 24) | (r << 16) | (g << 8) | b;
    }

    /// <summary>True when an ARGB colour is fully transparent.</summary>
    internal static bool IsTransparent(int argb) => ((argb >> 24) & 0xFF) == 0;
}
