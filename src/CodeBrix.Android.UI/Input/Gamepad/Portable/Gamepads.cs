using System;
using System.Collections.Generic;

namespace CodeBrix.Android.UI.Input;

/// <summary>
/// The app's gamepads: <see cref="Start"/> once (on the UI thread) and poll
/// <see cref="ConnectedGamepads"/> each frame, or listen to the events. Until started, gamepad
/// input is left to Android as before (a controller's D-pad moves the focus, its keys are ordinary
/// keys); once started, every gamepad button, stick and trigger event is consumed here and reaches
/// nothing else.
/// </summary>
/// <remarks>
/// <para>
/// A <see cref="VirtualGamepad"/> (<see cref="AddVirtual"/>) is listed whether or not the physical
/// pipeline is started, so tests and PlayTests drive the same state a game reads on a device.
/// </para>
/// <para>
/// A connected controller is logged (category <c>CodeBrix.Android.UI.Input.Gamepad</c>, Information)
/// with its name, vendor and product ids and its axis layout; that line is what tells a controller's
/// layout - and firmware generation - apart.
/// </para>
/// </remarks>
public static partial class Gamepads
{
    private static readonly GamepadTracker _tracker = new();

    /// <summary>True between <see cref="Start"/> and <see cref="Stop"/>.</summary>
    public static bool IsStarted { get; private set; }

    /// <summary>The connected gamepads, in connection order.</summary>
    public static IReadOnlyList<Gamepad> ConnectedGamepads => _tracker.ConnectedGamepads;

    /// <summary>Raised after a gamepad was added.</summary>
    public static event EventHandler<GamepadEventArgs> GamepadConnected
    {
        add => _tracker.GamepadConnected += value;
        remove => _tracker.GamepadConnected -= value;
    }

    /// <summary>Raised after a gamepad was removed.</summary>
    public static event EventHandler<GamepadEventArgs> GamepadDisconnected
    {
        add => _tracker.GamepadDisconnected += value;
        remove => _tracker.GamepadDisconnected -= value;
    }

    /// <summary>Raised after an event changed a gamepad's buttons, sticks or triggers.</summary>
    public static event EventHandler<GamepadEventArgs> GamepadStateChanged
    {
        add => _tracker.GamepadStateChanged += value;
        remove => _tracker.GamepadStateChanged -= value;
    }

    /// <summary>The tracker behind the app's gamepads (the activity feeds it).</summary>
    internal static GamepadTracker Tracker => _tracker;

    /// <summary>
    /// Starts reading physical gamepads: the connected controllers are listed, connections and
    /// disconnections followed, and their events consumed. Idempotent. UI thread.
    /// </summary>
    public static void Start()
    {
        if (IsStarted)
        {
            return;
        }

        IsStarted = true;
        OnStarted();
    }

    /// <summary>
    /// Stops reading physical gamepads: the device and injected gamepads are removed (virtual ones
    /// stay until disconnected) and gamepad events go to Android again. Idempotent. UI thread.
    /// </summary>
    public static void Stop()
    {
        if (!IsStarted)
        {
            return;
        }

        IsStarted = false;
        OnStopped();
        _tracker.RemoveAll(GamepadOrigin.Device);
        _tracker.RemoveAll(GamepadOrigin.InjectedInput);
    }

    /// <summary>Adds a virtual gamepad the caller drives itself.</summary>
    /// <param name="name">The gamepad's name.</param>
    public static VirtualGamepad AddVirtual(string name = "Virtual gamepad") => _tracker.AddVirtual(name);

    static partial void OnStarted();

    static partial void OnStopped();
}
