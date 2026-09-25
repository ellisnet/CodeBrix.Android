using System;
using Windows.UI;

namespace CodeBrix.Platform.UI.Core.UIReqs.Canvas;

/// <summary>
/// The vocabulary the Range group's requirements need: where in a rectangle a given colour
/// actually is. A progress indicator, a slider's filled part and a scroll bar's thumb are all
/// requirements about how far across a track one colour reaches, which is a question about the
/// bounding box of that colour rather than about the ink as a whole.
/// </summary>
public static partial class CanvasAssert
{
	/// <summary>
	/// The tight bounding box of the pixels of one colour inside a region, in frame
	/// coordinates.
	/// </summary>
	/// <param name="region">The region to search.</param>
	/// <param name="color">The colour to look for.</param>
	/// <param name="bounds">The bounding box, when the colour is present at all.</param>
	/// <returns><c>true</c> when the region holds at least one pixel of that colour.</returns>
	public static bool TryColorBounds(Region region, Color color, out DeviceRect bounds)
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
				if (!ColorMatch.Matches(region.Frame.GetPixel(x, y), color, background))
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

	/// <summary>The tight bounding box of the pixels of one colour inside a region.</summary>
	/// <param name="region">The region to search.</param>
	/// <param name="color">The colour to look for.</param>
	/// <returns>The bounding box, in frame coordinates.</returns>
	public static DeviceRect ColorBounds(this Region region, Color color)
	{
		if (TryColorBounds(region, color, out var bounds))
		{
			return bounds;
		}

		ArgumentNullException.ThrowIfNull(region);
		throw new InvalidOperationException(Explain(region,
			$"{region.Description} holds no pixel of {ColorMatch.Describe(color)}, so that "
			+ "colour has no bounding box"));
	}
}
