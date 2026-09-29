using System;
using System.Collections.Generic;
using CodeBrix.Plotter;
using CodeBrix.Plotter.Axes;
using CodeBrix.Plotter.Series;

namespace CodeBrix.Platform.UI.AddIn.PlotterView.UIReqs.Support;

/// <summary>
/// The plots a scenario can ask for by name. Nothing here is loaded from disk: a feature file
/// writes <c>the Model of "plot" is set to "Line"</c> and this class builds that plot in C#.
/// <para>
/// Every model is deliberately small, deliberately flat and deliberately loud - a dozen points,
/// fat strokes and saturated explicit colours rather than the engine's own colour cycle - so
/// that a frame is cheap to render and a colour assertion has something unambiguous to find.
/// The line models draw one wave across an x axis of 0 to 10 with a y axis of -2 to 2. The wave
/// crosses zero at x = 5, so the data point at x = 5 sits exactly at the centre of the plot
/// area - that is the point a finger lands on when a scenario touches the middle of it - and
/// because the wave is not a straight line, zooming or panning the axes visibly redraws it
/// rather than sliding the same picture along itself.
/// </para>
/// </summary>
public static class PlotterModels
{
	private static readonly string[] BarCategories = ["One", "Two", "Three", "Four"];

	/// <summary>A red line across a bounded pair of linear axes.</summary>
	public const string Line = "Line";

	/// <summary>The <see cref="Line"/> plot on an opaque yellow model background.</summary>
	public const string LineOnYellow = "Line on Yellow";

	/// <summary>The <see cref="Line"/> plot with every piece of its text in magenta.</summary>
	public const string LineInMagenta = "Line in Magenta";

	/// <summary>A red line whose x axis takes its range from the data rather than from a limit.</summary>
	public const string GrowingLine = "Growing Line";

	/// <summary>Blue bars against a category axis.</summary>
	public const string Bar = "Bar";

	/// <summary>Two clusters of markers in two colours.</summary>
	public const string Scatter = "Scatter";

	/// <summary>Four slices, each in a colour of its own.</summary>
	public const string Pie = "Pie";

	/// <summary>The colour the line models draw their first series in: opaque red.</summary>
	public static PlotterColor SeriesColor => PlotterColor.FromArgb(0xFF, 0xFF, 0x00, 0x00);

	/// <summary>The colour <see cref="LineOnYellow"/> paints its background: opaque yellow.</summary>
	public static PlotterColor BackgroundColor => PlotterColor.FromArgb(0xFF, 0xFF, 0xFF, 0x00);

	/// <summary>The colour <see cref="LineInMagenta"/> draws its text in: opaque magenta.</summary>
	public static PlotterColor TextColor => PlotterColor.FromArgb(0xFF, 0xFF, 0x00, 0xFF);

	/// <summary>The names a feature file may write, in the order they are declared.</summary>
	public static IReadOnlyCollection<string> Names => new[]
	{
		Line, LineOnYellow, LineInMagenta, GrowingLine, Bar, Scatter, Pie,
	};

	/// <summary>Builds the plot a scenario named.</summary>
	/// <param name="name">The model name, as a feature file writes it.</param>
	/// <returns>A new plot, attached to nothing.</returns>
	/// <exception cref="NotSupportedException">No model of that name is built here.</exception>
	public static PlotModel Build(string name)
	{
		ArgumentException.ThrowIfNullOrEmpty(name);

		if (Matches(name, Line))
		{
			return BuildLine();
		}

		if (Matches(name, LineOnYellow))
		{
			var model = BuildLine();
			model.Background = BackgroundColor;
			return model;
		}

		if (Matches(name, LineInMagenta))
		{
			var model = BuildLine();
			model.TextColor = TextColor;
			return model;
		}

		if (Matches(name, GrowingLine))
		{
			return BuildGrowingLine();
		}

		if (Matches(name, Bar))
		{
			return BuildBar();
		}

		if (Matches(name, Scatter))
		{
			return BuildScatter();
		}

		if (Matches(name, Pie))
		{
			return BuildPie();
		}

		throw new NotSupportedException(
			$"There is no plot called \"{name}\". The models are: {string.Join(", ", Names)}.");
	}

	/// <summary>
	/// Adds one point far to the right of the first line series of a plot. This is the mutation
	/// half of the engine's documented "change the data, then invalidate" contract, and it is
	/// only meaningful on a model whose x axis takes its range from the data.
	/// </summary>
	/// <param name="model">The plot to mutate. Call this under the model's own lock.</param>
	/// <exception cref="NotSupportedException">The plot's first series is not a line.</exception>
	public static void AddAPointBeyondTheData(PlotModel model)
	{
		ArgumentNullException.ThrowIfNull(model);

		if (model.Series.Count == 0 || model.Series[0] is not LineSeries series)
		{
			throw new NotSupportedException(
				"The plot's first series is not a LineSeries, so there is no line to add a point to.");
		}

		var lastX = series.Points.Count == 0 ? 0 : series.Points[series.Points.Count - 1].X;
		series.Points.Add(new DataPoint(lastX + 10, 0));
	}

	private static bool Matches(string name, string known) =>
		string.Equals(name, known, StringComparison.OrdinalIgnoreCase);

	private static PlotModel BuildLine()
	{
		var model = new PlotModel { Title = "Signal" };
		model.Axes.Add(new LinearAxis
		{
			Position = AxisPosition.Bottom,
			Minimum = 0,
			Maximum = 10,
			MajorGridlineStyle = LineStyle.Solid,
		});
		model.Axes.Add(new LinearAxis
		{
			Position = AxisPosition.Left,
			Minimum = -2,
			Maximum = 2,
		});
		model.Series.Add(Wave());
		return model;
	}

	private static PlotModel BuildGrowingLine()
	{
		// No Minimum and no Maximum on the x axis: its range follows the data, which is what
		// makes "the range grew" a fact about the mutation rather than about the axis limits.
		var model = new PlotModel { Title = "Growing Signal" };
		model.Axes.Add(new LinearAxis
		{
			Position = AxisPosition.Bottom,
			MajorGridlineStyle = LineStyle.Solid,
		});
		model.Axes.Add(new LinearAxis
		{
			Position = AxisPosition.Left,
			Minimum = -2,
			Maximum = 2,
		});
		model.Series.Add(Wave());
		return model;
	}

	private static LineSeries Wave()
	{
		var series = new LineSeries
		{
			Title = "A",
			Color = SeriesColor,
			StrokeThickness = 10,
			MarkerType = MarkerType.None,
		};

		// One full wave over the axis, crossing zero at x = 5: eleven points, no random seed,
		// and the same picture in every run on every panel. The values are rounded so that the
		// crossing is exactly zero rather than the sine of pi - the data point at x = 5 is what
		// a finger lands on at the centre of the plot area, and the tracker prints its value.
		for (var x = 0; x <= 10; x++)
		{
			series.Points.Add(new DataPoint(x, Math.Round(1.6 * Math.Sin(Math.PI * x / 5.0), 4)));
		}

		return series;
	}

	private static PlotModel BuildBar()
	{
		var model = new PlotModel { Title = "Bars" };

		var categories = new CategoryAxis { Position = AxisPosition.Left };
		categories.Labels.AddRange(BarCategories);
		model.Axes.Add(categories);
		model.Axes.Add(new LinearAxis
		{
			Position = AxisPosition.Bottom,
			Minimum = 0,
			Maximum = 50,
			MajorGridlineStyle = LineStyle.Solid,
		});

		var series = new BarSeries
		{
			FillColor = PlotterColor.FromArgb(0xFF, 0x00, 0x00, 0xFF),
			StrokeThickness = 0,
		};
		series.Items.Add(new BarItem { Value = 42 });
		series.Items.Add(new BarItem { Value = 31 });
		series.Items.Add(new BarItem { Value = 17 });
		series.Items.Add(new BarItem { Value = 38 });
		model.Series.Add(series);
		return model;
	}

	private static PlotModel BuildScatter()
	{
		var model = new PlotModel { Title = "Clusters" };
		model.Axes.Add(new LinearAxis
		{
			Position = AxisPosition.Bottom,
			Minimum = 0,
			Maximum = 10,
		});
		model.Axes.Add(new LinearAxis
		{
			Position = AxisPosition.Left,
			Minimum = 0,
			Maximum = 10,
		});

		model.Series.Add(Cluster("Alpha", PlotterColor.FromArgb(0xFF, 0xFF, 0x00, 0x00), 3, 3));
		model.Series.Add(Cluster("Beta", PlotterColor.FromArgb(0xFF, 0x00, 0x00, 0xFF), 7, 7));
		return model;
	}

	private static ScatterSeries Cluster(string title, PlotterColor fill, double centreX, double centreY)
	{
		var series = new ScatterSeries
		{
			Title = title,
			MarkerType = MarkerType.Square,
			MarkerSize = 8,
			MarkerFill = fill,
		};

		// A fixed lattice rather than a random cloud: the same twelve markers land in the same
		// places in every run, which is what lets a pixel assertion be about the plot rather
		// than about a seed.
		for (var row = 0; row < 3; row++)
		{
			for (var column = 0; column < 4; column++)
			{
				series.Points.Add(new ScatterPoint(
					centreX + ((column - 1.5) * 0.6),
					centreY + ((row - 1.0) * 0.6)));
			}
		}

		return series;
	}

	private static PlotModel BuildPie()
	{
		var model = new PlotModel { Title = "Shares" };
		var series = new PieSeries { StrokeThickness = 0, InsideLabelPosition = 0.7 };
		series.Slices.Add(new PieSlice("Red", 40) { Fill = PlotterColor.FromArgb(0xFF, 0xFF, 0x00, 0x00) });
		series.Slices.Add(new PieSlice("Blue", 30) { Fill = PlotterColor.FromArgb(0xFF, 0x00, 0x00, 0xFF) });
		series.Slices.Add(new PieSlice("Lime", 20) { Fill = PlotterColor.FromArgb(0xFF, 0x00, 0xFF, 0x00) });
		series.Slices.Add(new PieSlice("Cyan", 10) { Fill = PlotterColor.FromArgb(0xFF, 0x00, 0xFF, 0xFF) });
		model.Series.Add(series);
		return model;
	}
}
