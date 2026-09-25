using System;
using System.Globalization;
using CodeBrix.Platform.UI.Core.UIReqs.Hosting;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace CodeBrix.Platform.UI.Core.UIReqs.Support;

/// <summary>
/// The navigation and container controls a Navigation feature file may ask for, and the handful
/// of properties those scenarios set. Registration goes through the element factory's public
/// <see cref="ElementFactory.RegisterKind"/> and <see cref="ElementFactory.RegisterProperty"/>
/// so that several coverage groups can add their controls at the same time; every name here is
/// specific to one of these controls, because one name means one thing to the whole assembly.
/// </summary>
public static class NavigationElements
{
	/// <summary>The event name a scenario asks about when a NavigationView item is invoked.</summary>
	public const string ItemInvokedEvent = "ItemInvoked";

	/// <summary>The event name a scenario asks about when a NavigationView's selection changes.</summary>
	public const string SelectionChangedEvent = "SelectionChanged";

	/// <summary>The event name a scenario asks about when a Frame navigates.</summary>
	public const string NavigatedEvent = "Navigated";

	/// <summary>The event name a scenario asks about when an Expander expands.</summary>
	public const string ExpandingEvent = "Expanding";

	/// <summary>The event name a scenario asks about when an Expander collapses.</summary>
	public const string CollapsedEvent = "Collapsed";

	private static bool _registered;

	/// <summary>
	/// Adds the Navigation controls and properties to the element factory. Calling it twice is
	/// harmless; the navigation steps call it once, before the first scenario.
	/// </summary>
	public static void Register()
	{
		if (_registered)
		{
			return;
		}

		_registered = true;

		ElementFactory.RegisterKind("Frame", BuildFrame);
		ElementFactory.RegisterKind("NavigationView", BuildNavigationView);
		ElementFactory.RegisterKind("SplitView", () => new SplitView());
		ElementFactory.RegisterKind("Expander", BuildExpander);
		ElementFactory.RegisterKind("ScrollViewer", () => new ScrollViewer());
		ElementFactory.RegisterKind("TwoPaneView", () => new TwoPaneView());

		ElementFactory.RegisterProperty("IsPaneOpen", (element, value) => SetIsPaneOpen(element, ReadBoolean(value)));
		ElementFactory.RegisterProperty("OpenPaneLength", (element, value) =>
			SetOpenPaneLength(element, GherkinValue.ToDouble(value)));
		ElementFactory.RegisterProperty("CompactPaneLength", (element, value) =>
			SetCompactPaneLength(element, GherkinValue.ToDouble(value)));
		ElementFactory.RegisterProperty("SplitViewDisplayMode", (element, value) =>
			Split(element, "SplitViewDisplayMode").DisplayMode = GherkinValue.ToEnum<SplitViewDisplayMode>(value));
		ElementFactory.RegisterProperty("PaneDisplayMode", (element, value) =>
			Navigation(element, "PaneDisplayMode").PaneDisplayMode =
				GherkinValue.ToEnum<NavigationViewPaneDisplayMode>(value));
		ElementFactory.RegisterProperty("ExpandDirection", (element, value) =>
			Expanding(element, "ExpandDirection").ExpandDirection = GherkinValue.ToEnum<ExpandDirection>(value));
		ElementFactory.RegisterProperty("VerticalScrollBarVisibility", (element, value) =>
			Scrolling(element, "VerticalScrollBarVisibility").VerticalScrollBarVisibility =
				GherkinValue.ToEnum<ScrollBarVisibility>(value));
		ElementFactory.RegisterProperty("HorizontalScrollBarVisibility", (element, value) =>
			Scrolling(element, "HorizontalScrollBarVisibility").HorizontalScrollBarVisibility =
				GherkinValue.ToEnum<ScrollBarVisibility>(value));
		ElementFactory.RegisterProperty("MinWideModeWidth", (element, value) =>
			TwoPane(element, "MinWideModeWidth").MinWideModeWidth = GherkinValue.ToDouble(value));
		ElementFactory.RegisterProperty("MinTallModeHeight", (element, value) =>
			TwoPane(element, "MinTallModeHeight").MinTallModeHeight = GherkinValue.ToDouble(value));
		ElementFactory.RegisterProperty("PanePriority", (element, value) =>
			TwoPane(element, "PanePriority").PanePriority = GherkinValue.ToEnum<TwoPaneViewPriority>(value));
	}

	/// <summary>A panel of a known colour that fills whatever it is put inside.</summary>
	/// <param name="name">The name the scenario refers to it by.</param>
	/// <param name="color">The colour to paint it.</param>
	/// <returns>The panel, registered under its name. Call this on the UI thread.</returns>
	public static Border BuildPanel(string name, Color color)
	{
		ArgumentException.ThrowIfNullOrEmpty(name);

		var panel = new Border
		{
			Name = name,
			Background = new SolidColorBrush(color),
			HorizontalAlignment = HorizontalAlignment.Stretch,
			VerticalAlignment = VerticalAlignment.Stretch,
		};

		ElementRegistry.Register(name, panel);
		return panel;
	}

	/// <summary>A panel of a known colour and a fixed size.</summary>
	/// <param name="name">The name the scenario refers to it by.</param>
	/// <param name="width">The panel's width in logical pixels.</param>
	/// <param name="height">The panel's height in logical pixels.</param>
	/// <param name="color">The colour to paint it.</param>
	/// <returns>The panel, registered under its name. Call this on the UI thread.</returns>
	public static Border BuildPanel(string name, int width, int height, Color color)
	{
		var panel = BuildPanel(name, color);
		panel.Width = width;
		panel.Height = height;
		panel.HorizontalAlignment = HorizontalAlignment.Left;
		panel.VerticalAlignment = VerticalAlignment.Top;
		return panel;
	}

	/// <summary>The Frame an element is, or a failure that says what it is instead.</summary>
	/// <param name="element">The element a scenario named.</param>
	/// <param name="what">What the scenario was asking for.</param>
	/// <returns>The Frame.</returns>
	public static Frame AsFrame(FrameworkElement element, string what) =>
		element as Frame ?? throw Unsupported(element, what);

	/// <summary>The NavigationView an element is, or a failure that says what it is instead.</summary>
	/// <param name="element">The element a scenario named.</param>
	/// <param name="what">What the scenario was asking for.</param>
	/// <returns>The NavigationView.</returns>
	public static NavigationView AsNavigationView(FrameworkElement element, string what) =>
		Navigation(element, what);

	/// <summary>The SplitView an element is, or a failure that says what it is instead.</summary>
	/// <param name="element">The element a scenario named.</param>
	/// <param name="what">What the scenario was asking for.</param>
	/// <returns>The SplitView.</returns>
	public static SplitView AsSplitView(FrameworkElement element, string what) => Split(element, what);

	/// <summary>The Expander an element is, or a failure that says what it is instead.</summary>
	/// <param name="element">The element a scenario named.</param>
	/// <param name="what">What the scenario was asking for.</param>
	/// <returns>The Expander.</returns>
	public static Expander AsExpander(FrameworkElement element, string what) => Expanding(element, what);

	/// <summary>The ScrollViewer an element is, or a failure that says what it is instead.</summary>
	/// <param name="element">The element a scenario named.</param>
	/// <param name="what">What the scenario was asking for.</param>
	/// <returns>The ScrollViewer.</returns>
	public static ScrollViewer AsScrollViewer(FrameworkElement element, string what) => Scrolling(element, what);

	/// <summary>The TwoPaneView an element is, or a failure that says what it is instead.</summary>
	/// <param name="element">The element a scenario named.</param>
	/// <param name="what">What the scenario was asking for.</param>
	/// <returns>The TwoPaneView.</returns>
	public static TwoPaneView AsTwoPaneView(FrameworkElement element, string what) => TwoPane(element, what);

	/// <summary>Reads a True/False written in a feature file.</summary>
	/// <param name="value">The word the feature file used.</param>
	/// <returns>What it means.</returns>
	public static bool ReadBoolean(string value) =>
		bool.TryParse(GherkinValue.Unquote(value), out var parsed)
			? parsed
			: throw new FormatException($"\"{value}\" is not True or False.");

	private static FrameworkElement BuildFrame()
	{
		var frame = new Frame();
		frame.Navigated += (sender, _) => EventRecorder.Record(((FrameworkElement) sender).Name, NavigatedEvent);
		return frame;
	}

	private static FrameworkElement BuildNavigationView()
	{
		var view = new NavigationView
		{
			// A scenario about a pane is a scenario about a pane the panel actually shows: the
			// default display mode picks one from the window's width, which would make the same
			// requirement mean different things on the two panels.
			PaneDisplayMode = NavigationViewPaneDisplayMode.Left,
			IsSettingsVisible = false,
			IsBackButtonVisible = NavigationViewBackButtonVisible.Collapsed,
		};

		view.SelectionChanged += (sender, _) =>
			EventRecorder.Record(((FrameworkElement) sender).Name, SelectionChangedEvent);
		view.ItemInvoked += (sender, _) => EventRecorder.Record(((FrameworkElement) sender).Name, ItemInvokedEvent);
		return view;
	}

	private static FrameworkElement BuildExpander()
	{
		var expander = new Expander();
		expander.Expanding += (sender, _) => EventRecorder.Record(sender.Name, ExpandingEvent);
		expander.Collapsed += (sender, _) => EventRecorder.Record(sender.Name, CollapsedEvent);
		return expander;
	}

	private static void SetIsPaneOpen(FrameworkElement element, bool open)
	{
		switch (element)
		{
			case SplitView split:
				split.IsPaneOpen = open;
				break;
			case NavigationView view:
				view.IsPaneOpen = open;
				break;
			default:
				throw Unsupported(element, "IsPaneOpen");
		}
	}

	private static void SetOpenPaneLength(FrameworkElement element, double length)
	{
		switch (element)
		{
			case SplitView split:
				split.OpenPaneLength = length;
				break;
			case NavigationView view:
				view.OpenPaneLength = length;
				break;
			default:
				throw Unsupported(element, "OpenPaneLength");
		}
	}

	private static void SetCompactPaneLength(FrameworkElement element, double length)
	{
		switch (element)
		{
			case SplitView split:
				split.CompactPaneLength = length;
				break;
			case NavigationView view:
				view.CompactPaneLength = length;
				break;
			default:
				throw Unsupported(element, "CompactPaneLength");
		}
	}

	private static SplitView Split(FrameworkElement element, string what) =>
		element as SplitView ?? throw Unsupported(element, what);

	private static NavigationView Navigation(FrameworkElement element, string what) =>
		element as NavigationView ?? throw Unsupported(element, what);

	private static Expander Expanding(FrameworkElement element, string what) =>
		element as Expander ?? throw Unsupported(element, what);

	private static ScrollViewer Scrolling(FrameworkElement element, string what) =>
		element as ScrollViewer ?? throw Unsupported(element, what);

	private static TwoPaneView TwoPane(FrameworkElement element, string what) =>
		element as TwoPaneView ?? throw Unsupported(element, what);

	private static NotSupportedException Unsupported(FrameworkElement element, string what) =>
		new(string.Create(CultureInfo.InvariantCulture,
			$"A {element.GetType().Name} named \"{element.Name}\" has no {what} the harness can use."));
}
