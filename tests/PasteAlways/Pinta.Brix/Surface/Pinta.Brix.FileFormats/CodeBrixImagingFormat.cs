// CodeBrixImagingFormat.cs
//
// Pinta.Brix importer/exporter backed by CodeBrix.Imaging, covering the
// formats SkiaSharp cannot encode (BMP, GIF, TIFF). Pixels are bridged
// between CodeBrix.Imaging's straight-alpha Bgra32 and the engine's
// premultiplied-alpha ColorBgra surfaces.

using System;
using System.IO;
using CodeBrix.Imaging.Formats;
using CodeBrix.Imaging.Formats.Bmp;
using CodeBrix.Imaging.Formats.Gif;
using CodeBrix.Imaging.Formats.Tiff;
using CodeBrix.Imaging.PixelFormats;
using Pinta.Brix.Engine;
using Pinta.Brix.Engine.Drawing;
using ImagingImage = CodeBrix.Imaging.Image;

namespace Pinta.Brix.FileFormats;

public sealed class CodeBrixImagingFormat : IImageImporter, IImageExporter
{
	private readonly string file_type;
	private readonly IImageFormat image_format;
	private readonly IImageEncoder encoder;

	private CodeBrixImagingFormat (string fileType, IImageFormat imageFormat, IImageEncoder encoder)
	{
		file_type = fileType;
		image_format = imageFormat;
		this.encoder = encoder;
	}

	/// <summary>Windows bitmap, written as uncompressed 32-bit BGRA.</summary>
	public static CodeBrixImagingFormat CreateBmp ()
		=> new (
			"bmp",
			BmpFormat.Instance,
			new BmpEncoder {
				BitsPerPixel = BmpBitsPerPixel.Pixel32,
				SupportTransparency = true,
			});

	/// <summary>GIF, quantized to a 256-color palette by the encoder.</summary>
	public static CodeBrixImagingFormat CreateGif ()
		=> new ("gif", GifFormat.Instance, new GifEncoder ());

	/// <summary>TIFF, written with the encoder's default settings.</summary>
	public static CodeBrixImagingFormat CreateTiff ()
		=> new ("tiff", TiffFormat.Instance, new TiffEncoder ());

	/// <inheritdoc/>
	public Document Import (string file)
	{
		using CodeBrix.Imaging.Image<Bgra32> image = ImagingImage.Load<Bgra32> (file);

		Size imageSize = new (image.Width, image.Height);

		Document newDocument = new (
			PintaCore.Tools,
			PintaCore.Workspace,
			imageSize,
			file,
			file_type);

		Layer layer = newDocument.Layers.AddNewLayer (System.IO.Path.GetFileName (file));

		Span<ColorBgra> destination = layer.Surface.GetPixelData ();
		Bgra32[] source = new Bgra32[image.Width * image.Height];
		image.CopyPixelDataTo (source);

		// CodeBrix.Imaging pixels are straight (unpremultiplied) alpha; the
		// engine's surfaces are premultiplied.
		for (int i = 0; i < source.Length; i++) {
			Bgra32 p = source[i];
			destination[i] = ColorBgra.FromBgra (p.B, p.G, p.R, p.A).ToPremultipliedAlpha ();
		}

		layer.Surface.MarkDirty ();

		return newDocument;
	}

	/// <inheritdoc/>
	public void Export (Document document, string file)
	{
		using ImageSurface flattenedImage = document.GetFlattenedImage ();

		ReadOnlySpan<ColorBgra> source = flattenedImage.GetReadOnlyPixelData ();
		Bgra32[] destination = new Bgra32[flattenedImage.Width * flattenedImage.Height];

		// Premultiplied engine pixels back to straight alpha for the encoder.
		for (int i = 0; i < destination.Length; i++) {
			ColorBgra p = source[i].ToStraightAlpha ();
			destination[i] = new Bgra32 (r: p.R, g: p.G, b: p.B, a: p.A);
		}

		using CodeBrix.Imaging.Image<Bgra32> image = ImagingImage.LoadPixelData<Bgra32> (
			destination,
			flattenedImage.Width,
			flattenedImage.Height,
			image_format);

		using FileStream stream = File.Create (file);
		encoder.Encode (image, stream);
	}
}
