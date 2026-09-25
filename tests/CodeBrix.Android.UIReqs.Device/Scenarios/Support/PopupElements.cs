using System;
using System.Globalization;
using System.Threading.Tasks;
using CodeBrix.Platform.UI.Core.UIReqs.Hosting;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace CodeBrix.Platform.UI.Core.UIReqs.Support;

/// <summary>
/// The controls a Popups feature file shows over the application rather than inside it, and the
/// properties those scenarios set. Everything here is registered through the public element
/// factory API from a hook in this group's steps class, never by editing the factory.
/// <para>
/// A popup is hosted BESIDE the application's root, so an element inside one is not below the
/// root and the harness's usual tree walk does not reach it. <see cref="OpenPopupContent"/> is
/// how a step reaches what is over the panel when nothing else names it.
/// </para>
/// </summary>
public static class PopupElements
{
	/// <summary>The name a feature file writes for the fill an InfoBar takes when it is informational.</summary>
	public const string InformationalFillName = "InfoBarInformationalFill";

	/// <summary>The name a feature file writes for the fill an InfoBar takes when it reports an error.</summary>
	public const string ErrorFillName = "InfoBarErrorFill";

	/// <summary>The name a feature file writes for the disc an InfoBar draws its error icon on.</summary>
	public const string ErrorIconName = "InfoBarErrorIcon";

	private const string InformationalFillKey = "InfoBarInformationalSeverityBackgroundBrush";
	private const string ErrorFillKey = "InfoBarErrorSeverityBackgroundBrush";
	private const string ErrorIconKey = "InfoBarErrorSeverityIconBackground";

	private static bool _registered;
	private static bool _colorsRegistered;

	/// <summary>
	/// Adds the Popups controls and properties to the element factory. Calling it twice is
	/// harmless; the popup steps call it once, before the first scenario.
	/// </summary>
	public static void Register()
	{
		if (_registered)
		{
			return;
		}

		_registered = true;

		ElementFactory.RegisterKind("InfoBar", BuildInfoBar);
		ElementFactory.RegisterKind("TeachingTip", BuildTeachingTip);

		ElementFactory.RegisterProperty("IsOpen", (element, value) => SetIsOpen(element, value));
		ElementFactory.RegisterProperty("Severity", (element, value) =>
			Bar(element, "Severity").Severity = GherkinValue.ToEnum<InfoBarSeverity>(value));
		ElementFactory.RegisterProperty("Message", (element, value) =>
			Bar(element, "Message").Message = value);
		ElementFactory.RegisterProperty("IsClosable", (element, value) =>
			Bar(element, "IsClosable").IsClosable = ReadBoolean(value));
		ElementFactory.RegisterProperty("Title", (element, value) => SetTitle(element, value));
		ElementFactory.RegisterProperty("Subtitle", (element, value) =>
			Tip(element, "Subtitle").Subtitle = value);
	}

	/// <summary>
	/// Binds the InfoBar severity colour names to the brushes the running application holds,
	/// so that a requirement about a severity stays a statement about the control rather than
	/// about one palette.
	/// </summary>
	/// <returns>A task that completes once the names are usable in a feature file.</returns>
	public static async Task RegisterColorsAsync()
	{
		if (_colorsRegistered)
		{
			return;
		}

		_colorsRegistered = true;

		var informational = default(Color);
		var error = default(Color);
		var icon = default(Color);
		await TestTargetFixture.RunOnUIThreadAsync(() =>
		{
			informational = ResolveBrushColor(InformationalFillKey);
			error = ResolveBrushColor(ErrorFillKey);
			icon = ResolveBrushColor(ErrorIconKey);
		}).ConfigureAwait(false);

		Colors.RegisterName(InformationalFillName, informational);
		Colors.RegisterName(ErrorFillName, error);
		Colors.RegisterName(ErrorIconName, icon);
	}

	/// <summary>The content of the first popup that is open. Call it on the UI thread.</summary>
	/// <returns>The popup's child, or <c>null</c> when no popup is open.</returns>
	public static FrameworkElement? OpenPopupContent()
	{
		foreach (var popup in VisualTreeHelper.GetOpenPopups(VirtualApplication.Instance.Window))
		{
			if (popup.Child is FrameworkElement child)
			{
				return child;
			}
		}

		return null;
	}

	private static FrameworkElement BuildInfoBar()
	{
		var bar = new InfoBar();

		// The element factory names an element only after building it, so the name is read
		// when the event fires rather than when the handler is attached.
		bar.CloseButtonClick += (sender, _) => EventRecorder.Record(sender.Name, "CloseButtonClick");
		bar.Closed += (sender, _) => EventRecorder.Record(sender.Name, "Closed");
		return bar;
	}

	private static FrameworkElement BuildTeachingTip()
	{
		var tip = new TeachingTip();
		tip.Closed += (sender, _) => EventRecorder.Record(sender.Name, "Closed");
		return tip;
	}

	private static void SetIsOpen(FrameworkElement element, string value)
	{
		var open = ReadBoolean(value);
		switch (element)
		{
			case Popup popup:
				popup.IsOpen = open;
				break;
			case InfoBar bar:
				bar.IsOpen = open;
				break;
			case TeachingTip tip:
				tip.IsOpen = open;
				break;
			default:
				throw Unsupported(element, "IsOpen");
		}
	}

	private static void SetTitle(FrameworkElement element, string value)
	{
		switch (element)
		{
			case InfoBar bar:
				bar.Title = value;
				break;
			case TeachingTip tip:
				tip.Title = value;
				break;
			case ContentDialog dialog:
				dialog.Title = value;
				break;
			default:
				throw Unsupported(element, "Title");
		}
	}

	private static Color ResolveBrushColor(string key)
	{
		var resources = Application.Current?.Resources
			?? throw new InvalidOperationException(
				"The application has no resources, so a theme brush cannot be read.");

		if (resources.TryGetValue(key, out var resource) && resource is SolidColorBrush brush)
		{
			return brush.Color;
		}

		throw new InvalidOperationException(
			$"The application's resources hold no SolidColorBrush called \"{key}\", so the "
			+ "feature files cannot name that theme colour.");
	}

	private static bool ReadBoolean(string value) =>
		bool.TryParse(GherkinValue.Unquote(value), out var parsed)
			? parsed
			: throw new FormatException($"\"{value}\" is not True or False.");

	private static InfoBar Bar(FrameworkElement element, string property) =>
		element as InfoBar ?? throw Unsupported(element, property);

	private static TeachingTip Tip(FrameworkElement element, string property) =>
		element as TeachingTip ?? throw Unsupported(element, property);

	private static NotSupportedException Unsupported(FrameworkElement element, string property) =>
		new(string.Create(CultureInfo.InvariantCulture,
			$"A {element.GetType().Name} named \"{element.Name}\" has no {property} the harness can set."));
}
