// Derived from .NET MAUI, src/Core/src/Platform/Android/MeasureSpecExtensions.cs @ 828569a864. Copyright (c) .NET Foundation and Contributors.
// Licensed under the MIT License. See THIRD-PARTY-NOTICES.txt.

using CodeBrix.Android.UI.Portable.Layout;
using AMeasureSpec = global::Android.Views.View.MeasureSpec;
using AMeasureSpecMode = global::Android.Views.MeasureSpecMode;

namespace CodeBrix.Android.UI.Platform;

/// <summary>Android MeasureSpec helpers (density passed in: per XamlRoot, never a static).</summary>
internal static class MeasureSpecExtensions
{
    /// <summary>The size part of a spec.</summary>
    internal static int GetSize(this int measureSpec)
    {
        const int modeMask = 0x3 << 30;
        return measureSpec & ~modeMask;
    }

    /// <summary>The mode part of a spec.</summary>
    internal static AMeasureSpecMode GetMode(this int measureSpec) => AMeasureSpec.GetMode(measureSpec);

    /// <summary>Makes a spec.</summary>
    internal static int MakeMeasureSpec(this AMeasureSpecMode mode, int size) => size + (int)mode;

    /// <summary>A spec in DIPs: infinity for Unspecified, else the size over the density.</summary>
    internal static double ToDips(this int measureSpec, double density) =>
        measureSpec.GetMode() == AMeasureSpecMode.Unspecified
            ? double.PositiveInfinity
            : LayoutReplayMath.FromPixels(measureSpec.GetSize(), density);

    /// <summary>An EXACTLY spec of <paramref name="pixels"/>.</summary>
    internal static int Exactly(int pixels) => AMeasureSpecMode.Exactly.MakeMeasureSpec(System.Math.Max(0, pixels));
}
