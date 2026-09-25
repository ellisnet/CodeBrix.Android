using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;

namespace CodeBrix.Platform.UI.Core.UIReqs.Support;

/// <summary>
/// The controls that show a collection, and the properties an Items feature file sets on one.
/// Registration goes through the public <see cref="ElementFactory.RegisterKind"/> and
/// <see cref="ElementFactory.RegisterProperty"/> API, called once from a hook in this group's
/// steps class, so that several coverage groups can teach the factory their own nouns at the
/// same time without any of them editing the factory.
/// </summary>
public static class ItemsElements
{
	/// <summary>The event name a selection change is recorded under.</summary>
	public const string SelectionChangedEvent = "SelectionChanged";

	/// <summary>The event name a TreeView node's expansion is recorded under.</summary>
	public const string ExpandingEvent = "Expanding";

	/// <summary>The event name a TreeView node's collapse is recorded under.</summary>
	public const string CollapsedEvent = "Collapsed";

	/// <summary>The smallest a colour panel built for an item is allowed to be.</summary>
	public const double ItemPanelMinimumWidth = 80;

	/// <summary>The shortest a colour panel built for an item is allowed to be.</summary>
	public const double ItemPanelMinimumHeight = 40;

	private static bool _registered;

	/// <summary>
	/// Adds the Items controls and properties to the element factory. Calling it twice is
	/// harmless; the items steps call it once, before the first scenario.
	/// </summary>
	public static void Register()
	{
		if (_registered)
		{
			return;
		}

		_registered = true;

		ElementFactory.RegisterKind("ItemsControl", () => new ItemsControl());
		ElementFactory.RegisterKind("ItemsRepeater", () => new ItemsRepeater());
		ElementFactory.RegisterKind("ListView", BuildListView);
		ElementFactory.RegisterKind("GridView", BuildGridView);
		ElementFactory.RegisterKind("ComboBox", BuildComboBox);
		ElementFactory.RegisterKind("FlipView", BuildFlipView);
		ElementFactory.RegisterKind("TreeView", BuildTreeView);
		ElementFactory.RegisterKind("Pivot", BuildPivot);
		ElementFactory.RegisterKind("TabView", BuildTabView);
		ElementFactory.RegisterKind("SelectorBar", BuildSelectorBar);

		ElementFactory.RegisterProperty("ItemLabels", (element, value) => SetItemLabels(element, value));
		ElementFactory.RegisterProperty("ItemColors", (element, value) => SetItemColors(element, value));
		ElementFactory.RegisterProperty("SelectionMode", (element, value) => SetSelectionMode(element, value));
		ElementFactory.RegisterProperty("SelectedIndex", (element, value) => SetSelectedIndex(element, value));
	}

	/// <summary>Attaches the recorder to a selection-changing control, on the UI thread.</summary>
	/// <param name="selector">The control whose selection a requirement talks about.</param>
	public static void RecordSelectionChanges(Selector selector)
	{
		ArgumentNullException.ThrowIfNull(selector);

		// The element factory names an element only after building it, so the name is read
		// when the event fires rather than when the handler is attached.
		selector.SelectionChanged += (sender, _) =>
			EventRecorder.Record(((FrameworkElement) sender).Name, SelectionChangedEvent);
	}

	/// <summary>
	/// The items a feature file wrote as one comma-separated list, in the order it wrote them.
	/// </summary>
	/// <param name="value">The list, as the feature file spelled it.</param>
	/// <returns>The separate values.</returns>
	public static IReadOnlyList<string> ReadList(string value) =>
		GherkinValue.Unquote(value)
			.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

	/// <summary>A panel of a known colour, which is what makes an item visible on the panel.</summary>
	/// <param name="color">The colour to paint it.</param>
	/// <param name="name">The name to give it.</param>
	/// <returns>The panel.</returns>
	public static Border BuildColorPanel(string color, string name) => new()
	{
		Name = name,
		Background = new SolidColorBrush(Colors.Parse(color)),
		MinWidth = ItemPanelMinimumWidth,
		MinHeight = ItemPanelMinimumHeight,
		HorizontalAlignment = HorizontalAlignment.Stretch,
		VerticalAlignment = VerticalAlignment.Stretch,
	};

	/// <summary>The scroll viewer a control scrolls its items in, or <c>null</c> when it has none.</summary>
	/// <param name="element">The items control.</param>
	/// <returns>The scroll viewer inside the control's template.</returns>
	public static ScrollViewer? ScrollViewerOf(FrameworkElement element)
	{
		ArgumentNullException.ThrowIfNull(element);
		return VisualTreeSearch.FindDescendant<ScrollViewer>(element);
	}

	private static FrameworkElement BuildListView()
	{
		var list = new ListView();
		RecordSelectionChanges(list);
		return list;
	}

	private static FrameworkElement BuildGridView()
	{
		var grid = new GridView();
		RecordSelectionChanges(grid);
		return grid;
	}

	private static FrameworkElement BuildComboBox()
	{
		var combo = new ComboBox();
		RecordSelectionChanges(combo);
		return combo;
	}

	private static FrameworkElement BuildFlipView()
	{
		var flip = new FlipView();
		RecordSelectionChanges(flip);
		return flip;
	}

	private static FrameworkElement BuildPivot()
	{
		var pivot = new Pivot();
		pivot.SelectionChanged += (sender, _) =>
			EventRecorder.Record(((FrameworkElement) sender).Name, SelectionChangedEvent);
		return pivot;
	}

	private static FrameworkElement BuildTabView()
	{
		var tabs = new TabView();
		tabs.SelectionChanged += (sender, _) =>
			EventRecorder.Record(((FrameworkElement) sender).Name, SelectionChangedEvent);
		return tabs;
	}

	private static FrameworkElement BuildSelectorBar()
	{
		var bar = new SelectorBar();
		bar.SelectionChanged += (sender, _) =>
			EventRecorder.Record(sender.Name, SelectionChangedEvent);
		return bar;
	}

	private static FrameworkElement BuildTreeView()
	{
		var tree = new TreeView();
		tree.Expanding += (sender, _) => EventRecorder.Record(sender.Name, ExpandingEvent);
		tree.Collapsed += (sender, _) => EventRecorder.Record(sender.Name, CollapsedEvent);
		return tree;
	}

	private static void SetItemLabels(FrameworkElement element, string value)
	{
		var labels = ReadList(value);
		SetItemsSource(element, labels.Cast<object>().ToList());
	}

	private static void SetItemColors(FrameworkElement element, string value)
	{
		var colors = ReadList(value);
		var panels = new List<object>(colors.Count);
		for (var i = 0; i < colors.Count; i++)
		{
			var name = string.Create(CultureInfo.InvariantCulture, $"{element.Name} item {i + 1}");
			panels.Add(BuildColorPanel(colors[i], name));
		}

		SetItemsSource(element, panels);
	}

	private static void SetItemsSource(FrameworkElement element, IList<object> items)
	{
		switch (element)
		{
			case ItemsRepeater repeater:
				repeater.ItemsSource = items;
				break;
			case Pivot or TabView or SelectorBar or TreeView:
				throw new NotSupportedException(
					$"A {element.GetType().Name} named \"{element.Name}\" carries sections rather than plain "
					+ "items: a scenario builds one with its own step rather than with ItemLabels or ItemColors.");
			case ItemsControl control:
				control.ItemsSource = items;
				break;
			default:
				throw Unsupported(element, "ItemLabels");
		}
	}

	private static void SetSelectionMode(FrameworkElement element, string value)
	{
		switch (element)
		{
			case ListViewBase list:
				list.SelectionMode = GherkinValue.ToEnum<ListViewSelectionMode>(value);
				break;
			case TreeView tree:
				tree.SelectionMode = GherkinValue.ToEnum<TreeViewSelectionMode>(value);
				break;
			default:
				throw Unsupported(element, "SelectionMode");
		}
	}

	private static void SetSelectedIndex(FrameworkElement element, string value)
	{
		var index = (int) Math.Round(GherkinValue.ToDouble(value));
		switch (element)
		{
			case Selector selector:
				selector.SelectedIndex = index;
				break;
			case Pivot pivot:
				pivot.SelectedIndex = index;
				break;
			case TabView tabs:
				tabs.SelectedIndex = index;
				break;
			default:
				throw Unsupported(element, "SelectedIndex");
		}
	}

	private static NotSupportedException Unsupported(FrameworkElement element, string property) =>
		new(string.Create(CultureInfo.InvariantCulture,
			$"A {element.GetType().Name} named \"{element.Name}\" has no {property} the harness can set."));
}
