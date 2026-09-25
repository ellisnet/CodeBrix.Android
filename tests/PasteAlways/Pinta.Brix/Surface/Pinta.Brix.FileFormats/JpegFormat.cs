//
// JpegFormat.cs
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
using Pinta.Brix.Engine;
using Pinta.Brix.Engine.Drawing;
using SkiaSharp;

//was previously: namespace Pinta.Core;
namespace Pinta.Brix.FileFormats;

/// <summary>
/// JPEG import/export over the SkiaSharp codecs, with the upstream
/// compression-quality plumbing: the first save of a document in a session
/// raises <see cref="ModifyCompression"/> so a UI layer can show its quality
/// dialog (upstream showed a GTK dialog through its file actions).
/// </summary>
public sealed class JpegFormat : SkiaCodecFormat
{
	//The default JPG compression quality to use when no saved setting is loaded. This will usually
	//occur when Pinta is first run on a machine, although there are other possible cases as well.
	private const int DefaultQuality = 85;

	// Matches SettingNames.JPG_QUALITY (internal to Pinta.Brix.Engine).
	private const string JpgQualitySetting = "jpg-quality";

	/// <summary>
	/// Raised before a document's first JPEG save of the session so a UI layer
	/// can let the user adjust the compression quality. Handlers may update
	/// <see cref="ModifyCompressionEventArgs.Quality"/> or set
	/// <see cref="ModifyCompressionEventArgs.Cancel"/> to abort the save.
	/// With no handler installed the pending quality value is used as-is.
	/// </summary>
	public static event EventHandler<ModifyCompressionEventArgs>? ModifyCompression;

	public JpegFormat ()
		: base ("jpeg", SKEncodedImageFormat.Jpeg)
	{
	}

	/// <inheritdoc/>
	protected override void DoSave (ImageSurface flattenedImage, Document document, string file, SKEncodedImageFormat format)
	{
		//Load the JPG compression quality, but use the default value if there is no saved value.
		int level = PintaCore.Settings.GetSetting (JpgQualitySetting, DefaultQuality);

		//Check to see if the Document has been saved before.
		if (!document.HasBeenSavedInSession) {
			//Give the UI layer the chance to show the JPG export compression quality
			//dialog, with the default value being the one loaded in (or the default
			//value if it was not saved).
			level = RaiseModifyCompression (level);

			if (level == -1)
				throw new OperationCanceledException ();
		}

		//Store the "previous" JPG compression quality value (before saving with it).
		PintaCore.Settings.PutSetting (JpgQualitySetting, level);

		SaveBitmap (flattenedImage, file, format, level);
	}

	private int RaiseModifyCompression (int defaultQuality)
	{
		ModifyCompressionEventArgs args = new (defaultQuality);
		ModifyCompression?.Invoke (this, args);
		return args.Cancel ? -1 : args.Quality;
	}
}
