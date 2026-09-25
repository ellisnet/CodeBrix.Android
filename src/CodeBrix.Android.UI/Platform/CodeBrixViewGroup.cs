// Derived from .NET MAUI, src/Core/src/Platform/Android/LayoutViewGroup.cs @ 828569a864. Copyright (c) .NET Foundation and Contributors.
// Licensed under the MIT License. See THIRD-PARTY-NOTICES.txt.

using System;
using System.Collections.Generic;
using CodeBrix.Android.UI.Handlers;
using CodeBrix.Android.UI.Portable.Layout;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;
using AContext = global::Android.Content.Context;
using AView = global::Android.Views.View;
using AViewGroup = global::Android.Views.ViewGroup;
using AViewStates = global::Android.Views.ViewStates;

namespace CodeBrix.Android.UI.Platform;

/// <summary>
/// The native view of a Core element that has visual children (panels, borders, presenters,
/// templated controls). Core is in charge of layout (plan 2.6): the view REPLAYS it. Children
/// are the native views of the element's visual children, in Core's child order;
/// <see cref="OnMeasure"/> measures each child EXACTLY at its element's Core size and
/// <see cref="OnLayout"/> places each at its Core rectangle, rounded in window pixels
/// (<see cref="LayoutReplayMath"/>). Nothing here asks Core to measure: the view is driven by
/// Core's layout tick (which requests an Android layout pass when it arranged something).
/// </summary>
/// <remarks>
/// Adapted from MAUI's LayoutViewGroup, whose cross-platform measure/arrange ran the other way
/// (Android drove the cross-platform layout); the Java base PlatformViewGroup is not used.
/// WinUI does not clip children to their parent, so neither does this view
/// (clipChildren/clipToPadding off); Canvas.ZIndex orders drawing. What WinUI does clip - an element
/// arranged larger than its layout slot or its own size constraints (Core's layout clip) - is replayed
/// on each child view after it is laid out (<see cref="ClipReplay"/>).
/// </remarks>
internal class CodeBrixViewGroup : AViewGroup
{
    private readonly List<ChildEntry> _children = new();
    private int[] _drawingOrder;

    /// <summary>Creates the view group.</summary>
    /// <param name="context">The context.</param>
    internal CodeBrixViewGroup(AContext context)
        : base(context)
    {
        SetClipChildren(false);
        SetClipToPadding(false);
    }

    /// <summary>
    /// When semi-transparent, draw children with their own alpha instead of through an
    /// offscreen layer bounded by this view (which would clip overflowing children).
    /// </summary>
    public override bool HasOverlappingRendering => Alpha >= 1.0f && base.HasOverlappingRendering;

    /// <summary>The handler whose element this view shows (null once disconnected).</summary>
    internal IAndroidElementHandler ElementHandler { get; set; }

    /// <summary>
    /// This view's element origin in window DIPs; set by the parent view before laying this
    /// one out (the root's is its own arranged position).
    /// </summary>
    internal Point AbsoluteDipOrigin { get; set; }

    /// <summary>The element children shown by this view, in Core's order.</summary>
    internal int ElementChildCount => _children.Count;

    /// <summary>Adds the view of an element child at Core child index <paramref name="index"/>.</summary>
    internal void AddElementChild(UIElement child, AView view, int index)
    {
        ArgumentNullException.ThrowIfNull(child);
        ArgumentNullException.ThrowIfNull(view);
        RemoveElementChild(child);
        if (view.Parent is AViewGroup previous)
        {
            previous.RemoveView(view);
        }

        index = Math.Clamp(index, 0, _children.Count);
        _children.Insert(index, new ChildEntry(child, view));
        AddView(view, index);
        InvalidateDrawingOrder();
    }

    /// <summary>Removes the view of an element child (no-op when not shown here).</summary>
    internal void RemoveElementChild(UIElement child)
    {
        var index = _children.FindIndex(c => ReferenceEquals(c.Element, child));
        if (index < 0)
        {
            return;
        }

        var view = _children[index].View;
        _children.RemoveAt(index);
        if (view.Parent == this)
        {
            RemoveView(view);
        }

        InvalidateDrawingOrder();
    }

    /// <summary>Moves an element child's view from one Core index to another.</summary>
    internal void MoveElementChild(int oldIndex, int newIndex)
    {
        if (oldIndex < 0 || oldIndex >= _children.Count || newIndex < 0 || newIndex >= _children.Count || oldIndex == newIndex)
        {
            return;
        }

        var entry = _children[oldIndex];
        _children.RemoveAt(oldIndex);
        _children.Insert(newIndex, entry);
        RemoveView(entry.View);
        AddView(entry.View, newIndex);
        InvalidateDrawingOrder();
    }

    /// <summary>The element children shown by this view, in order.</summary>
    internal IReadOnlyList<UIElement> ElementChildren => _children.ConvertAll(c => c.Element);

    /// <summary>The native view of an element child, or null.</summary>
    internal AView ViewOf(UIElement child) => _children.Find(c => ReferenceEquals(c.Element, child)).View;

    /// <summary>Recomputes the drawing order (Canvas.ZIndex of a child changed).</summary>
    internal void InvalidateDrawingOrder()
    {
        _drawingOrder = null;
        var anyZ = false;
        foreach (var entry in _children)
        {
            if (Canvas.GetZIndex(entry.Element) != 0)
            {
                anyZ = true;
                break;
            }
        }

        ChildrenDrawingOrderEnabled = anyZ;
        Invalidate();
    }

    /// <summary>The pixel rectangle of an element child inside this view, from Core's last arrange.</summary>
    internal global::CodeBrix.Android.UI.Portable.Projection.PixelRect ChildPixelRect(UIElement child, double density)
    {
        if (child.Handler is not IAndroidElementHandler { HasArranged: true } handler)
        {
            return default;
        }

        return LayoutReplayMath.ChildPixels(AbsoluteDipOrigin, handler.ArrangedRect, density);
    }

    /// <inheritdoc />
    protected override int GetChildDrawingOrder(int childCount, int drawingPosition)
    {
        if (_drawingOrder == null || _drawingOrder.Length != childCount)
        {
            var order = new int[childCount];
            var keys = new int[childCount];
            for (var i = 0; i < childCount; i++)
            {
                order[i] = i;
                var view = GetChildAt(i);
                var entry = _children.Find(c => c.View == view);
                keys[i] = entry.Element != null ? Canvas.GetZIndex(entry.Element) : 0;
            }

            // Stable: equal ZIndex keeps Core's child order.
            Array.Sort(keys, order);
            var stable = new List<(int Key, int Index)>(childCount);
            for (var i = 0; i < childCount; i++)
            {
                stable.Add((keys[i], order[i]));
            }

            stable.Sort((a, b) => a.Key != b.Key ? a.Key.CompareTo(b.Key) : a.Index.CompareTo(b.Index));
            for (var i = 0; i < childCount; i++)
            {
                order[i] = stable[i].Index;
            }

            _drawingOrder = order;
        }

        return _drawingOrder[drawingPosition];
    }

    /// <inheritdoc />
    protected override void OnMeasure(int widthMeasureSpec, int heightMeasureSpec)
    {
        var element = ElementHandler?.Element;
        var density = HandlerContext.Density(element);

        int width;
        int height;
        if (widthMeasureSpec.GetMode() == global::Android.Views.MeasureSpecMode.Exactly)
        {
            width = widthMeasureSpec.GetSize();
        }
        else
        {
            width = OwnPixelSize(density).Width;
        }

        if (heightMeasureSpec.GetMode() == global::Android.Views.MeasureSpecMode.Exactly)
        {
            height = heightMeasureSpec.GetSize();
        }
        else
        {
            height = OwnPixelSize(density).Height;
        }

        foreach (var entry in _children)
        {
            if (entry.View.Visibility == AViewStates.Gone)
            {
                continue;
            }

            var rect = ChildPixelRect(entry.Element, density);
            entry.View.Measure(MeasureSpecExtensions.Exactly(rect.Width), MeasureSpecExtensions.Exactly(rect.Height));
        }

        SetMeasuredDimension(Math.Max(MinimumWidth, width), Math.Max(MinimumHeight, height));
    }

    /// <inheritdoc />
    protected override void OnLayout(bool changed, int l, int t, int r, int b)
    {
        var density = HandlerContext.Density(ElementHandler?.Element);
        foreach (var entry in _children)
        {
            if (entry.View.Visibility == AViewStates.Gone)
            {
                continue;
            }

            var rect = ChildPixelRect(entry.Element, density);
            if (entry.View is CodeBrixViewGroup group && entry.Element.Handler is IAndroidElementHandler childHandler)
            {
                group.AbsoluteDipOrigin = LayoutReplayMath.ChildOrigin(AbsoluteDipOrigin, childHandler.ArrangedRect);
            }

            entry.View.Layout(rect.Left, rect.Top, rect.Right, rect.Bottom);

            // Core's layout clip (clip to the layout slot / to MaxWidth-MaxHeight) with the element's own Clip.
            ClipReplay.Apply(entry.View, entry.Element, density);
        }

        OnLaidOut(r - l, b - t);
    }

    /// <summary>Called at the end of every layout pass with this view's pixel size.</summary>
    /// <param name="width">Width in pixels.</param>
    /// <param name="height">Height in pixels.</param>
    protected virtual void OnLaidOut(int width, int height)
    {
    }

    private (int Width, int Height) OwnPixelSize(double density)
    {
        if (ElementHandler is not { HasArranged: true } handler)
        {
            return (0, 0);
        }

        var rect = handler.ArrangedRect;
        var parentOrigin = new Point(AbsoluteDipOrigin.X - rect.X, AbsoluteDipOrigin.Y - rect.Y);
        return LayoutReplayMath.PixelSize(parentOrigin, rect, density);
    }

    private readonly record struct ChildEntry(UIElement Element, AView View);
}
