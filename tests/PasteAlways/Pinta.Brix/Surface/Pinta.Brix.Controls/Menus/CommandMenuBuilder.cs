// CommandMenuBuilder.cs
//
// Pinta.Brix note: upstream built its menus from Gio.Menu models attached to
// Gio.SimpleActions. The port keeps the same shape - menus are generated from
// the Command objects rather than hand-written in XAML - so a command declared
// once in Pinta.Brix.Engine.Actions gets its label, icon, enabled state and
// keyboard shortcut everywhere without further wiring.

using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml.Controls;
using Pinta.Brix.Engine;

namespace Pinta.Brix.Controls;

/// <summary>
/// Builds menu items from <see cref="Command"/> objects.
/// </summary>
public static class CommandMenuBuilder
{
	/// <summary>The icon size menu items are rendered at.</summary>
	public const int MenuIconSize = 16;

	/// <summary>
	/// Creates a menu item for a command, wiring its label, icon, enabled state
	/// and keyboard accelerators.
	/// </summary>
	/// <param name="command">The command the item represents.</param>
	/// <param name="showIcon">Whether to render the command's icon.</param>
	/// <returns>The menu item.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="command"/> is null.</exception>
	public static MenuFlyoutItemBase Create (Command command, bool showIcon = true)
	{
		ArgumentNullException.ThrowIfNull (command);

		if (command is ToggleCommand toggle)
			return CreateToggle (toggle);

		MenuFlyoutItem item = new () {
			Text = command.Label,
			IsEnabled = command.Sensitive,
		};

		ApplyIcon (item, command, showIcon);
		ApplyAcceleratorText (item, command);

		item.Click += (_, _) => command.Activate ();
		command.SensitiveChanged += (_, _) => item.IsEnabled = command.Sensitive;

		return item;
	}

	/// <summary>
	/// Creates a separator.
	/// </summary>
	/// <returns>The separator.</returns>
	public static MenuFlyoutSeparator CreateSeparator () => new ();

	/// <summary>
	/// Creates a submenu holding the given commands.
	/// </summary>
	/// <param name="text">The submenu's label.</param>
	/// <param name="commands">The commands it contains.</param>
	/// <returns>The submenu.</returns>
	public static MenuFlyoutSubItem CreateSubmenu (string text, IEnumerable<Command> commands)
	{
		ArgumentNullException.ThrowIfNull (commands);

		MenuFlyoutSubItem submenu = new () { Text = text };

		foreach (Command command in commands)
			submenu.Items.Add (Create (command));

		return submenu;
	}

	private static ToggleMenuFlyoutItem CreateToggle (ToggleCommand command)
	{
		ToggleMenuFlyoutItem item = new () {
			Text = command.Label,
			IsEnabled = command.Sensitive,
			IsChecked = command.Value,
		};

		ApplyAcceleratorText (item, command);

		// The Command raises Toggled for both interactive and programmatic
		// changes, so guard against the echo rather than unhooking.
		bool updating = false;

		item.Click += (_, _) => {
			if (updating)
				return;
			updating = true;
			command.Value = item.IsChecked;
			updating = false;
		};

		command.Toggled += (value, _) => {
			if (updating)
				return;
			updating = true;
			item.IsChecked = value;
			updating = false;
		};

		command.SensitiveChanged += (_, _) => item.IsEnabled = command.Sensitive;

		return item;
	}

	private static void ApplyIcon (MenuFlyoutItem item, Command command, bool showIcon)
	{
		if (!showIcon || string.IsNullOrEmpty (command.IconName))
			return;

		// A missing icon must not take the menu down with it - the resource
		// service falls back to a placeholder, but an unknown name can still
		// come back null.
		var source = IconImageSource.Create (command.IconName, MenuIconSize);

		if (source is not null)
			item.Icon = new ImageIcon { Source = source };
	}

	private static void ApplyAcceleratorText (MenuFlyoutItemBase item, Command command)
	{
		string? shortcut = null;

		foreach (string candidate in command.Shortcuts) {
			// Only advertise a shortcut we can actually dispatch.
			if (AcceleratorParser.TryParse (candidate, out _)) {
				shortcut = candidate;
				break;
			}
		}

		if (shortcut is null)
			return;

		string text = FormatAccelerator (shortcut);

		// Deliberately the TEXT and not a real KeyboardAccelerator: XAML
		// accelerators do not invoke on the Skia heads, and adding one anyway
		// would mean a second dispatch path the day they start working.
		// CommandAcceleratorTable does the actual dispatching.
		// ToggleMenuFlyoutItem derives from MenuFlyoutItem, so this covers both.
		if (item is MenuFlyoutItem menuItem)
			menuItem.KeyboardAcceleratorTextOverride = text;
	}

	/// <summary>
	/// Renders a GTK accelerator string the way a menu should show it.
	/// </summary>
	/// <param name="accelerator">The accelerator string.</param>
	/// <returns>The display text, for example "Ctrl+Shift+X".</returns>
	public static string FormatAccelerator (string accelerator)
	{
		if (string.IsNullOrEmpty (accelerator))
			return string.Empty;

		System.Text.StringBuilder builder = new ();
		int index = 0;

		while (index < accelerator.Length && accelerator[index] == '<') {

			int close = accelerator.IndexOf ('>', index);

			if (close < 0)
				break;

			string modifier = accelerator[(index + 1)..close].ToLowerInvariant ();

			builder.Append (modifier switch {
				"primary" or "ctrl" or "control" => "Ctrl+",
				"shift" => "Shift+",
				"alt" => "Alt+",
				"meta" or "super" => "Super+",
				_ => string.Empty,
			});

			index = close + 1;
		}

		string key = accelerator[index..];

		builder.Append (key switch {
			"plus" => "+",
			"equal" => "=",
			"minus" => "-",
			"underscore" => "_",
			"comma" => ",",
			"period" => ".",
			"KP_Add" => "Num +",
			"KP_Subtract" => "Num -",
			"BackSpace" => "Backspace",
			"Page_Up" => "Page Up",
			"Page_Down" => "Page Down",
			_ => key.Length == 1 ? key.ToUpperInvariant () : key,
		});

		return builder.ToString ();
	}
}
