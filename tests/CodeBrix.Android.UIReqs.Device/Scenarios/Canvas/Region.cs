using System;
using System.Collections.Generic;
using System.Globalization;
using CodeBrix.Android.UIReqs.Device.TestTarget;
using SKColor = CodeBrix.Android.UIReqs.Device.Canvas.PixelColor;

namespace CodeBrix.Platform.UI.Core.UIReqs.Canvas;

/// <summary>
/// A rectangle of one captured frame, together with the words a failure message needs to say
/// which rectangle it was. Every appearance assertion in the harness is made about one of
/// these.
/// </summary>
public sealed class Region
{
	private readonly TestFrame _frame;

	/// <summary>Builds a region, clamped to the frame it belongs to.</summary>
	/// <param name="frame">The captured frame.</param>
	/// <param name="bounds">The rectangle of interest, in device pixels.</param>
	/// <param name="description">What the rectangle is, in the words a feature file used.</param>
	/// <exception cref="InvalidOperationException">The rectangle lies outside the frame.</exception>
	public Region(TestFrame frame, DeviceRect bounds, string description)
	{
		ArgumentNullException.ThrowIfNull(frame);
		ArgumentException.ThrowIfNullOrEmpty(description);

		_frame = frame;
		Description = description;
		Bounds = bounds.Intersect(new DeviceRect(0, 0, frame.Width, frame.Height));

		if (Bounds.IsEmpty)
		{
			throw new InvalidOperationException(
				$"The region of {description} is {bounds}, which holds no pixel of the "
				+ $"{frame.Width} x {frame.Height} frame.");
		}
	}

	/// <summary>The frame the region is cut from.</summary>
	public TestFrame Frame => _frame;

	/// <summary>The region's rectangle, clamped to the frame.</summary>
	public DeviceRect Bounds { get; }

	/// <summary>What the region is, in the words the feature file used.</summary>
	public string Description { get; }

	/// <summary>How many pixels the region holds.</summary>
	public int PixelCount => Bounds.Width * Bounds.Height;

	/// <summary>One pixel of the region, addressed relative to the region's top left corner.</summary>
	/// <param name="x">The column, relative to the region.</param>
	/// <param name="y">The row, relative to the region.</param>
	/// <returns>The pixel's colour.</returns>
	public SKColor this[int x, int y] => _frame.GetPixel(Bounds.X + x, Bounds.Y + y);

	/// <summary>Every pixel of the region, row by row.</summary>
	/// <returns>The pixels.</returns>
	public IEnumerable<SKColor> Pixels()
	{
		for (var y = Bounds.Y; y < Bounds.Bottom; y++)
		{
			for (var x = Bounds.X; x < Bounds.Right; x++)
			{
				yield return _frame.GetPixel(x, y);
			}
		}
	}

	/// <summary>The whole frame as one region.</summary>
	/// <param name="frame">The captured frame.</param>
	/// <returns>A region covering every pixel.</returns>
	public static Region WholeFrame(TestFrame frame)
	{
		ArgumentNullException.ThrowIfNull(frame);
		return new Region(frame, new DeviceRect(0, 0, frame.Width, frame.Height), "the whole panel");
	}

	/// <summary>The region as "&lt;description&gt; at (x,y) w x h in frame n".</summary>
	/// <returns>The description.</returns>
	public override string ToString() =>
		$"{Description} at {Bounds} in frame {_frame.Sequence.ToString(CultureInfo.InvariantCulture)}";
}
