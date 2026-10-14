namespace CodeBrix.Android.UI.Input;

/// <summary>One axis reading of a motion event: the axis and its value.</summary>
public readonly struct GamepadAxisValue
{
    /// <summary>Creates a reading.</summary>
    /// <param name="axis">The axis (an <see cref="AndroidGamepadCodes"/> <c>Axis*</c> value).</param>
    /// <param name="value">The value Android reported for it.</param>
    public GamepadAxisValue(int axis, float value)
    {
        Axis = axis;
        Value = value;
    }

    /// <summary>The axis (an <see cref="AndroidGamepadCodes"/> <c>Axis*</c> value).</summary>
    public int Axis { get; }

    /// <summary>The value Android reported for the axis.</summary>
    public float Value { get; }
}
