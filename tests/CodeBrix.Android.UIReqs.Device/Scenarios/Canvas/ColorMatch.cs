using System;
using System.Globalization;
using Windows.UI;
using SKColor = CodeBrix.Android.UIReqs.Device.Canvas.PixelColor;

namespace CodeBrix.Platform.UI.Core.UIReqs.Canvas;

/// <summary>
/// How two colours are judged the same. Matching is per-channel with a tolerance, on the RGB
/// of opaque pixels; a pixel that is not fully opaque is judged as it would be seen, composited
/// over the panel's known background.
/// </summary>
public static class ColorMatch
{
	/// <summary>The default per-channel tolerance, out of 255.</summary>
	public const int DefaultTolerance = 8;

	/// <summary>How many bits per channel survive quantization when ink colours are counted.</summary>
	public const int InkQuantizationBits = 4;

	/// <summary>Whether a captured pixel matches an expected colour.</summary>
	/// <param name="actual">The pixel as captured.</param>
	/// <param name="expected">The colour the scenario asked for.</param>
	/// <param name="background">The panel background, used when the pixel is not opaque.</param>
	/// <param name="tolerance">The per-channel tolerance.</param>
	/// <returns><c>true</c> when every channel is within the tolerance.</returns>
	public static bool Matches(SKColor actual, Color expected, Color background,
		int tolerance = DefaultTolerance)
	{
		var seen = Composite(actual, background);
		var wanted = Composite(ToSkia(expected), background);
		return Near(seen.Red, wanted.Red, tolerance)
			&& Near(seen.Green, wanted.Green, tolerance)
			&& Near(seen.Blue, wanted.Blue, tolerance);
	}

	/// <summary>Whether two captured pixels match each other.</summary>
	/// <param name="actual">One pixel.</param>
	/// <param name="other">The other pixel.</param>
	/// <param name="tolerance">The per-channel tolerance.</param>
	/// <returns><c>true</c> when every channel is within the tolerance.</returns>
	public static bool Matches(SKColor actual, SKColor other, int tolerance = DefaultTolerance) =>
		Near(actual.Red, other.Red, tolerance)
		&& Near(actual.Green, other.Green, tolerance)
		&& Near(actual.Blue, other.Blue, tolerance)
		&& Near(actual.Alpha, other.Alpha, tolerance);

	/// <summary>
	/// What a colour looks like on the panel: fully opaque colours are themselves, anything
	/// else is judged as it is seen, over the background.
	/// </summary>
	/// <param name="color">The colour to compose.</param>
	/// <param name="background">The panel background.</param>
	/// <returns>The composited, opaque colour.</returns>
	public static SKColor Composite(SKColor color, Color background)
	{
		if (color.Alpha == 255)
		{
			return color;
		}

		var alpha = color.Alpha / 255.0;
		return new SKColor(
			Blend(color.Red, background.R, alpha),
			Blend(color.Green, background.G, alpha),
			Blend(color.Blue, background.B, alpha),
			255);
	}

	/// <summary>Drops the low bits of every channel, so near-identical colours count as one.</summary>
	/// <param name="color">The colour to quantize.</param>
	/// <param name="bitsPerChannel">How many bits per channel to keep.</param>
	/// <returns>The quantized colour.</returns>
	public static SKColor Quantize(SKColor color, int bitsPerChannel = InkQuantizationBits)
	{
		if (bitsPerChannel is < 1 or > 8)
		{
			throw new ArgumentOutOfRangeException(nameof(bitsPerChannel), bitsPerChannel,
				"A channel has 8 bits, so between 1 and 8 of them can be kept.");
		}

		var mask = (byte) (0xFF << (8 - bitsPerChannel));
		return new SKColor(
			(byte) (color.Red & mask),
			(byte) (color.Green & mask),
			(byte) (color.Blue & mask),
			color.Alpha);
	}

	/// <summary>A colour as the #AARRGGBB a feature file would write.</summary>
	/// <param name="color">The colour.</param>
	/// <returns>The description.</returns>
	public static string Describe(SKColor color) => string.Create(CultureInfo.InvariantCulture,
		$"#{color.Alpha:X2}{color.Red:X2}{color.Green:X2}{color.Blue:X2}");

	/// <summary>A colour as the #AARRGGBB a feature file would write.</summary>
	/// <param name="color">The colour.</param>
	/// <returns>The description.</returns>
	public static string Describe(Color color) => string.Create(CultureInfo.InvariantCulture,
		$"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}");

	/// <summary>The Skia colour a framework colour stands for.</summary>
	/// <param name="color">The framework colour.</param>
	/// <returns>The same colour as Skia sees it.</returns>
	public static SKColor ToSkia(Color color) => new(color.R, color.G, color.B, color.A);

	private static bool Near(byte actual, byte expected, int tolerance) =>
		Math.Abs(actual - expected) <= tolerance;

	private static byte Blend(byte foreground, byte background, double alpha) =>
		(byte) Math.Clamp(Math.Round((foreground * alpha) + (background * (1 - alpha))), 0, 255);
}
