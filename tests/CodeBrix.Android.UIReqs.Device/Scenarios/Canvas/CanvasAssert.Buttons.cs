using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SilverAssertions;
using SKColor = CodeBrix.Android.UIReqs.Device.Canvas.PixelColor;

namespace CodeBrix.Platform.UI.Core.UIReqs.Canvas;

/// <summary>
/// The vocabulary the Buttons group's requirements need on top of the harness's own: a way to
/// say "this rectangle is one flat colour" and "this rectangle is not", without naming the
/// colour. A control's box, glyph and knob are painted from theme brushes whose exact values
/// are the theme's business, so a requirement about a glyph being drawn is best stated as "the
/// box stopped being one flat colour", which stays true whatever the palette says.
/// </summary>
public static partial class CanvasAssert
{
	/// <summary>The share of a region its single colour must cover for the region to be flat.</summary>
	public const double FlatColorFraction = 0.97;

	/// <summary>The share of a region that must not be its dominant colour for it to be patterned.</summary>
	/// <remarks>
	/// A CheckBox's indeterminate dash is the smallest glyph any Buttons requirement talks
	/// about and covers about a twenty-fifth of the box's inside, so the threshold sits below
	/// that and well above the nothing-at-all a box with no glyph shows.
	/// </remarks>
	public const double PatternedFraction = 0.02;

	/// <summary>
	/// The colour that covers most of a region, and how much of the region it covers. Colours
	/// are quantized the way ink colour is, so antialiasing does not split one flat fill into
	/// a dozen near-identical shades.
	/// </summary>
	/// <param name="region">The region to measure.</param>
	/// <returns>The dominant colour and its share of the region.</returns>
	public static (SKColor Color, double Share) DominantColor(Region region)
	{
		ArgumentNullException.ThrowIfNull(region);

		var background = Background;
		var counts = new Dictionary<uint, int>();
		var representatives = new ModeColor(); // ANDROID PORT (fix): see ModeColor.

		foreach (var pixel in region.Pixels())
		{
			var composited = ColorMatch.Composite(pixel, background);
			var key = (uint) ColorMatch.Quantize(composited);
			counts.TryGetValue(key, out var count);
			counts[key] = count + 1;
			representatives.Add(key, composited);
		}

		if (counts.Count == 0)
		{
			return (default, 0.0);
		}

		var dominant = counts.OrderByDescending(pair => pair.Value).First();
		return (representatives.Of(dominant.Key), dominant.Value / (double) region.PixelCount);
	}

	/// <summary>
	/// Asserts that a region is one flat colour, whatever that colour is - nothing has been
	/// drawn on top of it.
	/// </summary>
	/// <param name="region">The region to check.</param>
	/// <param name="minFraction">How much of it one colour must cover; the default is 97%.</param>
	public static void ShowsASingleFlatColor(this Region region, double minFraction = FlatColorFraction)
	{
		var (color, share) = DominantColor(region);
		if (share < minFraction)
		{
			share.Should().BeGreaterThanOrEqualTo(minFraction, "{0}", Explain(region,
				$"{region.Description} must be one flat colour, but its most common colour "
				+ $"{ColorMatch.Describe(color)} covers only " + Share(share) + " of it"));
		}
	}

	/// <summary>
	/// Asserts that a region is not one flat colour - something was drawn on top of whatever
	/// fills it.
	/// </summary>
	/// <param name="region">The region to check.</param>
	/// <param name="minFraction">How much of it must differ from the dominant colour; the default is 3%.</param>
	public static void ShowsMoreThanOneColor(this Region region, double minFraction = PatternedFraction)
	{
		var (color, share) = DominantColor(region);
		var rest = 1.0 - share;
		if (rest < minFraction)
		{
			rest.Should().BeGreaterThanOrEqualTo(minFraction, "{0}", Explain(region,
				$"{region.Description} must show more than one colour, but "
				+ Share(share) + $" of it is {ColorMatch.Describe(color)}"));
		}
	}

	private static string Share(double fraction) => string.Create(CultureInfo.InvariantCulture,
		$"{fraction * 100:0.00}%");
}
