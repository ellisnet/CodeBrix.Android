using System;
using System.Globalization;
using SilverAssertions;
using Windows.UI;

namespace CodeBrix.Platform.UI.Core.UIReqs.Canvas;

/// <summary>
/// The extra vocabulary the Layout scenarios speak: the four corners of a region, one half of a
/// region, and what happened to a region's ink between two frames.
/// </summary>
public static partial class CanvasAssert
{
	/// <summary>How far in from a corner the "corner pixel" of a region is sampled.</summary>
	public const int CornerInset = 2;

	/// <summary>How many pixels an ink bound may be out by and still count as having moved exactly.</summary>
	public const int InkMoveTolerance = 2;

	/// <summary>How far off a scale factor may be and still count as the scale that was asked for.</summary>
	public const double InkScaleTolerance = 0.08;

	/// <summary>The left half of a region.</summary>
	/// <param name="region">The region to cut.</param>
	/// <returns>The half, as a region of its own.</returns>
	public static Region LeftHalf(this Region region) => Part(region, "the left half",
		r => new DeviceRect(r.X, r.Y, r.Width / 2, r.Height));

	/// <summary>The right half of a region.</summary>
	/// <param name="region">The region to cut.</param>
	/// <returns>The half, as a region of its own.</returns>
	public static Region RightHalf(this Region region) => Part(region, "the right half",
		r => new DeviceRect(r.X + ((r.Width + 1) / 2), r.Y, r.Width / 2, r.Height));

	/// <summary>The top half of a region.</summary>
	/// <param name="region">The region to cut.</param>
	/// <returns>The half, as a region of its own.</returns>
	public static Region TopHalf(this Region region) => Part(region, "the top half",
		r => new DeviceRect(r.X, r.Y, r.Width, r.Height / 2));

	/// <summary>The bottom half of a region.</summary>
	/// <param name="region">The region to cut.</param>
	/// <returns>The half, as a region of its own.</returns>
	public static Region BottomHalf(this Region region) => Part(region, "the bottom half",
		r => new DeviceRect(r.X, r.Y + ((r.Height + 1) / 2), r.Width, r.Height / 2));

	/// <summary>
	/// Asserts that all four corners of a region are one colour. This is how a rounded corner is
	/// seen: the corner pixels are still the panel background because the shape does not reach
	/// them, while the middle of the same region is painted.
	/// </summary>
	/// <param name="region">The region to check.</param>
	/// <param name="color">The colour every corner must be.</param>
	/// <param name="inset">How far in from each corner to sample.</param>
	public static void CornerPixelsAre(this Region region, Color color, int inset = CornerInset)
	{
		ArgumentNullException.ThrowIfNull(region);

		var background = Background;
		var bounds = region.Bounds;
		var last = (X: bounds.Width - 1 - inset, Y: bounds.Height - 1 - inset);

		Check(region, color, inset, inset, "top left", background);
		Check(region, color, last.X, inset, "top right", background);
		Check(region, color, inset, last.Y, "bottom left", background);
		Check(region, color, last.X, last.Y, "bottom right", background);

		static void Check(Region region, Color color, int x, int y, string corner, Color background)
		{
			var pixel = region[x, y];
			if (ColorMatch.Matches(pixel, color, background))
			{
				return;
			}

			ColorMatch.Describe(pixel).Should().Be(ColorMatch.Describe(color), "{0}", Explain(region,
				string.Create(CultureInfo.InvariantCulture,
					$"the {corner} corner of {region.Description}, at ({x},{y}) inside it, must be {ColorMatch.Describe(color)}")));
		}
	}

	/// <summary>
	/// Asserts that a region's ink sits where it did in another frame, moved by an exact offset.
	/// This is how a translation is seen: the element's own rectangle moves with it, so the ink
	/// is measured inside a container that did not move.
	/// </summary>
	/// <param name="region">The later region.</param>
	/// <param name="other">The earlier region, which must be the same rectangle.</param>
	/// <param name="deltaX">How far right the ink must have moved.</param>
	/// <param name="deltaY">How far down the ink must have moved.</param>
	/// <param name="tolerance">How many pixels each edge may be out by.</param>
	public static void InkMovedBy(this Region region, Region other, int deltaX, int deltaY,
		int tolerance = InkMoveTolerance)
	{
		var bounds = region.InkBounds();
		var otherBounds = other.InkBounds();
		var actualX = bounds.X - otherBounds.X;
		var actualY = bounds.Y - otherBounds.Y;

		if (Math.Abs(actualX - deltaX) > tolerance || Math.Abs(actualY - deltaY) > tolerance)
		{
			Describe(actualX, actualY).Should().Be(Describe(deltaX, deltaY), "{0}", Explain(region,
				$"the ink of {region.Description} must have moved by {Describe(deltaX, deltaY)}: "
				+ $"it was at {otherBounds} and is now at {bounds}"));
		}
	}

	/// <summary>
	/// Asserts that a region's ink is a multiple of the size it was in another frame. This is how
	/// a scale is seen, and it is insensitive to where the scaled content ended up.
	/// </summary>
	/// <param name="region">The later region.</param>
	/// <param name="other">The earlier region, which must be the same rectangle.</param>
	/// <param name="scaleX">The factor the ink's width must have grown by.</param>
	/// <param name="scaleY">The factor the ink's height must have grown by.</param>
	/// <param name="tolerance">How far off each factor may be.</param>
	public static void InkScaledBy(this Region region, Region other, double scaleX, double scaleY,
		double tolerance = InkScaleTolerance)
	{
		var bounds = region.InkBounds();
		var otherBounds = other.InkBounds();
		var actualX = bounds.Width / (double) otherBounds.Width;
		var actualY = bounds.Height / (double) otherBounds.Height;

		if (Math.Abs(actualX - scaleX) > tolerance || Math.Abs(actualY - scaleY) > tolerance)
		{
			Factor(actualX, actualY).Should().Be(Factor(scaleX, scaleY), "{0}", Explain(region,
				$"the ink of {region.Description} must have scaled by {Factor(scaleX, scaleY)}: "
				+ $"it was {otherBounds} and is now {bounds}"));
		}
	}

	/// <summary>
	/// Asserts that a region's ink covers more of the region than it did in another frame - a
	/// rotation, a bolder weight or a longer string all show up this way.
	/// </summary>
	/// <param name="region">The later region.</param>
	/// <param name="other">The earlier region, which must be the same rectangle.</param>
	/// <param name="minGrowth">How many times as much ink there must be; the default is 1.05.</param>
	public static void HasMoreInkThan(this Region region, Region other, double minGrowth = 1.05)
	{
		var fraction = InkFraction(region);
		var otherFraction = InkFraction(other);
		var grown = fraction > otherFraction && fraction >= otherFraction * minGrowth;

		if (!grown)
		{
			grown.Should().BeTrue("{0}", Explain(region,
				$"{region.Description} must hold at least {Times(minGrowth)} the ink it held before, but it "
				+ $"holds {Percent(fraction)} of the region where it held {Percent(otherFraction)}"));
		}
	}

	/// <summary>
	/// Asserts what colour ONE pixel of a region is, addressed as a fraction of the region's own
	/// width and height so that a scenario never has to know where the element landed on the
	/// panel. The corner, half and strip primitives all say what a whole area looks like, which
	/// cannot state where a gradient puts a particular colour; this can.
	/// </summary>
	/// <param name="region">The region to look in.</param>
	/// <param name="x">How far across the region, from 0 at the left edge to 1 at the right.</param>
	/// <param name="y">How far down the region, from 0 at the top edge to 1 at the bottom.</param>
	/// <param name="color">The colour that pixel must be.</param>
	public static void PixelIs(this Region region, double x, double y, Color color)
	{
		var (px, py) = PixelOf(region, x, y);
		var pixel = region[px, py];
		if (!ColorMatch.Matches(pixel, color, Background))
		{
			ColorMatch.Describe(pixel).Should().Be(ColorMatch.Describe(color), "{0}", Explain(region,
				string.Create(CultureInfo.InvariantCulture,
					$"the pixel at {x}, {y} of {region.Description}, at ({px},{py}) inside it, must be {ColorMatch.Describe(color)}")));
		}
	}

	/// <summary>
	/// Asserts what colour one pixel of a region is NOT. This is how a scenario says that
	/// something moved without claiming to know what took its place.
	/// </summary>
	/// <param name="region">The region to look in.</param>
	/// <param name="x">How far across the region, from 0 at the left edge to 1 at the right.</param>
	/// <param name="y">How far down the region, from 0 at the top edge to 1 at the bottom.</param>
	/// <param name="color">The colour that pixel must not be.</param>
	public static void PixelIsNot(this Region region, double x, double y, Color color)
	{
		var (px, py) = PixelOf(region, x, y);
		var pixel = region[px, py];
		if (ColorMatch.Matches(pixel, color, Background))
		{
			ColorMatch.Describe(pixel).Should().NotBe(ColorMatch.Describe(color), "{0}", Explain(region,
				string.Create(CultureInfo.InvariantCulture,
					$"the pixel at {x}, {y} of {region.Description}, at ({px},{py}) inside it, must not be {ColorMatch.Describe(color)}")));
		}
	}

	private static (int X, int Y) PixelOf(Region region, double x, double y)
	{
		ArgumentNullException.ThrowIfNull(region);
		return (Coordinate(x, region.Bounds.Width, nameof(x)), Coordinate(y, region.Bounds.Height, nameof(y)));

		static int Coordinate(double fraction, int size, string axis)
		{
			if (double.IsNaN(fraction) || fraction < 0 || fraction > 1)
			{
				throw new ArgumentOutOfRangeException(axis, fraction,
					"A pixel inside a region is addressed as a fraction from 0 to 1 of the region.");
			}

			return (int) Math.Round(fraction * (size - 1), MidpointRounding.AwayFromZero);
		}
	}

	private static Region Part(Region region, string what, Func<DeviceRect, DeviceRect> cut)
	{
		ArgumentNullException.ThrowIfNull(region);
		return new Region(region.Frame, cut(region.Bounds), what + " of " + region.Description);
	}

	private static string Describe(int x, int y) => string.Create(CultureInfo.InvariantCulture, $"({x},{y})");

	private static string Factor(double x, double y) => string.Create(CultureInfo.InvariantCulture, $"({x:0.00}x, {y:0.00}x)");

	private static string Times(double factor) => string.Create(CultureInfo.InvariantCulture, $"{factor:0.00} times");
}
