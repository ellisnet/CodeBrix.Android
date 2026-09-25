using System.Runtime.CompilerServices;
using CodeBrix.Android.UI.Policy;
using AChoreographer = global::Android.Views.Choreographer;
using ALinearProgressIndicator = Google.Android.Material.ProgressIndicator.LinearProgressIndicator;
using ASystemClock = global::Android.OS.SystemClock;
using AValueAnimator = global::Android.Animation.ValueAnimator;

namespace CodeBrix.Android.UI.Platform.Animation;

/// <summary>
/// The CodeBrix-driven sweep of a native indeterminate linear progress indicator whose own animation the system
/// removed (animator duration scale 0) while the motion policy says everything moves
/// (<see cref="MotionMode.AlwaysAnimate"/>): the indicator, which the ProgressBar handler shows as a still
/// segment in that case, is re-drawn every frame with a growing segment (<see cref="MotionPolicy.SweepFraction"/>).
/// </summary>
internal sealed class IndeterminateProgressDriver : global::Java.Lang.Object, AChoreographer.IFrameCallback
{
    /// <summary>The length of one sweep in milliseconds.</summary>
    internal const double CycleMilliseconds = 1500;

    private static readonly ConditionalWeakTable<ALinearProgressIndicator, IndeterminateProgressDriver> _drivers = new();
    private readonly ALinearProgressIndicator _view;
    private readonly long _start;
    private bool _active;
    private bool _posted;

    private IndeterminateProgressDriver(ALinearProgressIndicator view)
    {
        _view = view;
        _start = ASystemClock.UptimeMillis();
        _view.ViewAttachedToWindow += (_, _) =>
        {
            if (_active)
            {
                Post();
            }
        };
    }

    /// <summary>How many frames the drivers have drawn (diagnostics, tests).</summary>
    internal static long FrameCount { get; private set; }

    /// <summary>
    /// Starts or stops the driver of <paramref name="view"/>: it runs while the bar is indeterminate, the
    /// system removed animations and the motion policy wants motion anyway.
    /// </summary>
    /// <param name="view">The indicator.</param>
    /// <param name="indeterminate">True when the ProgressBar is indeterminate.</param>
    internal static void Update(ALinearProgressIndicator view, bool indeterminate)
    {
        if (view == null)
        {
            return;
        }

        var wanted = indeterminate && MotionPolicy.DrivesFrozenNativeAnimations(AValueAnimator.AreAnimatorsEnabled());
        if (!_drivers.TryGetValue(view, out var driver))
        {
            if (!wanted)
            {
                return;
            }

            driver = new IndeterminateProgressDriver(view);
            _drivers.Add(view, driver);
        }

        driver.SetActive(wanted);
    }

    /// <summary>True while a driver moves <paramref name="view"/>.</summary>
    /// <param name="view">The indicator.</param>
    /// <returns>True when driven.</returns>
    internal static bool IsDriving(ALinearProgressIndicator view) => view != null && _drivers.TryGetValue(view, out var driver) && driver._active;

    /// <inheritdoc />
    public void DoFrame(long frameTimeNanos)
    {
        _posted = false;
        if (!_active || _view.Handle == System.IntPtr.Zero)
        {
            return;
        }

        if (!_view.IsAttachedToWindow)
        {
            // Off screen (disconnected or not shown yet): no frames until the view is attached again.
            return;
        }

        var fraction = MotionPolicy.SweepFraction(ASystemClock.UptimeMillis() - _start, CycleMilliseconds);
        _view.SetProgressCompat((int)(fraction * _view.Max), false);
        FrameCount++;
        Post();
    }

    private void SetActive(bool active)
    {
        _active = active;
        if (active)
        {
            Post();
        }
    }

    private void Post()
    {
        if (!_posted && AChoreographer.Instance is { } choreographer)
        {
            _posted = true;
            choreographer.PostFrameCallback(this);
        }
    }
}
