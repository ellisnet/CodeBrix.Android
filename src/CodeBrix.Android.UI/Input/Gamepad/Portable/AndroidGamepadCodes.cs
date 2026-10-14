namespace CodeBrix.Android.UI.Input;

/// <summary>
/// The Android key codes and motion axes a gamepad reports, as plain integers, so that the gamepad state
/// machine (and a test, or a synthetic input feed) can be driven without the Android SDK.
/// </summary>
/// <remarks>The values are Android's <c>KeyEvent.KEYCODE_*</c> and <c>MotionEvent.AXIS_*</c> constants.</remarks>
public static class AndroidGamepadCodes
{
    /// <summary><c>KEYCODE_DPAD_UP</c>.</summary>
    public const int KeyDPadUp = 19;

    /// <summary><c>KEYCODE_DPAD_DOWN</c>.</summary>
    public const int KeyDPadDown = 20;

    /// <summary><c>KEYCODE_DPAD_LEFT</c>.</summary>
    public const int KeyDPadLeft = 21;

    /// <summary><c>KEYCODE_DPAD_RIGHT</c>.</summary>
    public const int KeyDPadRight = 22;

    /// <summary><c>KEYCODE_DPAD_CENTER</c> (some controllers report the A button this way).</summary>
    public const int KeyDPadCenter = 23;

    /// <summary><c>KEYCODE_BUTTON_A</c>.</summary>
    public const int KeyButtonA = 96;

    /// <summary><c>KEYCODE_BUTTON_B</c>.</summary>
    public const int KeyButtonB = 97;

    /// <summary><c>KEYCODE_BUTTON_C</c> (not part of the standard layout).</summary>
    public const int KeyButtonC = 98;

    /// <summary><c>KEYCODE_BUTTON_X</c>.</summary>
    public const int KeyButtonX = 99;

    /// <summary><c>KEYCODE_BUTTON_Y</c>.</summary>
    public const int KeyButtonY = 100;

    /// <summary><c>KEYCODE_BUTTON_Z</c> (not part of the standard layout).</summary>
    public const int KeyButtonZ = 101;

    /// <summary><c>KEYCODE_BUTTON_L1</c>, the left shoulder.</summary>
    public const int KeyButtonL1 = 102;

    /// <summary><c>KEYCODE_BUTTON_R1</c>, the right shoulder.</summary>
    public const int KeyButtonR1 = 103;

    /// <summary><c>KEYCODE_BUTTON_L2</c>, the left trigger as a digital button.</summary>
    public const int KeyButtonL2 = 104;

    /// <summary><c>KEYCODE_BUTTON_R2</c>, the right trigger as a digital button.</summary>
    public const int KeyButtonR2 = 105;

    /// <summary><c>KEYCODE_BUTTON_THUMBL</c>, the left stick pressed in.</summary>
    public const int KeyButtonThumbLeft = 106;

    /// <summary><c>KEYCODE_BUTTON_THUMBR</c>, the right stick pressed in.</summary>
    public const int KeyButtonThumbRight = 107;

    /// <summary><c>KEYCODE_BUTTON_START</c>.</summary>
    public const int KeyButtonStart = 108;

    /// <summary><c>KEYCODE_BUTTON_SELECT</c>, the Back / View button.</summary>
    public const int KeyButtonSelect = 109;

    /// <summary><c>KEYCODE_BUTTON_MODE</c>, the Guide / Home button.</summary>
    public const int KeyButtonMode = 110;

    /// <summary><c>AXIS_X</c>, the left stick's horizontal axis.</summary>
    public const int AxisX = 0;

    /// <summary><c>AXIS_Y</c>, the left stick's vertical axis (positive downwards on Android).</summary>
    public const int AxisY = 1;

    /// <summary><c>AXIS_Z</c>, the right stick's horizontal axis on most controllers.</summary>
    public const int AxisZ = 11;

    /// <summary><c>AXIS_RX</c>, the right stick's horizontal axis on some controllers.</summary>
    public const int AxisRX = 12;

    /// <summary><c>AXIS_RY</c>, the right stick's vertical axis on some controllers.</summary>
    public const int AxisRY = 13;

    /// <summary><c>AXIS_RZ</c>, the right stick's vertical axis on most controllers.</summary>
    public const int AxisRZ = 14;

    /// <summary><c>AXIS_HAT_X</c>, the directional pad as an axis (-1 left, 1 right).</summary>
    public const int AxisHatX = 15;

    /// <summary><c>AXIS_HAT_Y</c>, the directional pad as an axis (-1 up, 1 down).</summary>
    public const int AxisHatY = 16;

    /// <summary><c>AXIS_LTRIGGER</c>, the left trigger (0..1).</summary>
    public const int AxisLeftTrigger = 17;

    /// <summary><c>AXIS_RTRIGGER</c>, the right trigger (0..1).</summary>
    public const int AxisRightTrigger = 18;

    /// <summary><c>AXIS_GAS</c>, the right trigger on controllers that report it as an accelerator (0..1).</summary>
    public const int AxisGas = 22;

    /// <summary><c>AXIS_BRAKE</c>, the left trigger on controllers that report it as a brake (0..1).</summary>
    public const int AxisBrake = 23;
}
