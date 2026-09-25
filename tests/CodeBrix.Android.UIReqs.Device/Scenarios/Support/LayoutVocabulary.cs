using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Windows.UI;
using XamlCanvas = Microsoft.UI.Xaml.Controls.Canvas;

namespace CodeBrix.Platform.UI.Core.UIReqs.Support;

/// <summary>
/// The nouns and adjectives the Layout scenarios need: the panels and shapes a feature file may
/// ask for, and the properties it may set on them.
/// <para>
/// Registration goes through <see cref="ElementFactory.RegisterKind"/> and
/// <see cref="ElementFactory.RegisterProperty"/> rather than through the factory's
/// <c>RegisterAdditionalKinds</c> partial method, because a partial method can only ever have
/// one implementation and two coverage groups adding to the same table at the same time would
/// collide on it. <see cref="Ensure"/> is idempotent and is called from a scenario hook.
/// </para>
/// </summary>
public static class LayoutVocabulary
{
	/// <summary>What separates a radial gradient's stop list from its gradient origin.</summary>
	private const string FromKeyword = " from ";

	private static readonly object RegistrationLock = new();

	private static bool _registered;

	/// <summary>Registers the Layout kinds and properties, once per process.</summary>
	public static void Ensure()
	{
		lock (RegistrationLock)
		{
			if (_registered)
			{
				return;
			}

			_registered = true;
			RegisterKinds();
			RegisterProperties();
		}
	}

	private static void RegisterKinds()
	{
		ElementFactory.RegisterKind("Canvas", () => new XamlCanvas());
		ElementFactory.RegisterKind("Rectangle", () => new Rectangle());
		ElementFactory.RegisterKind("Ellipse", () => new Ellipse());
		ElementFactory.RegisterKind("Line", () => new Line());
		ElementFactory.RegisterKind("Path", () => new Path());
		ElementFactory.RegisterKind("ContentControl", () => new ContentControl());
		ElementFactory.RegisterKind("MeasureProbe", () => new MeasureProbe());
	}

	private static void RegisterProperties()
	{
		// Grid
		ElementFactory.RegisterProperty("Rows", (element, value) => SetRows(element, value));
		ElementFactory.RegisterProperty("Columns", (element, value) => SetColumns(element, value));
		ElementFactory.RegisterProperty("Row", (element, value) => Grid.SetRow(element, ToInt(value)));
		ElementFactory.RegisterProperty("Column", (element, value) => Grid.SetColumn(element, ToInt(value)));
		ElementFactory.RegisterProperty("RowSpan", (element, value) => Grid.SetRowSpan(element, ToInt(value)));
		ElementFactory.RegisterProperty("ColumnSpan", (element, value) => Grid.SetColumnSpan(element, ToInt(value)));

		// StackPanel
		ElementFactory.RegisterProperty("Orientation", (element, value) => SetOrientation(element, value));
		ElementFactory.RegisterProperty("Spacing", (element, value) => SetSpacing(element, value));

		// Canvas, on the child
		ElementFactory.RegisterProperty("Left", (element, value) => XamlCanvas.SetLeft(element, GherkinValue.ToDouble(value)));
		ElementFactory.RegisterProperty("Top", (element, value) => XamlCanvas.SetTop(element, GherkinValue.ToDouble(value)));
		ElementFactory.RegisterProperty("ZIndex", (element, value) => XamlCanvas.SetZIndex(element, ToInt(value)));

		// Border
		ElementFactory.RegisterProperty("BorderBrush", (element, value) => SetBorderBrush(element, value));
		ElementFactory.RegisterProperty("BorderThickness", (element, value) => SetBorderThickness(element, value));
		ElementFactory.RegisterProperty("CornerRadius", (element, value) => SetCornerRadius(element, value));

		// Shapes
		ElementFactory.RegisterProperty("Fill", (element, value) => SetFill(element, new SolidColorBrush(Colors.Parse(value))));
		ElementFactory.RegisterProperty("Stroke", (element, value) => Shape(element, "Stroke").Stroke = new SolidColorBrush(Colors.Parse(value)));
		ElementFactory.RegisterProperty("StrokeThickness", (element, value) => Shape(element, "StrokeThickness").StrokeThickness = GherkinValue.ToDouble(value));
		ElementFactory.RegisterProperty("Data", (element, value) => SetPathData(element, value));
		ElementFactory.RegisterProperty("X1", (element, value) => LineOf(element).X1 = GherkinValue.ToDouble(value));
		ElementFactory.RegisterProperty("Y1", (element, value) => LineOf(element).Y1 = GherkinValue.ToDouble(value));
		ElementFactory.RegisterProperty("X2", (element, value) => LineOf(element).X2 = GherkinValue.ToDouble(value));
		ElementFactory.RegisterProperty("Y2", (element, value) => LineOf(element).Y2 = GherkinValue.ToDouble(value));

		// Brushes
		ElementFactory.RegisterProperty("HorizontalGradient",
			(element, value) => SetFill(element, LinearGradient(value, horizontal: true)));
		ElementFactory.RegisterProperty("VerticalGradient",
			(element, value) => SetFill(element, LinearGradient(value, horizontal: false)));
		ElementFactory.RegisterProperty("RadialGradient",
			(element, value) => SetFill(element, RadialGradient(value)));

		// Transforms and clipping
		ElementFactory.RegisterProperty("RenderTransform", (element, value) => element.RenderTransform = ToTransform(value));
		ElementFactory.RegisterProperty("RenderTransformOrigin", (element, value) => element.RenderTransformOrigin = ToPoint(value));
		ElementFactory.RegisterProperty("Clip", (element, value) => element.Clip = ToClip(value));

		// Content alignment, on a ContentControl. It is a Control property rather than a
		// FrameworkElement one, so it is registered for that type: an element that is not a
		// Control is then told it has no such property instead of silently ignoring it.
		ElementFactory.RegisterProperty<Control>("HorizontalContentAlignment",
			(control, value) => control.HorizontalContentAlignment = ToHorizontalAlignment(value));
		ElementFactory.RegisterProperty<Control>("VerticalContentAlignment",
			(control, value) => control.VerticalContentAlignment = ToVerticalAlignment(value));
	}

	private static HorizontalAlignment ToHorizontalAlignment(string value) =>
		GherkinValue.ToEnum<HorizontalAlignment>(value);

	private static VerticalAlignment ToVerticalAlignment(string value) =>
		GherkinValue.ToEnum<VerticalAlignment>(value);

	private static int ToInt(string value) => (int) Math.Round(GherkinValue.ToDouble(value));

	private static Grid GridOf(FrameworkElement element) => element as Grid
		?? throw Unsupported(element, "row and column definitions");

	private static Line LineOf(FrameworkElement element) => element as Line
		?? throw Unsupported(element, "line end points");

	private static Microsoft.UI.Xaml.Shapes.Shape Shape(FrameworkElement element, string property) =>
		element as Microsoft.UI.Xaml.Shapes.Shape ?? throw Unsupported(element, property);

	private static void SetRows(FrameworkElement element, string value)
	{
		var grid = GridOf(element);
		grid.RowDefinitions.Clear();
		foreach (var length in ToGridLengths(value))
		{
			grid.RowDefinitions.Add(new RowDefinition { Height = length });
		}
	}

	private static void SetColumns(FrameworkElement element, string value)
	{
		var grid = GridOf(element);
		grid.ColumnDefinitions.Clear();
		foreach (var length in ToGridLengths(value))
		{
			grid.ColumnDefinitions.Add(new ColumnDefinition { Width = length });
		}
	}

	private static IEnumerable<GridLength> ToGridLengths(string value)
	{
		foreach (var part in value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
		{
			yield return ToGridLength(part);
		}
	}

	private static GridLength ToGridLength(string text)
	{
		if (string.Equals(text, "Auto", StringComparison.OrdinalIgnoreCase))
		{
			return GridLength.Auto;
		}

		if (text.EndsWith('*'))
		{
			var factor = text.Length == 1 ? 1.0 : GherkinValue.ToDouble(text[..^1]);
			return new GridLength(factor, GridUnitType.Star);
		}

		return new GridLength(GherkinValue.ToDouble(text), GridUnitType.Pixel);
	}

	private static void SetOrientation(FrameworkElement element, string value)
	{
		var orientation = GherkinValue.ToEnum<Orientation>(value);
		switch (element)
		{
			case StackPanel stack:
				stack.Orientation = orientation;
				break;
			default:
				throw Unsupported(element, "Orientation");
		}
	}

	private static void SetSpacing(FrameworkElement element, string value)
	{
		var spacing = GherkinValue.ToDouble(value);
		switch (element)
		{
			case StackPanel stack:
				stack.Spacing = spacing;
				break;
			default:
				throw Unsupported(element, "Spacing");
		}
	}

	private static void SetBorderBrush(FrameworkElement element, string value)
	{
		var brush = new SolidColorBrush(Colors.Parse(value));
		switch (element)
		{
			case Border border:
				border.BorderBrush = brush;
				break;
			case Control control:
				control.BorderBrush = brush;
				break;
			default:
				throw Unsupported(element, "BorderBrush");
		}
	}

	private static void SetBorderThickness(FrameworkElement element, string value)
	{
		var thickness = GherkinValue.ToThickness(value);
		switch (element)
		{
			case Border border:
				border.BorderThickness = thickness;
				break;
			case Control control:
				control.BorderThickness = thickness;
				break;
			default:
				throw Unsupported(element, "BorderThickness");
		}
	}

	private static void SetCornerRadius(FrameworkElement element, string value)
	{
		var radius = new CornerRadius(GherkinValue.ToDouble(value));
		switch (element)
		{
			case Border border:
				border.CornerRadius = radius;
				break;
			case Control control:
				control.CornerRadius = radius;
				break;
			default:
				throw Unsupported(element, "CornerRadius");
		}
	}

	private static void SetFill(FrameworkElement element, Brush brush)
	{
		switch (element)
		{
			case Microsoft.UI.Xaml.Shapes.Shape shape:
				shape.Fill = brush;
				break;
			case Border border:
				border.Background = brush;
				break;
			case Panel panel:
				panel.Background = brush;
				break;
			case Control control:
				control.Background = brush;
				break;
			default:
				throw Unsupported(element, "a fill");
		}
	}

	private static LinearGradientBrush LinearGradient(string value, bool horizontal)
	{
		var parts = value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
		if (parts.Length != 2)
		{
			throw new FormatException(
				$"\"{value}\" is not a gradient. Write two colours separated by a comma, such as \"Red,Blue\".");
		}

		var brush = new LinearGradientBrush
		{
			StartPoint = new Point(0, 0),
			EndPoint = horizontal ? new Point(1, 0) : new Point(0, 1),
		};
		brush.GradientStops.Add(new GradientStop { Color = Colors.Parse(parts[0]), Offset = 0.0 });
		brush.GradientStops.Add(new GradientStop { Color = Colors.Parse(parts[1]), Offset = 1.0 });
		return brush;
	}

	/// <summary>
	/// Reads a radial gradient: a comma-separated list of stops, each a colour with an optional
	/// "@offset", followed by an optional " from x,y" that moves the gradient origin off the
	/// centre. A stop written without an offset takes its place evenly along the run, so the
	/// first is at 0 and the last at 1 - "Red,Blue" is Red at the origin fading to Blue at the
	/// ellipse. Center stays 0.5,0.5 and both radii stay 0.5, so the painted ellipse is the
	/// element's own bounding box and the device radius is half the element.
	/// </summary>
	private static RadialGradientBrush RadialGradient(string value)
	{
		var stops = value;
		var origin = new Point(0.5, 0.5);

		var from = value.IndexOf(FromKeyword, StringComparison.OrdinalIgnoreCase);
		if (from >= 0)
		{
			stops = value[..from];
			var (x, y) = ToPair(value[(from + FromKeyword.Length)..]);
			origin = new Point(x, y);
		}

		var parts = stops.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
		if (parts.Length < 2)
		{
			throw new FormatException(
				$"\"{value}\" is not a radial gradient. Write two or more colours separated by commas, "
				+ "each with an optional \"@offset\", and an optional \" from x,y\" for the gradient "
				+ "origin - such as \"Red,Blue\" or \"Red,Lime@0.25,Blue@0.75,White from 0.2,0.5\".");
		}

		var brush = new RadialGradientBrush
		{
			Center = new Point(0.5, 0.5),
			RadiusX = 0.5,
			RadiusY = 0.5,
			GradientOrigin = origin,
		};

		for (var index = 0; index < parts.Length; index++)
		{
			brush.GradientStops.Add(ToGradientStop(parts[index], index, parts.Length));
		}

		return brush;
	}

	private static GradientStop ToGradientStop(string text, int index, int count)
	{
		var at = text.IndexOf('@', StringComparison.Ordinal);
		var name = at < 0 ? text : text[..at].TrimEnd();
		var offset = at < 0
			? index / (double) (count - 1)
			: GherkinValue.ToDouble(text[(at + 1)..]);

		return new GradientStop { Color = Colors.Parse(name), Offset = offset };
	}

	private static Transform? ToTransform(string value)
	{
		if (string.Equals(value, "None", StringComparison.OrdinalIgnoreCase))
		{
			return null;
		}

		var space = value.IndexOf(' ', StringComparison.Ordinal);
		if (space <= 0)
		{
			throw new FormatException(
				$"\"{value}\" is not a transform. Write \"Translate 60,40\", \"Scale 2,1.5\", \"Rotate 30\" or \"None\".");
		}

		var kind = value[..space];
		var argument = value[(space + 1)..].Trim();

		if (string.Equals(kind, "Translate", StringComparison.OrdinalIgnoreCase))
		{
			var (x, y) = ToPair(argument);
			return new TranslateTransform { X = x, Y = y };
		}

		if (string.Equals(kind, "Scale", StringComparison.OrdinalIgnoreCase))
		{
			var (x, y) = ToPair(argument);
			return new ScaleTransform { ScaleX = x, ScaleY = y };
		}

		if (string.Equals(kind, "Rotate", StringComparison.OrdinalIgnoreCase))
		{
			return new RotateTransform { Angle = GherkinValue.ToDouble(argument) };
		}

		throw new FormatException(
			$"\"{value}\" is not a transform. Write \"Translate 60,40\", \"Scale 2,1.5\", \"Rotate 30\" or \"None\".");
	}

	private static RectangleGeometry? ToClip(string value)
	{
		if (string.Equals(value, "None", StringComparison.OrdinalIgnoreCase))
		{
			return null;
		}

		var parts = value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
		if (parts.Length != 4)
		{
			throw new FormatException(
				$"\"{value}\" is not a clip. Write \"x,y,width,height\" in the element's own coordinates, or \"None\".");
		}

		return new RectangleGeometry
		{
			Rect = new Rect(
				GherkinValue.ToDouble(parts[0]),
				GherkinValue.ToDouble(parts[1]),
				GherkinValue.ToDouble(parts[2]),
				GherkinValue.ToDouble(parts[3])),
		};
	}

	private static Point ToPoint(string value)
	{
		var (x, y) = ToPair(value);
		return new Point(x, y);
	}

	private static (double X, double Y) ToPair(string value)
	{
		var parts = value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
		return parts.Length switch
		{
			1 => (GherkinValue.ToDouble(parts[0]), GherkinValue.ToDouble(parts[0])),
			2 => (GherkinValue.ToDouble(parts[0]), GherkinValue.ToDouble(parts[1])),
			_ => throw new FormatException($"\"{value}\" is not a pair of numbers."),
		};
	}

	// A deliberately tiny subset of the path mini-language: absolute move, absolute line and
	// close. It is enough for the shapes a requirement talks about and it fails loudly rather
	// than quietly drawing something else.
	private static void SetPathData(FrameworkElement element, string value)
	{
		var path = element as Path ?? throw Unsupported(element, "Data");
		var tokens = value.Split(' ', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
		var geometry = new PathGeometry();
		PathFigure? figure = null;

		for (var index = 0; index < tokens.Length; index++)
		{
			var token = tokens[index];
			if (string.Equals(token, "M", StringComparison.OrdinalIgnoreCase))
			{
				figure = new PathFigure { StartPoint = ToPoint(tokens[++index]) };
				geometry.Figures.Add(figure);
			}
			else if (string.Equals(token, "L", StringComparison.OrdinalIgnoreCase))
			{
				Figure(figure, value).Segments.Add(new LineSegment { Point = ToPoint(tokens[++index]) });
			}
			else if (string.Equals(token, "Z", StringComparison.OrdinalIgnoreCase))
			{
				Figure(figure, value).IsClosed = true;
			}
			else
			{
				throw new FormatException(
					$"\"{token}\" in \"{value}\" is not path data this harness reads. Write M, L and Z with absolute points.");
			}
		}

		path.Data = geometry;
	}

	private static PathFigure Figure(PathFigure? figure, string value) => figure
		?? throw new FormatException($"\"{value}\" does not start with an M, so it has no figure to draw into.");

	private static NotSupportedException Unsupported(FrameworkElement element, string property) =>
		new(string.Create(CultureInfo.InvariantCulture,
			$"A {element.GetType().Name} named \"{element.Name}\" has no {property} the harness can set."));
}

/// <summary>
/// A panel that remembers the size it was last offered, so a requirement can state what a
/// container measured its content with rather than only what it arranged it at.
/// <para>
/// Measure and arrange can disagree: a container that offers an unbounded width still arranges
/// its child at the real width, so every pixel assertion passes while content that lays itself
/// out from the width it is offered - a wrapping panel, a tool bar deciding its overflow - never
/// sees a limit. Nothing in the visual tree records that, which is why this probe exists.
/// </para>
/// </summary>
public sealed class MeasureProbe : Grid
{
	/// <summary>Gets the width this element was last offered, in logical pixels.</summary>
	/// <remarks><see cref="double.PositiveInfinity"/> when the offer was unbounded, and
	/// <see cref="double.NaN"/> before the first measure.</remarks>
	public double LastOfferedWidth { get; private set; } = double.NaN;

	/// <summary>Gets the height this element was last offered, in logical pixels.</summary>
	public double LastOfferedHeight { get; private set; } = double.NaN;

	/// <summary>Gets how many times the element has been measured.</summary>
	public int MeasureCount { get; private set; }

	/// <inheritdoc/>
	protected override Size MeasureOverride(Size availableSize)
	{
		LastOfferedWidth = availableSize.Width;
		LastOfferedHeight = availableSize.Height;
		MeasureCount++;

		return base.MeasureOverride(availableSize);
	}
}
