using System;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;

namespace CodeBrix.Android.UI.Overlay;

/// <summary>The Material motion a Frame page change is shown with (plan 2.8).</summary>
internal enum NavigationMotionKind
{
    /// <summary>No transition: the first page, or SuppressNavigationTransitionInfo.</summary>
    None,

    /// <summary>MaterialSharedAxis on X (SlideNavigationTransitionInfo).</summary>
    SharedAxisX,

    /// <summary>MaterialSharedAxis on Z (the default, EntranceNavigationTransitionInfo).</summary>
    SharedAxisZ,

    /// <summary>MaterialFadeThrough - a scale and fade (DrillInNavigationTransitionInfo).</summary>
    FadeThrough,
}

/// <summary>A Frame page change's motion and direction.</summary>
/// <param name="Kind">The motion.</param>
/// <param name="Forward">False for a back navigation (the motion plays in reverse).</param>
internal readonly record struct NavigationMotion(NavigationMotionKind Kind, bool Forward)
{
    /// <summary>
    /// The motion of a navigation: none for the frame's first page; otherwise by the WinUI transition info
    /// (Suppress = none, Slide = shared axis X, DrillIn = fade through, anything else = shared axis Z), played
    /// backwards for NavigationMode.Back.
    /// </summary>
    /// <param name="info">The navigation's NavigationTransitionInfo (may be null).</param>
    /// <param name="mode">The navigation mode.</param>
    /// <param name="hasCurrentPage">False when the frame shows no page yet.</param>
    /// <returns>The motion.</returns>
    internal static NavigationMotion For(NavigationTransitionInfo info, NavigationMode mode, bool hasCurrentPage)
    {
        var forward = mode != NavigationMode.Back;
        if (!hasCurrentPage)
        {
            return new NavigationMotion(NavigationMotionKind.None, forward);
        }

        var kind = info switch
        {
            SuppressNavigationTransitionInfo => NavigationMotionKind.None,
            SlideNavigationTransitionInfo => NavigationMotionKind.SharedAxisX,
            DrillInNavigationTransitionInfo => NavigationMotionKind.FadeThrough,
            _ => NavigationMotionKind.SharedAxisZ,
        };
        return new NavigationMotion(kind, forward);
    }
}

/// <summary>
/// The in-app predictive back preview of the page being left (Material guidance): the page shrinks to 90 % and
/// shifts toward the swipe by up to (width / 20 - 8 dp) as the gesture progresses.
/// </summary>
internal static class BackGestureMath
{
    /// <summary>The scale at full progress.</summary>
    internal const float MinScale = 0.9f;

    /// <summary>The preview transform at <paramref name="progress"/>.</summary>
    /// <param name="progress">The gesture progress, 0 to 1 (clamped).</param>
    /// <param name="fromLeftEdge">True when the swipe started at the left edge (the page moves right).</param>
    /// <param name="widthPx">The page width in pixels.</param>
    /// <param name="density">Pixels per DIP.</param>
    /// <returns>The scale (both axes) and the horizontal translation in pixels.</returns>
    internal static (float Scale, float TranslationX) Preview(float progress, bool fromLeftEdge, float widthPx, float density)
    {
        var p = Math.Clamp(float.IsNaN(progress) ? 0 : progress, 0f, 1f);
        var scale = 1f - ((1f - MinScale) * p);
        var maxShift = Math.Max(0f, (widthPx / 20f) - (8f * density));
        var translation = (fromLeftEdge ? 1f : -1f) * maxShift * p;
        return (scale, translation);
    }
}
