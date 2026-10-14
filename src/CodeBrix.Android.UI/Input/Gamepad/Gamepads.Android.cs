using System;
using System.Collections.Generic;
using CodeBrix.Android.UI.Hosting;
using Microsoft.Extensions.Logging;
using AAxis = global::Android.Views.Axis;
using AContext = global::Android.Content.Context;
using AHandler = global::Android.OS.Handler;
using AInputDevice = global::Android.Views.InputDevice;
using AInputManager = global::Android.Hardware.Input.InputManager;
using AInputSourceType = global::Android.Views.InputSourceType;
using AKeyEvent = global::Android.Views.KeyEvent;
using AKeyEventActions = global::Android.Views.KeyEventActions;
using ALooper = global::Android.OS.Looper;
using AMotionEvent = global::Android.Views.MotionEvent;
using AMotionEventActions = global::Android.Views.MotionEventActions;

namespace CodeBrix.Android.UI.Input;

/// <summary>
/// The Android half of <see cref="Gamepads"/>: lists the gamepad and joystick input devices, follows
/// their connections through the input manager, and feeds the activity's key and joystick-motion
/// events to the tracker. Everything runs on the UI thread (the activity's dispatch, and the input
/// manager's listener on the main looper).
/// </summary>
public static partial class Gamepads
{
    private static readonly ILogger _log = HostLog.For("CodeBrix.Android.UI.Input.Gamepad");

    /// <summary>The axes read from a joystick motion event, in the order the tracker prefers them.</summary>
    private static readonly int[] _knownAxes =
    {
        AndroidGamepadCodes.AxisX, AndroidGamepadCodes.AxisY,
        AndroidGamepadCodes.AxisZ, AndroidGamepadCodes.AxisRZ,
        AndroidGamepadCodes.AxisRX, AndroidGamepadCodes.AxisRY,
        AndroidGamepadCodes.AxisLeftTrigger, AndroidGamepadCodes.AxisRightTrigger,
        AndroidGamepadCodes.AxisBrake, AndroidGamepadCodes.AxisGas,
        AndroidGamepadCodes.AxisHatX, AndroidGamepadCodes.AxisHatY,
    };

    private static DeviceListener _listener;
    private static AInputManager _inputManager;

    /// <summary>
    /// Offers a key event of the activity to the gamepads: consumed when started and the key is a
    /// gamepad button of a gamepad (see <see cref="GamepadTracker.HandleKey"/>).
    /// </summary>
    /// <param name="e">The key event.</param>
    /// <returns>True when consumed.</returns>
    internal static bool TryHandleKey(AKeyEvent e)
    {
        if (!IsStarted || e == null || (e.Action != AKeyEventActions.Down && e.Action != AKeyEventActions.Up))
        {
            return false;
        }

        try
        {
            var fromGamepad = IsGamepadSource(e.Source);
            if (fromGamepad && !_tracker.TryGetGamepad(e.DeviceId, out _))
            {
                // A controller whose connection the listener has not reported yet (or input injected through the
                // system, which has no device): describe it from the device when there is one.
                AddDescribedDevice(e.DeviceId);
            }

            return _tracker.HandleKey(e.DeviceId, (int)e.KeyCode, e.Action == AKeyEventActions.Down, e.RepeatCount > 0, fromGamepad);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            _log.LogError(exception, "Failed to read a gamepad key event.");
            return false;
        }
    }

    /// <summary>
    /// Offers a generic motion event of the activity to the gamepads: consumed when started and the
    /// event is a joystick move (see <see cref="GamepadTracker.HandleMotion"/>).
    /// </summary>
    /// <param name="e">The motion event.</param>
    /// <returns>True when consumed.</returns>
    internal static bool TryHandleMotion(AMotionEvent e)
    {
        if (!IsStarted || e == null || !IsGamepadSource(e.Source) || (e.Action & AMotionEventActions.Mask) != AMotionEventActions.Move)
        {
            return false;
        }

        try
        {
            if (!_tracker.TryGetGamepad(e.DeviceId, out var gamepad))
            {
                gamepad = AddDescribedDevice(e.DeviceId);
            }

            var profile = gamepad?.Info.AxisProfile ?? GamepadAxisProfile.Empty;
            Span<GamepadAxisValue> values = stackalloc GamepadAxisValue[_knownAxes.Length];
            var count = 0;
            foreach (var axis in _knownAxes)
            {
                // A profile names the axes the device has; without one (injected input) every known axis is read,
                // the absent ones as 0, which the tracker's preference order then resolves.
                if (profile.IsEmpty || profile.HasAxis(axis))
                {
                    values[count++] = new GamepadAxisValue(axis, e.GetAxisValue((AAxis)axis));
                }
            }

            return _tracker.HandleMotion(e.DeviceId, values.Slice(0, count));
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            _log.LogError(exception, "Failed to read a gamepad motion event.");
            return false;
        }
    }

    /// <summary>True for an event (or a device) whose source is a gamepad or a joystick.</summary>
    /// <param name="source">The input source flags.</param>
    internal static bool IsGamepadSource(AInputSourceType source) =>
        (source & AInputSourceType.Gamepad) == AInputSourceType.Gamepad || (source & AInputSourceType.Joystick) == AInputSourceType.Joystick;

    static partial void OnStarted()
    {
        try
        {
            foreach (var id in AInputDevice.GetDeviceIds() ?? Array.Empty<int>())
            {
                AddDescribedDevice(id);
            }

            if (_listener == null && global::Android.App.Application.Context?.GetSystemService(AContext.InputService) is AInputManager inputManager)
            {
                _inputManager = inputManager;
                _listener = new DeviceListener();
                inputManager.RegisterInputDeviceListener(_listener, new AHandler(ALooper.MainLooper));
            }

            if (_tracker.ConnectedGamepads.Count == 0)
            {
                _log.LogInformation("Gamepads started: no controller connected. Make sure it is switched on and paired - wireless controllers power down on their own after a period of inactivity.");
            }
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            _log.LogError(exception, "Failed to start the gamepads.");
        }
    }

    static partial void OnStopped()
    {
        try
        {
            if (_listener != null)
            {
                _inputManager?.UnregisterInputDeviceListener(_listener);
                _listener.Dispose();
                _listener = null;
                _inputManager = null;
            }
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            _log.LogError(exception, "Failed to stop the gamepads.");
        }
    }

    /// <summary>Adds the gamepad of a device id when Android knows a gamepad or joystick device by it; logs the connection.</summary>
    /// <returns>The gamepad, or null when the id is not a gamepad device (injected input, or a keyboard).</returns>
    private static Gamepad AddDescribedDevice(int deviceId)
    {
        if (_tracker.TryGetGamepad(deviceId, out var existing))
        {
            return existing;
        }

        if (!TryDescribe(deviceId, out var info))
        {
            return null;
        }

        var gamepad = _tracker.AddDevice(deviceId, info);
        _log.LogInformation("Gamepad connected (device {DeviceId}): {Description}", deviceId, info.Describe());
        return gamepad;
    }

    private static bool TryDescribe(int deviceId, out GamepadDeviceInfo info)
    {
        info = null;
        var device = AInputDevice.GetDevice(deviceId);
        if (device == null || device.IsVirtual || !IsGamepadSource(device.Sources))
        {
            return false;
        }

        var axes = new List<GamepadAxisRange>();
        foreach (var axis in _knownAxes)
        {
            var range = device.GetMotionRange((AAxis)axis, AInputSourceType.Joystick) ?? device.GetMotionRange((AAxis)axis);
            if (range != null)
            {
                axes.Add(new GamepadAxisRange(axis, range.Flat));
            }
        }

        info = new GamepadDeviceInfo(device.Name, GamepadOrigin.Device, GamepadAxisProfile.FromAxes(axes), device.VendorId, device.ProductId, device.Descriptor);
        return true;
    }

    private sealed class DeviceListener : global::Java.Lang.Object, AInputManager.IInputDeviceListener
    {
        public void OnInputDeviceAdded(int deviceId)
        {
            if (IsStarted)
            {
                AddDescribedDevice(deviceId);
            }
        }

        public void OnInputDeviceChanged(int deviceId)
        {
            // The axes of a controller do not change while it is connected; a change (a re-enumeration, a
            // configuration change) is noted but the gamepad is kept, so a game's reference stays valid.
            if (IsStarted && _tracker.TryGetGamepad(deviceId, out var gamepad))
            {
                _log.LogDebug("Gamepad device {DeviceId} changed: {Name}", deviceId, gamepad.Name);
            }
        }

        public void OnInputDeviceRemoved(int deviceId)
        {
            if (_tracker.TryGetGamepad(deviceId, out var gamepad) && _tracker.RemoveDevice(deviceId))
            {
                _log.LogInformation("Gamepad disconnected (device {DeviceId}): {Name}", deviceId, gamepad.Name);
            }
        }
    }
}
