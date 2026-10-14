using System;

namespace CodeBrix.Android.UI.Input;

/// <summary>One motion axis a gamepad reports, with the dead zone Android recommends for it.</summary>
public readonly struct GamepadAxisRange : IEquatable<GamepadAxisRange>
{
    /// <summary>Creates an axis description.</summary>
    /// <param name="axis">The axis (an <see cref="AndroidGamepadCodes"/> <c>Axis*</c> value).</param>
    /// <param name="flat">The extent around centre within which a reading is noise (Android's <c>flat</c>), 0 or more.</param>
    public GamepadAxisRange(int axis, float flat = 0f)
    {
        Axis = axis;
        Flat = float.IsFinite(flat) && flat > 0f ? flat : 0f;
    }

    /// <summary>The axis (an <see cref="AndroidGamepadCodes"/> <c>Axis*</c> value).</summary>
    public int Axis { get; }

    /// <summary>The extent around centre within which a reading is treated as centred.</summary>
    public float Flat { get; }

    /// <inheritdoc />
    public bool Equals(GamepadAxisRange other) => Axis == other.Axis && Flat == other.Flat;

    /// <inheritdoc />
    public override bool Equals(object obj) => obj is GamepadAxisRange other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Axis, Flat);

    /// <summary>True when both describe the same axis with the same dead zone.</summary>
    public static bool operator ==(GamepadAxisRange left, GamepadAxisRange right) => left.Equals(right);

    /// <summary>True when the descriptions differ.</summary>
    public static bool operator !=(GamepadAxisRange left, GamepadAxisRange right) => !left.Equals(right);
}
