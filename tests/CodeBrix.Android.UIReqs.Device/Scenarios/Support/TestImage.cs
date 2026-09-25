using System.IO;
using CodeBrix.Android.UIReqs.Protocol;
using Windows.UI;

namespace CodeBrix.Platform.UI.Core.UIReqs.Support;

/// <summary>
/// The picture the Image scenarios draw: a tiny two-colour PNG, made at run time rather than
/// carried as a file, so that what the scenario expects to see on the panel and what the file
/// holds cannot drift apart. Its left half is one colour and its right half another, which is
/// what makes a Stretch mode visible - Fill spreads the halves across the whole box, None
/// leaves them at their own size, and UniformToFill crops the halves rather than shrinking them.
/// </summary>
public static class TestImage
{
	/// <summary>The picture's width in pixels.</summary>
	public const int PictureWidth = 40;

	/// <summary>The picture's height in pixels.</summary>
	public const int PictureHeight = 20;

	/// <summary>Encodes the two-colour picture as a PNG.</summary>
	/// <param name="left">The colour of the picture's left half.</param>
	/// <param name="right">The colour of the picture's right half.</param>
	/// <returns>The PNG bytes.</returns>
	public static byte[] EncodePng(Color left, Color right)
	{
		// ANDROID PORT: the Platform harness drew this picture with SkiaSharp; the Android harness
		// carries no Skia, so the same two halves are written with the managed PNG codec.
		var pixels = new byte[PictureWidth * PictureHeight * 4];
		for (var y = 0; y < PictureHeight; y++)
		{
			for (var x = 0; x < PictureWidth; x++)
			{
				var color = x < PictureWidth / 2 ? left : right;
				var i = ((y * PictureWidth) + x) * 4;
				pixels[i] = color.R;
				pixels[i + 1] = color.G;
				pixels[i + 2] = color.B;
				pixels[i + 3] = color.A;
			}
		}

		return PngCodec.Encode(pixels, PictureWidth, PictureHeight);
	}

	public static Stream OpenPng(Color left, Color right) => new MemoryStream(EncodePng(left, right), false);

}
