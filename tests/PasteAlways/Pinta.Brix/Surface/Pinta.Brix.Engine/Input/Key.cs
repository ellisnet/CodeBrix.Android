// Key.cs
//
// Pinta.Brix replacement for the GDK key wrapper the upstream Pinta code used.
// Key values are X11 keysym values (the same values GDK uses), so ported
// comparisons keep their upstream semantics; platform heads map their own
// virtual-key events onto these values.

using System;

namespace Pinta.Brix.Engine;

/// <summary>
/// Wrapper for keyboard key values, e.g. <see cref="KeyConstants.KEY_Delete"/>.
/// </summary>
public readonly record struct Key (uint Value)
{
	public static Key Invalid { get; } = new (KeyConstants.KEY_VoidSymbol);

	/// <summary>
	/// Returns the name of the key value, e.g. 'A' for 'KEY_A'
	/// </summary>
	public string Name ()
		=> KeyConstants.NameOf (Value);

	public Key ToUpper ()
		=> Value is >= 'a' and <= 'z' ? new (Value - 0x20) : this;

	/// <summary>
	/// Returns whether this key is a Ctrl key (or the Cmd key on macOS).
	/// </summary>
	public bool IsControlKey ()
	{
		if (OperatingSystem.IsMacOS ())
			return Value == KeyConstants.KEY_Meta_L || Value == KeyConstants.KEY_Meta_R;
		else
			return Value == KeyConstants.KEY_Control_L || Value == KeyConstants.KEY_Control_R;
	}
}

/// <summary>
/// X11 keysym values for the keys the application uses, mirroring the
/// constant names the upstream code compared against.
/// </summary>
public static class KeyConstants
{
	public const uint KEY_VoidSymbol = 0xFFFFFF;

	public const uint KEY_BackSpace = 0xFF08;
	public const uint KEY_Tab = 0xFF09;
	public const uint KEY_Return = 0xFF0D;
	public const uint KEY_Escape = 0xFF1B;
	public const uint KEY_Delete = 0xFFFF;
	public const uint KEY_Insert = 0xFF63;

	public const uint KEY_Home = 0xFF50;
	public const uint KEY_Left = 0xFF51;
	public const uint KEY_Up = 0xFF52;
	public const uint KEY_Right = 0xFF53;
	public const uint KEY_Down = 0xFF54;
	public const uint KEY_Prior = 0xFF55; // Page Up
	public const uint KEY_Next = 0xFF56;  // Page Down
	public const uint KEY_End = 0xFF57;

	public const uint KEY_KP_Enter = 0xFF8D;

	public const uint KEY_Shift_L = 0xFFE1;
	public const uint KEY_Shift_R = 0xFFE2;
	public const uint KEY_Control_L = 0xFFE3;
	public const uint KEY_Control_R = 0xFFE4;
	public const uint KEY_Meta_L = 0xFFE7;
	public const uint KEY_Meta_R = 0xFFE8;
	public const uint KEY_Alt_L = 0xFFE9;
	public const uint KEY_Alt_R = 0xFFEA;

	public const uint KEY_space = 0x20;
	public const uint KEY_bracketleft = 0x5B;
	public const uint KEY_bracketright = 0x5D;

	// Latin letters: uppercase 0x41..0x5A, lowercase 0x61..0x7A (ASCII).
	public const uint KEY_A = 'A'; public const uint KEY_a = 'a';
	public const uint KEY_B = 'B'; public const uint KEY_b = 'b';
	public const uint KEY_C = 'C'; public const uint KEY_c = 'c';
	public const uint KEY_D = 'D'; public const uint KEY_d = 'd';
	public const uint KEY_E = 'E'; public const uint KEY_e = 'e';
	public const uint KEY_F = 'F'; public const uint KEY_f = 'f';
	public const uint KEY_G = 'G'; public const uint KEY_g = 'g';
	public const uint KEY_H = 'H'; public const uint KEY_h = 'h';
	public const uint KEY_I = 'I'; public const uint KEY_i = 'i';
	public const uint KEY_J = 'J'; public const uint KEY_j = 'j';
	public const uint KEY_K = 'K'; public const uint KEY_k = 'k';
	public const uint KEY_L = 'L'; public const uint KEY_l = 'l';
	public const uint KEY_M = 'M'; public const uint KEY_m = 'm';
	public const uint KEY_N = 'N'; public const uint KEY_n = 'n';
	public const uint KEY_O = 'O'; public const uint KEY_o = 'o';
	public const uint KEY_P = 'P'; public const uint KEY_p = 'p';
	public const uint KEY_Q = 'Q'; public const uint KEY_q = 'q';
	public const uint KEY_R = 'R'; public const uint KEY_r = 'r';
	public const uint KEY_S = 'S'; public const uint KEY_s = 's';
	public const uint KEY_T = 'T'; public const uint KEY_t = 't';
	public const uint KEY_U = 'U'; public const uint KEY_u = 'u';
	public const uint KEY_V = 'V'; public const uint KEY_v = 'v';
	public const uint KEY_W = 'W'; public const uint KEY_w = 'w';
	public const uint KEY_X = 'X'; public const uint KEY_x = 'x';
	public const uint KEY_Y = 'Y'; public const uint KEY_y = 'y';
	public const uint KEY_Z = 'Z'; public const uint KEY_z = 'z';

	public const uint KEY_0 = '0';
	public const uint KEY_1 = '1';
	public const uint KEY_2 = '2';
	public const uint KEY_3 = '3';
	public const uint KEY_4 = '4';
	public const uint KEY_5 = '5';
	public const uint KEY_6 = '6';
	public const uint KEY_7 = '7';
	public const uint KEY_8 = '8';
	public const uint KEY_9 = '9';

	public static string NameOf (uint value) => value switch {
		>= 0x20 and <= 0x7E => ((char) value).ToString (),
		KEY_BackSpace => "BackSpace",
		KEY_Tab => "Tab",
		KEY_Return => "Return",
		KEY_Escape => "Escape",
		KEY_Delete => "Delete",
		KEY_Insert => "Insert",
		KEY_Home => "Home",
		KEY_Left => "Left",
		KEY_Up => "Up",
		KEY_Right => "Right",
		KEY_Down => "Down",
		KEY_Prior => "Prior",
		KEY_Next => "Next",
		KEY_End => "End",
		KEY_KP_Enter => "KP_Enter",
		KEY_Shift_L => "Shift_L",
		KEY_Shift_R => "Shift_R",
		KEY_Control_L => "Control_L",
		KEY_Control_R => "Control_R",
		KEY_Meta_L => "Meta_L",
		KEY_Meta_R => "Meta_R",
		KEY_Alt_L => "Alt_L",
		KEY_Alt_R => "Alt_R",
		_ => string.Empty,
	};
}
