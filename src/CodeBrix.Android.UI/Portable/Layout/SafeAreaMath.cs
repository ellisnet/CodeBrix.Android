// Derived from .NET MAUI, src/Core/src/Platform/Android/SafeAreaExtensions.cs (the per-edge overlap in
// ApplyAdjustedSafeAreaInsetsPx) and src/Core/src/Platform/Android/SafeAreaPadding.cs @ 828569a864.
// Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License. See THIRD-PARTY-NOTICES.txt.

using System;

namespace CodeBrix.Android.UI.Portable.Layout;

/// <summary>
/// Per-edge insets (left, top, right, bottom) in one unit (pixels or DIPs). MAUI's SafeAreaPadding,
/// with the WinUI edge order.
/// </summary>
internal readonly record struct SafeAreaPadding(double Left, double Top, double Right, double Bottom)
{
    /// <summary>No insets.</summary>
    public static SafeAreaPadding Empty { get; } = new(0, 0, 0, 0);

    /// <summary>True when every edge is zero.</summary>
    public bool IsEmpty => Left == 0 && Top == 0 && Right == 0 && Bottom == 0;

    /// <summary>The per-edge maximum of two paddings (system bars vs display cutout).</summary>
    public static SafeAreaPadding Max(SafeAreaPadding a, SafeAreaPadding b) =>
        new(Math.Max(a.Left, b.Left), Math.Max(a.Top, b.Top), Math.Max(a.Right, b.Right), Math.Max(a.Bottom, b.Bottom));

    /// <summary>The same padding divided by <paramref name="density"/> (pixels to DIPs).</summary>
    public SafeAreaPadding ToDips(double density) =>
        density > 0 ? new(Left / density, Top / density, Right / density, Bottom / density) : this;
}

/// <summary>
/// How much of a window's safe-area insets an element must absorb (plan D-P10: Page and ScrollViewer
/// absorb the system-bar and display-cutout insets as padding). MAUI's rule, per edge: only the part of
/// the inset the element actually overlaps is absorbed, measured from the element's MARGIN box (so a
/// margin and the inset add up), and an element already clear of an edge's inset absorbs nothing for it
/// - which is also why a page nested inside an absorbing page gets nothing.
/// </summary>
internal static class SafeAreaMath
{
    /// <summary>
    /// The insets an element absorbs, in DIPs.
    /// </summary>
    /// <param name="left">The element's margin-box left, in window DIPs.</param>
    /// <param name="top">The element's margin-box top, in window DIPs.</param>
    /// <param name="width">The element's margin-box width, in DIPs.</param>
    /// <param name="height">The element's margin-box height, in DIPs.</param>
    /// <param name="windowWidth">The window width in DIPs.</param>
    /// <param name="windowHeight">The window height in DIPs.</param>
    /// <param name="safeArea">The window's safe-area insets in DIPs (system bars and cutout).</param>
    /// <returns>The per-edge insets to absorb (never negative, never more than the element's extent).</returns>
    internal static SafeAreaPadding Overlap(
        double left, double top, double width, double height,
        double windowWidth, double windowHeight,
        SafeAreaPadding safeArea)
    {
        if (safeArea.IsEmpty || width <= 0 || height <= 0)
        {
            return SafeAreaPadding.Empty;
        }

        var right = left + width;
        var bottom = top + height;

        // Top: how much the element extends into the top inset (an element panned above the
        // window, top < 0, is treated as starting at the window top - MAUI ignores negative tops).
        var topInset = 0.0;
        if (safeArea.Top > 0 && top < safeArea.Top && bottom > 0)
        {
            topInset = Math.Min(safeArea.Top - Math.Max(0, top), safeArea.Top);
        }

        var bottomInset = 0.0;
        var bottomEdge = windowHeight - safeArea.Bottom;
        if (safeArea.Bottom > 0 && bottom > bottomEdge && top < windowHeight)
        {
            bottomInset = Math.Min(Math.Min(bottom, windowHeight) - bottomEdge, safeArea.Bottom);
        }

        var leftInset = 0.0;
        if (safeArea.Left > 0 && left < safeArea.Left && right > 0)
        {
            leftInset = Math.Min(safeArea.Left - Math.Max(0, left), safeArea.Left);
        }

        var rightInset = 0.0;
        var rightEdge = windowWidth - safeArea.Right;
        if (safeArea.Right > 0 && right > rightEdge && left < windowWidth)
        {
            rightInset = Math.Min(Math.Min(right, windowWidth) - rightEdge, safeArea.Right);
        }

        // Never absorb more than the element has.
        if (topInset + bottomInset > height)
        {
            var scale = height / (topInset + bottomInset);
            topInset *= scale;
            bottomInset *= scale;
        }

        if (leftInset + rightInset > width)
        {
            var scale = width / (leftInset + rightInset);
            leftInset *= scale;
            rightInset *= scale;
        }

        return new SafeAreaPadding(Math.Max(0, leftInset), Math.Max(0, topInset), Math.Max(0, rightInset), Math.Max(0, bottomInset));
    }

    /// <summary>True when two paddings differ by more than a hundredth of a DIP on any edge.</summary>
    internal static bool Differs(SafeAreaPadding a, SafeAreaPadding b) =>
        Math.Abs(a.Left - b.Left) > 0.01 || Math.Abs(a.Top - b.Top) > 0.01
        || Math.Abs(a.Right - b.Right) > 0.01 || Math.Abs(a.Bottom - b.Bottom) > 0.01;
}
