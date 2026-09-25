// 
// FormatDescriptor.cs
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
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Text;

//was previously: namespace Pinta.Core;
namespace Pinta.Brix.Engine;

/// <summary>
/// Describes information about a file format, such as the
/// supported file extensions.
/// </summary>
public sealed class FormatDescriptor
{
	/// <summary>
	/// A list of the supported extensions (for example, "jpeg" and "JPEG").
	/// </summary>
	public ImmutableArray<string> Extensions { get; }

	/// <summary>
	/// A list of supported MIME types (for example, "image/jpg" and "image/png").
	/// </summary>
	public ImmutableArray<string> Mimes { get; }

	/// <summary>
	/// The importer for this file format. This may be null if only exporting
	/// is supported for this format.
	/// </summary>
	public IImageImporter? Importer { get; }

	/// <summary>
	/// The exporter for this file format. This may be null if only importing
	/// is supported for this format.
	/// </summary>
	public IImageExporter? Exporter { get; }

	/// <summary>
	/// Display name for this format's file-dialog filter entry.
	/// (Upstream exposed a toolkit filter object; the UI layer builds its own
	/// picker filters from this name and the Extensions list.)
	/// </summary>
	public string FilterName { get; }

	/// <summary>
	/// Whether the format supports layers.
	/// </summary>
	public bool SupportsLayers { get; }

	/// <param name="displayPrefix">
	/// A descriptive name for the format, such as "OpenRaster". This will be displayed
	/// in the file dialog's filter.
	/// </param>
	/// <param name="extensions">A list of supported file extensions (for example, "jpeg" and "JPEG").</param>
	/// <param name="mimes">A list of supported file MIME types (for example, "image/jpeg" and "image/png").</param>
	/// <param name="importer">The importer for this file format, or null if importing is not supported.</param>
	/// <param name="exporter">The exporter for this file format, or null if exporting is not supported.</param>
	/// <param name="supportsLayers">Whether the format supports layers.</param>
	public FormatDescriptor (
		string displayPrefix,
		IEnumerable<string> extensions,
		IEnumerable<string> mimes,
		IImageImporter? importer,
		IImageExporter? exporter,
		bool supportsLayers = false)
	{
		if (importer == null && exporter == null)
			throw new ArgumentException ("Format descriptor is initialized incorrectly", $"{nameof (importer)}, {nameof (exporter)}");

		Extensions = [.. extensions]; // Create a read-only copy
		Mimes = [.. mimes]; // Create a read-only copy
		Importer = importer;
		Exporter = exporter;
		SupportsLayers = supportsLayers;

		StringBuilder formatNames = new ();

		foreach (string ext in this.Extensions) {
			if (formatNames.Length > 0)
				formatNames.Append (", ");
			formatNames.Append ($"*.{ext}");
		}

		// Translators: {0} is the file format (e.g. "OpenRaster") and {1} is a list of file extensions.
		FilterName = Translations.GetString ("{0} image ({1})", displayPrefix, formatNames);
	}

		[MemberNotNullWhen (returnValue: true, member: nameof (Exporter))]
	public bool IsExportAvailable () => Exporter is not null;

	[MemberNotNullWhen (returnValue: true, member: nameof (Importer))]
	public bool IsImportAvailable () => Importer is not null;
}
