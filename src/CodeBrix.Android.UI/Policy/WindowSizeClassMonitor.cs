#if __ANDROID__
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using CodeBrix.Android.UI.Hosting;
using CodeBrix.Platform.UI.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using AComponentCallbacks = global::Android.Content.IComponentCallbacks;
using AConfiguration = global::Android.Content.Res.Configuration;
using AContext = global::Android.Content.Context;
using AHandler = global::Android.OS.Handler;
using AInputDevice = global::Android.Views.InputDevice;
using AInputManager = global::Android.Hardware.Input.InputManager;
using AInputSourceType = global::Android.Views.InputSourceType;
using ALooper = global::Android.OS.Looper;
using AView = global::Android.Views.View;

namespace CodeBrix.Android.UI.Policy;

/// <summary>A window changed size class (or the fine-pointer dimension changed).</summary>
internal sealed class WindowSizeClassChangedEventArgs : EventArgs
{
    /// <summary>Creates the arguments.</summary>
    /// <param name="activity">The activity whose window changed (null = every window: an override changed).</param>
    /// <param name="previous">The classes before.</param>
    /// <param name="current">The classes now.</param>
    internal WindowSizeClassChangedEventArgs(CodeBrixActivity activity, WindowSizeClass previous, WindowSizeClass current)
    {
        Activity = activity;
        Previous = previous;
        Current = current;
    }

    /// <summary>The activity whose window changed (null = every window).</summary>
    internal CodeBrixActivity Activity { get; }

    /// <summary>The classes before.</summary>
    internal WindowSizeClass Previous { get; }

    /// <summary>The classes now.</summary>
    internal WindowSizeClass Current { get; }

    /// <summary>True when an element shown by <paramref name="activity"/> is concerned.</summary>
    /// <param name="activity">The element's activity.</param>
    /// <returns>True when concerned.</returns>
    internal bool Concerns(CodeBrixActivity activity) => Activity == null || ReferenceEquals(Activity, activity);
}

/// <summary>
/// The size-class service (plan 2.10): the Material 3 size classes of every CodeBrix activity's window, computed
/// from the window metrics (WindowManager.CurrentWindowMetrics - what AndroidX Window's WindowMetricsCalculator
/// returns on API 30+) in dp, recomputed on every configuration change (rotation, a docked phone entering
/// desktop mode: screenSize is a handled configuration change, so the activity is not recreated) and on every
/// layout change of the window's decor view (a multi-window or freeform resize may not deliver a configuration
/// change), plus the fine-pointer dimension (a mouse or touchpad is connected), recomputed when input devices
/// come and go. Handlers read <see cref="For(UIElement)"/> and re-map when <see cref="Changed"/> fires; the page
/// is never recreated.
/// </summary>
internal static class WindowSizeClassMonitor
{
    private static readonly ConditionalWeakTable<CodeBrixActivity, State> _states = new();
    private static readonly List<WeakReference<CodeBrixActivity>> _attached = new();
    private static DeviceListener _deviceListener;
    private static bool? _finePointer;

    /// <summary>Raised on the UI thread when a window's size classes (or the pointer dimension) change.</summary>
    internal static event EventHandler<WindowSizeClassChangedEventArgs> Changed;

    /// <summary>True when a mouse or touchpad is connected (cached; refreshed when devices change).</summary>
    internal static bool FinePointerPresent => _finePointer ??= DetectFinePointer();

    /// <summary>Starts following <paramref name="activity"/>'s window (idempotent; UI thread).</summary>
    /// <param name="activity">The activity.</param>
    internal static void Attach(CodeBrixActivity activity)
    {
        if (activity == null || _states.TryGetValue(activity, out _))
        {
            return;
        }

        var state = new State(activity);
        _states.Add(activity, state);
        _attached.RemoveAll(r => !r.TryGetTarget(out var a) || a.IsDestroyed);
        _attached.Add(new WeakReference<CodeBrixActivity>(activity));
        state.Current = Compute(activity);
        activity.RegisterComponentCallbacks(state.Callbacks);
        if (activity.Window?.DecorView is { } decor)
        {
            decor.LayoutChange += state.OnLayoutChange;
        }

        EnsureDeviceListener(activity);
        HostLog.For("CodeBrix.Android.UI.Policy").LogInformation("Window size classes of {Activity}: {Classes}.", activity.GetType().Name, state.Current);
    }

    /// <summary>Stops following <paramref name="activity"/> (it is being destroyed).</summary>
    /// <param name="activity">The activity.</param>
    internal static void Detach(CodeBrixActivity activity)
    {
        if (activity == null || !_states.TryGetValue(activity, out var state))
        {
            return;
        }

        try
        {
            activity.UnregisterComponentCallbacks(state.Callbacks);
            if (activity.Window?.DecorView is { } decor)
            {
                decor.LayoutChange -= state.OnLayoutChange;
            }
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            HostLog.For("CodeBrix.Android.UI.Policy").LogDebug(exception, "Detaching the size-class monitor failed.");
        }

        _states.Remove(activity);
    }

    /// <summary>The size classes an element is shown with: the override, else its window's, else the current activity's.</summary>
    /// <param name="element">The element (may be null).</param>
    /// <returns>The classes.</returns>
    internal static WindowSizeClass For(UIElement element)
    {
        if (AdaptivePolicy.Override is { } simulated)
        {
            return simulated;
        }

        var activity = ActivityOf(element);
        if (activity != null)
        {
            return Current(activity);
        }

        if (element?.XamlRoot is { Size: var size } && size.Width > 0)
        {
            return new WindowSizeClass(size.Width, size.Height, FinePointerPresent);
        }

        return WindowSizeClass.Unknown;
    }

    /// <summary>The size classes of an activity's window (the override when one is set).</summary>
    /// <param name="activity">The activity.</param>
    /// <returns>The classes.</returns>
    internal static WindowSizeClass Current(CodeBrixActivity activity)
    {
        if (AdaptivePolicy.Override is { } simulated)
        {
            return simulated;
        }

        if (activity == null)
        {
            return WindowSizeClass.Unknown;
        }

        if (!_states.TryGetValue(activity, out var state))
        {
            return Compute(activity);
        }

        return state.Current;
    }

    /// <summary>The activity showing an element's window, or null.</summary>
    /// <param name="element">The element.</param>
    /// <returns>The activity.</returns>
    internal static CodeBrixActivity ActivityOf(UIElement element) =>
        element?.XamlRoot is { } root && XamlRootMap.GetHostForRoot(root) is AndroidXamlRootHost { Wrapper.Activity: { } activity } ? activity : null;

    /// <summary>
    /// Sets (or clears, with null) the size-class override and tells every consumer, exactly as a window resize
    /// would (diagnostics, tests).
    /// </summary>
    /// <param name="simulated">The classes to report, or null for the real ones.</param>
    internal static void SetOverride(WindowSizeClass? simulated)
    {
        var previous = AdaptivePolicy.Override ?? CurrentOfAnyActivity();
        AdaptivePolicy.Override = simulated;
        var current = simulated ?? CurrentOfAnyActivity();
        Raise(new WindowSizeClassChangedEventArgs(null, previous, current));
    }

    /// <summary>Recomputes every followed window now (after a resize the caller knows about).</summary>
    internal static void RecomputeAll()
    {
        _finePointer = null;
        foreach (var reference in _attached.ToArray())
        {
            if (reference.TryGetTarget(out var activity) && !activity.IsDestroyed)
            {
                Recompute(activity);
            }
        }
    }

    /// <summary>The classes of a window from its metrics (in dp).</summary>
    /// <param name="activity">The activity.</param>
    /// <returns>The classes.</returns>
    internal static WindowSizeClass Compute(CodeBrixActivity activity)
    {
        try
        {
            var bounds = activity.WindowManager?.CurrentWindowMetrics?.Bounds;
            var density = activity.Resources?.DisplayMetrics?.Density ?? 1f;
            if (bounds == null || density <= 0)
            {
                return WindowSizeClass.Unknown;
            }

            return new WindowSizeClass(bounds.Width() / density, bounds.Height() / density, FinePointerPresent);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            HostLog.For("CodeBrix.Android.UI.Policy").LogDebug(exception, "Computing the window metrics failed.");
            return WindowSizeClass.Unknown;
        }
    }

    private static WindowSizeClass CurrentOfAnyActivity() =>
        ActivityRegistry.Current is { } activity ? Current(activity) : WindowSizeClass.Unknown;

    private static void Recompute(CodeBrixActivity activity)
    {
        if (activity == null || activity.IsDestroyed || !_states.TryGetValue(activity, out var state))
        {
            return;
        }

        var previous = state.Current;
        var current = Compute(activity);
        state.Current = current;
        if (!current.SameClassesAs(previous) && AdaptivePolicy.Override == null)
        {
            HostLog.For("CodeBrix.Android.UI.Policy").LogInformation("Window size classes changed: {Previous} -> {Current}.", previous, current);
            Raise(new WindowSizeClassChangedEventArgs(activity, previous, current));
        }
    }

    private static void Raise(WindowSizeClassChangedEventArgs args)
    {
        var handlers = Changed;
        if (handlers == null)
        {
            return;
        }

        foreach (EventHandler<WindowSizeClassChangedEventArgs> handler in handlers.GetInvocationList())
        {
            try
            {
                handler(null, args);
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                HostLog.For("CodeBrix.Android.UI.Policy").LogError(exception, "A size-class consumer failed to re-map.");
            }
        }
    }

    private static bool DetectFinePointer()
    {
        try
        {
            foreach (var id in AInputDevice.GetDeviceIds() ?? Array.Empty<int>())
            {
                var device = AInputDevice.GetDevice(id);
                if (device == null || device.IsVirtual || !device.IsEnabled)
                {
                    continue;
                }

                if (device.SupportsSource(AInputSourceType.Mouse) || device.SupportsSource(AInputSourceType.Touchpad))
                {
                    return true;
                }
            }
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            HostLog.For("CodeBrix.Android.UI.Policy").LogDebug(exception, "Listing the input devices failed.");
        }

        return false;
    }

    private static void EnsureDeviceListener(AContext context)
    {
        if (_deviceListener != null || context.GetSystemService(AContext.InputService) is not AInputManager inputManager)
        {
            return;
        }

        _deviceListener = new DeviceListener();
        inputManager.RegisterInputDeviceListener(_deviceListener, new AHandler(ALooper.MainLooper));
    }

    private sealed class State
    {
        private readonly WeakReference<CodeBrixActivity> _activity;
        private bool _posted;

        internal State(CodeBrixActivity activity)
        {
            _activity = new WeakReference<CodeBrixActivity>(activity);
            Callbacks = new ConfigurationCallbacks(this);
        }

        internal WindowSizeClass Current { get; set; }

        internal ConfigurationCallbacks Callbacks { get; }

        internal void OnLayoutChange(object sender, AView.LayoutChangeEventArgs e)
        {
            if (e.Right - e.Left == e.OldRight - e.OldLeft && e.Bottom - e.Top == e.OldBottom - e.OldTop)
            {
                return;
            }

            // Re-map after this layout pass, not inside it.
            if (!_posted && _activity.TryGetTarget(out var activity) && activity.Window?.DecorView is { } decor)
            {
                _posted = true;
                decor.Post(() =>
                {
                    _posted = false;
                    Recompute(activity);
                });
            }
        }

        internal void OnConfigurationChanged()
        {
            if (_activity.TryGetTarget(out var activity))
            {
                Recompute(activity);
            }
        }
    }

    private sealed class ConfigurationCallbacks : global::Java.Lang.Object, AComponentCallbacks
    {
        private readonly State _state;

        internal ConfigurationCallbacks(State state) => _state = state;

        public void OnConfigurationChanged(AConfiguration newConfig) => _state.OnConfigurationChanged();

        public void OnLowMemory()
        {
        }
    }

    private sealed class DeviceListener : global::Java.Lang.Object, AInputManager.IInputDeviceListener
    {
        public void OnInputDeviceAdded(int deviceId) => RecomputeAll();

        public void OnInputDeviceChanged(int deviceId) => RecomputeAll();

        public void OnInputDeviceRemoved(int deviceId) => RecomputeAll();
    }
}
#endif
