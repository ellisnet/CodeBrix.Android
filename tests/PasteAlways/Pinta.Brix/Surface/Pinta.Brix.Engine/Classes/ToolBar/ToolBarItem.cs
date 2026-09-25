// ToolBarItem.cs
//
// Pinta.Brix replacement for the toolkit widgets upstream tools appended to
// their options toolbar. Tools describe their toolbar as a list of these
// items; the UI layer renders them with real controls and binds their events.
// Type names deliberately mirror the upstream widget names so tool code ports
// with minimal changes.

using System;
using System.Collections.Generic;

namespace Pinta.Brix.Engine;

public abstract class ToolBarItem
{
	private bool visible = true;

	/// <summary>Tooltip the UI layer shows for this item, when set.</summary>
	public string? TooltipText { get; set; }

	/// <summary>
	/// Whether the rendered control is shown. A hidden item keeps its state;
	/// tools toggle this the way upstream toggled widget visibility.
	/// </summary>
	public bool Visible {
		get => visible;
		set {
			if (visible == value)
				return;
			visible = value;
			VisibleChanged?.Invoke (this, EventArgs.Empty);
		}
	}

	/// <summary>Raised when <see cref="Visible"/> changes.</summary>
	public event EventHandler? VisibleChanged;
}

public sealed class ToolBarLabel : ToolBarItem
{
	public ToolBarLabel (string text)
	{
		Text = text;
	}

	public string Text { get; set; }
}

public sealed class ToolBarSeparator : ToolBarItem
{
}

public sealed class ToolBarImage : ToolBarItem
{
	public string? IconName { get; set; }
}

/// <summary>
/// The tool-options toolbar a tool builds in OnBuildToolBar: an ordered list
/// of items the UI layer materializes.
/// </summary>
public sealed class ToolBar
{
	private readonly List<ToolBarItem> items = [];

	public IReadOnlyList<ToolBarItem> Items => items;

	public event EventHandler? ItemsChanged;

	public void Append (ToolBarItem item)
	{
		items.Add (item);
		ItemsChanged?.Invoke (this, EventArgs.Empty);
	}

	/// <summary>Inserts an item directly after another (upstream Gtk.Box.InsertChildAfter); appends when the anchor is absent.</summary>
	public void InsertChildAfter (ToolBarItem item, ToolBarItem after)
	{
		int index = items.IndexOf (after);
		items.Insert (index < 0 ? items.Count : index + 1, item);
		ItemsChanged?.Invoke (this, EventArgs.Empty);
	}

	public void Remove (ToolBarItem item)
	{
		if (items.Remove (item))
			ItemsChanged?.Invoke (this, EventArgs.Empty);
	}

	public void Clear ()
	{
		items.Clear ();
		ItemsChanged?.Invoke (this, EventArgs.Empty);
	}
}

/// <summary>
/// A two-state toggle button (upstream Gtk.ToggleButton) with an icon and an
/// optional short label fallback for when the icon is unavailable.
/// </summary>
public sealed class ToolBarToggleButton : ToolBarItem
{
	private bool active;

	public string? IconName { get; init; }

	/// <summary>Short text shown when the icon cannot be resolved.</summary>
	public string? Label { get; init; }

	public event EventHandler? Toggled;

	public bool Active {
		get => active;
		set {
			if (active == value)
				return;
			active = value;
			Toggled?.Invoke (this, EventArgs.Empty);
		}
	}

	public void Toggle ()
		=> Active = !Active;
}

public sealed class ToolBarDropDownItem
{
	public required string Text { get; init; }
	public string? IconName { get; init; }
	public object? Tag { get; init; }

	public T GetTagOrDefault<T> (T defaultValue)
		=> Tag is T value ? value : defaultValue;
}

public sealed class ToolBarDropDownButton : ToolBarItem
{
	private readonly List<ToolBarDropDownItem> items = [];
	private int selected_index = -1;

	public static ToolBarDropDownButton New (bool showLabel = false)
		=> new () { ShowLabel = showLabel };

	/// <summary>Whether the rendered control shows the selected item's label next to its icon.</summary>
	public bool ShowLabel { get; init; }

	public IReadOnlyList<ToolBarDropDownItem> Items => items;

	public event EventHandler? SelectedItemChanged;

	public ToolBarDropDownItem AddItem (string text, string iconName, object? tag = null)
	{
		ToolBarDropDownItem item = new () { Text = text, IconName = iconName, Tag = tag };
		items.Add (item);
		if (selected_index < 0)
			SelectedIndex = 0;
		return item;
	}

	public int SelectedIndex {
		get => selected_index;
		set {
			if (selected_index == value || value < 0 || value >= items.Count)
				return;
			selected_index = value;
			SelectedItemChanged?.Invoke (this, EventArgs.Empty);
		}
	}

	public ToolBarDropDownItem SelectedItem {
		get => items[selected_index];
		set => SelectedIndex = items.IndexOf (value);
	}
}

public sealed class ToolBarComboBox : ToolBarItem
{
	private readonly List<string> items = [];
	private int selected_index = -1;
	private string text = string.Empty;

	public ToolBarComboBox (int width, int activeIndex, bool isEditable, params IEnumerable<string> contents)
	{
		Width = width;
		IsEditable = isEditable;
		items.AddRange (contents);
		if (activeIndex >= 0 && activeIndex < items.Count) {
			selected_index = activeIndex;
			text = items[activeIndex];
		}
	}

	public static ToolBarComboBox New (int width, int activeIndex, bool isEditable, params string[] contents)
		=> new (width, activeIndex, isEditable, contents);

	public int Width { get; }

	public bool IsEditable { get; }

	public IReadOnlyList<string> Items => items;

	public event EventHandler? SelectedItemChanged;
	public event EventHandler? TextChanged;

	// ---- upstream-widget-flavored aliases so ported call sites compile ----

	/// <summary>Self-reference mirroring the upstream wrapper's inner-widget property.</summary>
	public ToolBarComboBox ComboBox => this;

	public int Active {
		get => SelectedIndex;
		set => SelectedIndex = value;
	}

	public event EventHandler? OnChanged {
		add => SelectedItemChanged += value;
		remove => SelectedItemChanged -= value;
	}

	public string? GetActiveText ()
		=> SelectedIndex >= 0 ? Text : null;

	public void AppendText (string item)
	{
		items.Add (item);
		ItemsChangedEvent?.Invoke (this, EventArgs.Empty);
	}

	public void RemoveAll ()
	{
		items.Clear ();
		selected_index = -1;
		text = string.Empty;
		ItemsChangedEvent?.Invoke (this, EventArgs.Empty);
	}

	/// <summary>Raised when the item list itself changes (append/clear).</summary>
	public event EventHandler? ItemsChangedEvent;

	public int SelectedIndex {
		get => selected_index;
		set {
			if (selected_index == value || value < 0 || value >= items.Count)
				return;
			selected_index = value;
			text = items[value];
			SelectedItemChanged?.Invoke (this, EventArgs.Empty);
			TextChanged?.Invoke (this, EventArgs.Empty);
		}
	}

	/// <summary>Current text, which for an editable combo may not match any item.</summary>
	public string Text {
		get => text;
		set {
			if (text == value)
				return;
			text = value;
			selected_index = items.IndexOf (value);
			if (selected_index >= 0)
				SelectedItemChanged?.Invoke (this, EventArgs.Empty);
			TextChanged?.Invoke (this, EventArgs.Empty);
		}
	}
}

public sealed class ToolBarSpinButton : ToolBarItem
{
	private double value;

	public ToolBarSpinButton (double min, double max, double step, double initial)
	{
		Minimum = min;
		Maximum = max;
		Step = step;
		value = initial;
	}

	public double Minimum { get; }
	public double Maximum { get; }
	public double Step { get; }

	public event EventHandler? ValueChanged;

	/// <summary>Alias matching the upstream widget's event name, so ported call sites compile unchanged.</summary>
	public event EventHandler? OnValueChanged {
		add => ValueChanged += value;
		remove => ValueChanged -= value;
	}

	public double GetValue () => Value;

	public int GetValueAsInt () => (int) Math.Round (Value);

	public void SetValue (double newValue) => Value = newValue;

	public double Value {
		get => value;
		set {
			double clamped = Math.Clamp (value, Minimum, Maximum);
			if (this.value == clamped)
				return;
			this.value = clamped;
			ValueChanged?.Invoke (this, EventArgs.Empty);
		}
	}
}

public sealed class ToolBarScale : ToolBarItem
{
	private double value;

	public ToolBarScale (int min, int max, int step, int initial)
	{
		Minimum = min;
		Maximum = max;
		Step = step;
		value = initial;
	}

	public int Minimum { get; }
	public int Maximum { get; }
	public int Step { get; }

	public event EventHandler? ValueChanged;

	/// <summary>Alias matching the upstream widget's event name, so ported call sites compile unchanged.</summary>
	public event EventHandler? OnValueChanged {
		add => ValueChanged += value;
		remove => ValueChanged -= value;
	}

	public double Value {
		get => value;
		set {
			double clamped = Math.Clamp (value, Minimum, Maximum);
			if (this.value == clamped)
				return;
			this.value = clamped;
			ValueChanged?.Invoke (this, EventArgs.Empty);
		}
	}

	public double GetValue () => Value;

	public void SetValue (double newValue) => Value = newValue;
}

/// <summary>
/// A nested group of toolbar items (upstream used a child box); the UI layer
/// renders it as an inline panel that rebuilds when its items change.
/// </summary>
public sealed class ToolBarContainer : ToolBarItem
{
	private readonly List<ToolBarItem> items = [];

	public IReadOnlyList<ToolBarItem> Items => items;

	public event EventHandler? ItemsChanged;

	public void Append (ToolBarItem item)
	{
		items.Add (item);
		ItemsChanged?.Invoke (this, EventArgs.Empty);
	}

	public void RemoveAll ()
	{
		items.Clear ();
		ItemsChanged?.Invoke (this, EventArgs.Empty);
	}
}
