using CodeBrix.Android.UI.Overlay;
using CodeBrix.Android.UI.Platform;
using CodeBrix.Android.UI.Policy;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using AMaterialFadeThrough = global::Google.Android.Material.Transition.MaterialFadeThrough;
using AMaterialSharedAxis = global::Google.Android.Material.Transition.MaterialSharedAxis;
using ATransition = global::AndroidX.Transitions.Transition;
using ATransitionManager = global::AndroidX.Transitions.TransitionManager;
using AViewGroup = global::Android.Views.ViewGroup;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>
/// Plays a Frame page change with Material motion (plan 2.8): TransitionManager.BeginDelayedTransition on the
/// frame's view just before Core swaps the page, so the page view that leaves and the one that arrives animate
/// with the motion <see cref="NavigationMotion"/> chose. Honours the system animator scale (0 = instant).
/// </summary>
internal static class FrameTransitions
{
    /// <summary>The Material transition of a motion, or null for none.</summary>
    internal static ATransition Create(NavigationMotion motion) => motion.Kind switch
    {
        NavigationMotionKind.SharedAxisX => new AMaterialSharedAxis(AMaterialSharedAxis.X, motion.Forward),
        NavigationMotionKind.SharedAxisZ => new AMaterialSharedAxis(AMaterialSharedAxis.Z, motion.Forward),
        NavigationMotionKind.FadeThrough => new AMaterialFadeThrough(),
        _ => null,
    };

    /// <summary>
    /// Starts the page-change transition of <paramref name="frameView"/> (no-op for no motion). A Frame inside a
    /// natively shown NavigationView switches top-level destinations: fade through (AP5, <see cref="AdaptivePolicy.FrameMotion"/>).
    /// </summary>
    internal static void Begin(AViewGroup frameView, NavigationMotion motion)
    {
        if (frameView == null || !frameView.IsAttachedToWindow)
        {
            return;
        }

        var element = (frameView as CodeBrixViewGroup)?.ElementHandler?.Element;
        motion = motion with { Kind = AdaptivePolicy.FrameMotion(motion.Kind, UnderNativeNavigation(element)) };
        if (Create(motion) is not { } transition)
        {
            return;
        }

        ATransitionManager.BeginDelayedTransition(frameView, transition);
    }

    /// <summary>True when <paramref name="element"/> is (inside) the content of a NavigationView shown in a native container.</summary>
    internal static bool UnderNativeNavigation(UIElement element)
    {
        for (DependencyObject node = element; node != null; node = VisualTreeHelper.GetParent(node))
        {
            if (node is NavigationView view)
            {
                return view.Handler is NavigationViewHandler { IsNative: true };
            }
        }

        return false;
    }
}
