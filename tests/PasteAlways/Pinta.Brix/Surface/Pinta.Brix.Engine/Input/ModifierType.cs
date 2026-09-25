// ModifierType.cs
//
// Pinta.Brix replacement for the GDK modifier flags the upstream Pinta code
// used, keeping the same flag names and bit layout so ported call sites and
// their extension-method helpers compile unchanged.

using System;

namespace Pinta.Brix.Engine;

[Flags]
public enum ModifierType : uint
{
	None = 0,
	ShiftMask = 1 << 0,
	LockMask = 1 << 1,
	ControlMask = 1 << 2,
	AltMask = 1 << 3,
	Button1Mask = 1 << 8,
	Button2Mask = 1 << 9,
	Button3Mask = 1 << 10,
	SuperMask = 1 << 26,
	MetaMask = 1 << 28,
}

public static class ModifierTypeExtensions
{
	public static bool IsShiftPressed (this ModifierType m)
		=> m.HasFlag (ModifierType.ShiftMask);

	/// <summary>
	/// Returns whether a Ctrl modifier is pressed (or the Cmd key on macOS).
	/// </summary>
	public static bool IsControlPressed (this ModifierType m)
	{
		if (OperatingSystem.IsMacOS ())
			return m.HasFlag (ModifierType.MetaMask);
		else
			return m.HasFlag (ModifierType.ControlMask);
	}

	public static bool IsAltPressed (this ModifierType m)
		=> m.HasFlag (ModifierType.AltMask);

	public static bool IsLeftMousePressed (this ModifierType m)
		=> m.HasFlag (ModifierType.Button1Mask);

	public static bool IsRightMousePressed (this ModifierType m)
		=> m.HasFlag (ModifierType.Button3Mask);

	/// <summary>
	/// Returns whether any of the Ctrl/Cmd/Shift/Alt modifiers are active.
	/// This prevents Caps Lock, Num Lock, etc from appearing as active modifier keys.
	/// </summary>
	public static bool HasModifierKey (this ModifierType current_state)
		=> current_state.IsControlPressed () || current_state.IsShiftPressed () || current_state.IsAltPressed ();
}
