using System;
using CodeBrix.Android.UI.Lottie.Portable;
using CodeBrix.Platform.UI.Lottie.Engine;
using AChoreographer = global::Android.Views.Choreographer;

namespace CodeBrix.Android.UI.Lottie.Android;

/// <summary>
/// A Lottie frame clock on Android's Choreographer (AP1.12, WPE1-13 item b): the engine's ITickSource, ticking on the
/// display's frame callbacks (vsync) of the UI thread that created it instead of a dispatcher timer, so an animation's
/// frames line up with the frames the display shows. The engine sets <see cref="Interval"/> (1 / the frame rate); frames
/// in between are skipped (<see cref="FrameTickGate"/>). Which animation frame is drawn is still the engine's own
/// stopwatch, so the clock changes WHEN frames are drawn, never WHICH frame a given time shows.
/// </summary>
internal sealed class ChoreographerTickSource : Java.Lang.Object, AChoreographer.IFrameCallback, ITickSource
{
    private readonly AChoreographer _choreographer;
    private readonly FrameTickGate _gate = new();
    private TimeSpan _interval;
    private bool _running;

    /// <summary>Creates a stopped tick source for the calling (UI, looper) thread.</summary>
    internal ChoreographerTickSource()
    {
        _choreographer = AChoreographer.Instance;
    }

    /// <inheritdoc />
    public event Action Tick;

    /// <inheritdoc />
    public TimeSpan Interval
    {
        get => _interval;
        set
        {
            _interval = value;
            _gate.IntervalNanos = value.Ticks * 100L;
        }
    }

    /// <summary>Whether the tick source is running (diagnostics and fences).</summary>
    internal bool IsRunning => _running;

    /// <inheritdoc />
    public void Start()
    {
        if (_running)
        {
            _choreographer.RemoveFrameCallback(this);
        }

        _running = true;
        _gate.Reset();
        _choreographer.PostFrameCallback(this);
    }

    /// <inheritdoc />
    public void Stop()
    {
        _running = false;
        _choreographer.RemoveFrameCallback(this);
    }

    /// <inheritdoc />
    public void DoFrame(long frameTimeNanos)
    {
        if (!_running)
        {
            return;
        }

        // Next frame first: a tick handler that stops the source removes it again.
        _choreographer.PostFrameCallback(this);
        if (_gate.OnFrame(frameTimeNanos))
        {
            Tick?.Invoke();
        }
    }
}

/// <summary>The Android implementation of the optional ILottieTickSourcePlatform contract (AP1.12, WPE1-13 item b).</summary>
internal sealed class LottieTickSourceAndroidPlatform : CodeBrix.Platform.UI.Lottie.Contracts.ILottieTickSourcePlatform
{
    /// <summary>The number of tick sources created (diagnostics and the device fence).</summary>
    internal static int Created { get; private set; }

    /// <inheritdoc />
    public ITickSource CreateTickSource()
    {
        Created++;
        return new ChoreographerTickSource();
    }
}
