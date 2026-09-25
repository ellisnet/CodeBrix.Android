// Technique from .NET MAUI, src/Core/src/Animations/PlatformTicker.Android.cs @ 828569a864 (following the system
// animator duration scale live with ValueAnimator.RegisterDurationScaleChangeListener, API 33). Copyright (c) .NET
// Foundation and Contributors. Licensed under the MIT License. See THIRD-PARTY-NOTICES.txt.

using System;
using CodeBrix.Android.UI.Handlers;
using CodeBrix.Android.UI.Hosting;
using CodeBrix.Android.UI.Policy;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml.Controls;
using AHandler = global::Android.OS.Handler;
using ALooper = global::Android.OS.Looper;
using AValueAnimator = global::Android.Animation.ValueAnimator;

namespace CodeBrix.Android.UI.Platform.Animation;

/// <summary>
/// Follows the system's animator duration scale (Settings > Accessibility > Remove animations, the developer
/// options' animator scale) while the app runs: when it changes, every native ProgressBar re-maps its indeterminate
/// state - Material's own animation where the system allows it, the still segment or the CodeBrix-driven sweep
/// (<see cref="MotionPolicy"/>) where it does not.
/// </summary>
internal static class AnimatorScaleMonitor
{
    private static ScaleListener _listener;

    /// <summary>The animators-enabled state last seen.</summary>
    internal static bool AnimatorsEnabled { get; private set; } = true;

    /// <summary>How many scale changes were seen (diagnostics, tests).</summary>
    internal static int ChangeCount { get; private set; }

    /// <summary>Starts following the scale (idempotent; UI thread).</summary>
    internal static void EnsureStarted()
    {
        if (_listener != null)
        {
            return;
        }

        AnimatorsEnabled = AValueAnimator.AreAnimatorsEnabled();
        _listener = new ScaleListener();
        try
        {
            AValueAnimator.RegisterDurationScaleChangeListener(_listener);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            HostLog.For("CodeBrix.Android.UI.Motion").LogDebug(exception, "Following the animator duration scale failed.");
        }
    }

    private static void OnScaleChanged()
    {
        var enabled = AValueAnimator.AreAnimatorsEnabled();
        if (enabled == AnimatorsEnabled)
        {
            return;
        }

        AnimatorsEnabled = enabled;
        ChangeCount++;
        foreach (var root in ThemeRefresh.Roots())
        {
            ThemeRefresh.Walk(root, element =>
            {
                if (element is ProgressBar && PolicyDiagnostics.HandlerOf(element) is { } handler)
                {
                    handler.UpdateValue(ProgressBar.IsIndeterminateProperty);
                }
            });
        }
    }

    private sealed class ScaleListener : global::Java.Lang.Object, AValueAnimator.IDurationScaleChangeListener
    {
        public void OnChanged(float scale)
        {
            // The system may call this off the main thread: re-map on the main looper.
            new AHandler(ALooper.MainLooper).Post(OnScaleChanged);
        }
    }
}
