using System;

namespace CodeBrix.Android.UI.Input;

/// <summary>The deflection of a thumb stick: both axes in -1..1, with Y positive UPWARDS.</summary>
/// <remarks>
/// Android reports a stick's Y axis positive downwards; this type already has it flipped, so that
/// a stick pushed away from the player reads positive, as the desktop controller backends report it.
/// </remarks>
public readonly struct GamepadStick : IEquatable<GamepadStick>
{
    /// <summary>A centred stick.</summary>
    public static GamepadStick Centered => default;

    /// <summary>Creates a stick state.</summary>
    /// <param name="x">The horizontal deflection, -1 (left) to 1 (right).</param>
    /// <param name="y">The vertical deflection, -1 (down) to 1 (up).</param>
    public GamepadStick(float x, float y)
    {
        // A flipped Android axis at rest is -0: normalised so a centred stick prints and compares as 0.
        X = Normalize(x);
        Y = Normalize(y);
    }

    private static float Normalize(float value) => float.IsFinite(value) && value != 0f ? Math.Clamp(value, -1f, 1f) : 0f;

    /// <summary>The horizontal deflection, -1 (left) to 1 (right).</summary>
    public float X { get; }

    /// <summary>The vertical deflection, -1 (down) to 1 (up).</summary>
    public float Y { get; }

    /// <summary>The distance from centre, 0 to about 1.41 (a diagonal corner).</summary>
    public float Magnitude => MathF.Sqrt(X * X + Y * Y);

    /// <summary>True when the stick is deflected at least <paramref name="threshold"/> from centre.</summary>
    /// <param name="threshold">The magnitude at which the stick counts as moved.</param>
    public bool IsEngaged(float threshold = 0.15f) => Magnitude >= threshold;

    /// <inheritdoc />
    public bool Equals(GamepadStick other) => X == other.X && Y == other.Y;

    /// <inheritdoc />
    public override bool Equals(object obj) => obj is GamepadStick other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(X, Y);

    /// <summary>True when both sticks read the same.</summary>
    public static bool operator ==(GamepadStick left, GamepadStick right) => left.Equals(right);

    /// <summary>True when the sticks read differently.</summary>
    public static bool operator !=(GamepadStick left, GamepadStick right) => !left.Equals(right);

    /// <inheritdoc />
    public override string ToString() => $"({X:0.00}, {Y:0.00})";
}
