// ToolBarRenderer.cs
//
// Renders the engine's descriptor-based tool-options toolbar (ToolBar /
// ToolBarItem model) into real CodeBrix.Platform controls, two-way binding
// their state back onto the descriptors.

using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Pinta.Brix.Engine;
using EngineToolBar = Pinta.Brix.Engine.ToolBar;

namespace Pinta.Brix.Controls;

public sealed class ToolBarRenderer : IDisposable
{
	private readonly EngineToolBar model;
	private readonly StackPanel panel;

	//Container descriptors outlive any single Rebuild (they belong to the
	//tool), so their event subscriptions must be detached when the toolbar
	//is rebuilt or the old handlers keep rebuilding orphaned panels.
	private readonly List<Action> detachers = [];

	public ToolBarRenderer (EngineToolBar model, StackPanel panel)
	{
		this.model = model;
		this.panel = panel;
		model.ItemsChanged += OnItemsChanged;
		Rebuild ();
	}

	public void Dispose ()
	{
		model.ItemsChanged -= OnItemsChanged;
		DetachAll ();
	}

	private void OnItemsChanged (object? sender, EventArgs e)
		=> Rebuild ();

	private void DetachAll ()
	{
		foreach (Action detach in detachers)
			detach ();
		detachers.Clear ();
	}

	private void Rebuild ()
	{
		DetachAll ();
		panel.Children.Clear ();
		foreach (ToolBarItem item in model.Items) {
			UIElement? element = CreateElement (item);
			if (element is not null)
				panel.Children.Add (element);
		}
	}

	private UIElement? CreateElement (ToolBarItem item)
	{
		UIElement? element = item switch {
			ToolBarLabel label => new TextBlock {
				Text = label.Text,
				VerticalAlignment = VerticalAlignment.Center,
				Margin = new Thickness (4, 0, 4, 0),
			},
			ToolBarSeparator => new Border {
				Width = 1,
				Margin = new Thickness (6, 4, 6, 4),
				Background = new SolidColorBrush (Windows.UI.Color.FromArgb (0x40, 0x80, 0x80, 0x80)),
			},
			ToolBarImage image => CreateImage (image),
			ToolBarToggleButton toggle => CreateToggle (toggle),
			ToolBarDropDownButton dropDown => CreateDropDown (dropDown),
			ToolBarComboBox combo => CreateCombo (combo),
			ToolBarSpinButton spin => CreateSpin (spin),
			ToolBarScale scale => CreateScale (scale),
			ToolBarContainer container => CreateContainer (container),
			_ => null,
		};

		if (element is FrameworkElement fe && item.TooltipText is { } tooltip)
			ToolTipService.SetToolTip (fe, tooltip);

		if (element is not null) {
			UIElement el = element;
			el.Visibility = item.Visible ? Visibility.Visible : Visibility.Collapsed;
			void OnVisibleChanged (object? sender, EventArgs args)
				=> el.Visibility = item.Visible ? Visibility.Visible : Visibility.Collapsed;
			item.VisibleChanged += OnVisibleChanged;
			detachers.Add (() => item.VisibleChanged -= OnVisibleChanged);
		}

		return element;
	}

	private static UIElement CreateImage (ToolBarImage image)
	{
		// V1 renders the current tool's icon as a 16px image when available.
		Image element = new () {
			Width = 16,
			Height = 16,
			VerticalAlignment = VerticalAlignment.Center,
		};
		if (image.IconName is { } icon)
			element.Source = IconImageSource.Create (icon, 16);
		return element;
	}

	private static UIElement CreateToggle (ToolBarToggleButton toggle)
	{
		Microsoft.UI.Xaml.Controls.Primitives.ToggleButton button = new () {
			IsChecked = toggle.Active,
			Padding = new Thickness (6, 4, 6, 4),
			Margin = new Thickness (1, 0, 1, 0),
			VerticalAlignment = VerticalAlignment.Center,
		};

		//IconImageSource.Create returns null for an unknown icon; fall back
		//to the short label so the button never renders blank.
		if (toggle.IconName is { } icon && IconImageSource.Create (icon, 16) is { } source)
			button.Content = new Image { Width = 16, Height = 16, Source = source };
		else
			button.Content = new TextBlock { Text = toggle.Label ?? "?" };

		bool updating = false;
		button.Checked += (_, _) => {
			if (updating)
				return;
			updating = true;
			toggle.Active = true;
			updating = false;
		};
		button.Unchecked += (_, _) => {
			if (updating)
				return;
			updating = true;
			toggle.Active = false;
			updating = false;
		};
		toggle.Toggled += (_, _) => {
			if (updating)
				return;
			updating = true;
			button.IsChecked = toggle.Active;
			updating = false;
		};
		return button;
	}

	private static UIElement CreateDropDown (ToolBarDropDownButton dropDown)
	{
		ComboBox combo = new () {
			VerticalAlignment = VerticalAlignment.Center,
			Margin = new Thickness (2, 0, 2, 0),
		};
		foreach (ToolBarDropDownItem entry in dropDown.Items)
			combo.Items.Add (CreateDropDownRow (entry));
		combo.SelectedIndex = dropDown.SelectedIndex;

		bool updating = false;
		combo.SelectionChanged += (_, _) => {
			if (updating || combo.SelectedIndex < 0)
				return;
			updating = true;
			dropDown.SelectedIndex = combo.SelectedIndex;
			updating = false;
		};
		dropDown.SelectedItemChanged += (_, _) => {
			if (updating)
				return;
			updating = true;
			combo.SelectedIndex = dropDown.SelectedIndex;
			updating = false;
		};
		return combo;
	}

	private static UIElement CreateDropDownRow (ToolBarDropDownItem entry)
	{
		//IconImageSource.Create returns null for an unknown icon name, so a
		//missing icon degrades to a text-only row rather than a blank gap.
		ImageSource? source = entry.IconName is { } icon ? IconImageSource.Create (icon, 16) : null;

		StackPanel row = new () {
			Orientation = Orientation.Horizontal,
			Spacing = 6,
		};
		if (source is not null) {
			row.Children.Add (new Image {
				Width = 16,
				Height = 16,
				Source = source,
				VerticalAlignment = VerticalAlignment.Center,
			});
		}
		row.Children.Add (new TextBlock {
			Text = entry.Text,
			VerticalAlignment = VerticalAlignment.Center,
		});
		return row;
	}

	private static UIElement CreateCombo (ToolBarComboBox comboModel)
	{
		ComboBox combo = new () {
			MinWidth = comboModel.Width,
			IsEditable = comboModel.IsEditable,
			VerticalAlignment = VerticalAlignment.Center,
			Margin = new Thickness (2, 0, 2, 0),
		};
		foreach (string entry in comboModel.Items)
			combo.Items.Add (entry);
		combo.SelectedIndex = comboModel.SelectedIndex;

		bool updating = false;
		combo.SelectionChanged += (_, _) => {
			if (updating || combo.SelectedIndex < 0)
				return;
			updating = true;
			comboModel.SelectedIndex = combo.SelectedIndex;
			updating = false;
		};
		//Typed text in an editable combo reaches the model when submitted.
		combo.TextSubmitted += (_, args) => {
			if (updating)
				return;
			updating = true;
			comboModel.Text = args.Text;
			updating = false;
		};
		comboModel.SelectedItemChanged += (_, _) => {
			if (updating)
				return;
			updating = true;
			combo.SelectedIndex = comboModel.SelectedIndex;
			updating = false;
		};
		return combo;
	}

	private static UIElement CreateSpin (ToolBarSpinButton spin)
	{
		NumberBox box = new () {
			Minimum = spin.Minimum,
			Maximum = spin.Maximum,
			SmallChange = spin.Step,
			Value = spin.Value,
			SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline,
			VerticalAlignment = VerticalAlignment.Center,
			Margin = new Thickness (2, 0, 2, 0),
		};

		bool updating = false;
		box.ValueChanged += (_, _) => {
			if (updating || double.IsNaN (box.Value))
				return;
			updating = true;
			spin.Value = box.Value;
			updating = false;
		};
		spin.ValueChanged += (_, _) => {
			if (updating)
				return;
			updating = true;
			box.Value = spin.Value;
			updating = false;
		};
		return box;
	}

	private static UIElement CreateScale (ToolBarScale scale)
	{
		//Upstream (GtkExtensions.CreateToolBarSlider) is a 150px horizontal
		//scale that draws its value to the LEFT of the trough.
		TextBlock valueText = new () {
			Text = ((int) scale.Value).ToString (),
			MinWidth = 24,
			TextAlignment = Microsoft.UI.Xaml.TextAlignment.Right,
			VerticalAlignment = VerticalAlignment.Center,
		};
		Slider slider = new () {
			Minimum = scale.Minimum,
			Maximum = scale.Maximum,
			StepFrequency = scale.Step,
			Value = scale.Value,
			Width = 150,
			VerticalAlignment = VerticalAlignment.Center,
		};

		bool updating = false;
		slider.ValueChanged += (_, args) => {
			valueText.Text = ((int) args.NewValue).ToString ();
			if (updating)
				return;
			updating = true;
			scale.Value = args.NewValue;
			updating = false;
		};
		scale.ValueChanged += (_, _) => {
			if (updating)
				return;
			updating = true;
			slider.Value = scale.Value;
			updating = false;
		};

		StackPanel host = new () {
			Orientation = Orientation.Horizontal,
			Spacing = 4,
			VerticalAlignment = VerticalAlignment.Center,
			Margin = new Thickness (2, 0, 2, 0),
		};
		host.Children.Add (valueText);
		host.Children.Add (slider);
		return host;
	}

	private UIElement CreateContainer (ToolBarContainer container)
	{
		//A nested group of items (upstream used a child Gtk.Box). The panel
		//rebuilds whenever the tool changes the container's contents - the
		//paint brush swaps its brush-specific options this way.
		StackPanel host = new () {
			Orientation = Orientation.Horizontal,
			VerticalAlignment = VerticalAlignment.Center,
		};

		void RebuildContainer ()
		{
			host.Children.Clear ();
			foreach (ToolBarItem child in container.Items) {
				UIElement? element = CreateElement (child);
				if (element is not null)
					host.Children.Add (element);
			}
		}

		void OnContainerItemsChanged (object? sender, EventArgs e)
			=> RebuildContainer ();

		container.ItemsChanged += OnContainerItemsChanged;
		detachers.Add (() => container.ItemsChanged -= OnContainerItemsChanged);

		RebuildContainer ();
		return host;
	}
}
