using System;
using CodeBrix.Android.UI.Portable.Projection;
using AContext = global::Android.Content.Context;
using AMeasureSpec = global::Android.Views.View.MeasureSpec;
using AMeasureSpecMode = global::Android.Views.MeasureSpecMode;
using AView = global::Android.Views.View;
using AViewGroup = global::Android.Views.ViewGroup;

namespace CodeBrix.Android.UI.Projection;

/// <summary>
/// The native layer the projection viewer fills: every child is placed at the pixel
/// rectangle the viewer computed from Core's layout (no Android layout logic of its own),
/// and drawn in Core's tree order.
/// </summary>
internal sealed class ProjectionLayer : AViewGroup
{
    private int[] _drawOrder = Array.Empty<int>();
    private int[] _orderKeys = Array.Empty<int>();
    private bool _orderDirty = true;

    internal ProjectionLayer(AContext context)
        : base(context)
    {
        ChildrenDrawingOrderEnabled = true;
        SetClipChildren(true);
        Focusable = false;
    }

    /// <summary>Places a child at a pixel rectangle with a draw order (Core tree order).</summary>
    internal void Place(AView child, PixelRect rect, int order)
    {
        if (child.LayoutParameters is not ProjectionLayoutParams lp)
        {
            lp = new ProjectionLayoutParams();
            child.LayoutParameters = lp;
        }

        var sizeChanged = lp.Rect.Width != rect.Width || lp.Rect.Height != rect.Height;
        var moved = lp.Rect.Left != rect.Left || lp.Rect.Top != rect.Top;
        if (lp.Order != order)
        {
            lp.Order = order;
            _orderDirty = true;
        }

        lp.Rect = rect;
        if (child.Parent == null)
        {
            AddView(child);
            _orderDirty = true;
        }
        else if (sizeChanged)
        {
            child.RequestLayout();
        }
        else if (moved)
        {
            child.Layout(rect.Left, rect.Top, rect.Right, rect.Bottom);
        }

        if (_orderDirty)
        {
            Invalidate();
        }
    }

    /// <summary>Removes a child (it goes back to the viewer's pool).</summary>
    internal void Remove(AView child)
    {
        if (child.Parent == this)
        {
            RemoveView(child);
            _orderDirty = true;
        }
    }

    /// <inheritdoc />
    protected override void OnMeasure(int widthMeasureSpec, int heightMeasureSpec)
    {
        var count = ChildCount;
        for (var i = 0; i < count; i++)
        {
            var child = GetChildAt(i);
            if (child?.LayoutParameters is ProjectionLayoutParams lp)
            {
                child.Measure(
                    AMeasureSpec.MakeMeasureSpec(lp.Rect.Width, AMeasureSpecMode.Exactly),
                    AMeasureSpec.MakeMeasureSpec(lp.Rect.Height, AMeasureSpecMode.Exactly));
            }
        }

        SetMeasuredDimension(AView.GetDefaultSize(SuggestedMinimumWidth, widthMeasureSpec), AView.GetDefaultSize(SuggestedMinimumHeight, heightMeasureSpec));
    }

    /// <inheritdoc />
    protected override void OnLayout(bool changed, int l, int t, int r, int b)
    {
        var count = ChildCount;
        for (var i = 0; i < count; i++)
        {
            var child = GetChildAt(i);
            if (child?.LayoutParameters is ProjectionLayoutParams lp)
            {
                child.Layout(lp.Rect.Left, lp.Rect.Top, lp.Rect.Right, lp.Rect.Bottom);
            }
        }
    }

    /// <inheritdoc />
    protected override int GetChildDrawingOrder(int childCount, int drawingPosition)
    {
        if (_orderDirty || _drawOrder.Length != childCount)
        {
            RebuildDrawOrder(childCount);
        }

        return drawingPosition < _drawOrder.Length ? _drawOrder[drawingPosition] : drawingPosition;
    }

    /// <inheritdoc />
    public override bool ShouldDelayChildPressedState() => false;

    private void RebuildDrawOrder(int childCount)
    {
        if (_drawOrder.Length != childCount)
        {
            _drawOrder = new int[childCount];
            _orderKeys = new int[childCount];
        }

        for (var i = 0; i < childCount; i++)
        {
            _drawOrder[i] = i;
            _orderKeys[i] = GetChildAt(i)?.LayoutParameters is ProjectionLayoutParams lp ? lp.Order : int.MaxValue;
        }

        Array.Sort(_orderKeys, _drawOrder);
        _orderDirty = false;
    }

    /// <summary>Layout parameters of a projected view: its pixel rectangle and draw order.</summary>
    internal sealed class ProjectionLayoutParams : AViewGroup.LayoutParams
    {
        internal ProjectionLayoutParams()
            : base(0, 0)
        {
        }

        internal PixelRect Rect { get; set; }

        internal int Order { get; set; }
    }
}
