namespace CodeBrix.Android.UI.Overlay;

/// <summary>The Material 3 window width size classes (plan 2.10).</summary>
internal enum WindowWidthClass
{
    /// <summary>Narrower than 600 dp (a phone in portrait).</summary>
    Compact,

    /// <summary>600 dp up to 840 dp (a small tablet, a phone in landscape, an unfolded foldable).</summary>
    Medium,

    /// <summary>840 dp and wider (a tablet in landscape, a desktop window).</summary>
    Expanded,
}

/// <summary>Computes the window width size class from a width in DIPs (the Material 3 breakpoints).</summary>
internal static class WindowSizeClasses
{
    /// <summary>The first width of the Medium class, in dp.</summary>
    internal const double MediumMinWidth = 600;

    /// <summary>The first width of the Expanded class, in dp.</summary>
    internal const double ExpandedMinWidth = 840;

    /// <summary>The width class of a window <paramref name="widthDips"/> wide.</summary>
    /// <param name="widthDips">The window width in DIPs (dp).</param>
    /// <returns>The class; a non-positive or unknown width counts as Expanded (no size information).</returns>
    internal static WindowWidthClass FromWidth(double widthDips)
    {
        if (double.IsNaN(widthDips) || widthDips <= 0)
        {
            return WindowWidthClass.Expanded;
        }

        return widthDips < MediumMinWidth ? WindowWidthClass.Compact
            : widthDips < ExpandedMinWidth ? WindowWidthClass.Medium
            : WindowWidthClass.Expanded;
    }
}
