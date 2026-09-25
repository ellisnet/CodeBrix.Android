using System;
using System.Collections.Generic;
using CodeBrix.Android.UI.Hosting;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using AContext = global::Android.Content.Context;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>
/// The live Frames of each CodeBrix activity (the frame part of the Android back button / gesture): which
/// frame a back press goes back through - the innermost visible one that can go back. The back dispatch itself
/// (overlays first, then the app's BackRequested, then NavigationView, then the frame; predictive back) is
/// <see cref="BackNavigation"/>.
/// </summary>
internal static class FrameBackNavigation
{
    private static readonly List<WeakReference<FrameHandler>> _frames = new();

    /// <summary>Tracks a connected frame.</summary>
    internal static void Add(FrameHandler handler)
    {
        _frames.Add(new WeakReference<FrameHandler>(handler));
        Update(handler.Context);
    }

    /// <summary>Forgets a disconnected frame.</summary>
    internal static void Remove(FrameHandler handler)
    {
        _frames.RemoveAll(w => !w.TryGetTarget(out var live) || ReferenceEquals(live, handler));
        Update(handler.Context);
    }

    /// <summary>Re-evaluates whether back is intercepted in the activity of <paramref name="context"/>.</summary>
    internal static void Update(AContext context)
    {
        if (context is CodeBrixActivity activity)
        {
            BackNavigation.Update(activity);
        }
    }

    /// <summary>The handler of the innermost (deepest) visible frame of the activity that can go back, or null.</summary>
    internal static FrameHandler FindFrameThatCanGoBack(CodeBrixActivity activity)
    {
        FrameHandler best = null;
        var bestDepth = -1;
        foreach (var weak in _frames.ToArray())
        {
            if (!weak.TryGetTarget(out var handler) || !ReferenceEquals(handler.Context, activity) || handler.Element is not Frame { CanGoBack: true } frame)
            {
                continue;
            }

            var depth = 0;
            var visible = frame.Visibility == Visibility.Visible;
            for (var parent = VisualTreeHelper.GetParent(frame); parent != null; parent = VisualTreeHelper.GetParent(parent))
            {
                depth++;
                if (parent is UIElement { Visibility: not Visibility.Visible })
                {
                    visible = false;
                }
            }

            if (visible && depth > bestDepth)
            {
                best = handler;
                bestDepth = depth;
            }
        }

        return best;
    }

    /// <summary>The handlers of the activity's live frames.</summary>
    internal static IEnumerable<FrameHandler> FramesOf(CodeBrixActivity activity)
    {
        foreach (var weak in _frames.ToArray())
        {
            if (weak.TryGetTarget(out var handler) && ReferenceEquals(handler.Context, activity) && handler.Element != null)
            {
                yield return handler;
            }
        }
    }
}
