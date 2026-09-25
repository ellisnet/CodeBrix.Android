// Derived from .NET MAUI, src/Controls/src/Core/Handlers/Items/Android/ItemContentView.cs (the measure/layout technique) @ 828569a864. Copyright (c) .NET Foundation and Contributors.
// Licensed under the MIT License. See THIRD-PARTY-NOTICES.txt.

using System;
using CodeBrix.Android.UI.Handlers;
using CodeBrix.Android.UI.Portable.Layout;
using Microsoft.UI.Xaml;
using Windows.Foundation;
using AContext = global::Android.Content.Context;
using AMeasureSpecMode = global::Android.Views.MeasureSpecMode;
using AView = global::Android.Views.View;

namespace CodeBrix.Android.UI.Platform.Recycler;

/// <summary>
/// The item view of a RecyclerView ViewHolder: hosts ONE Core element (an item container, a header,
/// an ItemsRepeater element) whose native view (its handler's view) is this view's only child. Android
/// drives the element's Core layout here, like MAUI's ItemContentView (plan 2.6: the one reverse path):
/// <see cref="OnMeasure"/> measures the element with the spec's constraint (Unspecified = infinite), and
/// <see cref="OnLayout"/> arranges it where THIS view sits in the RecyclerView - in DIPs, relative to the
/// Core panel that holds the element - so Core's own rectangles (hit testing, TransformToVisual) match the
/// screen. The element view is laid out at this view's size and its subtree replays Core's layout.
/// </summary>
/// <remarks>
/// A sibling of <see cref="CoreSubtreeHost"/> (read-only for this lane): that host arranges its content at
/// (0, 0), which is right for dialog content but not for a list item whose Core parent is the list's panel.
/// </remarks>
internal sealed class RecyclerItemHost : CodeBrixViewGroup
{
    private UIElement _element;
    private AView _elementView;
    private Rect _lastArranged = Rect.Empty;

    /// <summary>Creates the host.</summary>
    /// <param name="context">The context.</param>
    internal RecyclerItemHost(AContext context)
        : base(context)
    {
        SetClipChildren(true);
    }

    /// <summary>The Core panel the hosted element is a child of (its rectangles are recorded there).</summary>
    internal RecyclerItemsPanel Panel { get; set; }

    /// <summary>The origin of the RecyclerView in window DIPs (for the replay's pixel rounding), or null.</summary>
    internal Func<Point> RecyclerOrigin { get; set; }

    /// <summary>
    /// A fixed item size in DIPs (uniform grids: every item gets the first item's size), or null to size
    /// the item from its own measure.
    /// </summary>
    internal Size? FixedSize { get; set; }

    /// <summary>The hosted element.</summary>
    internal UIElement Element
    {
        get => _element;
        set
        {
            if (ReferenceEquals(_element, value))
            {
                return;
            }

            ReleaseElementView();
            _element = value;
            _lastArranged = Rect.Empty;
            AdoptElementView();
        }
    }

    /// <summary>
    /// Shows the element's native view (its handler exists once the element is in a live tree); called
    /// after the element entered the panel and whenever its handler may have changed.
    /// </summary>
    internal void AdoptElementView()
    {
        if (_element?.Handler is IViewHandler { NativeView: { } view } && !ReferenceEquals(view, _elementView))
        {
            ReleaseElementView();
            _elementView = view;
            AddElementChild(_element, view, 0);
            RequestLayout();
        }
    }

    /// <summary>Takes the element's view out (the element left the live tree).</summary>
    internal void ReleaseElementView()
    {
        if (_element != null && _elementView != null)
        {
            RemoveElementChild(_element);
        }

        _elementView = null;
        _lastArranged = Rect.Empty;
    }

    /// <summary>
    /// Arranges the element where this view is now (after the RecyclerView moved it without a layout pass,
    /// e.g. a scroll); a no-op when the rectangle did not change.
    /// </summary>
    internal void SyncCoreRect()
    {
        if (_element == null || Width <= 0 && Height <= 0)
        {
            return;
        }

        ArrangeElement(Left, Top, Width, Height);
    }

    /// <inheritdoc />
    protected override void OnMeasure(int widthMeasureSpec, int heightMeasureSpec)
    {
        AdoptElementView();
        if (_element == null)
        {
            SetMeasuredDimension(0, 0);
            return;
        }

        var density = HandlerContext.Density(_element);
        Size constraint;
        if (FixedSize is { } fixedSize)
        {
            constraint = fixedSize;
        }
        else
        {
            constraint = new Size(widthMeasureSpec.ToDips(density), heightMeasureSpec.ToDips(density));
        }

        Panel?.SetConstraint(_element, constraint);
        _element.Measure(constraint);
        var desired = _element.DesiredSize;
        var desiredWidth = FixedSize is { } w ? w.Width : desired.Width;
        var desiredHeight = FixedSize is { } h ? h.Height : desired.Height;

        var width = Resolve(widthMeasureSpec, LayoutReplayMath.ToPixels(desiredWidth, density));
        var height = Resolve(heightMeasureSpec, LayoutReplayMath.ToPixels(desiredHeight, density));
        _elementView?.Measure(MeasureSpecExtensions.Exactly(width), MeasureSpecExtensions.Exactly(height));
        SetMeasuredDimension(width, height);
    }

    /// <inheritdoc />
    protected override void OnLayout(bool changed, int l, int t, int r, int b)
    {
        if (_element == null)
        {
            return;
        }

        ArrangeElement(l, t, r - l, b - t);
        if (RecyclerTrace.IsEnabled)
        {
            RecyclerTrace.Write($"host layout {l},{t},{r},{b} element={_element.GetType().Name} view={_elementView?.GetType().Name} desired={_element.DesiredSize} handler={(_element.XamlRoot != null)}");
        }

        if (_elementView != null)
        {
            // The element's own rectangle inside the host: its margins are outside it (Core arranged it in the
            // host's slot, margins and alignment applied).
            var density = HandlerContext.Density(_element);
            var slot = new Point(LayoutReplayMath.FromPixels(l, density), LayoutReplayMath.FromPixels(t, density));
            var origin = RecyclerOrigin?.Invoke() ?? default;
            var hostOrigin = new Point(origin.X + slot.X, origin.Y + slot.Y);
            var pixels = _element.Handler is IAndroidElementHandler { HasArranged: true } handler
                ? LayoutReplayMath.ChildPixels(hostOrigin, new Rect(handler.ArrangedRect.X - slot.X, handler.ArrangedRect.Y - slot.Y, handler.ArrangedRect.Width, handler.ArrangedRect.Height), density)
                : new global::CodeBrix.Android.UI.Portable.Projection.PixelRect(0, 0, r - l, b - t);
            if (_elementView is CodeBrixViewGroup elementGroup && _element.Handler is IAndroidElementHandler { HasArranged: true } arranged)
            {
                elementGroup.AbsoluteDipOrigin = new Point(origin.X + arranged.ArrangedRect.X, origin.Y + arranged.ArrangedRect.Y);
            }

            if (_elementView.MeasuredWidth != pixels.Width || _elementView.MeasuredHeight != pixels.Height)
            {
                _elementView.Measure(MeasureSpecExtensions.Exactly(pixels.Width), MeasureSpecExtensions.Exactly(pixels.Height));
            }

            _elementView.Layout(pixels.Left, pixels.Top, pixels.Right, pixels.Bottom);
        }
    }

    private void ArrangeElement(int left, int top, int width, int height)
    {
        var density = HandlerContext.Density(_element);
        var rect = new Rect(
            LayoutReplayMath.FromPixels(left, density),
            LayoutReplayMath.FromPixels(top, density),
            LayoutReplayMath.FromPixels(Math.Max(0, width), density),
            LayoutReplayMath.FromPixels(Math.Max(0, height), density));
        Panel?.SetRect(_element, rect);
        if (rect != _lastArranged || _element.IsArrangeDirtyForHost())
        {
            _lastArranged = rect;
            _element.Arrange(rect);
        }
    }

    private static int Resolve(int spec, int desired) => spec.GetMode() switch
    {
        AMeasureSpecMode.Exactly => spec.GetSize(),
        AMeasureSpecMode.AtMost => Math.Min(desired, spec.GetSize()),
        _ => desired,
    };
}

/// <summary>Layout-state helpers for elements hosted by native containers.</summary>
internal static class HostedElementExtensions
{
    /// <summary>True when Core wants the element arranged again.</summary>
    internal static bool IsArrangeDirtyForHost(this UIElement element) => element.IsArrangeDirty;
}
