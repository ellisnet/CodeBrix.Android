namespace CodeBrix.Android.UI.Lottie.Portable;

/// <summary>
/// Which display frames raise a Lottie tick (AP1.12): the Choreographer calls back once per display frame (vsync), the
/// engine asks for a tick every <c>Interval</c> (1 / the animation's frame rate, at least 1/120 s). Ticks are due on an
/// ideal schedule (start, start + interval, start + 2 x interval, ...); a display frame raises the due tick when it is
/// at most half a display frame early, so a 30-fps animation on a 60-Hz display ticks on every second frame and a 24-fps
/// one averages exactly 24 ticks a second (alternating 2 and 3 frames). A frame more than one interval late restarts the
/// schedule from itself (no burst of catch-up ticks). Pure C#, host-free tested.
/// </summary>
internal sealed class FrameTickGate
{
    /// <summary>A 60-Hz display frame, in nanoseconds (the tolerance before the first real frame period is known).</summary>
    internal const long DefaultFrameNanos = 16_666_667L;

    private long _nextDueNanos = -1;
    private long _lastFrameNanos = -1;
    private long _frameNanos = DefaultFrameNanos;

    /// <summary>The time between ticks, in nanoseconds (0 or less = every frame).</summary>
    internal long IntervalNanos { get; set; }

    /// <summary>Forgets the last tick (the tick source was started again).</summary>
    internal void Reset()
    {
        _nextDueNanos = -1;
        _lastFrameNanos = -1;
    }

    /// <summary>Whether the display frame at <paramref name="frameTimeNanos"/> raises a tick.</summary>
    /// <param name="frameTimeNanos">The frame time the Choreographer reported (monotonic nanoseconds).</param>
    /// <returns>True when a tick is due.</returns>
    internal bool OnFrame(long frameTimeNanos)
    {
        if (_lastFrameNanos >= 0 && frameTimeNanos > _lastFrameNanos)
        {
            _frameNanos = frameTimeNanos - _lastFrameNanos;
        }

        _lastFrameNanos = frameTimeNanos;
        var interval = IntervalNanos > 0 ? IntervalNanos : 1L;
        if (_nextDueNanos < 0 || frameTimeNanos - _nextDueNanos > interval)
        {
            // First frame after a start, or far behind: tick now and schedule from here.
            _nextDueNanos = frameTimeNanos + interval;
            return true;
        }

        if (frameTimeNanos >= _nextDueNanos - (_frameNanos / 2))
        {
            _nextDueNanos += interval;
            return true;
        }

        return false;
    }
}
