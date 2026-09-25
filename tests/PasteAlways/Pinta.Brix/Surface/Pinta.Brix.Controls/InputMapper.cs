// InputMapper.cs
//
// Maps CodeBrix.Platform input event data onto the engine's key and modifier
// types (X11-keysym-valued Key structs, GDK-style modifier flags).

using Microsoft.UI.Xaml.Input;
using Pinta.Brix.Engine;
using Windows.System;
using PointerPointProperties = Microsoft.UI.Input.PointerPointProperties;

namespace Pinta.Brix.Controls;

public static class InputMapper
{
	public static ModifierType ToModifierType (VirtualKeyModifiers modifiers, PointerPointProperties? props = null)
	{
		ModifierType state = ModifierType.None;
		if (modifiers.HasFlag (VirtualKeyModifiers.Shift))
			state |= ModifierType.ShiftMask;
		if (modifiers.HasFlag (VirtualKeyModifiers.Control))
			state |= ModifierType.ControlMask;
		if (modifiers.HasFlag (VirtualKeyModifiers.Menu))
			state |= ModifierType.AltMask;
		if (modifiers.HasFlag (VirtualKeyModifiers.Windows))
			state |= ModifierType.SuperMask;

		if (props is not null) {
			if (props.IsLeftButtonPressed)
				state |= ModifierType.Button1Mask;
			if (props.IsMiddleButtonPressed)
				state |= ModifierType.Button2Mask;
			if (props.IsRightButtonPressed)
				state |= ModifierType.Button3Mask;
		}

		return state;
	}

	//Modifier state tracked from the modifier keys' own down/up events. The
	//CoreWindow.GetKeyState probe below returns nothing on the Skia heads
	//(Window.Current is null there - the same platform gap that keeps XAML
	//KeyboardAccelerators from firing), so the canvas reports every key
	//transition here and this is the state that counts.
	private static ModifierType tracked_modifiers;

	/// <summary>Records a modifier key transition; called by the canvas for every key event.</summary>
	public static void NoteKey (VirtualKey key, bool down)
	{
		ModifierType mask = key switch {
			VirtualKey.Shift or VirtualKey.LeftShift or VirtualKey.RightShift => ModifierType.ShiftMask,
			VirtualKey.Control or VirtualKey.LeftControl or VirtualKey.RightControl => ModifierType.ControlMask,
			VirtualKey.Menu or VirtualKey.LeftMenu or VirtualKey.RightMenu => ModifierType.AltMask,
			_ => ModifierType.None,
		};

		if (mask == ModifierType.None)
			return;

		if (down)
			tracked_modifiers |= mask;
		else
			tracked_modifiers &= ~mask;
	}

	public static ToolKeyEventArgs ToKeyArgs (KeyRoutedEventArgs e)
		=> new () {
			Key = new Key (ToKeysym (e.Key)),
			State = CurrentModifiers (),
		};

	private static ModifierType CurrentModifiers ()
	{
		ModifierType state = tracked_modifiers;
		if (IsDown (VirtualKey.Shift))
			state |= ModifierType.ShiftMask;
		if (IsDown (VirtualKey.Control))
			state |= ModifierType.ControlMask;
		if (IsDown (VirtualKey.Menu))
			state |= ModifierType.AltMask;
		return state;
	}

	private static bool IsDown (VirtualKey key)
	{
		var window = Microsoft.UI.Xaml.Window.Current;
		if (window is null)
			return false;
		var keyState = window.CoreWindow?.GetKeyState (key);
		return keyState.HasValue && keyState.Value.HasFlag (Windows.UI.Core.CoreVirtualKeyStates.Down);
	}

	public static uint ToKeysym (VirtualKey key)
	{
		// Letters/digits map onto ASCII keysyms.
		if (key >= VirtualKey.A && key <= VirtualKey.Z)
			return (uint) ('a' + (key - VirtualKey.A));
		if (key >= VirtualKey.Number0 && key <= VirtualKey.Number9)
			return (uint) ('0' + (key - VirtualKey.Number0));

		return key switch {
			VirtualKey.Escape => KeyConstants.KEY_Escape,
			VirtualKey.Enter => KeyConstants.KEY_Return,
			VirtualKey.Tab => KeyConstants.KEY_Tab,
			VirtualKey.Back => KeyConstants.KEY_BackSpace,
			VirtualKey.Delete => KeyConstants.KEY_Delete,
			VirtualKey.Insert => KeyConstants.KEY_Insert,
			VirtualKey.Home => KeyConstants.KEY_Home,
			VirtualKey.End => KeyConstants.KEY_End,
			VirtualKey.PageUp => KeyConstants.KEY_Prior,
			VirtualKey.PageDown => KeyConstants.KEY_Next,
			VirtualKey.Left => KeyConstants.KEY_Left,
			VirtualKey.Up => KeyConstants.KEY_Up,
			VirtualKey.Right => KeyConstants.KEY_Right,
			VirtualKey.Down => KeyConstants.KEY_Down,
			VirtualKey.Space => KeyConstants.KEY_space,
			VirtualKey.Shift => KeyConstants.KEY_Shift_L,
			VirtualKey.Control => KeyConstants.KEY_Control_L,
			VirtualKey.Menu => KeyConstants.KEY_Alt_L,
			(VirtualKey) 219 => KeyConstants.KEY_bracketleft,  // OEM 4
			(VirtualKey) 221 => KeyConstants.KEY_bracketright, // OEM 6
			// The remaining OEM punctuation keys map onto their unshifted
			// ASCII keysyms; the text tool derives shifted characters from
			// these plus the shift state.
			(VirtualKey) 186 => (uint) ';',  // OEM 1
			(VirtualKey) 187 => (uint) '=',  // OEM plus
			(VirtualKey) 188 => (uint) ',',  // OEM comma
			(VirtualKey) 189 => (uint) '-',  // OEM minus
			(VirtualKey) 190 => (uint) '.',  // OEM period
			(VirtualKey) 191 => (uint) '/',  // OEM 2
			(VirtualKey) 192 => (uint) '`',  // OEM 3
			(VirtualKey) 220 => (uint) '\\', // OEM 5
			(VirtualKey) 222 => (uint) '\'', // OEM 7
			_ => KeyConstants.KEY_VoidSymbol,
		};
	}
}
