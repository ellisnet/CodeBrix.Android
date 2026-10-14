using System;
using System.Collections.Generic;
using CodeBrix.Platform.Extensions;
using CodeBrix.Platform.UI.Dispatching;
using CodeBrix.Platform.UI.Dispatching.Contracts;
using Microsoft.Extensions.Logging;
using AHandler = global::Android.OS.Handler;
using ALooper = global::Android.OS.Looper;

namespace CodeBrix.Android.UI.Dispatching.Android;

/// <summary>
/// The Android implementation of <see cref="IDispatcherPumpPlatform"/>: Core's dispatch
/// callback runs on the main Looper. One reusable <c>IRunnable</c> exists per callback
/// (Core passes the same static callback every time), so a post allocates no Java peer.
/// </summary>
/// <remarks>
/// Every priority is posted as a normal Looper message. The priority queues live in managed
/// Core, and Core asks the pump for ONE dispatch when its queues go from empty to non-empty,
/// passing the priority of that first item; the dispatch then drains High before Normal
/// before Low before Idle. Deferring an Idle-first dispatch to MessageQueue idle time would
/// hold back every item queued behind it (a Looper that is never idle, e.g. during an
/// animation, would stall the UI), so Idle is not special-cased here.
/// </remarks>
internal sealed class DispatcherPumpAndroidPlatform : IDispatcherPumpPlatform
{
    private readonly object _gate = new();
    private readonly Dictionary<Action, CallbackRunnable> _runnables = new();
    private AHandler _handler;
    private int _traced;

    /// <inheritdoc />
    public bool HasThreadAccess => ALooper.MyLooper() == ALooper.MainLooper;

    /// <summary>The number of dispatches scheduled so far.</summary>
    internal long ScheduledCount { get; private set; }

    /// <summary>
    /// Run on the UI thread after every dispatch of Core's queues (set by the animation frame clock, which uses it to
    /// start its frame loop when a dispatch began an animation). A looper that is never idle - an app whose UI thread
    /// always has a message due - never runs idle handlers, so "after a dispatch" is the moment that is always there.
    /// It must be cheap; an exception it throws is swallowed so the pump keeps running.
    /// </summary>
    internal static Action AfterDispatch { get; set; }

    /// <inheritdoc />
    public void Schedule(Action dispatchCallback, NativeDispatcherPriority priority)
    {
        ArgumentNullException.ThrowIfNull(dispatchCallback);

        AHandler handler;
        CallbackRunnable runnable;
        lock (_gate)
        {
            handler = _handler ??= new AHandler(ALooper.MainLooper);
            if (!_runnables.TryGetValue(dispatchCallback, out runnable))
            {
                runnable = new CallbackRunnable(dispatchCallback);
                _runnables.Add(dispatchCallback, runnable);
            }

            ScheduledCount++;
        }

        TraceSchedule(priority);
        handler.Post(runnable);
    }

    // Diagnostics: the first schedules are logged at Debug level (category CodeBrix.Android.UI.Dispatching).
    private void TraceSchedule(NativeDispatcherPriority priority)
    {
        if (_traced < 3 && LogExtensionPoint.AmbientLoggerFactory?.CreateLogger("CodeBrix.Android.UI.Dispatching") is { } log && log.IsEnabled(LogLevel.Debug))
        {
            _traced++;
            log.LogDebug("Dispatcher pump: schedule #{Count} (priority {Priority}, main thread {Main}).", _traced, priority, HasThreadAccess);
        }
    }

    private sealed class CallbackRunnable : Java.Lang.Object, Java.Lang.IRunnable
    {
        private readonly Action _callback;

        public CallbackRunnable(Action callback) => _callback = callback;

        public void Run()
        {
            try
            {
                _callback();
            }
            finally
            {
                try
                {
                    AfterDispatch?.Invoke();
                }
                catch (Exception exception) when (exception is not OutOfMemoryException)
                {
                    // the hook is a hint, never a reason for the dispatcher to stop
                }
            }
        }
    }
}
