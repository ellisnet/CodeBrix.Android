using System;
using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace CodeBrix.Platform.UI.Core.UIReqs.Support;

/// <summary>
/// The buttons and toggles a Buttons feature file may ask for, and the properties those
/// scenarios set. Registration goes through <see cref="ElementFactory.RegisterKind"/> and
/// <see cref="ElementFactory.RegisterProperty"/> rather than through the factory's partial
/// hooks, so that two coverage groups can add their controls at the same time without either
/// one owning the single implementation of a partial method.
/// </summary>
public static class ButtonElements
{
	private static bool _registered;

	/// <summary>
	/// Adds the Buttons controls and properties to the element factory. Calling it twice is
	/// harmless; the button steps call it once, before the first scenario.
	/// </summary>
	public static void Register()
	{
		if (_registered)
		{
			return;
		}

		_registered = true;

		ElementFactory.RegisterKind("ToggleButton", () => new ToggleButton());
		ElementFactory.RegisterKind("CheckBox", () => new CheckBox());
		ElementFactory.RegisterKind("RadioButton", () => new RadioButton());
		ElementFactory.RegisterKind("HyperlinkButton", () => new HyperlinkButton());
		ElementFactory.RegisterKind("RepeatButton", () => new RepeatButton());
		ElementFactory.RegisterKind("DropDownButton", () => new DropDownButton());
		ElementFactory.RegisterKind("SplitButton", BuildSplitButton);
		ElementFactory.RegisterKind("ToggleSwitch", BuildToggleSwitch);

		ElementFactory.RegisterProperty("IsChecked", (element, value) => SetIsChecked(element, value));
		ElementFactory.RegisterProperty("IsThreeState", (element, value) =>
			Toggle(element, "IsThreeState").IsThreeState = ReadBoolean(value));
		ElementFactory.RegisterProperty("GroupName", (element, value) =>
			Radio(element, "GroupName").GroupName = value);
		ElementFactory.RegisterProperty("IsOn", (element, value) =>
			Switch(element, "IsOn").IsOn = ReadBoolean(value));
		ElementFactory.RegisterProperty("OnContent", (element, value) =>
			Switch(element, "OnContent").OnContent = value);
		ElementFactory.RegisterProperty("OffContent", (element, value) =>
			Switch(element, "OffContent").OffContent = value);
		ElementFactory.RegisterProperty("Delay", (element, value) =>
			Repeat(element, "Delay").Delay = ReadMilliseconds(value));
		ElementFactory.RegisterProperty("Interval", (element, value) =>
			Repeat(element, "Interval").Interval = ReadMilliseconds(value));

		ElementFactory.RegisterProperty("Command", (element, value) => SetCommand(element, value));
		ElementFactory.RegisterProperty("CommandParameter", (element, value) => SetCommandParameter(element, value));
	}

	/// <summary>
	/// Binds an element to the scenario's named command, naming the command on first mention.
	/// Call this on the UI thread.
	/// </summary>
	/// <param name="element">The element to bind.</param>
	/// <param name="commandName">The Gherkin name of the command.</param>
	public static void SetCommand(FrameworkElement element, string commandName)
	{
		ArgumentNullException.ThrowIfNull(element);

		var command = TestCommands.Declare(commandName);
		switch (element)
		{
			case ButtonBase button:
				button.Command = command;
				break;
			case SplitButton split:
				split.Command = command;
				break;
			default:
				throw Unsupported(element, "Command");
		}
	}

	private static void SetCommandParameter(FrameworkElement element, string value)
	{
		ArgumentNullException.ThrowIfNull(element);

		switch (element)
		{
			case ButtonBase button:
				button.CommandParameter = value;
				break;
			case SplitButton split:
				split.CommandParameter = value;
				break;
			default:
				throw Unsupported(element, "CommandParameter");
		}
	}

	private static FrameworkElement BuildSplitButton()
	{
		var button = new SplitButton();

		// The name is read when the event fires rather than when the control is built,
		// because the element factory gives an element its name only after building it.
		button.Click += (sender, _) => EventRecorder.Record(sender.Name, "Click");
		return button;
	}

	private static FrameworkElement BuildToggleSwitch()
	{
		var toggle = new ToggleSwitch();
		toggle.Toggled += (sender, _) =>
			EventRecorder.Record(((FrameworkElement) sender).Name, "Toggled");
		return toggle;
	}

	private static void SetIsChecked(FrameworkElement element, string value)
	{
		var toggle = Toggle(element, "IsChecked");
		toggle.IsChecked = value.Trim() switch
		{
			var text when string.Equals(text, "Indeterminate", StringComparison.OrdinalIgnoreCase) => null,
			var text when string.Equals(text, "null", StringComparison.OrdinalIgnoreCase) => null,
			var text => ReadBoolean(text),
		};
	}

	private static bool ReadBoolean(string value) =>
		bool.TryParse(GherkinValue.Unquote(value), out var parsed)
			? parsed
			: throw new FormatException($"\"{value}\" is not True or False.");

	private static int ReadMilliseconds(string value) =>
		(int) Math.Round(GherkinValue.ToDouble(value));

	private static ToggleButton Toggle(FrameworkElement element, string property) =>
		element as ToggleButton ?? throw Unsupported(element, property);

	private static RadioButton Radio(FrameworkElement element, string property) =>
		element as RadioButton ?? throw Unsupported(element, property);

	private static ToggleSwitch Switch(FrameworkElement element, string property) =>
		element as ToggleSwitch ?? throw Unsupported(element, property);

	private static RepeatButton Repeat(FrameworkElement element, string property) =>
		element as RepeatButton ?? throw Unsupported(element, property);

	private static NotSupportedException Unsupported(FrameworkElement element, string property) =>
		new(string.Create(CultureInfo.InvariantCulture,
			$"A {element.GetType().Name} named \"{element.Name}\" has no {property} the harness can set."));
}
