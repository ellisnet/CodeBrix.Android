using System;

namespace CodeBrix.Android.UI.Input;

/// <summary>
/// A gamepad an app or a test drives itself: it is listed among the connected gamepads like a
/// physical one, and each call below changes its state as a controller event would (raising
/// <see cref="GamepadTracker.GamepadStateChanged"/> when something changed).
/// </summary>
/// <remarks>Created by <see cref="Gamepads.AddVirtual"/> or <see cref="GamepadTracker.AddVirtual"/>.</remarks>
public sealed class VirtualGamepad
{
    private readonly GamepadTracker _tracker;

    internal VirtualGamepad(GamepadTracker tracker, Gamepad gamepad)
    {
        _tracker = tracker;
        Gamepad = gamepad;
    }

    /// <summary>The gamepad as the rest of the app sees it.</summary>
    public Gamepad Gamepad { get; }

    /// <summary>Presses a button (a <see cref="GamepadButtons"/> name).</summary>
    /// <param name="button">The button name.</param>
    public void Press(string button) => Apply(Gamepad.SetKeyButton(RequireButton(button), true));

    /// <summary>Releases a button (a <see cref="GamepadButtons"/> name).</summary>
    /// <param name="button">The button name.</param>
    public void Release(string button) => Apply(Gamepad.SetKeyButton(RequireButton(button), false));

    /// <summary>Presses and releases a button.</summary>
    /// <param name="button">The button name.</param>
    public void Tap(string button)
    {
        Press(button);
        Release(button);
    }

    /// <summary>Moves the left stick (Y positive upwards; both in -1..1).</summary>
    /// <param name="x">The horizontal deflection.</param>
    /// <param name="y">The vertical deflection.</param>
    public void SetLeftStick(float x, float y) => Apply(Gamepad.SetLeftStick(new GamepadStick(x, y)));

    /// <summary>Moves the right stick (Y positive upwards; both in -1..1).</summary>
    /// <param name="x">The horizontal deflection.</param>
    /// <param name="y">The vertical deflection.</param>
    public void SetRightStick(float x, float y) => Apply(Gamepad.SetRightStick(new GamepadStick(x, y)));

    /// <summary>Sets the left trigger, 0 (released) to 1 (fully pulled).</summary>
    /// <param name="value">The trigger value.</param>
    public void SetLeftTrigger(float value) => Apply(Gamepad.SetTriggerAxes(value, Gamepad.RightTrigger));

    /// <summary>Sets the right trigger, 0 (released) to 1 (fully pulled).</summary>
    /// <param name="value">The trigger value.</param>
    public void SetRightTrigger(float value) => Apply(Gamepad.SetTriggerAxes(Gamepad.LeftTrigger, value));

    /// <summary>Releases every button, centres both sticks and releases both triggers.</summary>
    public void ReleaseAll() => Apply(Gamepad.ReleaseAll());

    /// <summary>Disconnects the gamepad: it leaves the connected list and <see cref="GamepadTracker.GamepadDisconnected"/> is raised.</summary>
    public void Disconnect() => _tracker.RemoveDevice(Gamepad.DeviceId);

    private static string RequireButton(string button)
    {
        ArgumentNullException.ThrowIfNull(button);
        foreach (var known in GamepadButtons.All)
        {
            if (string.Equals(known, button, StringComparison.Ordinal))
            {
                return known;
            }
        }

        throw new ArgumentException($"'{button}' is not a gamepad button name; see GamepadButtons.", nameof(button));
    }

    private void Apply(bool changed)
    {
        if (!Gamepad.IsConnected)
        {
            throw new InvalidOperationException("The virtual gamepad is disconnected.");
        }

        if (changed)
        {
            _tracker.RaiseStateChanged(Gamepad);
        }
    }
}
