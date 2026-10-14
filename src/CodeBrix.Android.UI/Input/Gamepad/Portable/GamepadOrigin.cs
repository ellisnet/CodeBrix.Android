namespace CodeBrix.Android.UI.Input;

/// <summary>Where a <see cref="Gamepad"/> comes from.</summary>
public enum GamepadOrigin
{
    /// <summary>A physical (or system-provided) input device Android enumerates.</summary>
    Device = 0,

    /// <summary>
    /// Input injected through the system with no device behind it (for example <c>adb shell input gamepad keyevent</c>):
    /// the gamepad appears when its first event arrives and reports no axis profile.
    /// </summary>
    InjectedInput = 1,

    /// <summary>A <see cref="VirtualGamepad"/> an app or a test created and drives itself.</summary>
    Virtual = 2,
}
