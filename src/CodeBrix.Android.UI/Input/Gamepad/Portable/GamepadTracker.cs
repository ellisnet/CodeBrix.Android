using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace CodeBrix.Android.UI.Input;

/// <summary>
/// The gamepad state machine: keeps the table of connected gamepads and turns the key and motion
/// events Android delivers (as plain key codes and axis values, see <see cref="AndroidGamepadCodes"/>)
/// into each <see cref="Gamepad"/>'s state. Pure C#, no Android types: a test or a synthetic input feed
/// drives it exactly as the activity does.
/// </summary>
/// <remarks>
/// Not thread-safe: every call is made on one thread (the UI thread on a device). An app normally
/// uses the process-wide <see cref="Gamepads"/> rather than its own tracker.
/// </remarks>
public sealed class GamepadTracker
{
    private const int FirstVirtualDeviceId = -1000;

    private static readonly Dictionary<int, string> _buttonsByKeyCode = new()
    {
        [AndroidGamepadCodes.KeyButtonA] = GamepadButtons.A,
        [AndroidGamepadCodes.KeyDPadCenter] = GamepadButtons.A,
        [AndroidGamepadCodes.KeyButtonB] = GamepadButtons.B,
        [AndroidGamepadCodes.KeyButtonX] = GamepadButtons.X,
        [AndroidGamepadCodes.KeyButtonY] = GamepadButtons.Y,
        [AndroidGamepadCodes.KeyButtonSelect] = GamepadButtons.Back,
        [AndroidGamepadCodes.KeyButtonMode] = GamepadButtons.Guide,
        [AndroidGamepadCodes.KeyButtonStart] = GamepadButtons.Start,
        [AndroidGamepadCodes.KeyButtonThumbLeft] = GamepadButtons.LeftStick,
        [AndroidGamepadCodes.KeyButtonThumbRight] = GamepadButtons.RightStick,
        [AndroidGamepadCodes.KeyButtonL1] = GamepadButtons.LeftShoulder,
        [AndroidGamepadCodes.KeyButtonR1] = GamepadButtons.RightShoulder,
        [AndroidGamepadCodes.KeyDPadUp] = GamepadButtons.DPadUp,
        [AndroidGamepadCodes.KeyDPadDown] = GamepadButtons.DPadDown,
        [AndroidGamepadCodes.KeyDPadLeft] = GamepadButtons.DPadLeft,
        [AndroidGamepadCodes.KeyDPadRight] = GamepadButtons.DPadRight,
    };

    private readonly Dictionary<int, Gamepad> _byDeviceId = new();
    private readonly List<Gamepad> _connected = new();
    private readonly ReadOnlyCollection<Gamepad> _connectedView;
    private int _nextVirtualDeviceId = FirstVirtualDeviceId;

    /// <summary>Creates an empty tracker.</summary>
    public GamepadTracker()
    {
        _connectedView = new ReadOnlyCollection<Gamepad>(_connected);
    }

    /// <summary>Raised after a gamepad was added.</summary>
    public event EventHandler<GamepadEventArgs> GamepadConnected;

    /// <summary>Raised after a gamepad was removed (its state is already cleared).</summary>
    public event EventHandler<GamepadEventArgs> GamepadDisconnected;

    /// <summary>Raised after an event changed a gamepad's buttons, sticks or triggers.</summary>
    public event EventHandler<GamepadEventArgs> GamepadStateChanged;

    /// <summary>The connected gamepads, in connection order.</summary>
    public IReadOnlyList<Gamepad> ConnectedGamepads => _connectedView;

    /// <summary>The key code of a button of the standard layout, as <see cref="HandleKey"/> maps it; null for any other key.</summary>
    /// <param name="keyCode">The Android key code.</param>
    public static string ButtonOfKeyCode(int keyCode) => _buttonsByKeyCode.TryGetValue(keyCode, out var button) ? button : null;

    /// <summary>True when <see cref="HandleKey"/> would act on <paramref name="keyCode"/> from a gamepad.</summary>
    /// <param name="keyCode">The Android key code.</param>
    public static bool IsGamepadKeyCode(int keyCode) =>
        _buttonsByKeyCode.ContainsKey(keyCode) || keyCode == AndroidGamepadCodes.KeyButtonL2 || keyCode == AndroidGamepadCodes.KeyButtonR2;

    /// <summary>Adds a device's gamepad (a second add of the same device id returns the existing gamepad).</summary>
    /// <param name="deviceId">Android's input device id.</param>
    /// <param name="info">The device's description.</param>
    /// <returns>The gamepad.</returns>
    public Gamepad AddDevice(int deviceId, GamepadDeviceInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);
        if (_byDeviceId.TryGetValue(deviceId, out var existing))
        {
            return existing;
        }

        return Add(new Gamepad($"android:{deviceId}", deviceId, info));
    }

    /// <summary>Removes a device's gamepad: its state is cleared and <see cref="GamepadDisconnected"/> raised.</summary>
    /// <param name="deviceId">Android's input device id.</param>
    /// <returns>True when the device was connected.</returns>
    public bool RemoveDevice(int deviceId)
    {
        if (!_byDeviceId.Remove(deviceId, out var gamepad))
        {
            return false;
        }

        _connected.Remove(gamepad);
        gamepad.MarkDisconnected();
        GamepadDisconnected?.Invoke(this, new GamepadEventArgs(gamepad));
        return true;
    }

    /// <summary>Removes every gamepad of the given origin.</summary>
    /// <param name="origin">The origin to remove.</param>
    public void RemoveAll(GamepadOrigin origin)
    {
        var ids = new List<int>();
        foreach (var gamepad in _connected)
        {
            if (gamepad.Info.Origin == origin)
            {
                ids.Add(gamepad.DeviceId);
            }
        }

        foreach (var id in ids)
        {
            RemoveDevice(id);
        }
    }

    /// <summary>The gamepad of a device id.</summary>
    /// <param name="deviceId">Android's input device id.</param>
    /// <param name="gamepad">The gamepad, when connected.</param>
    /// <returns>True when connected.</returns>
    public bool TryGetGamepad(int deviceId, out Gamepad gamepad) => _byDeviceId.TryGetValue(deviceId, out gamepad);

    /// <summary>
    /// Applies a key event. A button of the standard layout from a connected gamepad (or from any
    /// gamepad-sourced event, which adds an <see cref="GamepadOrigin.InjectedInput"/> gamepad for its
    /// device id) is consumed; everything else is left alone.
    /// </summary>
    /// <param name="deviceId">The event's device id.</param>
    /// <param name="keyCode">The Android key code.</param>
    /// <param name="isDown">True for a press, false for a release.</param>
    /// <param name="isRepeat">True for an auto-repeat of a held key (consumed, no state change).</param>
    /// <param name="fromGamepadSource">True when the event's source is a gamepad or joystick.</param>
    /// <returns>True when the event was consumed.</returns>
    public bool HandleKey(int deviceId, int keyCode, bool isDown, bool isRepeat, bool fromGamepadSource)
    {
        if (!IsGamepadKeyCode(keyCode))
        {
            return false;
        }

        if (!TryGetGamepad(deviceId, out var gamepad))
        {
            if (!fromGamepadSource)
            {
                // A hardware keyboard's arrow keys are D-pad key codes too: not ours.
                return false;
            }

            gamepad = AddDevice(deviceId, new GamepadDeviceInfo("Injected gamepad input", GamepadOrigin.InjectedInput));
        }

        if (isRepeat)
        {
            return true;
        }

        bool changed;
        if (keyCode == AndroidGamepadCodes.KeyButtonL2 || keyCode == AndroidGamepadCodes.KeyButtonR2)
        {
            changed = gamepad.SetTriggerKey(keyCode == AndroidGamepadCodes.KeyButtonL2, isDown);
        }
        else
        {
            changed = gamepad.SetKeyButton(_buttonsByKeyCode[keyCode], isDown);
        }

        if (changed)
        {
            GamepadStateChanged?.Invoke(this, new GamepadEventArgs(gamepad));
        }

        return true;
    }

    /// <summary>
    /// Applies a joystick motion event: the sticks, the triggers and the hat are read from the axes
    /// the device's <see cref="GamepadAxisProfile"/> names (or, for a profile without axes, from
    /// whichever of the known axes the event carries). An unknown device id adds an
    /// <see cref="GamepadOrigin.InjectedInput"/> gamepad.
    /// </summary>
    /// <param name="deviceId">The event's device id.</param>
    /// <param name="axes">The axis values of the event.</param>
    /// <returns>True when the event was consumed (always, for a known or gamepad-sourced device).</returns>
    public bool HandleMotion(int deviceId, ReadOnlySpan<GamepadAxisValue> axes)
    {
        if (!TryGetGamepad(deviceId, out var gamepad))
        {
            gamepad = AddDevice(deviceId, new GamepadDeviceInfo("Injected gamepad input", GamepadOrigin.InjectedInput));
        }

        var profile = gamepad.Info.AxisProfile;
        var changed = false;

        if (TryReadPair(profile, axes, AndroidGamepadCodes.AxisX, AndroidGamepadCodes.AxisY, out var x, out var y))
        {
            changed |= gamepad.SetLeftStick(new GamepadStick(x, -y));
        }

        if (TryReadPair(profile, axes, AndroidGamepadCodes.AxisZ, AndroidGamepadCodes.AxisRZ, out x, out y)
            || TryReadPair(profile, axes, AndroidGamepadCodes.AxisRX, AndroidGamepadCodes.AxisRY, out x, out y))
        {
            changed |= gamepad.SetRightStick(new GamepadStick(x, -y));
        }

        if (TryReadPair(profile, axes, AndroidGamepadCodes.AxisLeftTrigger, AndroidGamepadCodes.AxisRightTrigger, out x, out y)
            || TryReadPair(profile, axes, AndroidGamepadCodes.AxisBrake, AndroidGamepadCodes.AxisGas, out x, out y))
        {
            changed |= gamepad.SetTriggerAxes(x, y);
        }

        if (TryReadPair(profile, axes, AndroidGamepadCodes.AxisHatX, AndroidGamepadCodes.AxisHatY, out x, out y))
        {
            changed |= gamepad.SetHat(x, y);
        }

        if (changed)
        {
            GamepadStateChanged?.Invoke(this, new GamepadEventArgs(gamepad));
        }

        return true;
    }

    /// <summary>Adds a virtual gamepad the caller drives itself (tests, synthetic input, PlayTests).</summary>
    /// <param name="name">The gamepad's name.</param>
    public VirtualGamepad AddVirtual(string name = "Virtual gamepad")
    {
        var deviceId = _nextVirtualDeviceId--;
        var gamepad = Add(new Gamepad($"virtual:{FirstVirtualDeviceId - deviceId}", deviceId, new GamepadDeviceInfo(name, GamepadOrigin.Virtual)));
        return new VirtualGamepad(this, gamepad);
    }

    internal void RaiseStateChanged(Gamepad gamepad) => GamepadStateChanged?.Invoke(this, new GamepadEventArgs(gamepad));

    private Gamepad Add(Gamepad gamepad)
    {
        _byDeviceId[gamepad.DeviceId] = gamepad;
        _connected.Add(gamepad);
        GamepadConnected?.Invoke(this, new GamepadEventArgs(gamepad));
        return gamepad;
    }

    /// <summary>
    /// Reads an axis pair: from the profile's axes when the profile names both (an event that omits
    /// an axis the device has means that axis is unchanged, so the pair is skipped), or, for a
    /// profile without axes, when the event carries both. The dead zone is applied per axis.
    /// </summary>
    private static bool TryReadPair(GamepadAxisProfile profile, ReadOnlySpan<GamepadAxisValue> axes, int first, int second, out float firstValue, out float secondValue)
    {
        firstValue = 0f;
        secondValue = 0f;
        if (!profile.IsEmpty && !(profile.HasAxis(first) && profile.HasAxis(second)))
        {
            return false;
        }

        if (!TryFind(axes, first, out var a) || !TryFind(axes, second, out var b))
        {
            return false;
        }

        firstValue = ApplyFlat(a, profile.FlatOf(first));
        secondValue = ApplyFlat(b, profile.FlatOf(second));
        return true;
    }

    private static bool TryFind(ReadOnlySpan<GamepadAxisValue> axes, int axis, out float value)
    {
        // The last reading of an axis wins when it is repeated.
        value = 0f;
        var found = false;
        for (var i = 0; i < axes.Length; i++)
        {
            if (axes[i].Axis == axis)
            {
                value = axes[i].Value;
                found = true;
            }
        }

        return found;
    }

    private static float ApplyFlat(float value, float flat)
    {
        if (!float.IsFinite(value))
        {
            return 0f;
        }

        return flat > 0f && Math.Abs(value) <= flat ? 0f : value;
    }
}
