// AcceleratorParser.cs
//
// Pinta.Brix note: the ported Command objects carry upstream's GTK
// accelerator strings verbatim ("<Primary><Shift>X", "KP_Add", "F11", ...).
// Upstream handed those to Gtk.Application.SetAccelsForAction; here they have
// to become KeyboardAccelerator values instead, so this translates between the
// two notations. Keeping upstream's strings on the commands means the shortcut
// data stays a straight port and only the consumption changes.

using System;
using System.Collections.Generic;
using Windows.System;

namespace Pinta.Brix.Controls;

/// <summary>One parsed accelerator: a key plus its modifiers.</summary>
/// <param name="Key">The key to press.</param>
/// <param name="Modifiers">The modifiers that must be held.</param>
public readonly record struct ParsedAccelerator (VirtualKey Key, VirtualKeyModifiers Modifiers);

/// <summary>
/// Translates GTK accelerator strings into key/modifier pairs.
/// </summary>
public static class AcceleratorParser
{
	// The OEM keys have no name in the VirtualKey enum, so they are spelled as
	// their Win32 virtual-key codes. 187 is the "=/+" key, 189 the "-/_" key,
	// 188 the comma and 190 the period.
	private const VirtualKey OemPlus = (VirtualKey) 187;
	private const VirtualKey OemMinus = (VirtualKey) 189;
	private const VirtualKey OemComma = (VirtualKey) 188;
	private const VirtualKey OemPeriod = (VirtualKey) 190;

	// GDK key names that are not simply the character or "F<n>". Upstream uses
	// the shifted and unshifted spellings interchangeably ("plus" and "equal"
	// both mean the "=" key), because GTK matched on the produced character.
	private static readonly Dictionary<string, VirtualKey> named_keys = new (StringComparer.OrdinalIgnoreCase) {
		["plus"] = OemPlus,
		["equal"] = OemPlus,
		["minus"] = OemMinus,
		["underscore"] = OemMinus,
		["comma"] = OemComma,
		["period"] = OemPeriod,
		["KP_Add"] = VirtualKey.Add,
		["KP_Subtract"] = VirtualKey.Subtract,
		["KP_Multiply"] = VirtualKey.Multiply,
		["KP_Divide"] = VirtualKey.Divide,
		["KP_Decimal"] = VirtualKey.Decimal,
		["BackSpace"] = VirtualKey.Back,
		["Delete"] = VirtualKey.Delete,
		["Insert"] = VirtualKey.Insert,
		["Return"] = VirtualKey.Enter,
		["Enter"] = VirtualKey.Enter,
		["Escape"] = VirtualKey.Escape,
		["Tab"] = VirtualKey.Tab,
		["space"] = VirtualKey.Space,
		["Home"] = VirtualKey.Home,
		["End"] = VirtualKey.End,
		["Page_Up"] = VirtualKey.PageUp,
		["Page_Down"] = VirtualKey.PageDown,
		["Left"] = VirtualKey.Left,
		["Right"] = VirtualKey.Right,
		["Up"] = VirtualKey.Up,
		["Down"] = VirtualKey.Down,
	};

	/// <summary>
	/// Parses one GTK accelerator string.
	/// </summary>
	/// <param name="accelerator">
	/// A string such as "&lt;Primary&gt;&lt;Shift&gt;X", "F11" or "KP_Add".
	/// </param>
	/// <param name="result">The parsed accelerator, when this returns true.</param>
	/// <returns>True when the string was understood.</returns>
	public static bool TryParse (string? accelerator, out ParsedAccelerator result)
	{
		result = default;

		if (string.IsNullOrWhiteSpace (accelerator))
			return false;

		VirtualKeyModifiers modifiers = VirtualKeyModifiers.None;
		int index = 0;

		while (index < accelerator.Length && accelerator[index] == '<') {

			int close = accelerator.IndexOf ('>', index);

			if (close < 0)
				return false; // An unterminated modifier is not an accelerator.

			string modifier = accelerator[(index + 1)..close];

			switch (modifier.ToLowerInvariant ()) {
				// "Primary" is Ctrl everywhere except macOS, where GTK maps it
				// to Command. The platform's Control modifier is the closest
				// equivalent on every head we ship.
				case "primary":
				case "ctrl":
				case "control":
					modifiers |= VirtualKeyModifiers.Control;
					break;
				case "shift":
					modifiers |= VirtualKeyModifiers.Shift;
					break;
				case "alt":
					modifiers |= VirtualKeyModifiers.Menu;
					break;
				case "meta":
				case "super":
					modifiers |= VirtualKeyModifiers.Windows;
					break;
				default:
					return false; // An unknown modifier - better to drop the accelerator than to guess.
			}

			index = close + 1;
		}

		string key = accelerator[index..];

		if (key.Length == 0)
			return false;

		if (!TryParseKey (key, out VirtualKey virtualKey))
			return false;

		result = new ParsedAccelerator (virtualKey, modifiers);
		return true;
	}

	/// <summary>
	/// Parses every accelerator in a command's shortcut list, dropping any that
	/// are not understood.
	/// </summary>
	/// <param name="accelerators">The accelerator strings.</param>
	/// <returns>The ones that parsed.</returns>
	public static IEnumerable<ParsedAccelerator> ParseAll (IEnumerable<string>? accelerators)
	{
		if (accelerators is null)
			yield break;

		foreach (string accelerator in accelerators)
			if (TryParse (accelerator, out ParsedAccelerator parsed))
				yield return parsed;
	}

	private static bool TryParseKey (string key, out VirtualKey virtualKey)
	{
		virtualKey = default;

		if (named_keys.TryGetValue (key, out virtualKey))
			return true;

		if (key.Length == 1) {

			char c = char.ToUpperInvariant (key[0]);

			if (c is >= 'A' and <= 'Z') {
				virtualKey = (VirtualKey) c;
				return true;
			}

			if (c is >= '0' and <= '9') {
				virtualKey = (VirtualKey) c;
				return true;
			}

			// A few single characters are punctuation on the OEM keys.
			switch (c) {
				case '+':
				case '=':
					virtualKey = OemPlus;
					return true;
				case '-':
				case '_':
					virtualKey = OemMinus;
					return true;
				case ',':
					virtualKey = OemComma;
					return true;
				case '.':
					virtualKey = OemPeriod;
					return true;
			}

			return false;
		}

		if ((key[0] is 'F' or 'f') && int.TryParse (key[1..], out int functionKey) && functionKey is >= 1 and <= 24) {
			virtualKey = VirtualKey.F1 + (functionKey - 1);
			return true;
		}

		return false;
	}
}
