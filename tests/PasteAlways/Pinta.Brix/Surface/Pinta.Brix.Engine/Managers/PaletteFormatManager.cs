//
// PaletteFormats.cs
//
// Author:
//       Matthias Mailänder
//
// Copyright (c) 2017 
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
using System.IO;
using System.Linq;

//was previously: namespace Pinta.Core;
namespace Pinta.Brix.Engine;

public sealed class PaletteFormatManager
{
	public IReadOnlyList<PaletteDescriptor> Formats { get; }

	public PaletteFormatManager ()
	{
		PdnPalette pdnHandler = new ();
		GimpPalette gimpHandler = new ();
		PaintShopProPalette pspHandler = new ();

		Formats = [
			new PaletteDescriptor (
				"Paint.NET",
				["txt", "TXT"],
				pdnHandler,
				pdnHandler),
			new PaletteDescriptor (
				"GIMP",
				["gpl", "GPL"],
				gimpHandler,
				gimpHandler),
			new PaletteDescriptor (
				"PaintShop Pro",
				["pal", "PAL"],
				pspHandler,
				pspHandler),
		];
	}

	/// <summary>
	/// The filter entries a "load palette" dialog should offer: one per format
	/// that can be read.
	/// </summary>
	public IReadOnlyList<FileDialogFilter> GetLoadFilters ()
		=> BuildFilters (f => f.Loader is not null);

	/// <summary>
	/// The filter entries a "save palette as" dialog should offer: one per
	/// format that can be written.
	/// </summary>
	public IReadOnlyList<FileDialogFilter> GetSaveFilters ()
		=> BuildFilters (f => f.Saver is not null);

	/// <summary>
	/// Every extension a "load palette" dialog should accept, with no
	/// duplicates. Both spellings of each extension are offered, because the
	/// descriptors list both and a dialog filter is matched literally.
	/// </summary>
	public IReadOnlyList<string> GetLoadExtensions ()
		=> [.. GetLoadFilters ().SelectMany (f => f.Extensions).Distinct ()];

	/// <summary>
	/// Builds one filter entry per matching format, carrying every extension
	/// spelling the descriptor lists.
	/// </summary>
	private List<FileDialogFilter> BuildFilters (Func<PaletteDescriptor, bool> isAvailable)
	{
		List<FileDialogFilter> filters = [];

		foreach (PaletteDescriptor format in Formats.Where (isAvailable)) {

			List<string> extensions = [.. format.Extensions.Select (x => $".{x}")];

			if (extensions.Count > 0)
				filters.Add (new FileDialogFilter (format.FilterName, extensions));
		}

		return filters;
	}

	public PaletteDescriptor? GetFormatByFilename (string fileName)
	{
		string extension = Path.GetExtension (fileName);

		string normalized =
			extension
			.ToLowerInvariant ()
			.TrimStart ('.')
			.Trim ();

		return
			Formats
			.Where (p => p.Extensions.Contains (normalized))
			.FirstOrDefault ();
	}
}
