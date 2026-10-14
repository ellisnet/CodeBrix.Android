using System;
using System.Collections.Generic;

namespace CodeBrix.Android.UI.Input;

/// <summary>
/// The current state of one connected gamepad: which buttons are down (by their
/// <see cref="GamepadButtons"/> names), both sticks and both triggers.
/// </summary>
/// <remarks>
/// The state is a snapshot a game polls each frame; it changes only on the thread that feeds the
/// <see cref="GamepadTracker"/> (the UI thread for a physical device). The directional pad reads as
/// pressed whether the controller reports it as keys or as the hat axes, and a trigger reads 1 while
/// its digital button (L2 / R2) is down even on a controller without analogue triggers.
/// </remarks>
public sealed class Gamepad
{
    private readonly HashSet<string> _pressed = new(StringComparer.Ordinal);
    private readonly HashSet<string> _pressedFromKeys = new(StringComparer.Ordinal);
    private readonly HashSet<string> _pressedFromHat = new(StringComparer.Ordinal);
    private float _leftTriggerAxis;
    private float _rightTriggerAxis;
    private bool _leftTriggerKey;
    private bool _rightTriggerKey;

    internal Gamepad(string id, int deviceId, GamepadDeviceInfo info)
    {
        Id = id;
        DeviceId = deviceId;
        Info = info;
        IsConnected = true;
    }

    /// <summary>A stable id for the gamepad while it is connected (<c>android:&lt;device id&gt;</c>, or <c>virtual:&lt;n&gt;</c>).</summary>
    public string Id { get; }

    /// <summary>Android's input device id (negative for a virtual gamepad).</summary>
    public int DeviceId { get; }

    /// <summary>The device behind the gamepad.</summary>
    public GamepadDeviceInfo Info { get; }

    /// <summary>The device's name.</summary>
    public string Name => Info.Name;

    /// <summary>False once the device was removed; the state is then empty.</summary>
    public bool IsConnected { get; private set; }

    /// <summary>The names of the buttons currently down (see <see cref="GamepadButtons"/>).</summary>
    public IReadOnlyCollection<string> PressedButtons => _pressed;

    /// <summary>The left stick (Y positive upwards).</summary>
    public GamepadStick LeftStick { get; private set; }

    /// <summary>The right stick (Y positive upwards).</summary>
    public GamepadStick RightStick { get; private set; }

    /// <summary>The left trigger, 0 (released) to 1 (fully pulled).</summary>
    public float LeftTrigger => _leftTriggerKey ? 1f : _leftTriggerAxis;

    /// <summary>The right trigger, 0 (released) to 1 (fully pulled).</summary>
    public float RightTrigger => _rightTriggerKey ? 1f : _rightTriggerAxis;

    /// <summary>True while <paramref name="button"/> (a <see cref="GamepadButtons"/> name) is down.</summary>
    /// <param name="button">The button name.</param>
    public bool IsPressed(string button) => button != null && _pressed.Contains(button);

    /// <inheritdoc />
    public override string ToString() => $"{Name} ({Id})";

    internal bool SetKeyButton(string button, bool down)
    {
        var changed = down ? _pressedFromKeys.Add(button) : _pressedFromKeys.Remove(button);
        if (changed)
        {
            RebuildPressed();
        }

        return changed;
    }

    internal bool SetHat(float x, float y)
    {
        var changed = false;
        changed |= SetHatName(GamepadButtons.DPadLeft, x <= -0.5f);
        changed |= SetHatName(GamepadButtons.DPadRight, x >= 0.5f);
        changed |= SetHatName(GamepadButtons.DPadUp, y <= -0.5f);
        changed |= SetHatName(GamepadButtons.DPadDown, y >= 0.5f);
        if (changed)
        {
            RebuildPressed();
        }

        return changed;
    }

    internal bool SetLeftStick(GamepadStick stick)
    {
        if (LeftStick == stick)
        {
            return false;
        }

        LeftStick = stick;
        return true;
    }

    internal bool SetRightStick(GamepadStick stick)
    {
        if (RightStick == stick)
        {
            return false;
        }

        RightStick = stick;
        return true;
    }

    internal bool SetTriggerAxes(float left, float right)
    {
        left = ClampTrigger(left);
        right = ClampTrigger(right);
        if (_leftTriggerAxis == left && _rightTriggerAxis == right)
        {
            return false;
        }

        _leftTriggerAxis = left;
        _rightTriggerAxis = right;
        return true;
    }

    internal bool SetTriggerKey(bool leftTrigger, bool down)
    {
        if (leftTrigger)
        {
            if (_leftTriggerKey == down)
            {
                return false;
            }

            _leftTriggerKey = down;
            return true;
        }

        if (_rightTriggerKey == down)
        {
            return false;
        }

        _rightTriggerKey = down;
        return true;
    }

    internal bool ReleaseAll()
    {
        var changed = _pressedFromKeys.Count > 0 || _pressedFromHat.Count > 0
            || LeftStick != GamepadStick.Centered || RightStick != GamepadStick.Centered
            || _leftTriggerAxis != 0f || _rightTriggerAxis != 0f || _leftTriggerKey || _rightTriggerKey;
        _pressedFromKeys.Clear();
        _pressedFromHat.Clear();
        _pressed.Clear();
        LeftStick = GamepadStick.Centered;
        RightStick = GamepadStick.Centered;
        _leftTriggerAxis = 0f;
        _rightTriggerAxis = 0f;
        _leftTriggerKey = false;
        _rightTriggerKey = false;
        return changed;
    }

    internal void MarkDisconnected()
    {
        ReleaseAll();
        IsConnected = false;
    }

    private static float ClampTrigger(float value) => float.IsFinite(value) ? Math.Clamp(value, 0f, 1f) : 0f;

    private bool SetHatName(string button, bool down) => down ? _pressedFromHat.Add(button) : _pressedFromHat.Remove(button);

    private void RebuildPressed()
    {
        _pressed.Clear();
        _pressed.UnionWith(_pressedFromKeys);
        _pressed.UnionWith(_pressedFromHat);
    }
}
