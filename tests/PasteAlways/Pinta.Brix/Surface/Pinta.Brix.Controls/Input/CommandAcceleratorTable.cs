// CommandAcceleratorTable.cs
//
// Pinta.Brix note: XAML KeyboardAccelerators are declared on the menu items
// (so the shortcut is visible where the user looks for it) but they do NOT
// fire on the Skia heads - verified on X11 2026-07-20 by driving the running
// application: typing reaches a TextBox normally, while Ctrl+Z, Ctrl+Y and
// Ctrl+H registered on a Page or on a MenuFlyoutItem never invoke.
//
// So the shortcuts are dispatched here instead, from a single KeyDown handler
// on the page. This is close to what upstream did anyway - Pinta's MainWindow
// carried a HandleGlobalKeyPress for exactly the keys GTK would not route -
// and it keeps the shortcut data on the Command objects where the port put it.

using System;
using System.Collections.Generic;
using Windows.System;

namespace Pinta.Brix.Controls;

/// <summary>
/// Maps parsed accelerators onto commands, and tracks the modifier keys so a
/// plain key event can be matched against them.
/// </summary>
public sealed class CommandAcceleratorTable
{
	private readonly Dictionary<(VirtualKey Key, VirtualKeyModifiers Modifiers), Engine.Command> map = [];

	private bool control_down;
	private bool shift_down;
	private bool alt_down;
	private bool windows_down;

	/// <summary>The modifiers currently held.</summary>
	public VirtualKeyModifiers CurrentModifiers =>
		(control_down ? VirtualKeyModifiers.Control : VirtualKeyModifiers.None)
		| (shift_down ? VirtualKeyModifiers.Shift : VirtualKeyModifiers.None)
		| (alt_down ? VirtualKeyModifiers.Menu : VirtualKeyModifiers.None)
		| (windows_down ? VirtualKeyModifiers.Windows : VirtualKeyModifiers.None);

	/// <summary>
	/// Registers every accelerator a command declares.
	/// </summary>
	/// <param name="command">The command to register.</param>
	/// <exception cref="ArgumentNullException"><paramref name="command"/> is null.</exception>
	public void Register (Engine.Command command)
	{
		ArgumentNullException.ThrowIfNull (command);

		foreach (ParsedAccelerator parsed in AcceleratorParser.ParseAll (command.Shortcuts)) {

			// First registration wins. Upstream has genuine collisions - View's
			// ActualSize and Edit's Deselect both claim <Primary><Shift>A - and
			// silently keeping the first match is what GTK did too.
			map.TryAdd ((parsed.Key, parsed.Modifiers), command);
		}
	}

	/// <summary>
	/// Updates the tracked modifier state, and reports whether the key was
	/// itself a modifier.
	/// </summary>
	/// <param name="key">The key that changed.</param>
	/// <param name="down">True on key down, false on key up.</param>
	/// <returns>True when the key was a modifier and nothing else should happen.</returns>
	public bool TrackModifier (VirtualKey key, bool down)
	{
		switch (key) {
			case VirtualKey.Control:
			case VirtualKey.LeftControl:
			case VirtualKey.RightControl:
				control_down = down;
				return true;
			case VirtualKey.Shift:
			case VirtualKey.LeftShift:
			case VirtualKey.RightShift:
				shift_down = down;
				return true;
			case VirtualKey.Menu:
			case VirtualKey.LeftMenu:
			case VirtualKey.RightMenu:
				alt_down = down;
				return true;
			case VirtualKey.LeftWindows:
			case VirtualKey.RightWindows:
				windows_down = down;
				return true;
			default:
				return false;
		}
	}

	/// <summary>
	/// Clears the tracked modifiers. Call when the window loses focus, so a
	/// modifier released elsewhere does not stay stuck down.
	/// </summary>
	public void ResetModifiers ()
	{
		control_down = false;
		shift_down = false;
		alt_down = false;
		windows_down = false;
	}

	/// <summary>
	/// Finds and activates the command bound to a key plus the modifiers
	/// currently held.
	/// </summary>
	/// <param name="key">The key that was pressed.</param>
	/// <returns>True when a sensitive command was activated.</returns>
	public bool TryInvoke (VirtualKey key)
	{
		if (!map.TryGetValue ((key, CurrentModifiers), out Engine.Command? command))
			return false;

		// A disabled command must swallow nothing: the key should behave as if
		// the shortcut were not bound at all.
		if (!command.Sensitive)
			return false;

		command.Activate ();
		return true;
	}

	/// <summary>The number of registered accelerators.</summary>
	public int Count => map.Count;
}
