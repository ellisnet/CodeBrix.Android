using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SilverAssertions;
using Windows.UI;
using SKColor = CodeBrix.Android.UIReqs.Device.Canvas.PixelColor;

namespace CodeBrix.Platform.UI.Core.UIReqs.Canvas;

/// <summary>
/// The ThemeFocus group's additions to the vocabulary. Three things the harness could not say
/// before: what colour the ink is when the ink is NOT on the panel's own background - a control
/// repainted from the dark theme's brushes carries its text on its own dark fill - how much was
/// drawn in the ring of pixels AROUND a control, which is where a focus visual lives, and how
/// big the picture inside a region is, which is how a Stretch mode is seen.
/// </summary>
public static partial class CanvasAssert
{
	/// <summary>
	/// The share of the ring around a control that must gain ink for a focus visual to count as
	/// drawn. The Fluent focus visual is a pair of thin strokes just outside the control, so it
	/// covers a modest part of the ring and nothing like all of it.
	/// </summary>
	public const double FocusRingGain = 0.05;

	/// <summary>How many pixels of a control's surroundings the focus visual is looked for in.</summary>
	public const int FocusRingWidth = 6;

	/// <summary>How far a picture's measured size may be from the size a Stretch mode implies.</summary>
	public const int PictureSizeTolerance = 2;

	/// <summary>
	/// What share of a region differs from a surface colour of the caller's choosing, rather
	/// than from the panel background.
	/// </summary>
	/// <param name="region">The region to measure.</param>
	/// <param name="surface">The colour the region is painted behind its ink.</param>
	/// <returns>A share between 0 and 1.</returns>
	public static double InkFractionOver(Region region, Color surface)
	{
		ArgumentNullException.ThrowIfNull(region);

		var matches = region.Pixels().Count(pixel => ColorMatch.Matches(pixel, surface, surface));
		return 1.0 - (matches / (double) region.PixelCount);
	}

	/// <summary>
	/// The colour of a region's ink when the region is not painted the panel's background -
	/// the text on a control that carries its own fill. Ink is everything that differs from
	/// the surface colour; the mode is taken the way <see cref="TryInkColor"/> takes it.
	/// </summary>
	/// <param name="region">The region to measure.</param>
	/// <param name="surface">The colour the region is painted behind its ink.</param>
	/// <param name="color">The ink colour, when there is any ink.</param>
	/// <returns><c>true</c> when the region holds ink.</returns>
	public static bool TryInkColorOver(Region region, Color surface, out SKColor color)
	{
		ArgumentNullException.ThrowIfNull(region);

		var counts = new Dictionary<uint, int>();
		var representatives = new ModeColor(); // ANDROID PORT (fix): see ModeColor.

		foreach (var pixel in region.Pixels())
		{
			if (ColorMatch.Matches(pixel, surface, surface))
			{
				continue;
			}

			var composited = ColorMatch.Composite(pixel, surface);
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

	/// <summary>Asserts what colour a region's ink is when the region carries its own fill.</summary>
	/// <param name="region">The region to check.</param>
	/// <param name="surface">The colour the region is painted behind its ink.</param>
	/// <param name="ink">The colour the ink must be.</param>
	public static void InkColorOverIs(this Region region, Color surface, Color ink)
	{
		if (!TryInkColorOver(region, surface, out var actual))
		{
			"no ink".Should().Be(ColorMatch.Describe(ink), "{0}", Explain(region,
				$"{region.Description} must carry ink of {ColorMatch.Describe(ink)} on "
				+ $"{ColorMatch.Describe(surface)}, but every pixel of it is that surface colour"));
			return;
		}

		if (!ColorMatch.Matches(actual, ink, surface))
		{
			ColorMatch.Describe(actual).Should().Be(ColorMatch.Describe(ink), "{0}", Explain(region,
				$"the ink of {region.Description}, measured against the {ColorMatch.Describe(surface)} it "
				+ $"is painted on, must be {ColorMatch.Describe(ink)}"));
		}
	}

	/// <summary>
	/// Asserts what colour a region's ink is, measured against whatever colour the region is
	/// mostly painted. A control's own Background brush is often translucent - the theme means
	/// it to be composited over the page behind it - so the colour the text is actually drawn
	/// on is the one the panel ended up showing, not the one the brush holds.
	/// </summary>
	/// <param name="region">The region to check.</param>
	/// <param name="ink">The colour the ink must be.</param>
	public static void InkColorOverItsSurfaceIs(this Region region, Color ink)
	{
		var surface = DominantColor(region).Color;
		region.InkColorOverIs(Color.FromArgb(0xFF, surface.Red, surface.Green, surface.Blue), ink);
	}

	/// <summary>
	/// What share of the ring between two rectangles is ink. The ring is what lies inside the
	/// region and outside the rectangle the control itself fills, which is exactly where a
	/// focus visual is drawn.
	/// </summary>
	/// <param name="region">The grown region, control and surroundings together.</param>
	/// <param name="inner">The rectangle the control itself fills.</param>
	/// <returns>A share between 0 and 1.</returns>
	public static double RingInkFraction(Region region, DeviceRect inner)
	{
		ArgumentNullException.ThrowIfNull(region);

		var background = Background;
		var counted = 0;
		var ink = 0;

		for (var y = region.Bounds.Y; y < region.Bounds.Bottom; y++)
		{
			for (var x = region.Bounds.X; x < region.Bounds.Right; x++)
			{
				if (inner.Contains(x, y))
				{
					continue;
				}

				counted++;
				if (!ColorMatch.Matches(region.Frame.GetPixel(x, y), background, background))
				{
					ink++;
				}
			}
		}

		return counted == 0 ? 0.0 : ink / (double) counted;
	}

	/// <summary>
	/// Asserts that the ring around a control gained ink - something was drawn around it that
	/// was not drawn around it before.
	/// </summary>
	/// <param name="region">The grown region in the later frame.</param>
	/// <param name="other">The same grown region in the earlier frame.</param>
	/// <param name="inner">The rectangle the control itself fills.</param>
	/// <param name="minGain">How much more of the ring must be ink; the default is 5%.</param>
	public static void RingGainedInk(this Region region, Region other, DeviceRect inner,
		double minGain = FocusRingGain)
	{
		var now = RingInkFraction(region, inner);
		var before = RingInkFraction(other, inner);
		var gain = now - before;

		if (gain < minGain)
		{
			gain.Should().BeGreaterThanOrEqualTo(minGain, "{0}", Explain(region,
				$"the ring around {region.Description} must gain ink: it is {Percent(now)} ink now and was "
				+ $"{Percent(before)} ink before"));
		}
	}

	/// <summary>Asserts how big the picture drawn inside a region is.</summary>
	/// <param name="region">The region to measure.</param>
	/// <param name="width">The width the picture must have.</param>
	/// <param name="height">The height the picture must have.</param>
	/// <param name="tolerance">How many pixels each side may be out.</param>
	public static void PictureIsSized(this Region region, int width, int height,
		int tolerance = PictureSizeTolerance)
	{
		var bounds = region.InkBounds();
		var widthOff = Math.Abs(bounds.Width - width);
		var heightOff = Math.Abs(bounds.Height - height);

		if (widthOff > tolerance || heightOff > tolerance)
		{
			Math.Max(widthOff, heightOff).Should().BeLessThanOrEqualTo(tolerance, "{0}", Explain(region,
				string.Create(CultureInfo.InvariantCulture,
					$"the picture inside {region.Description} must be {width} x {height} pixels, but it is {bounds}")));
		}
	}

	/// <summary>
	/// How bright a colour is, on the usual perceptual weighting, between 0 for black and 1 for
	/// white. A requirement about a theme says a control's fill is DARK or LIGHT rather than
	/// naming the palette's exact value, which would make the scenario a statement about one
	/// palette instead of about the theme.
	/// </summary>
	/// <param name="color">The colour to weigh.</param>
	/// <returns>The brightness, between 0 and 1.</returns>
	public static double Luminance(Color color) =>
		((0.2126 * color.R) + (0.7152 * color.G) + (0.0722 * color.B)) / 255.0;
}
