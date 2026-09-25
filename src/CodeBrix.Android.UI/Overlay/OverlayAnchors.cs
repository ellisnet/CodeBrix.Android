#if __ANDROID__
using System;
using CodeBrix.Android.UI.Hosting;
using Microsoft.UI.Xaml;
using Windows.Foundation;
using AFrameLayout = global::Android.Widget.FrameLayout;
using AView = global::Android.Views.View;
using AViewStates = global::Android.Views.ViewStates;

namespace CodeBrix.Android.UI.Overlay;

/// <summary>
/// Invisible anchor views in the root layout's popup layer (<see cref="CodeBrixRootLayout.PopupLayer"/>): a
/// platform popup (a popup menu) is anchored to a VIEW, while a WinUI flyout is placed against an element's
/// rectangle or a point; the anchor is a view at exactly that rectangle. Anchors take no input (the popup layer
/// passes every touch through to the content below).
/// </summary>
internal static class OverlayAnchors
{
    /// <summary>
    /// The rectangle a flyout is placed against, in window DIPs: <paramref name="position"/> (relative to the
    /// target) as a one-DIP rectangle when given, else the target's bounds.
    /// </summary>
    internal static Rect PlacementRect(FrameworkElement target, Point? position)
    {
        var transform = target.TransformToVisual(null);
        if (position is { } point)
        {
            var at = transform.TransformPoint(point);
            return new Rect(at.X, at.Y, 1, 1);
        }

        return transform.TransformBounds(new Rect(0, 0, target.ActualWidth, target.ActualHeight));
    }

    /// <summary>Adds an anchor view at <paramref name="rectDips"/> to the activity's popup layer.</summary>
    /// <param name="activity">The activity.</param>
    /// <param name="rectDips">The rectangle in window DIPs.</param>
    /// <param name="density">Physical pixels per DIP.</param>
    /// <returns>The anchor (remove it with <see cref="Remove"/>).</returns>
    internal static AView Add(CodeBrixActivity activity, Rect rectDips, double density)
    {
        var layer = activity.RootLayout?.PopupLayer ?? throw new InvalidOperationException("The activity has no root layout yet.");
        var left = (int)Math.Floor(rectDips.X * density);
        var top = (int)Math.Floor(rectDips.Y * density);
        var width = Math.Max(1, (int)Math.Ceiling(rectDips.Right * density) - left);
        var height = Math.Max(1, (int)Math.Ceiling(rectDips.Bottom * density) - top);
        var anchor = new AView(activity)
        {
            Visibility = AViewStates.Invisible,
            Focusable = false,
            Clickable = false,
            ImportantForAccessibility = global::Android.Views.ImportantForAccessibility.No,
        };
        layer.AddView(anchor, new AFrameLayout.LayoutParams(width, height) { LeftMargin = left, TopMargin = top });

        // The popup reads the anchor's position when it shows: lay the anchor out now.
        anchor.Measure(
            global::Android.Views.View.MeasureSpec.MakeMeasureSpec(width, global::Android.Views.MeasureSpecMode.Exactly),
            global::Android.Views.View.MeasureSpec.MakeMeasureSpec(height, global::Android.Views.MeasureSpecMode.Exactly));
        anchor.Layout(left, top, left + width, top + height);
        return anchor;
    }

    /// <summary>Takes an anchor out of its layer.</summary>
    internal static void Remove(AView anchor)
    {
        if (anchor?.Parent is global::Android.Views.ViewGroup parent)
        {
            parent.RemoveView(anchor);
        }
    }
}
#endif
