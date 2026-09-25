// Derived from .NET MAUI, src/Controls/src/Core/Handlers/Items/Android/SpacingItemDecoration.cs @ 828569a864. Copyright (c) .NET Foundation and Contributors.
// Licensed under the MIT License. See THIRD-PARTY-NOTICES.txt.

using ARect = global::Android.Graphics.Rect;
using ARecyclerView = global::AndroidX.RecyclerView.Widget.RecyclerView;
using AGridLayoutManager = global::AndroidX.RecyclerView.Widget.GridLayoutManager;
using AView = global::Android.Views.View;

namespace CodeBrix.Android.UI.Platform.Recycler;

/// <summary>
/// Spacing between the items of a list or grid (StackLayout.Spacing, UniformGridLayout.MinRowSpacing):
/// the space goes AFTER each item along the scroll axis except the last line, so it appears only between
/// items (the MAUI decoration applied half spacing on both sides and removed it at the outer edges; the
/// result is the same, with whole pixels). Cross-axis spacing of a grid is part of the cell width (see
/// UniformGridMath), not a decoration.
/// </summary>
internal sealed class SpacingItemDecoration : ARecyclerView.ItemDecoration
{
    /// <summary>Creates the decoration.</summary>
    /// <param name="mainSpacingPx">The space after each line along the scroll axis, in pixels.</param>
    /// <param name="horizontal">True when the list scrolls horizontally.</param>
    internal SpacingItemDecoration(int mainSpacingPx, bool horizontal)
    {
        MainSpacingPx = mainSpacingPx;
        Horizontal = horizontal;
    }

    /// <summary>The space after each line along the scroll axis, in pixels.</summary>
    internal int MainSpacingPx { get; }

    /// <summary>True when the list scrolls horizontally.</summary>
    internal bool Horizontal { get; }

    /// <inheritdoc />
    public override void GetItemOffsets(ARect outRect, AView view, ARecyclerView parent, ARecyclerView.State state)
    {
        base.GetItemOffsets(outRect, view, parent, state);
        var adapter = parent.GetAdapter();
        if (adapter is null || MainSpacingPx <= 0)
        {
            return;
        }

        var position = parent.GetChildAdapterPosition(view);
        var itemCount = adapter.ItemCount;
        if (position == ARecyclerView.NoPosition || position < 0 || position >= itemCount)
        {
            return;
        }

        int line;
        int lastLine;
        if (parent.GetLayoutManager() is AGridLayoutManager grid)
        {
            // Span groups, not position / spanCount, so full-span items count as their own line.
            var lookup = grid.GetSpanSizeLookup();
            line = lookup.GetSpanGroupIndex(position, grid.SpanCount);
            lastLine = lookup.GetSpanGroupIndex(itemCount - 1, grid.SpanCount);
        }
        else
        {
            line = position;
            lastLine = itemCount - 1;
        }

        if (line == lastLine)
        {
            return;
        }

        if (Horizontal)
        {
            outRect.Right = MainSpacingPx;
        }
        else
        {
            outRect.Bottom = MainSpacingPx;
        }
    }
}
