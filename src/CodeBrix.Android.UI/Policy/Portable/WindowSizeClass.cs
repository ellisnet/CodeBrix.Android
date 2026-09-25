using System;
using System.Globalization;
using CodeBrix.Android.UI.Overlay;

namespace CodeBrix.Android.UI.Policy;

/// <summary>The Material 3 window height size classes.</summary>
internal enum WindowHeightClass
{
    /// <summary>Lower than 480 dp (a phone in landscape).</summary>
    Compact,

    /// <summary>480 dp up to 900 dp.</summary>
    Medium,

    /// <summary>900 dp and taller.</summary>
    Expanded,
}

/// <summary>
/// A window's size classes (plan 2.10): the Material 3 width class (Compact &lt; 600 dp, Medium 600-839 dp,
/// Expanded &gt;= 840 dp; Large and Extra-large count as Expanded), the height class, and the second input-mode
/// dimension "fine pointer present" (a mouse or touchpad is connected: a docked phone in desktop mode, a
/// laptop-class device) that tells a desktop window from a large touch tablet.
/// </summary>
internal readonly struct WindowSizeClass : IEquatable<WindowSizeClass>
{
    /// <summary>The first height of the Medium height class, in dp.</summary>
    internal const double MediumMinHeight = 480;

    /// <summary>The first height of the Expanded height class, in dp.</summary>
    internal const double ExpandedMinHeight = 900;

    /// <summary>Creates the size classes of a window <paramref name="widthDp"/> by <paramref name="heightDp"/>.</summary>
    /// <param name="widthDp">The window width in dp (DIPs).</param>
    /// <param name="heightDp">The window height in dp.</param>
    /// <param name="finePointer">True when a mouse or touchpad is connected.</param>
    internal WindowSizeClass(double widthDp, double heightDp, bool finePointer)
    {
        WidthDp = widthDp;
        HeightDp = heightDp;
        FinePointer = finePointer;
        Width = WindowSizeClasses.FromWidth(widthDp);
        Height = HeightFrom(heightDp);
    }

    /// <summary>The window width in dp.</summary>
    internal double WidthDp { get; }

    /// <summary>The window height in dp.</summary>
    internal double HeightDp { get; }

    /// <summary>The width size class.</summary>
    internal WindowWidthClass Width { get; }

    /// <summary>The height size class.</summary>
    internal WindowHeightClass Height { get; }

    /// <summary>True when a fine pointer (mouse, touchpad) is present.</summary>
    internal bool FinePointer { get; }

    /// <summary>The classes of a window with no size information (Expanded, no fine pointer).</summary>
    internal static WindowSizeClass Unknown => new(0, 0, false);

    /// <summary>The height class of a window <paramref name="heightDp"/> tall (unknown = Expanded).</summary>
    /// <param name="heightDp">The height in dp.</param>
    /// <returns>The class.</returns>
    internal static WindowHeightClass HeightFrom(double heightDp)
    {
        if (double.IsNaN(heightDp) || heightDp <= 0)
        {
            return WindowHeightClass.Expanded;
        }

        return heightDp < MediumMinHeight ? WindowHeightClass.Compact
            : heightDp < ExpandedMinHeight ? WindowHeightClass.Medium
            : WindowHeightClass.Expanded;
    }

    /// <summary>True when the classes (not the exact size) are the same: what decides a re-mapping.</summary>
    /// <param name="other">The other window's classes.</param>
    /// <returns>True for the same width class, height class and pointer.</returns>
    internal bool SameClassesAs(WindowSizeClass other) => Width == other.Width && Height == other.Height && FinePointer == other.FinePointer;

    /// <inheritdoc />
    public bool Equals(WindowSizeClass other) => WidthDp.Equals(other.WidthDp) && HeightDp.Equals(other.HeightDp) && FinePointer == other.FinePointer;

    /// <inheritdoc />
    public override bool Equals(object obj) => obj is WindowSizeClass other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(WidthDp, HeightDp, FinePointer);

    /// <inheritdoc />
    public override string ToString() => string.Format(
        CultureInfo.InvariantCulture,
        "{0} x {1} ({2:0} x {3:0} dp{4})",
        Width,
        Height,
        WidthDp,
        HeightDp,
        FinePointer ? ", fine pointer" : string.Empty);
}
