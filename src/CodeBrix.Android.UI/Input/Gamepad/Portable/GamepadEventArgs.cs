using System;

namespace CodeBrix.Android.UI.Input;

/// <summary>The gamepad a <see cref="GamepadTracker"/> event is about.</summary>
public sealed class GamepadEventArgs : EventArgs
{
    /// <summary>Creates the arguments.</summary>
    /// <param name="gamepad">The gamepad.</param>
    public GamepadEventArgs(Gamepad gamepad)
    {
        ArgumentNullException.ThrowIfNull(gamepad);
        Gamepad = gamepad;
    }

    /// <summary>The gamepad the event is about.</summary>
    public Gamepad Gamepad { get; }
}
