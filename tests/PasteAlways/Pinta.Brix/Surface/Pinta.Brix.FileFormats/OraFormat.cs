//
// OraFormat.cs
//
// Author:
//       Maia Kozheva <sikon@ubuntu.com>
//
// Copyright (c) 2010 Maia Kozheva <sikon@ubuntu.com>
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Xml;
using Pinta.Brix.Engine;
using Pinta.Brix.Engine.Drawing;
using SkiaSharp;

//was previously: namespace Pinta.Core;
namespace Pinta.Brix.FileFormats;

/// <summary>
/// OpenRaster (.ora) import/export: a zip archive holding per-layer PNGs plus
/// a stack.xml describing layer order, position, opacity, visibility and
/// composite operation. Layer PNG encode/decode is done with SkiaSharp
/// (upstream used the GTK toolkit's pixbuf codecs).
/// </summary>
public sealed class OraFormat : IImageImporter, IImageExporter
{
	private const int THUMB_MAX_SIZE = 256;

	/// <inheritdoc/>
	public Document Import (string file)
	{
		using FileStream stream = File.OpenRead (file);
		using ZipArchive zipfile = new (stream);

		XmlDocument stackXml = new ();

		ZipArchiveEntry stackXmlEntry = zipfile.GetEntry ("stack.xml") ?? throw new XmlException ("No 'stack.xml' found in OpenRaster file");
		stackXml.Load (stackXmlEntry.Open ());

		// NRT - This makes a lot of assumptions that the file will be perfectly
		// valid that we need to guard against.
		XmlElement imageElement = stackXml.DocumentElement!;

		Size imageSize = new (
			Width: int.Parse (imageElement.GetAttribute ("w")),
			Height: int.Parse (imageElement.GetAttribute ("h"))
		);

		Document newDocument = new (
			PintaCore.Tools,
			PintaCore.Workspace,
			imageSize,
			file,
			"ora");

		XmlElement stackElement = (XmlElement) stackXml.GetElementsByTagName ("stack")[0]!;
		XmlNodeList layerElements = stackElement.GetElementsByTagName ("layer");

		if (layerElements.Count == 0)
			throw new XmlException ("No layers found in OpenRaster file");

		for (int i = 0; i < layerElements.Count; i++) {

			XmlElement layerElement = (XmlElement) layerElements[i]!;

			PointI position = new (
				X: int.Parse (GetAttribute (layerElement, "x", "0")),
				Y: int.Parse (GetAttribute (layerElement, "y", "0"))
			);

			string name = GetAttribute (layerElement, "name", $"Layer {i}");

			try {
				// Upstream extracted each layer PNG to a temporary file for the
				// toolkit codec; SkiaSharp decodes straight from memory.
				ZipArchiveEntry zf = zipfile.GetEntry (layerElement.GetAttribute ("src")) ?? throw new XmlException ("Missing layer in OpenRaster file");
				using Stream s = zf.Open ();
				using MemoryStream layerData = new ();
				s.CopyTo (layerData);

				UserLayer layer = newDocument.Layers.CreateLayer (name);
				newDocument.Layers.Insert (layer, 0);

				string visibility = GetAttribute (layerElement, "visibility", "visible");
				if (visibility == "hidden") {
					layer.Hidden = true;
				}

				layer.Opacity = double.Parse (GetAttribute (layerElement, "opacity", "1"), GetFormat ());
				layer.BlendMode = StandardToBlendMode (GetAttribute (layerElement, "composite-op", "svg:src-over"));

				using SKBitmap layerBitmap = SKBitmap.Decode (layerData.ToArray ()) ?? throw new FormatException ("Could not decode layer image in OpenRaster file");
				using (SKCanvas canvas = new (layer.Surface.Bitmap))
					canvas.DrawBitmap (layerBitmap, position.X, position.Y, SKSamplingOptions.Default, paint: null);
				layer.Surface.MarkDirty ();
			} catch {
				// Translators: {0} is the name of a layer, and {1} is the path to a .ora file.
				string details = Translations.GetString ("Could not import layer \"{0}\" from {1}", name, file);
				try {
					_ = PintaCore.Chrome.ShowMessageDialog (Translations.GetString ("Error"), details);
				} catch (NullReferenceException) {
					// Headless: no message-dialog handler has been installed by a UI layer.
					Console.Error.WriteLine (details);
				}
			}
		}

		return newDocument;
	}

	private static CultureInfo GetFormat ()
		=> CultureInfo.CreateSpecificCulture ("en");

	private static string GetAttribute (XmlElement element, string attribute, string defValue)
	{
		string ret = element.GetAttribute (attribute);
		return string.IsNullOrEmpty (ret) ? defValue : ret;
	}

	private static Size GetThumbDimensions (int width, int height)
	{
		if (width <= THUMB_MAX_SIZE && height <= THUMB_MAX_SIZE)
			return new (width, height);
		else if (width > height)
			return new (THUMB_MAX_SIZE, (int) ((double) height / width * THUMB_MAX_SIZE));
		else
			return new ((int) ((double) width / height * THUMB_MAX_SIZE), THUMB_MAX_SIZE);
	}

	private static byte[] GetLayerXmlData (IReadOnlyList<UserLayer> layers)
	{
		using MemoryStream ms = new ();
		using XmlTextWriter writer = new (ms, System.Text.Encoding.UTF8) {
			Formatting = Formatting.Indented,
		};

		writer.WriteStartElement ("image");
		writer.WriteAttributeString ("w", layers[0].Surface.Width.ToString ());
		writer.WriteAttributeString ("h", layers[0].Surface.Height.ToString ());
		writer.WriteAttributeString ("version", "0.0.5"); // Current version of the spec.

		writer.WriteStartElement ("stack");

		// ORA stores layers top to bottom
		for (int i = layers.Count - 1; i >= 0; i--) {
			var layer = layers[i];
			writer.WriteStartElement ("layer");
			writer.WriteAttributeString ("opacity", string.Format (GetFormat (), "{0:0.00}", layer.Opacity));
			writer.WriteAttributeString ("name", layer.Name);
			writer.WriteAttributeString ("composite-op", BlendModeToStandard (layer.BlendMode));
			writer.WriteAttributeString ("src", "data/layer" + i.ToString () + ".png");

			if (layer.Hidden)
				writer.WriteAttributeString ("visibility", "hidden");

			writer.WriteEndElement ();
		}

		writer.WriteEndElement (); // stack
		writer.WriteEndElement (); // image

		writer.Close ();

		return ms.ToArray ();
	}

	/// <inheritdoc/>
	public void Export (Document document, string file)
	{
		using FileStream file_stream = File.Create (file);
		using ZipArchive archive = new (file_stream, ZipArchiveMode.Create);
		using ImageSurface flattened = document.GetFlattenedImage ();
		AddMimeEntry (archive);
		AddLayerEntries (archive, document);
		AddStackEntry (archive, document);
		AddMergedImage (archive, flattened);
		AddThumbnail (archive, flattened);
	}

	private static void AddMimeEntry (ZipArchive archive)
	{
		byte[] mimeBytes = System.Text.Encoding.ASCII.GetBytes ("image/openraster");
		ZipArchiveEntry mimeEntry = archive.CreateEntry ("mimetype", CompressionLevel.NoCompression);
		using Stream mimeStream = mimeEntry.Open ();
		mimeStream.Write (mimeBytes, 0, mimeBytes.Length);
	}

	private static void AddLayerEntries (ZipArchive archive, Document document)
	{
		for (int i = 0; i < document.Layers.UserLayers.Count; i++) {
			byte[] buf = EncodePng (document.Layers.UserLayers[i].Surface.Bitmap);
			ZipArchiveEntry layerEntry = archive.CreateEntry ($"data/layer{i}.png");
			using Stream layerStream = layerEntry.Open ();
			layerStream.Write (buf, 0, buf.Length);
		}
	}

	private static void AddStackEntry (ZipArchive archive, Document document)
	{
		byte[] userLayerBytes = GetLayerXmlData (document.Layers.UserLayers);
		ZipArchiveEntry stackEntry = archive.CreateEntry ("stack.xml");
		using Stream stackStream = stackEntry.Open ();
		stackStream.Write (userLayerBytes, 0, userLayerBytes.Length);
	}

	private static void AddMergedImage (ZipArchive archive, ImageSurface flattened)
	{
		byte[] mergedImageBytes = EncodePng (flattened.Bitmap);
		ZipArchiveEntry mergedImageEntry = archive.CreateEntry ("mergedimage.png");
		using Stream mergedImageStream = mergedImageEntry.Open ();
		mergedImageStream.Write (mergedImageBytes, 0, mergedImageBytes.Length);
	}

	private static void AddThumbnail (ZipArchive archive, ImageSurface flattened)
	{
		Size newSize = GetThumbDimensions (flattened.Width, flattened.Height);
		SKImageInfo thumbInfo = new (newSize.Width, newSize.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
		using SKBitmap thumb = new (thumbInfo);
		flattened.Bitmap.ScalePixels (thumb, new SKSamplingOptions (SKFilterMode.Linear)); // Bilinear, matching upstream
		byte[] thumbnailBytes = EncodePng (thumb);
		ZipArchiveEntry thumbnailEntry = archive.CreateEntry ("Thumbnails/thumbnail.png");
		using Stream thumbnailStream = thumbnailEntry.Open ();
		thumbnailStream.Write (thumbnailBytes, 0, thumbnailBytes.Length);
	}

	private static byte[] EncodePng (SKBitmap bitmap)
	{
		using SKImage image = SKImage.FromBitmap (bitmap) ?? throw new FormatException ("Could not snapshot surface for PNG encoding");
		using SKData data = image.Encode (SKEncodedImageFormat.Png, 100) ?? throw new FormatException ("Could not encode layer to PNG");
		return data.ToArray ();
	}

	private static string BlendModeToStandard (BlendMode mode)
		=> mode switch {
			BlendMode.Multiply => "svg:multiply",
			BlendMode.ColorBurn => "svg:color-burn",
			BlendMode.ColorDodge => "svg:color-dodge",
			BlendMode.Overlay => "svg:overlay",
			BlendMode.Difference => "svg:difference",
			BlendMode.Lighten => "svg:lighten",
			BlendMode.Darken => "svg:darken",
			BlendMode.Screen => "svg:screen",
			BlendMode.Xor => "svg:xor",
			BlendMode.HardLight => "svg:hard-light",
			BlendMode.SoftLight => "svg:soft-light",
			BlendMode.Color => "svg:color",
			BlendMode.Luminosity => "svg:luminosity",
			BlendMode.Hue => "svg:hue",
			BlendMode.Saturation => "svg:saturation",
			_ => "svg:src-over",
		};

	private static BlendMode StandardToBlendMode (string mode)
	{
		switch (mode) {
			case "svg:src-over":
				return BlendMode.Normal;
			case "svg:multiply":
				return BlendMode.Multiply;
			case "svg:color-burn":
				return BlendMode.ColorBurn;
			case "svg:color-dodge":
				return BlendMode.ColorDodge;
			case "svg:overlay":
				return BlendMode.Overlay;
			case "svg:difference":
				return BlendMode.Difference;
			case "svg:lighten":
				return BlendMode.Lighten;
			case "svg:darken":
				return BlendMode.Darken;
			case "svg:screen":
				return BlendMode.Screen;
			case "svg:xor":
				return BlendMode.Xor;
			case "svg:hard-light":
				return BlendMode.HardLight;
			case "svg:soft-light":
				return BlendMode.SoftLight;
			case "svg:color":
				return BlendMode.Color;
			case "svg:luminosity":
				return BlendMode.Luminosity;
			case "svg:hue":
				return BlendMode.Hue;
			case "svg:saturation":
				return BlendMode.Saturation;
			default:
				Console.WriteLine ("Unrecognized composite-op: {0}, using Normal.", mode);
				return BlendMode.Normal;
		}
	}
}
