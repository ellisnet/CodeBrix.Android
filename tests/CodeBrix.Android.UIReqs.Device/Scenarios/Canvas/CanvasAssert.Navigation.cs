using System;
using System.Globalization;

namespace CodeBrix.Platform.UI.Core.UIReqs.Canvas;

/// <summary>
/// The Navigation group's additions to the vocabulary. A pane, a header and a scrolled column
/// are all about a STRIP of something rather than the whole of it - the leftmost sixty pixels
/// of a control are the compact pane, the rest is the content - so the primitives here cut a
/// strip off a region and hand it back as a region of its own, keeping the words a failure
/// message needs.
/// </summary>
public static partial class CanvasAssert
{
	/// <summary>The strip along the left edge of a region.</summary>
	/// <param name="region">The region to cut from.</param>
	/// <param name="width">How many pixels wide the strip is.</param>
	/// <returns>The strip, as a region of its own.</returns>
	public static Region LeftStrip(this Region region, int width)
	{
		ArgumentNullException.ThrowIfNull(region);
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);

		var bounds = region.Bounds;
		return new Region(region.Frame,
			new DeviceRect(bounds.X, bounds.Y, Math.Min(width, bounds.Width), bounds.Height),
			string.Create(CultureInfo.InvariantCulture, $"the leftmost {width} pixels of {region.Description}"));
	}

	/// <summary>The strip along the right edge of a region.</summary>
	/// <param name="region">The region to cut from.</param>
	/// <param name="width">How many pixels wide the strip is.</param>
	/// <returns>The strip, as a region of its own.</returns>
	public static Region RightStrip(this Region region, int width)
	{
		ArgumentNullException.ThrowIfNull(region);
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);

		var bounds = region.Bounds;
		var strip = Math.Min(width, bounds.Width);
		return new Region(region.Frame,
			new DeviceRect(bounds.Right - strip, bounds.Y, strip, bounds.Height),
			string.Create(CultureInfo.InvariantCulture, $"the rightmost {width} pixels of {region.Description}"));
	}

	/// <summary>The strip along the top edge of a region.</summary>
	/// <param name="region">The region to cut from.</param>
	/// <param name="height">How many pixels tall the strip is.</param>
	/// <returns>The strip, as a region of its own.</returns>
	public static Region TopStrip(this Region region, int height)
	{
		ArgumentNullException.ThrowIfNull(region);
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

		var bounds = region.Bounds;
		return new Region(region.Frame,
			new DeviceRect(bounds.X, bounds.Y, bounds.Width, Math.Min(height, bounds.Height)),
			string.Create(CultureInfo.InvariantCulture, $"the topmost {height} pixels of {region.Description}"));
	}

	/// <summary>The strip along the bottom edge of a region.</summary>
	/// <param name="region">The region to cut from.</param>
	/// <param name="height">How many pixels tall the strip is.</param>
	/// <returns>The strip, as a region of its own.</returns>
	public static Region BottomStrip(this Region region, int height)
	{
		ArgumentNullException.ThrowIfNull(region);
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

		var bounds = region.Bounds;
		var strip = Math.Min(height, bounds.Height);
		return new Region(region.Frame,
			new DeviceRect(bounds.X, bounds.Bottom - strip, bounds.Width, strip),
			string.Create(CultureInfo.InvariantCulture, $"the bottommost {height} pixels of {region.Description}"));
	}
}
