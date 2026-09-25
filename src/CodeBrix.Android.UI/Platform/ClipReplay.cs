using System;
using CodeBrix.Android.UI.Handlers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using ARect = global::Android.Graphics.Rect;
using AView = global::Android.Views.View;

namespace CodeBrix.Android.UI.Platform;

/// <summary>
/// The clip of an element's native view: its own <see cref="UIElement.Clip"/> (a RectangleGeometry)
/// intersected with the LAYOUT clip Core computed during its arrange (FrameworkElement's clip to the
/// layout slot and to Width/Height/MaxWidth/MaxHeight - what WinUI shows when an element is arranged
/// larger than the box its parent or its own size constraints give it). Both are replayed through the
/// one <see cref="AView.ClipBounds"/>, so neither overwrites the other: the Clip mapper and the parent
/// <see cref="CodeBrixViewGroup"/>'s layout pass both call <see cref="Apply"/>.
/// </summary>
/// <remarks>
/// Core keeps the layout clip on the element's visual (<c>ContainerVisual.LayoutClip</c>): in the element's
/// own coordinates, except for Panels, whose clip is in the parent's coordinates ("ancestor clip"), so the
/// element's arranged offset is taken off. The clip of a view with a RenderTransform is applied after the
/// transform on Android (the view's clip bounds are in its own coordinates), as WinUI does for the element
/// clip; an ancestor clip on a transformed Panel is therefore approximate.
/// </remarks>
internal static class ClipReplay
{
    /// <summary>Sets <paramref name="view"/>'s clip bounds from <paramref name="element"/>'s clip and layout clip.</summary>
    /// <param name="view">The element's native view.</param>
    /// <param name="element">The element.</param>
    /// <param name="density">Physical pixels per DIP.</param>
    internal static void Apply(AView view, UIElement element, double density)
    {
        if (view == null || element == null)
        {
            return;
        }

        var clip = ElementClip(element);
        if (LayoutClip(element) is { } layout)
        {
            clip = clip is { } own ? Intersect(own, layout) : layout;
        }

        if (clip is not { } rect)
        {
            if (view.ClipBounds != null)
            {
                view.ClipBounds = null;
            }

            return;
        }

        var bounds = new ARect(
            (int)Math.Floor(rect.X * density),
            (int)Math.Floor(rect.Y * density),
            (int)Math.Ceiling((rect.X + rect.Width) * density),
            (int)Math.Ceiling((rect.Y + rect.Height) * density));
        if (bounds.Right < bounds.Left || bounds.Bottom < bounds.Top)
        {
            bounds = new ARect(0, 0, 0, 0);
        }

        if (!bounds.Equals(view.ClipBounds))
        {
            view.ClipBounds = bounds;
        }
    }

    /// <summary>The element's own Clip (transformed), in element DIPs, or null.</summary>
    internal static Rect? ElementClip(UIElement element)
    {
        if (element.Clip is RectangleGeometry { Rect: var rect } geometry && !rect.IsEmpty)
        {
            return geometry.Transform is { } transform ? transform.TransformBounds(rect) : rect;
        }

        return null;
    }

    /// <summary>Core's layout clip of the element, in element DIPs, or null when Core does not clip it.</summary>
    internal static Rect? LayoutClip(UIElement element)
    {
        if (element.Visual?.LayoutClip is not { } layout)
        {
            return null;
        }

        var rect = layout.rect;
        if (layout.isAncestorClip && element.Handler is IAndroidElementHandler { HasArranged: true } handler)
        {
            // Panels: the clip is in the parent's coordinates (it already contains the element's offset).
            rect = new Rect(rect.X - handler.ArrangedRect.X, rect.Y - handler.ArrangedRect.Y, rect.Width, rect.Height);
        }

        return rect;
    }

    private static Rect Intersect(Rect a, Rect b)
    {
        var left = Math.Max(a.X, b.X);
        var top = Math.Max(a.Y, b.Y);
        var right = Math.Min(a.X + a.Width, b.X + b.Width);
        var bottom = Math.Min(a.Y + a.Height, b.Y + b.Height);
        return new Rect(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));
    }
}
