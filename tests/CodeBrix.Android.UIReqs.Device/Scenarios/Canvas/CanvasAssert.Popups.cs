using System;
using System.Collections.Generic;
using System.Globalization;
using CodeBrix.Platform.UI.Core.UIReqs.Hosting;
using SilverAssertions;
using SKColor = CodeBrix.Android.UIReqs.Device.Canvas.PixelColor;

namespace CodeBrix.Platform.UI.Core.UIReqs.Canvas;

/// <summary>
/// The vocabulary the Popups group's requirements need. A modal dialog dims what is behind it,
/// and "dimmed" is not a colour a feature file can name: it is the same picture, darker. That
/// is a statement about a region's average brightness in two frames rather than about any one
/// colour, so it gets a primitive of its own.
/// </summary>
public static partial class CanvasAssert
{
	/// <summary>
	/// How much darker, on a 0-255 scale, a region must become before it counts as dimmed.
	/// The smoke layer a dialog paints is far darker than this; the margin keeps a repaint
	/// that merely shifts a shade from passing for one.
	/// </summary>
	public const double DimmedDrop = 20.0;

	/// <summary>
	/// The average brightness of a region, on a 0-255 scale, judged as a person sees it: a
	/// semi-transparent pixel is composited over the panel background first.
	/// </summary>
	/// <param name="region">The region to measure.</param>
	/// <returns>The average brightness.</returns>
	public static double MeanBrightness(Region region)
	{
		ArgumentNullException.ThrowIfNull(region);

		var background = Background;
		var total = 0.0;
		foreach (var pixel in region.Pixels())
		{
			var composited = ColorMatch.Composite(pixel, background);
			total += (0.299 * composited.Red) + (0.587 * composited.Green) + (0.114 * composited.Blue);
		}

		return total / region.PixelCount;
	}

	/// <summary>Asserts that a region became darker than it was in another frame.</summary>
	/// <param name="region">The region as it looks now.</param>
	/// <param name="other">The same region in the earlier frame.</param>
	/// <param name="minimumDrop">How much darker it must have become; the default is <see cref="DimmedDrop"/>.</param>
	public static void IsDimmedComparedTo(this Region region, Region other, double minimumDrop = DimmedDrop)
	{
		ArgumentNullException.ThrowIfNull(region);
		ArgumentNullException.ThrowIfNull(other);

		var before = MeanBrightness(other);
		var now = MeanBrightness(region);
		var drop = before - now;

		if (drop < minimumDrop)
		{
			drop.Should().BeGreaterThanOrEqualTo(minimumDrop, "{0}", Explain(region, string.Create(
				CultureInfo.InvariantCulture,
				$"{region.Description} must be dimmed: its average brightness was {before:0.0} and is now {now:0.0}")));
		}
	}

	/// <summary>
	/// How far a pixel of an opaque surface may sit from that surface's own colour, on each
	/// channel, before it counts as something showing through from behind. It is wide enough for
	/// the dither and the antialiasing a flat fill still carries, and far narrower than the swing
	/// a light backdrop and a dark one put into a surface that is not opaque.
	/// </summary>
	public const int OpaqueChannelTolerance = 10;

	/// <summary>
	/// How much of an opaque surface must be its own colour. The remainder covers a rounded
	/// corner or a shadow that reached inside the rectangle.
	/// </summary>
	public const double OpaqueFraction = 0.97;

	/// <summary>The smallest inset, in device pixels, that takes a surface's own border off it.</summary>
	public const int SurfaceInset = 6;

	/// <summary>How much of a surface's shorter side is taken off each edge before it is judged.</summary>
	public const int SurfaceInsetDivisor = 10;

	/// <summary>
	/// Asserts that a region is one opaque surface: every pixel of it is the same colour, so
	/// nothing of whatever it was drawn over is coming through. A translucent surface shown over
	/// a light half and a dark half of the same backdrop carries both of them, and fails here.
	/// </summary>
	/// <param name="region">The interior of the surface to judge.</param>
	public static void IsOneOpaqueSurface(this Region region)
	{
		ArgumentNullException.ThrowIfNull(region);

		var surface = MostCommonColor(region);
		var matching = 0;
		var darkest = 255;
		var lightest = 0;
		foreach (var pixel in region.Pixels())
		{
			if (IsWithinTolerance(pixel, surface))
			{
				matching++;
			}

			var luminance = (int) Math.Round((0.299 * pixel.Red) + (0.587 * pixel.Green) + (0.114 * pixel.Blue));
			darkest = Math.Min(darkest, luminance);
			lightest = Math.Max(lightest, luminance);
		}

		var fraction = (double) matching / region.PixelCount;
		if (fraction < OpaqueFraction)
		{
			fraction.Should().BeGreaterThanOrEqualTo(OpaqueFraction, "{0}", Explain(region, string.Create(
				CultureInfo.InvariantCulture,
				$"{region.Description} must be one opaque surface, so that what is behind it cannot "
				+ $"be read through it: {fraction * 100:0.0}% of it is #{surface.Red:X2}{surface.Green:X2}{surface.Blue:X2} "
				+ $"and its brightness runs from {darkest} to {lightest} of 255")));
		}
	}

	private static SKColor MostCommonColor(Region region)
	{
		var counts = new Dictionary<SKColor, int>();
		var best = default(SKColor);
		var bestCount = -1;
		foreach (var pixel in region.Pixels())
		{
			counts.TryGetValue(pixel, out var count);
			counts[pixel] = ++count;
			if (count > bestCount)
			{
				bestCount = count;
				best = pixel;
			}
		}

		return best;
	}

	private static bool IsWithinTolerance(SKColor pixel, SKColor surface) =>
		Math.Abs(pixel.Red - surface.Red) <= OpaqueChannelTolerance
		&& Math.Abs(pixel.Green - surface.Green) <= OpaqueChannelTolerance
		&& Math.Abs(pixel.Blue - surface.Blue) <= OpaqueChannelTolerance;

	/// <summary>The rectangle of the panel a scenario looks at to see whether it was dimmed.</summary>
	/// <remarks>
	/// It is a square in the panel's top-left corner, sized from the panel rather than from a
	/// constant, so the same requirement holds on both the landscape and the portrait panel.
	/// A dialog is centred, so this square is never underneath one.
	/// </remarks>
	/// <returns>The rectangle, in device pixels.</returns>
	public static DeviceRect PanelCorner()
	{
		var (width, height) = TestTargetFixture.PanelSize;
		var side = Math.Min(width, height) / 8;
		var inset = side / 4;
		return new DeviceRect(inset, inset, side, side);
	}
}
