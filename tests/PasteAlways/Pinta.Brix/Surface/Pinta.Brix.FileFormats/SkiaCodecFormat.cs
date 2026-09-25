// SkiaCodecFormat.cs
//
// Pinta.Brix importer/exporter backed by SkiaSharp's codecs, filling the role
// upstream's toolkit-pixbuf format class played: import decodes any format
// SKCodec understands (PNG, JPEG, WebP, BMP, GIF, ICO, ...) honoring the
// embedded EXIF orientation, and export encodes through SKImage.Encode for
// the formats Skia can write (PNG, JPEG, WebP).

using System;
using System.IO;
using Pinta.Brix.Engine;
using Pinta.Brix.Engine.Drawing;
using SkiaSharp;

namespace Pinta.Brix.FileFormats;

public class SkiaCodecFormat : IImageImporter, IImageExporter
{
	private readonly string file_type;
	private readonly SKEncodedImageFormat? encoded_format;

	/// <param name="fileType">
	/// The file type recorded on imported documents, e.g. "png" or "jpeg".
	/// </param>
	/// <param name="encodedFormat">
	/// The format used for export, or null if this instance is import-only.
	/// Skia can only encode PNG, JPEG and WebP.
	/// </param>
	public SkiaCodecFormat (string fileType, SKEncodedImageFormat? encodedFormat = null)
	{
		file_type = fileType;
		encoded_format = encodedFormat;
	}

	/// <inheritdoc/>
	public Document Import (string file)
	{
		using SKCodec codec = SKCodec.Create (file, out SKCodecResult createResult)
			?? throw new FormatException ($"Could not open image codec for '{file}': {createResult}");

		SKEncodedOrigin origin = codec.EncodedOrigin;

		SKImageInfo decodeInfo = new (
			codec.Info.Width,
			codec.Info.Height,
			SKColorType.Bgra8888,
			SKAlphaType.Premul);

		using SKBitmap decoded = SKBitmap.Decode (codec, decodeInfo)
			?? throw new FormatException ($"Could not decode image '{file}'");

		// EXIF origins 5-8 are transposed: the upright image swaps width/height.
		bool swapsDimensions = origin is
			SKEncodedOrigin.LeftTop or
			SKEncodedOrigin.RightTop or
			SKEncodedOrigin.RightBottom or
			SKEncodedOrigin.LeftBottom;

		Size imageSize =
			swapsDimensions
			? new (decoded.Height, decoded.Width)
			: new (decoded.Width, decoded.Height);

		Document newDocument = new (
			PintaCore.Tools,
			PintaCore.Workspace,
			imageSize,
			file,
			file_type);

		Layer layer = newDocument.Layers.AddNewLayer (System.IO.Path.GetFileName (file));

		using (SKCanvas canvas = new (layer.Surface.Bitmap)) {
			canvas.SetMatrix (GetOriginMatrix (origin, imageSize.Width, imageSize.Height));
			canvas.DrawBitmap (decoded, 0, 0, SKSamplingOptions.Default, paint: null);
		}

		layer.Surface.MarkDirty ();

		return newDocument;
	}

	/// <summary>
	/// Returns the transform that maps decoded (encoded-orientation) pixels to
	/// the upright image, mirroring Skia's SkEncodedOriginToMatrix. The width
	/// and height are the dimensions of the upright output image.
	/// </summary>
	private static SKMatrix GetOriginMatrix (SKEncodedOrigin origin, int w, int h)
		=> origin switch {
			SKEncodedOrigin.TopRight => new (-1, 0, w, 0, 1, 0, 0, 0, 1),
			SKEncodedOrigin.BottomRight => new (-1, 0, w, 0, -1, h, 0, 0, 1),
			SKEncodedOrigin.BottomLeft => new (1, 0, 0, 0, -1, h, 0, 0, 1),
			SKEncodedOrigin.LeftTop => new (0, 1, 0, 1, 0, 0, 0, 0, 1),
			SKEncodedOrigin.RightTop => new (0, -1, w, 1, 0, 0, 0, 0, 1),
			SKEncodedOrigin.RightBottom => new (0, -1, w, -1, 0, h, 0, 0, 1),
			SKEncodedOrigin.LeftBottom => new (0, 1, 0, -1, 0, h, 0, 0, 1),
			_ => SKMatrix.Identity, // TopLeft (default)
		};

	/// <inheritdoc/>
	public void Export (Document document, string file)
	{
		if (encoded_format is not SKEncodedImageFormat format)
			throw new NotSupportedException ($"SkiaSharp cannot encode '{file_type}' images");

		using ImageSurface flattenedImage = document.GetFlattenedImage ();
		DoSave (flattenedImage, document, file, format);
	}

	/// <summary>
	/// Encodes the flattened image to the given file. The default encodes at
	/// maximum quality; subclasses override to customize (e.g. JPEG quality).
	/// </summary>
	protected virtual void DoSave (ImageSurface flattenedImage, Document document, string file, SKEncodedImageFormat format)
	{
		SaveBitmap (flattenedImage, file, format, 100);
	}

	/// <summary>
	/// Encodes the given surface to a file with the given format and quality.
	/// </summary>
	protected static void SaveBitmap (ImageSurface image, string file, SKEncodedImageFormat format, int quality)
	{
		using SKImage skImage = SKImage.FromBitmap (image.Bitmap)
			?? throw new FormatException ("Could not snapshot surface for encoding");
		using SKData data = skImage.Encode (format, quality)
			?? throw new NotSupportedException ($"SkiaSharp could not encode to {format}");
		using FileStream stream = File.Create (file);
		data.SaveTo (stream);
	}
}
