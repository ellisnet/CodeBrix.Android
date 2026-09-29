using System;
using CodeBrix.Android.UI.Platform.Recycler.Portable;
using ARect = global::Android.Graphics.Rect;
using ARecyclerView = global::AndroidX.RecyclerView.Widget.RecyclerView;
using AGridLayoutManager = global::AndroidX.RecyclerView.Widget.GridLayoutManager;
using AView = global::Android.Views.View;

namespace CodeBrix.Android.UI.Platform.Recycler;

/// <summary>
/// Places each cell of a uniform grid where Core's UniformGridLayout / ItemsWrapGrid puts it: column c starts at
/// c x (item + spacing). A GridLayoutManager divides the list's cross extent into equal cells; when that extent is
/// Core's own (the items do not fill it, so the last column has no trailing spacing) the cells are narrower than
/// item + spacing, and this decoration moves each item from its cell's start by
/// <see cref="UniformGridMath.ColumnOffset"/> (0 when the cells are exact).
/// </summary>
internal sealed class UniformGridColumnDecoration : ARecyclerView.ItemDecoration
{
    private readonly Func<(double ItemAcross, double Spacing, double CellAcross, double Density)> _geometry;

    /// <summary>Creates the decoration.</summary>
    /// <param name="geometry">The current item extent, cross spacing and cell extent across the grid (DIPs) and the density.</param>
    internal UniformGridColumnDecoration(Func<(double ItemAcross, double Spacing, double CellAcross, double Density)> geometry)
    {
        _geometry = geometry;
    }

    /// <summary>True when the list scrolls horizontally (the columns are rows).</summary>
    internal bool Horizontal { get; init; }

    /// <inheritdoc />
    public override void GetItemOffsets(ARect outRect, AView view, ARecyclerView parent, ARecyclerView.State state)
    {
        base.GetItemOffsets(outRect, view, parent, state);
        if (parent.GetLayoutManager() is not AGridLayoutManager grid || grid.SpanCount <= 1)
        {
            return;
        }

        var column = view.LayoutParameters is AGridLayoutManager.LayoutParams lp ? lp.SpanIndex : -1;
        if (column < 0)
        {
            var position = parent.GetChildAdapterPosition(view);
            if (position == ARecyclerView.NoPosition || position < 0)
            {
                return;
            }

            column = grid.GetSpanSizeLookup().GetSpanIndex(position, grid.SpanCount);
        }

        var (item, spacing, cell, density) = _geometry();
        var offset = (int)Math.Floor(UniformGridMath.ColumnOffset(column, item, spacing, cell) * density + 0.5);
        if (offset <= 0)
        {
            return;
        }

        if (Horizontal)
        {
            outRect.Top += offset;
        }
        else
        {
            outRect.Left += offset;
        }
    }
}
