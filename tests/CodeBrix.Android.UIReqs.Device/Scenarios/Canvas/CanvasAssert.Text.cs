using System;
using System.Globalization;
using SilverAssertions;

namespace CodeBrix.Platform.UI.Core.UIReqs.Canvas;

/// <summary>
/// The extra vocabulary the Text scenarios speak: how tall a piece of text's ink is compared
/// with another frame's, and which side of its own rectangle that ink sits against. Text
/// CONTENT is never asserted here - that is a fact about the tree - only its appearance.
/// </summary>
public static partial class CanvasAssert
{
	/// <summary>How much taller ink has to be before it counts as having grown.</summary>
	public const double InkGrowthFactor = 1.15;

	/// <summary>How many pixels an ink measurement may be out by and still count as unchanged.</summary>
	public const int InkSizeTolerance = 2;

	/// <summary>
	/// Asserts that a region's ink is meaningfully taller than it was in another frame - a
	/// larger FontSize and a line that wrapped both show up this way.
	/// </summary>
	/// <param name="region">The later region.</param>
	/// <param name="other">The earlier region.</param>
	/// <param name="minGrowth">How many times as tall it must be; the default is 1.15.</param>
	public static void InkIsTallerThan(this Region region, Region other, double minGrowth = InkGrowthFactor)
	{
		var bounds = region.InkBounds();
		var otherBounds = other.InkBounds();
		var grown = bounds.Height >= otherBounds.Height * minGrowth;

		if (!grown)
		{
			grown.Should().BeTrue("{0}", Explain(region,
				$"the ink of {region.Description} must be at least {Growth(minGrowth)} as tall as it was, "
				+ $"but it is {Pixels(bounds.Height)} where it was {Pixels(otherBounds.Height)}"));
		}
	}

	/// <summary>Asserts that a region's ink is as tall as it was in another frame.</summary>
	/// <param name="region">The later region.</param>
	/// <param name="other">The earlier region.</param>
	/// <param name="tolerance">How many pixels it may differ by; the default is 2.</param>
	public static void InkIsAsTallAs(this Region region, Region other, int tolerance = InkSizeTolerance)
	{
		var bounds = region.InkBounds();
		var otherBounds = other.InkBounds();

		if (Math.Abs(bounds.Height - otherBounds.Height) > tolerance)
		{
			Pixels(bounds.Height).Should().Be(Pixels(otherBounds.Height), "{0}", Explain(region,
				$"the ink of {region.Description} must be the same height as it was: it is {bounds} "
				+ $"where it was {otherBounds}"));
		}
	}

	/// <summary>
	/// Asserts that a region's ink sits against the left of the region, which is what left
	/// alignment looks like to a person.
	/// </summary>
	/// <param name="region">The region to check.</param>
	public static void InkHugsLeft(this Region region) => InkSitsAt(region, InkPlacement.Left);

	/// <summary>Asserts that a region's ink sits in the middle of the region.</summary>
	/// <param name="region">The region to check.</param>
	public static void InkIsCentred(this Region region) => InkSitsAt(region, InkPlacement.Centre);

	/// <summary>Asserts that a region's ink sits against the right of the region.</summary>
	/// <param name="region">The region to check.</param>
	public static void InkHugsRight(this Region region) => InkSitsAt(region, InkPlacement.Right);

	private enum InkPlacement
	{
		Left,
		Centre,
		Right,
	}

	private static void InkSitsAt(Region region, InkPlacement placement)
	{
		var ink = region.InkBounds();
		var left = ink.X - region.Bounds.X;
		var right = region.Bounds.Right - ink.Right;
		var tolerance = Math.Max(6, region.Bounds.Width / 50);

		var correct = placement switch
		{
			InkPlacement.Left => left <= tolerance && right > left,
			InkPlacement.Right => right <= tolerance && left > right,
			_ => Math.Abs(left - right) <= tolerance && left > tolerance,
		};

		if (!correct)
		{
			correct.Should().BeTrue("{0}", Explain(region,
				$"the ink of {region.Description} must sit at the {Placement(placement)} of the region, but "
				+ string.Create(CultureInfo.InvariantCulture,
					$"it leaves {left} px free on the left and {right} px free on the right (tolerance {tolerance} px)")));
		}
	}

	private static string Placement(InkPlacement placement) => placement switch
	{
		InkPlacement.Left => "left",
		InkPlacement.Right => "right",
		_ => "centre",
	};

	private static string Pixels(int value) => string.Create(CultureInfo.InvariantCulture, $"{value} px");

	private static string Growth(double factor) => string.Create(CultureInfo.InvariantCulture, $"{factor:0.00} times");
}
