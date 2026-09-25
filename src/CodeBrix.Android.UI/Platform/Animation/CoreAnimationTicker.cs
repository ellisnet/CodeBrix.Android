using System;
using System.Reflection;
using CodeBrix.Android.UI.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml.Media;
using AChoreographer = global::Android.Views.Choreographer;
using ALooper = global::Android.OS.Looper;
using AMessageQueue = global::Android.OS.MessageQueue;

namespace CodeBrix.Android.UI.Platform.Animation;

/// <summary>
/// Ticks Core's animations on Android (plan 2.11 "Motion", 2.18): Core's Storyboards, VisualState transitions,
/// the templates' own animations and every other CompositionTarget.Rendering subscriber advance once per
/// display frame. On the Skia heads the renderer raises CompositionTarget.Rendering from its frame loop;
/// Android draws natively and never runs that loop, so this ticker raises it from Choreographer frames for as
/// long as Core has Rendering subscribers (a running animation subscribes; a finished one unsubscribes).
/// </summary>
/// <remarks>
/// Starting: Core gives the platform no reliable "a Rendering subscriber appeared" call on Android (its render
/// request goes through a render state machine that only a recording compositor resets), so the ticker looks
/// whenever the main looper goes idle - after every batch of UI-thread work, which is where a Storyboard is
/// begun - and starts the frame loop when Core has subscribers. The loop stops by itself when the last
/// subscriber goes. Nothing runs while nothing animates (no per-frame wake-ups when idle).
/// </remarks>
internal static class CoreAnimationTicker
{
    private static readonly FieldInfo _renderingField = typeof(CompositionTarget).GetField("_rendering", BindingFlags.NonPublic | BindingFlags.Static);
    private static FrameCallback _frameCallback;
    private static IdleHandler _idleHandler;
    private static bool _frameScheduled;
    private static bool _started;
    private static bool _warned;
    private static long _frames;

    /// <summary>True once <see cref="EnsureStarted"/> installed the ticker.</summary>
    internal static bool IsStarted => _started;

    /// <summary>How many frames the ticker raised CompositionTarget.Rendering for (diagnostics, tests).</summary>
    internal static long FrameCount => _frames;

    /// <summary>True while a frame is scheduled (Core has animations running).</summary>
    internal static bool IsTicking => _frameScheduled;

    /// <summary>Installs the ticker on the main looper (idempotent; call on the UI thread).</summary>
    internal static void EnsureStarted()
    {
        if (_started)
        {
            return;
        }

        _started = true;
        _frameCallback = new FrameCallback();
        _idleHandler = new IdleHandler();
        ALooper.MainLooper?.Queue?.AddIdleHandler(_idleHandler);
        if (_renderingField == null)
        {
            HostLog.For("CodeBrix.Android.UI.Motion").LogWarning(
                "CompositionTarget has no '_rendering' field in this Core build: Core animations are ticked on every frame while the app is idle-checked.");
        }

        Poke();
    }

    /// <summary>Starts the frame loop now if Core has animations running (safe to call any time on the UI thread).</summary>
    internal static void Poke()
    {
        if (!_frameScheduled && HasSubscribers())
        {
            Schedule();
        }
    }

    /// <summary>True when Core has CompositionTarget.Rendering subscribers (animations are running).</summary>
    /// <returns>True when a frame is needed.</returns>
    internal static bool HasSubscribers()
    {
        if (_renderingField == null)
        {
            return true;
        }

        try
        {
            return _renderingField.GetValue(null) != null;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            if (!_warned)
            {
                _warned = true;
                HostLog.For("CodeBrix.Android.UI.Motion").LogWarning(exception, "Reading Core's Rendering subscribers failed.");
            }

            return true;
        }
    }

    private static void Schedule()
    {
        if (_frameScheduled || AChoreographer.Instance is not { } choreographer)
        {
            return;
        }

        _frameScheduled = true;
        choreographer.PostFrameCallback(_frameCallback);
    }

    private static void OnFrame()
    {
        _frameScheduled = false;
        if (!HasSubscribers())
        {
            return;
        }

        try
        {
            _frames++;
            CompositionTarget.InvokeRendering();
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            HostLog.For("CodeBrix.Android.UI.Motion").LogError(exception, "A CompositionTarget.Rendering handler failed.");
        }

        if (HasSubscribers())
        {
            Schedule();
        }
    }

    private sealed class FrameCallback : global::Java.Lang.Object, AChoreographer.IFrameCallback
    {
        public void DoFrame(long frameTimeNanos) => OnFrame();
    }

    private sealed class IdleHandler : global::Java.Lang.Object, AMessageQueue.IIdleHandler
    {
        public bool QueueIdle()
        {
            Poke();
            return true;
        }
    }
}
