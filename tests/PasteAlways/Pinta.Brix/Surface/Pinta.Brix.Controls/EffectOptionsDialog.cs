// EffectOptionsDialog.cs
//
// The reflection-generated effect/adjustment configuration dialog: builds an
// options panel from an effect's EffectData members and their dialog
// attributes, firing PropertyChanged so the live-preview system re-renders
// as values change. Ports the role of the upstream reflection dialog onto
// ContentDialog; unsupported member types degrade to a read-only note.
//
// PointI, CenterOffset<double> and Color members ARE editable here - upstream
// renders them with PointPickerWidget and ColorPanelWidget. Without them nine
// effects were configurable in name only: Bulge, Dents, Polar Inversion,
// Radial Blur, Twist and Zoom Blur (CenterOffset), Vignette (PointI), and
// Cells and Voronoi Diagram (Color).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Pinta.Brix.Engine;
using Drawing = Pinta.Brix.Engine.Drawing;

namespace Pinta.Brix.Controls;

public static class EffectOptionsDialog
{
	public static async Task<bool> ShowAsync (BaseEffect effect, XamlRoot xamlRoot)
	{
		if (effect.EffectData is not { } data)
			return true;

		StackPanel panel = new () { Spacing = 10, MinWidth = 340 };

		foreach (MemberInfo member in GetDialogMembers (data)) {
			FrameworkElement? row = CreateRow (data, member);
			if (row is not null)
				panel.Children.Add (row);
		}

		//Modeless floating panel, not a ContentDialog: the live preview keeps
		//rendering on a fully visible, un-dimmed canvas while values change.
		return await FloatingDialogHost.ShowAsync (
			effect.Name,
			new ScrollViewer {
				Content = panel,
				VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
				MaxHeight = 480,
			},
			xamlRoot);
	}

	private static IEnumerable<MemberInfo> GetDialogMembers (EffectData data)
	{
		Type type = data.GetType ();
		foreach (MemberInfo member in type.GetMembers (BindingFlags.Public | BindingFlags.Instance)) {
			if (member is not PropertyInfo and not FieldInfo)
				continue;
			if (member is PropertyInfo { CanWrite: false })
				continue;
			if (member.DeclaringType == typeof (EffectData) || member.DeclaringType == typeof (ObservableObject))
				continue;
			if (member.GetCustomAttribute<SkipAttribute> () is not null)
				continue;
			yield return member;
		}
	}

	private static string GetCaption (MemberInfo member)
		=> member.GetCustomAttribute<CaptionAttribute> ()?.Caption
			?? AddSpaces (member.Name);

	private static string AddSpaces (string name)
		=> string.Concat (name.Select ((c, i) => i > 0 && char.IsUpper (c) ? " " + c : c.ToString ()));

	private static Type MemberType (MemberInfo member)
		=> member switch {
			PropertyInfo p => p.PropertyType,
			FieldInfo f => f.FieldType,
			_ => typeof (object),
		};

	private static object? GetValue (EffectData data, MemberInfo member)
		=> member switch {
			PropertyInfo p => p.GetValue (data),
			FieldInfo f => f.GetValue (data),
			_ => null,
		};

	private static void SetValue (EffectData data, MemberInfo member, object? value)
	{
		switch (member) {
			case PropertyInfo p:
				p.SetValue (data, value);
				break;
			case FieldInfo f:
				f.SetValue (data, value);
				break;
		}
		data.FirePropertyChanged (member.Name);
	}

	private static FrameworkElement? CreateRow (EffectData data, MemberInfo member)
	{
		Type type = MemberType (member);
		string caption = GetCaption (member);

		if (type == typeof (int))
			return CreateNumericRow (data, member, caption, isInteger: true);
		if (type == typeof (double))
			return CreateNumericRow (data, member, caption, isInteger: false);
		if (type == typeof (bool))
			return CreateCheckRow (data, member, caption);
		if (type.IsEnum)
			return CreateEnumRow (data, member, caption, type);
		if (type == typeof (string) && member.GetCustomAttribute<StaticListAttribute> () is { } list)
			return CreateStaticListRow (data, member, caption, list);
		if (type == typeof (DegreesAngle))
			return CreateAngleRow (data, member, caption);
		if (type == typeof (RandomSeed))
			return CreateSeedRow (data, member, caption);
		if (type == typeof (PointI))
			return CreatePointRow (data, member, caption);
		if (type == typeof (CenterOffset<double>))
			return CreateOffsetRow (data, member, caption);
		if (type == typeof (Drawing.Color))
			return CreateColorRow (data, member, caption);

		// Still unsupported: note it so the effect is at least usable with its
		// default value rather than silently ignoring the member.
		return new TextBlock {
			Text = $"{caption}: (not yet editable in this port)",
			Opacity = 0.6,
		};
	}

	private static FrameworkElement CreateNumericRow (EffectData data, MemberInfo member, string caption, bool isInteger)
	{
		double min = member.GetCustomAttribute<MinimumValueAttribute> ()?.Value ?? (isInteger ? 0 : 0.0);
		double max = member.GetCustomAttribute<MaximumValueAttribute> () is { } maxAttr ? maxAttr.Value : 100;
		double increment = member.GetCustomAttribute<IncrementValueAttribute> ()?.Value ?? (isInteger ? 1 : 0.01);

		double initial = Convert.ToDouble (GetValue (data, member) ?? 0);

		StackPanel row = new () { Spacing = 2 };
		row.Children.Add (new TextBlock { Text = caption });

		//Upstream's HScaleSpinButtonWidget: slider + spin entry + reset, all
		//bound to the same value.
		Slider slider = new () {
			Minimum = min,
			Maximum = max,
			StepFrequency = increment,
			Value = initial,
		};
		NumberBox spin = new () {
			Minimum = min,
			Maximum = max,
			SmallChange = increment,
			Value = initial,
			SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
			MinWidth = 72,
			Margin = new Thickness (8, 0, 0, 0),
		};
		Button reset = new () {
			Content = new SymbolIcon (Symbol.Undo),
			Padding = new Thickness (6, 4, 6, 4),
			Margin = new Thickness (4, 0, 0, 0),
		};
		ToolTipService.SetToolTip (reset, "Reset to default");

		bool updating = false;
		void Apply (double raw)
		{
			if (updating)
				return;
			updating = true;
			double clamped = Math.Clamp (raw, min, max);
			object newValue = isInteger ? (object) (int) Math.Round (clamped) : clamped;
			slider.Value = clamped;
			spin.Value = isInteger ? (int) Math.Round (clamped) : clamped;
			SetValue (data, member, newValue);
			updating = false;
		}

		slider.ValueChanged += (_, e) => Apply (e.NewValue);
		spin.ValueChanged += (_, _) => {
			if (!double.IsNaN (spin.Value))
				Apply (spin.Value);
		};
		reset.Click += (_, _) => Apply (initial);

		Grid grid = new ();
		grid.ColumnDefinitions.Add (new ColumnDefinition { Width = new GridLength (1, GridUnitType.Star) });
		grid.ColumnDefinitions.Add (new ColumnDefinition { Width = GridLength.Auto });
		grid.ColumnDefinitions.Add (new ColumnDefinition { Width = GridLength.Auto });
		Grid.SetColumn (slider, 0);
		Grid.SetColumn (spin, 1);
		Grid.SetColumn (reset, 2);
		grid.Children.Add (slider);
		grid.Children.Add (spin);
		grid.Children.Add (reset);
		row.Children.Add (grid);
		return row;
	}

	private static FrameworkElement CreateCheckRow (EffectData data, MemberInfo member, string caption)
	{
		CheckBox check = new () {
			Content = caption,
			IsChecked = (bool) (GetValue (data, member) ?? false),
		};
		check.Checked += (_, _) => SetValue (data, member, true);
		check.Unchecked += (_, _) => SetValue (data, member, false);
		return check;
	}

	private static FrameworkElement CreateEnumRow (EffectData data, MemberInfo member, string caption, Type enumType)
	{
		StackPanel row = new () { Spacing = 2 };
		row.Children.Add (new TextBlock { Text = caption });
		ComboBox combo = new () { HorizontalAlignment = HorizontalAlignment.Stretch };
		Array values = Enum.GetValues (enumType);
		foreach (object value in values)
			combo.Items.Add (AddSpaces (value.ToString () ?? string.Empty));
		combo.SelectedIndex = Array.IndexOf (values, GetValue (data, member));
		combo.SelectionChanged += (_, _) => {
			if (combo.SelectedIndex >= 0)
				SetValue (data, member, values.GetValue (combo.SelectedIndex));
		};
		row.Children.Add (combo);
		return row;
	}

	private static FrameworkElement CreateStaticListRow (EffectData data, MemberInfo member, string caption, StaticListAttribute listAttr)
	{
		StackPanel row = new () { Spacing = 2 };
		row.Children.Add (new TextBlock { Text = caption });
		ComboBox combo = new () { HorizontalAlignment = HorizontalAlignment.Stretch };

		// The attribute names a static member on the data type holding the choices.
		string[] choices =
			data.GetType ().GetProperty (listAttr.DictionaryName, BindingFlags.Public | BindingFlags.Static)?.GetValue (null) is IEnumerable<string> items
			? [.. items]
			: [];
		foreach (string choice in choices)
			combo.Items.Add (choice);
		combo.SelectedIndex = Array.IndexOf (choices, (string?) GetValue (data, member) ?? string.Empty);
		combo.SelectionChanged += (_, _) => {
			if (combo.SelectedIndex >= 0)
				SetValue (data, member, choices[combo.SelectedIndex]);
		};
		row.Children.Add (combo);
		return row;
	}

	private static FrameworkElement CreateAngleRow (EffectData data, MemberInfo member, string caption)
	{
		StackPanel row = new () { Spacing = 2 };
		row.Children.Add (new TextBlock { Text = caption });
		DegreesAngle initial = (DegreesAngle) (GetValue (data, member) ?? new DegreesAngle (0));

		Slider slider = new () {
			Minimum = 0,
			Maximum = 360,
			StepFrequency = 1,
			Value = initial.Degrees,
		};
		NumberBox spin = new () {
			Minimum = 0,
			Maximum = 360,
			SmallChange = 1,
			Value = initial.Degrees,
			SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact,
			MinWidth = 72,
			Margin = new Thickness (8, 0, 0, 0),
		};
		Button reset = new () {
			Content = new SymbolIcon (Symbol.Undo),
			Padding = new Thickness (6, 4, 6, 4),
			Margin = new Thickness (4, 0, 0, 0),
		};
		ToolTipService.SetToolTip (reset, "Reset to default");

		bool updating = false;
		void Apply (double raw)
		{
			if (updating)
				return;
			updating = true;
			double clamped = Math.Clamp (raw, 0, 360);
			slider.Value = clamped;
			spin.Value = clamped;
			SetValue (data, member, new DegreesAngle (clamped));
			updating = false;
		}

		slider.ValueChanged += (_, e) => Apply (e.NewValue);
		spin.ValueChanged += (_, _) => {
			if (!double.IsNaN (spin.Value))
				Apply (spin.Value);
		};
		reset.Click += (_, _) => Apply (initial.Degrees);

		Grid grid = new ();
		grid.ColumnDefinitions.Add (new ColumnDefinition { Width = new GridLength (1, GridUnitType.Star) });
		grid.ColumnDefinitions.Add (new ColumnDefinition { Width = GridLength.Auto });
		grid.ColumnDefinitions.Add (new ColumnDefinition { Width = GridLength.Auto });
		Grid.SetColumn (slider, 0);
		Grid.SetColumn (spin, 1);
		Grid.SetColumn (reset, 2);
		grid.Children.Add (slider);
		grid.Children.Add (spin);
		grid.Children.Add (reset);
		row.Children.Add (grid);
		return row;
	}

	/// <summary>
	/// A PointI member - upstream's PointPickerWidget. Rendered as an X/Y pair
	/// of spin entries: the widget's draggable thumbnail needs the source image,
	/// which the dialog does not have, and the numbers are the part that
	/// actually configures the effect.
	/// </summary>
	private static FrameworkElement CreatePointRow (EffectData data, MemberInfo member, string caption)
	{
		PointI current = (PointI) (GetValue (data, member) ?? PointI.Zero);

		StackPanel row = new () { Spacing = 2 };
		row.Children.Add (new TextBlock { Text = caption });

		NumberBox x = new () { Value = current.X, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline, Minimum = -32000, Maximum = 32000 };
		NumberBox y = new () { Value = current.Y, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline, Minimum = -32000, Maximum = 32000 };

		void Apply () => SetValue (data, member, new PointI (
			double.IsNaN (x.Value) ? 0 : (int) x.Value,
			double.IsNaN (y.Value) ? 0 : (int) y.Value));

		x.ValueChanged += (_, _) => Apply ();
		y.ValueChanged += (_, _) => Apply ();

		Grid grid = new () { ColumnSpacing = 8 };
		grid.ColumnDefinitions.Add (new ColumnDefinition { Width = new GridLength (1, GridUnitType.Star) });
		grid.ColumnDefinitions.Add (new ColumnDefinition { Width = new GridLength (1, GridUnitType.Star) });
		Grid.SetColumn (x, 0);
		Grid.SetColumn (y, 1);
		grid.Children.Add (x);
		grid.Children.Add (y);
		row.Children.Add (grid);

		return row;
	}

	/// <summary>
	/// A CenterOffset&lt;double&gt; member - upstream's PointPickerWidget in its
	/// proportional mode. Both components run -1..1, where 0 is the centre of
	/// the image and 1 is its right or bottom edge.
	/// </summary>
	private static FrameworkElement CreateOffsetRow (EffectData data, MemberInfo member, string caption)
	{
		CenterOffset<double> current = (CenterOffset<double>) (GetValue (data, member) ?? new CenterOffset<double> (0, 0));

		StackPanel row = new () { Spacing = 2 };
		row.Children.Add (new TextBlock { Text = caption });

		Slider horizontal = new () { Minimum = -1, Maximum = 1, StepFrequency = 0.01, Value = current.Horizontal };
		Slider vertical = new () { Minimum = -1, Maximum = 1, StepFrequency = 0.01, Value = current.Vertical };

		TextBlock readout = new () { MinWidth = 90, Margin = new Thickness (8, 0, 0, 0) };

		void Apply ()
		{
			readout.Text = $"{horizontal.Value:0.##}, {vertical.Value:0.##}";
			SetValue (data, member, new CenterOffset<double> (horizontal.Value, vertical.Value));
		}

		horizontal.ValueChanged += (_, _) => Apply ();
		vertical.ValueChanged += (_, _) => Apply ();
		readout.Text = $"{current.Horizontal:0.##}, {current.Vertical:0.##}";

		Grid grid = new ();
		grid.ColumnDefinitions.Add (new ColumnDefinition { Width = new GridLength (1, GridUnitType.Star) });
		grid.ColumnDefinitions.Add (new ColumnDefinition { Width = GridLength.Auto });

		StackPanel sliders = new () { Spacing = 2 };
		sliders.Children.Add (horizontal);
		sliders.Children.Add (vertical);

		Grid.SetColumn (sliders, 0);
		Grid.SetColumn (readout, 1);
		grid.Children.Add (sliders);
		grid.Children.Add (readout);
		row.Children.Add (grid);

		return row;
	}

	/// <summary>
	/// A Color member - upstream's ColorPanelWidget: a swatch that opens the
	/// colour picker.
	/// </summary>
	private static FrameworkElement CreateColorRow (EffectData data, MemberInfo member, string caption)
	{
		Drawing.Color current = (Drawing.Color) (GetValue (data, member) ?? Drawing.Color.Black);

		Button swatch = new () {
			Width = 48,
			Height = 24,
			MinWidth = 48,
			Padding = new Thickness (0),
			Background = new SolidColorBrush (ToWindowsColor (current)),
		};

		swatch.Click += async (_, _) => {

			XamlRoot? root = swatch.XamlRoot;

			if (root is null)
				return; // Not in a visual tree - nothing to attach a dialog to.

			Drawing.Color? chosen = await ColorPickerDialog.ShowAsync (
				caption,
				(Drawing.Color) (GetValue (data, member) ?? Drawing.Color.Black),
				root);

			if (chosen is null)
				return;

			SetValue (data, member, chosen.Value);
			swatch.Background = new SolidColorBrush (ToWindowsColor (chosen.Value));
		};

		Grid row = new () { ColumnSpacing = 8 };
		row.ColumnDefinitions.Add (new ColumnDefinition { Width = new GridLength (1, GridUnitType.Star) });
		row.ColumnDefinitions.Add (new ColumnDefinition { Width = GridLength.Auto });

		TextBlock label = new () { Text = caption, VerticalAlignment = VerticalAlignment.Center };
		Grid.SetColumn (label, 0);
		Grid.SetColumn (swatch, 1);
		row.Children.Add (label);
		row.Children.Add (swatch);

		return row;
	}

	private static Windows.UI.Color ToWindowsColor (Drawing.Color color) => Windows.UI.Color.FromArgb (
		(byte) Math.Clamp (color.A * 255, 0, 255),
		(byte) Math.Clamp (color.R * 255, 0, 255),
		(byte) Math.Clamp (color.G * 255, 0, 255),
		(byte) Math.Clamp (color.B * 255, 0, 255));

	private static FrameworkElement CreateSeedRow (EffectData data, MemberInfo member, string caption)
	{
		StackPanel row = new () { Orientation = Orientation.Horizontal, Spacing = 8 };
		row.Children.Add (new TextBlock { Text = caption, VerticalAlignment = VerticalAlignment.Center });
		Button reseed = new () { Content = "Reseed" };
		Random random = new ();
		reseed.Click += (_, _) => SetValue (data, member, new RandomSeed (random.Next ()));
		row.Children.Add (reseed);
		return row;
	}
}
