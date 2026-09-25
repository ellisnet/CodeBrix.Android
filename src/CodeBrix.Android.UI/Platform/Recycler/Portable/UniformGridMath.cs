using System;

namespace CodeBrix.Android.UI.Platform.Recycler.Portable;

/// <summary>
/// The arithmetic of a uniform grid laid out by a RecyclerView GridLayoutManager so that the cells sit
/// where Core's ItemsWrapGrid / UniformGridLayout put them: the number of cells across (the span count)
/// and the extra end padding that makes the layout manager's equal cells exactly one item (plus spacing)
/// wide, starting at the content start.
/// </summary>
internal static class UniformGridMath
{
    /// <summary>
    /// The number of cells that fit across <paramref name="available"/> DIPs: items of
    /// <paramref name="itemExtent"/> separated by <paramref name="spacing"/>, at least one, at most
    /// <paramref name="maximum"/> when that is positive.
    /// </summary>
    internal static int SpanCount(double available, double itemExtent, double spacing, int maximum)
    {
        if (!double.IsFinite(available) || !double.IsFinite(itemExtent) || itemExtent <= 0)
        {
            return maximum > 0 ? maximum : 1;
        }

        spacing = double.IsFinite(spacing) ? Math.Max(0, spacing) : 0;
        var count = (int)Math.Floor((available + spacing + 1e-6) / (itemExtent + spacing));
        count = Math.Max(1, count);
        return maximum > 0 ? Math.Min(count, maximum) : count;
    }

    /// <summary>
    /// The extra end padding (DIPs) that leaves exactly <paramref name="span"/> cells of
    /// (<paramref name="itemExtent"/> + <paramref name="spacing"/>) across <paramref name="available"/>
    /// (the last cell's trailing spacing is part of it), or 0 when they do not fit.
    /// </summary>
    internal static double ExtraEndPadding(double available, double itemExtent, double spacing, int span)
    {
        if (!double.IsFinite(available) || !double.IsFinite(itemExtent) || span <= 0)
        {
            return 0;
        }

        spacing = double.IsFinite(spacing) ? Math.Max(0, spacing) : 0;
        var used = span * (itemExtent + spacing);
        return Math.Max(0, available + spacing - used);
    }
}
