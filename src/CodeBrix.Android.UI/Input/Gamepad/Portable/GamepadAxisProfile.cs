using System;
using System.Collections.Generic;
using System.Text;

namespace CodeBrix.Android.UI.Input;

/// <summary>
/// Which motion axes a gamepad reports, and the dead zone of each: decides how its sticks and
/// triggers are read, and is what the connection log line prints so a controller's layout (and
/// so its firmware generation) can be recognised.
/// </summary>
/// <remarks>
/// Controllers differ in where they put the right stick (<c>Z</c>/<c>RZ</c> on most, <c>RX</c>/<c>RY</c>
/// on some) and the triggers (<c>LTRIGGER</c>/<c>RTRIGGER</c>, or <c>BRAKE</c>/<c>GAS</c>). The profile
/// prefers the first pair of each when the device reports both.
/// </remarks>
public sealed class GamepadAxisProfile
{
    private readonly Dictionary<int, float> _flat = new();

    private GamepadAxisProfile()
    {
    }

    /// <summary>A profile with no axes: the sticks and triggers are read from whatever axes an event carries.</summary>
    public static GamepadAxisProfile Empty { get; } = new GamepadAxisProfile();

    /// <summary>Builds a profile from the axes a device reports.</summary>
    /// <param name="axes">The axes and their dead zones; a repeated axis keeps the last dead zone given.</param>
    public static GamepadAxisProfile FromAxes(IEnumerable<GamepadAxisRange> axes)
    {
        ArgumentNullException.ThrowIfNull(axes);
        var profile = new GamepadAxisProfile();
        foreach (var axis in axes)
        {
            profile._flat[axis.Axis] = axis.Flat;
        }

        return profile;
    }

    /// <summary>The axes in the profile.</summary>
    public IReadOnlyCollection<int> Axes => _flat.Keys;

    /// <summary>True when the profile lists no axes.</summary>
    public bool IsEmpty => _flat.Count == 0;

    /// <summary>True when the device reports <paramref name="axis"/>.</summary>
    /// <param name="axis">The axis (an <see cref="AndroidGamepadCodes"/> <c>Axis*</c> value).</param>
    public bool HasAxis(int axis) => _flat.ContainsKey(axis);

    /// <summary>The dead zone of <paramref name="axis"/>, 0 when the axis is unknown.</summary>
    /// <param name="axis">The axis (an <see cref="AndroidGamepadCodes"/> <c>Axis*</c> value).</param>
    public float FlatOf(int axis) => _flat.TryGetValue(axis, out var flat) ? flat : 0f;

    /// <summary>True when the device reports the left stick (<c>X</c> and <c>Y</c>).</summary>
    public bool HasLeftStick => HasAxis(AndroidGamepadCodes.AxisX) && HasAxis(AndroidGamepadCodes.AxisY);

    /// <summary>True when the right stick is read from <c>Z</c> and <c>RZ</c>.</summary>
    public bool RightStickUsesZ => HasAxis(AndroidGamepadCodes.AxisZ) && HasAxis(AndroidGamepadCodes.AxisRZ);

    /// <summary>True when the right stick is read from <c>RX</c> and <c>RY</c> (no <c>Z</c>/<c>RZ</c> pair).</summary>
    public bool RightStickUsesRxRy => !RightStickUsesZ && HasAxis(AndroidGamepadCodes.AxisRX) && HasAxis(AndroidGamepadCodes.AxisRY);

    /// <summary>True when the device reports a right stick.</summary>
    public bool HasRightStick => RightStickUsesZ || RightStickUsesRxRy;

    /// <summary>True when the triggers are read from <c>LTRIGGER</c> and <c>RTRIGGER</c>.</summary>
    public bool TriggersUseTriggerAxes => HasAxis(AndroidGamepadCodes.AxisLeftTrigger) && HasAxis(AndroidGamepadCodes.AxisRightTrigger);

    /// <summary>True when the triggers are read from <c>BRAKE</c> and <c>GAS</c> (no <c>LTRIGGER</c>/<c>RTRIGGER</c> pair).</summary>
    public bool TriggersUseGasBrake => !TriggersUseTriggerAxes && HasAxis(AndroidGamepadCodes.AxisBrake) && HasAxis(AndroidGamepadCodes.AxisGas);

    /// <summary>True when the device reports analogue triggers.</summary>
    public bool HasTriggerAxes => TriggersUseTriggerAxes || TriggersUseGasBrake;

    /// <summary>True when the directional pad is reported as the hat axes.</summary>
    public bool HasHat => HasAxis(AndroidGamepadCodes.AxisHatX) && HasAxis(AndroidGamepadCodes.AxisHatY);

    /// <summary>A one-line description of the layout, as the connection log prints it.</summary>
    public string Describe()
    {
        if (IsEmpty)
        {
            return "no axes reported";
        }

        var text = new StringBuilder();
        Append(text, "left stick", HasLeftStick ? "X/Y" : "none");
        Append(text, "right stick", RightStickUsesZ ? "Z/RZ" : RightStickUsesRxRy ? "RX/RY" : "none");
        Append(text, "triggers", TriggersUseTriggerAxes ? "LTRIGGER/RTRIGGER" : TriggersUseGasBrake ? "BRAKE/GAS" : "none");
        Append(text, "hat", HasHat ? "yes" : "no");
        text.Append("; axes ");
        var first = true;
        foreach (var pair in _flat)
        {
            if (!first)
            {
                text.Append(", ");
            }

            first = false;
            text.Append(pair.Key);
            if (pair.Value > 0f)
            {
                text.Append(" (flat ").Append(pair.Value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)).Append(')');
            }
        }

        return text.ToString();
    }

    private static void Append(StringBuilder text, string label, string value)
    {
        if (text.Length > 0)
        {
            text.Append("; ");
        }

        text.Append(label).Append(' ').Append(value);
    }
}
