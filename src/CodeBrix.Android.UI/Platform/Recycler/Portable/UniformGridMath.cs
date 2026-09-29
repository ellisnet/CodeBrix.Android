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

    /// <summary>
    /// The cross-axis extent Core's UniformGridLayout reports when its items do not stretch (ItemsStretch None):
    /// <paramref name="span"/> items of <paramref name="itemExtent"/> separated by <paramref name="spacing"/>
    /// (no trailing spacing), whatever the item count; 0 when nothing fits.
    /// </summary>
    internal static double CrossExtent(double itemExtent, double spacing, int span)
    {
        if (!double.IsFinite(itemExtent) || itemExtent <= 0 || span <= 0)
        {
            return 0;
        }

        spacing = double.IsFinite(spacing) ? Math.Max(0, spacing) : 0;
        return Math.Max(0, span * (itemExtent + spacing) - spacing);
    }

    /// <summary>
    /// How far (DIPs) the item in <paramref name="column"/> moves from the start of its equal layout-manager cell
    /// (<paramref name="cellExtent"/> wide) to where Core puts it (column x (item + spacing)); 0 when the cells
    /// are already item + spacing wide, never so far that the item no longer fits its cell.
    /// </summary>
    internal static double ColumnOffset(int column, double itemExtent, double spacing, double cellExtent)
    {
        if (column <= 0 || !double.IsFinite(itemExtent) || !double.IsFinite(cellExtent) || cellExtent <= 0)
        {
            return 0;
        }

        spacing = double.IsFinite(spacing) ? Math.Max(0, spacing) : 0;
        var offset = column * (itemExtent + spacing - cellExtent);
        return Math.Clamp(offset, 0, Math.Max(0, cellExtent - itemExtent));
    }
}
