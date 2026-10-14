using System.Collections.Generic;

namespace CodeBrix.Android.UI.Input;

/// <summary>
/// The names of the buttons of the standard gamepad layout, as reported in <see cref="Gamepad.PressedButtons"/>.
/// </summary>
/// <remarks>
/// The names and their order are the ones a CodeBrix game already binds against on the desktop heads, so a
/// binding written for a desktop controller reads unchanged on Android. Every name is compared ordinally.
/// </remarks>
public static class GamepadButtons
{
    /// <summary>The bottom face button (A on an Xbox-style layout, Cross on a PlayStation-style one).</summary>
    public const string A = "A";

    /// <summary>The right face button (B / Circle).</summary>
    public const string B = "B";

    /// <summary>The left face button (X / Square).</summary>
    public const string X = "X";

    /// <summary>The top face button (Y / Triangle).</summary>
    public const string Y = "Y";

    /// <summary>The Back / View / Select button.</summary>
    public const string Back = "Back";

    /// <summary>The Guide / Home / Xbox button. Android reserves it on most devices, so it rarely reaches an app.</summary>
    public const string Guide = "Guide";

    /// <summary>The Start / Menu / Options button.</summary>
    public const string Start = "Start";

    /// <summary>The left stick pressed in.</summary>
    public const string LeftStick = "LeftStick";

    /// <summary>The right stick pressed in.</summary>
    public const string RightStick = "RightStick";

    /// <summary>The left shoulder (bumper) button.</summary>
    public const string LeftShoulder = "LeftShoulder";

    /// <summary>The right shoulder (bumper) button.</summary>
    public const string RightShoulder = "RightShoulder";

    /// <summary>The directional pad, up.</summary>
    public const string DPadUp = "DPadUp";

    /// <summary>The directional pad, down.</summary>
    public const string DPadDown = "DPadDown";

    /// <summary>The directional pad, left.</summary>
    public const string DPadLeft = "DPadLeft";

    /// <summary>The directional pad, right.</summary>
    public const string DPadRight = "DPadRight";

    /// <summary>Every button name, in the standard layout's order.</summary>
    public static IReadOnlyList<string> All { get; } = new[]
    {
        A, B, X, Y,
        Back, Guide, Start,
        LeftStick, RightStick,
        LeftShoulder, RightShoulder,
        DPadUp, DPadDown, DPadLeft, DPadRight,
    };
}
