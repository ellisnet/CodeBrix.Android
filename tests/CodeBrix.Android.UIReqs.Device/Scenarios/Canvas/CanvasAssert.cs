using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CodeBrix.Platform.UI.Core.UIReqs.Hosting;
using CodeBrix.Android.UIReqs.Device.TestTarget;
using SilverAssertions;
using Windows.UI;
using SKColor = CodeBrix.Android.UIReqs.Device.Canvas.PixelColor;

namespace CodeBrix.Platform.UI.Core.UIReqs.Canvas;

/// <summary>
/// The vocabulary the scenarios speak about pixels. Geometry comes from the visual tree,
/// appearance from these primitives, and change from two frames - a step never reads pixels
/// itself, and a new primitive is added here rather than written inline in a step.
/// <para>
/// The class is deliberately <c>partial</c>: a later coverage group adds its primitives in
/// <c>CanvasAssert.&lt;Group&gt;.cs</c> without touching this file.
/// </para>
/// </summary>
public static partial class CanvasAssert
{
	/// <summary>The share of a region that must match for it to count as uniformly one colour.</summary>
	public const double UniformFraction = 0.99;

	/// <summary>The share of a region a colour must reach for the region to "contain" it.</summary>
	public const double ContainsFraction = 0.02;

	/// <summary>The share of a region a colour may reach and still count as absent.</summary>
	public const double AbsentFraction = 0.005;

	/// <summary>The share of a region that must be ink for the region to count as having any.</summary>
	public const double HasInkFraction = 0.002;

	/// <summary>The share of pixels that may differ and still count as the same picture.</summary>
	public const double SameFraction = 0.001;

	/// <summary>The share of pixels that must differ for two frames to count as different.</summary>
	public const double DifferentFraction = 0.01;

	/// <summary>
	/// The panel's background colour: what "blank" looks like, and what ink is measured against.
	/// </summary>
	public static Color Background =>
		VirtualApplication.Running?.BackgroundColor ?? Color.FromArgb(0xFF, 0xFF, 0xFF, 0xFF);

	// ---------------------------------------------------------------- queries

	/// <summary>What share of a region's pixels match a colour.</summary>
	/// <param name="region">The region to count.</param>
	/// <param name="color">The colour to look for.</param>
	/// <returns>A share between 0 and 1.</returns>
	public static double FractionMatching(Region region, Color color)
	{
		ArgumentNullException.ThrowIfNull(region);

		var background = Background;
		var matches = region.Pixels().Count(pixel => ColorMatch.Matches(pixel, color, background));
		return matches / (double) region.PixelCount;
	}

	/// <summary>
	/// What share of a region is ink - that is, differs from the panel background by more than
	/// the colour tolerance.
	/// </summary>
	/// <param name="region">The region to count.</param>
	/// <returns>A share between 0 and 1.</returns>
	public static double InkFraction(Region region)
	{
		ArgumentNullException.ThrowIfNull(region);
		return 1.0 - FractionMatching(region, Background);
	}

	/// <summary>The tight bounding box of a region's ink, in frame coordinates.</summary>
	/// <param name="region">The region to search.</param>
	/// <param name="bounds">The bounding box, when there is any ink.</param>
	/// <returns><c>true</c> when the region holds ink.</returns>
	public static bool TryInkBounds(Region region, out DeviceRect bounds)
	{
		ArgumentNullException.ThrowIfNull(region);

		var background = Background;
		var left = int.MaxValue;
		var top = int.MaxValue;
		var right = int.MinValue;
		var bottom = int.MinValue;

		for (var y = region.Bounds.Y; y < region.Bounds.Bottom; y++)
		{
			for (var x = region.Bounds.X; x < region.Bounds.Right; x++)
			{
				if (ColorMatch.Matches(region.Frame.GetPixel(x, y), background, background))
				{
					continue;
				}

				left = Math.Min(left, x);
				top = Math.Min(top, y);
				right = Math.Max(right, x);
				bottom = Math.Max(bottom, y);
			}
		}

		if (right < left)
		{
			bounds = default;
			return false;
		}

		bounds = new DeviceRect(left, top, right - left + 1, bottom - top + 1);
		return true;
	}

	/// <summary>
	/// The colour of a region's ink: quantized to four bits per channel and taken as the mode,
	/// so that the antialiased minority at a glyph's edge cannot outvote the glyph's core.
	/// </summary>
	/// <param name="region">The region to measure.</param>
	/// <param name="color">The ink colour, when there is any ink.</param>
	/// <returns><c>true</c> when the region holds ink.</returns>
	public static bool TryInkColor(Region region, out SKColor color)
	{
		ArgumentNullException.ThrowIfNull(region);

		var background = Background;
		var counts = new Dictionary<uint, int>();
		var representatives = new ModeColor(); // ANDROID PORT (fix): see ModeColor.

		foreach (var pixel in region.Pixels())
		{
			if (ColorMatch.Matches(pixel, background, background))
			{
				continue;
			}

			var composited = ColorMatch.Composite(pixel, background);
			var key = (uint) ColorMatch.Quantize(composited);
			counts.TryGetValue(key, out var count);
			counts[key] = count + 1;
			representatives.Add(key, composited);
		}

		if (counts.Count == 0)
		{
			color = default;
			return false;
		}

		var mode = counts.OrderByDescending(pair => pair.Value).First().Key;
		color = representatives.Of(mode);
		return true;
	}

	/// <summary>What share of two regions' pixels differ from each other.</summary>
	/// <param name="region">One region.</param>
	/// <param name="other">The other region, which must be the same size.</param>
	/// <returns>A share between 0 and 1.</returns>
	public static double FractionDiffering(Region region, Region other)
	{
		ArgumentNullException.ThrowIfNull(region);
		ArgumentNullException.ThrowIfNull(other);

		if (region.Bounds.Width != other.Bounds.Width || region.Bounds.Height != other.Bounds.Height)
		{
			throw new InvalidOperationException(
				$"Two regions can only be compared when they are the same size: "
				+ $"{region.Description} is {region.Bounds} and {other.Description} is {other.Bounds}.");
		}

		var different = 0;
		for (var y = 0; y < region.Bounds.Height; y++)
		{
			for (var x = 0; x < region.Bounds.Width; x++)
			{
				if (!ColorMatch.Matches(region[x, y], other[x, y]))
				{
					different++;
				}
			}
		}

		return different / (double) region.PixelCount;
	}

	// ------------------------------------------------------------ assertions

	/// <summary>Asserts that a region is painted one colour throughout.</summary>
	/// <param name="region">The region to check.</param>
	/// <param name="color">The colour it must be.</param>
	/// <param name="minFraction">How much of it must match; the default is 99%.</param>
	public static void IsUniformly(this Region region, Color color, double minFraction = UniformFraction)
	{
		var fraction = FractionMatching(region, color);
		if (fraction < minFraction)
		{
			fraction.Should().BeGreaterThanOrEqualTo(minFraction, "{0}", Explain(region,
				$"{region.Description} must be uniformly {ColorMatch.Describe(color)}, but only "
				+ Percent(fraction) + " of its pixels match"));
		}
	}

	/// <summary>Asserts that a colour is present in a region.</summary>
	/// <param name="region">The region to check.</param>
	/// <param name="color">The colour that must be present.</param>
	/// <param name="minFraction">How much of the region it must cover; the default is 2%.</param>
	public static void Contains(this Region region, Color color, double minFraction = ContainsFraction)
	{
		var fraction = FractionMatching(region, color);
		if (fraction < minFraction)
		{
			fraction.Should().BeGreaterThanOrEqualTo(minFraction, "{0}", Explain(region,
				$"{region.Description} must contain {ColorMatch.Describe(color)}, but only "
				+ Percent(fraction) + " of its pixels match"));
		}
	}

	/// <summary>Asserts that a colour is absent from a region.</summary>
	/// <param name="region">The region to check.</param>
	/// <param name="color">The colour that must be absent.</param>
	/// <param name="maxFraction">How much of the region it may cover; the default is 0.5%.</param>
	public static void DoesNotContain(this Region region, Color color, double maxFraction = AbsentFraction)
	{
		var fraction = FractionMatching(region, color);
		if (fraction > maxFraction)
		{
			fraction.Should().BeLessThanOrEqualTo(maxFraction, "{0}", Explain(region,
				$"{region.Description} must not contain {ColorMatch.Describe(color)}, but "
				+ Percent(fraction) + " of its pixels match"));
		}
	}

	/// <summary>Asserts what colour a region's ink is.</summary>
	/// <param name="region">The region to check.</param>
	/// <param name="color">The colour the ink must be.</param>
	public static void InkColorIs(this Region region, Color color)
	{
		if (!TryInkColor(region, out var ink))
		{
			"no ink".Should().Be(ColorMatch.Describe(color), "{0}", Explain(region,
				$"{region.Description} must have ink of {ColorMatch.Describe(color)}, but every "
				+ "pixel of it is the panel background"));
			return;
		}

		if (!ColorMatch.Matches(ink, color, Background))
		{
			ColorMatch.Describe(ink).Should().Be(ColorMatch.Describe(color), "{0}", Explain(region,
				$"the ink of {region.Description} must be {ColorMatch.Describe(color)}"));
		}
	}

	/// <summary>Asserts that a region has ink - something was drawn there.</summary>
	/// <param name="region">The region to check.</param>
	/// <param name="minFraction">How much of it must be ink; the default is 0.2%.</param>
	public static void HasInk(this Region region, double minFraction = HasInkFraction)
	{
		var fraction = InkFraction(region);
		if (fraction < minFraction)
		{
			fraction.Should().BeGreaterThanOrEqualTo(minFraction, "{0}", Explain(region,
				$"{region.Description} must have ink, but only " + Percent(fraction)
				+ " of it differs from the panel background"));
		}
	}

	/// <summary>Asserts that a region is nothing but panel background.</summary>
	/// <param name="region">The region to check.</param>
	public static void IsBlank(this Region region)
	{
		var fraction = InkFraction(region);
		if (fraction > AbsentFraction)
		{
			fraction.Should().BeLessThanOrEqualTo(AbsentFraction, "{0}", Explain(region,
				$"{region.Description} must be blank, but " + Percent(fraction)
				+ " of it differs from the panel background"));
		}
	}

	/// <summary>The tight bounding box of a region's ink.</summary>
	/// <param name="region">The region to measure.</param>
	/// <returns>The bounding box, in frame coordinates.</returns>
	public static DeviceRect InkBounds(this Region region)
	{
		if (TryInkBounds(region, out var bounds))
		{
			return bounds;
		}

		true.Should().BeFalse("{0}", Explain(region,
			$"{region.Description} has no ink, so it has no ink bounds"));
		return default;
	}

	/// <summary>Asserts that two regions show the same picture.</summary>
	/// <param name="region">One region.</param>
	/// <param name="other">The other region, which must be the same size.</param>
	public static void SameAs(this Region region, Region other)
	{
		var fraction = FractionDiffering(region, other);
		if (fraction > SameFraction)
		{
			fraction.Should().BeLessThanOrEqualTo(SameFraction, "{0}", Explain(region,
				$"{region.Description} must be unchanged from {other}, but " + Percent(fraction)
				+ " of its pixels differ"));
		}
	}

	/// <summary>Asserts that two regions show different pictures.</summary>
	/// <param name="region">One region.</param>
	/// <param name="other">The other region, which must be the same size.</param>
	/// <param name="minFraction">How much must differ; the default is 1%.</param>
	public static void DiffersFrom(this Region region, Region other, double minFraction = DifferentFraction)
	{
		var fraction = FractionDiffering(region, other);
		if (fraction < minFraction)
		{
			fraction.Should().BeGreaterThanOrEqualTo(minFraction, "{0}", Explain(region,
				$"{region.Description} must differ from {other}, but only " + Percent(fraction)
				+ " of its pixels do"));
		}
	}

	/// <summary>
	/// Asserts that a region runs as a gradient: it starts at one colour, ends at another, and
	/// its middle lies between the two on every channel.
	/// </summary>
	/// <param name="region">The region to check.</param>
	/// <param name="start">The colour at the start of the axis.</param>
	/// <param name="end">The colour at the end of the axis.</param>
	/// <param name="axis">Which way the gradient runs.</param>
	public static void GradientRunsFrom(this Region region, Color start, Color end, GradientAxis axis)
	{
		ArgumentNullException.ThrowIfNull(region);

		var (first, middle, last) = SampleAlong(region, axis);

		if (!ColorMatch.Matches(first, start, Background))
		{
			ColorMatch.Describe(first).Should().Be(ColorMatch.Describe(start), "{0}", Explain(region,
				$"the {axis} gradient of {region.Description} must start at {ColorMatch.Describe(start)}"));
		}

		if (!ColorMatch.Matches(last, end, Background))
		{
			ColorMatch.Describe(last).Should().Be(ColorMatch.Describe(end), "{0}", Explain(region,
				$"the {axis} gradient of {region.Description} must end at {ColorMatch.Describe(end)}"));
		}

		var between = IsBetween(middle.Red, first.Red, last.Red)
			&& IsBetween(middle.Green, first.Green, last.Green)
			&& IsBetween(middle.Blue, first.Blue, last.Blue);
		if (!between)
		{
			between.Should().BeTrue("{0}", Explain(region,
				$"the middle of the {axis} gradient of {region.Description} is "
				+ $"{ColorMatch.Describe(middle)}, which does not lie between "
				+ $"{ColorMatch.Describe(first)} and {ColorMatch.Describe(last)}"));
		}
	}

	/// <summary>
	/// Asserts that nothing outside one rectangle repainted between two frames - the change a
	/// scenario made was the only change on the panel.
	/// </summary>
	/// <param name="frame">The later frame.</param>
	/// <param name="other">The earlier frame.</param>
	/// <param name="region">The rectangle that was allowed to change.</param>
	/// <param name="maxFraction">How much of the rest may differ; the default is 0.1%.</param>
	public static void UnchangedOutside(this TestFrame frame, TestFrame other, DeviceRect region,
		double maxFraction = SameFraction)
	{
		ArgumentNullException.ThrowIfNull(frame);
		ArgumentNullException.ThrowIfNull(other);

		var outside = 0;
		var different = 0;
		for (var y = 0; y < frame.Height; y++)
		{
			for (var x = 0; x < frame.Width; x++)
			{
				if (region.Contains(x, y))
				{
					continue;
				}

				outside++;
				if (!ColorMatch.Matches(frame.GetPixel(x, y), other.GetPixel(x, y)))
				{
					different++;
				}
			}
		}

		var fraction = outside == 0 ? 0.0 : different / (double) outside;
		if (fraction > maxFraction)
		{
			var whole = Region.WholeFrame(frame);
			fraction.Should().BeLessThanOrEqualTo(maxFraction, "{0}", Explain(whole,
				$"nothing outside {region} was allowed to repaint, but " + Percent(fraction)
				+ " of the pixels outside it differ from frame " + other.Sequence.ToString(CultureInfo.InvariantCulture)));
		}
	}

	// ------------------------------------------------------------- reporting

	/// <summary>
	/// The report the most recent failed assertion produced, so a hook can print it again
	/// after the scenario has ended.
	/// </summary>
	public static string? LastReport { get; private set; }

	/// <summary>Forgets the last failure report. The scenario hooks call this per scenario.</summary>
	public static void ResetLastReport() => LastReport = null;

	/// <summary>
	/// Builds the standard failure report for a statement about a region - the statement itself,
	/// the region report beneath it and the path of the saved frame - and remembers it as
	/// <see cref="LastReport"/> so the scenario hooks print it again after the scenario has ended.
	/// <para>
	/// This is the seam an ADD-IN's own pixel primitive uses. A partial class cannot span
	/// assemblies, so a coverage group that genuinely needs a new primitive writes it in a static
	/// class of its own and reports through this, rather than printing one line less than every
	/// other failure in the suite.
	/// </para>
	/// </summary>
	/// <param name="region">The region the statement is about.</param>
	/// <param name="statement">What was required of it, in the words a person would use.</param>
	/// <returns>The report, ready to be handed to an assertion as its reason.</returns>
	public static string Explain(Region region, string statement)
	{
		var report = statement + Environment.NewLine
			+ RegionReport.Describe(region, FrameArchive.TrySave(region.Frame));
		LastReport = report;
		return report;
	}

	private static string Percent(double fraction) => string.Create(CultureInfo.InvariantCulture,
		$"{fraction * 100:0.00}%");

	private static (SKColor First, SKColor Middle, SKColor Last) SampleAlong(Region region, GradientAxis axis)
	{
		var bounds = region.Bounds;
		if (axis == GradientAxis.Horizontal)
		{
			var y = bounds.Height / 2;
			return (region[0, y], region[bounds.Width / 2, y], region[bounds.Width - 1, y]);
		}

		var x = bounds.Width / 2;
		return (region[x, 0], region[x, bounds.Height / 2], region[x, bounds.Height - 1]);
	}

	private static bool IsBetween(byte value, byte first, byte last)
	{
		var low = Math.Min(first, last) - ColorMatch.DefaultTolerance;
		var high = Math.Max(first, last) + ColorMatch.DefaultTolerance;
		return value >= low && value <= high;
	}
}
