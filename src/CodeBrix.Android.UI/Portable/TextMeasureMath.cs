using System;

namespace CodeBrix.Android.UI.Portable;

/// <summary>
/// Unit conversions for text measurement: CodeBrix layout works in DIPs, Android text
/// layout in physical pixels.
/// </summary>
internal static class TextMeasureMath
{
    /// <summary>The largest width passed to Android's StaticLayout for an unconstrained measure.</summary>
    internal const int UnconstrainedPx = 1 << 20;

    /// <summary>Converts an available width in DIPs to a StaticLayout width in whole pixels (at least 0).</summary>
    internal static int AvailableWidthToPx(double availableDip, double density)
    {
        if (double.IsInfinity(availableDip) || double.IsNaN(availableDip) || availableDip * density >= UnconstrainedPx)
        {
            return UnconstrainedPx;
        }

        return Math.Max(0, (int)Math.Floor(availableDip * density));
    }

    /// <summary>Converts a measured pixel extent to DIPs.</summary>
    internal static double PxToDip(double px, double density) => density > 0 ? px / density : px;

    /// <summary>Converts a DIP extent to pixels.</summary>
    internal static float DipToPx(double dip, double density) => (float)(dip * density);

    /// <summary>
    /// WinUI CharacterSpacing is in 1/1000 em; Android's Paint.LetterSpacing is in em.
    /// </summary>
    internal static float CharacterSpacingToEm(int characterSpacing) => characterSpacing / 1000f;
}
