// FileFormats.cs
//
// Pinta.Brix file-format registration. Upstream seeded its format list from
// the GTK toolkit's codec registry inside the core image-converter manager;
// in the port the manager starts empty and the application registers this
// library's formats at startup via RegisterAll.

using Pinta.Brix.Engine;
using SkiaSharp;

namespace Pinta.Brix.FileFormats;

/// <summary>
/// Registers every file format this library provides with an
/// <see cref="ImageConverterManager"/>.
/// </summary>
public static class FileFormatsRegistration
{
	/// <summary>
	/// Registers all supported file formats. Call once at application startup:
	/// <c>FileFormatsRegistration.RegisterAll (PintaCore.ImageFormats);</c>
	/// </summary>
	public static void RegisterAll (ImageConverterManager formats)
	{
		// --- SkiaSharp-codec formats (Skia decodes and encodes these)

		SkiaCodecFormat pngHandler = new ("png", SKEncodedImageFormat.Png);
		formats.RegisterFormat (new FormatDescriptor (
			displayPrefix: "PNG",
			extensions: ["png", "PNG"],
			mimes: ["image/png"],
			importer: pngHandler,
			exporter: pngHandler));

		JpegFormat jpegHandler = new ();
		formats.RegisterFormat (new FormatDescriptor (
			displayPrefix: "JPEG",
			extensions: ["jpg", "jpeg", "JPG", "JPEG"],
			mimes: ["image/jpeg"],
			importer: jpegHandler,
			exporter: jpegHandler));

		SkiaCodecFormat webpHandler = new ("webp", SKEncodedImageFormat.Webp);
		formats.RegisterFormat (new FormatDescriptor (
			displayPrefix: "WEBP",
			extensions: ["webp", "WEBP"],
			mimes: ["image/webp"],
			importer: webpHandler,
			exporter: webpHandler));

		// --- Formats Skia decodes but cannot encode (CodeBrix.Imaging exports)

		formats.RegisterFormat (new FormatDescriptor (
			displayPrefix: "BMP",
			extensions: ["bmp", "BMP"],
			mimes: ["image/bmp"],
			importer: new SkiaCodecFormat ("bmp"),
			exporter: CodeBrixImagingFormat.CreateBmp ()));

		formats.RegisterFormat (new FormatDescriptor (
			displayPrefix: "GIF",
			extensions: ["gif", "GIF"],
			mimes: ["image/gif"],
			importer: new SkiaCodecFormat ("gif"),
			exporter: CodeBrixImagingFormat.CreateGif ()));

		// --- Formats Skia cannot decode at all (CodeBrix.Imaging both ways)

		CodeBrixImagingFormat tiffHandler = CodeBrixImagingFormat.CreateTiff ();
		formats.RegisterFormat (new FormatDescriptor (
			displayPrefix: "TIFF",
			extensions: ["tif", "tiff", "TIF", "TIFF"],
			mimes: ["image/tiff"],
			importer: tiffHandler,
			exporter: tiffHandler));

		// --- This library's own format implementations

		OraFormat oraHandler = new ();
		formats.RegisterFormat (new FormatDescriptor (
			displayPrefix: "OpenRaster",
			extensions: ["ora", "ORA"],
			mimes: ["image/openraster"],
			importer: oraHandler,
			exporter: oraHandler,
			supportsLayers: true));

		NetpbmPortablePixmap netpbmPortablePixmap = new ();
		formats.RegisterFormat (new FormatDescriptor (
			displayPrefix: "Netpbm Portable Pixmap",
			extensions: ["ppm", "PPM"],
			mimes: ["image/x-portable-pixmap"], // Not official, but conventional
			importer: netpbmPortablePixmap,
			exporter: netpbmPortablePixmap));

		// Export-only, matching upstream
		formats.RegisterFormat (new FormatDescriptor (
			displayPrefix: "TGA",
			extensions: ["tga", "TGA"],
			mimes: ["image/x-tga"],
			importer: null,
			exporter: new TgaExporter ()));
	}
}
