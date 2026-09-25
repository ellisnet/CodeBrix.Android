// 
// ImageConverterManager.cs
//  
// Author:
//       Jonathan Pobst <monkey@jpobst.com>
// 
// Copyright (c) 2010 Jonathan Pobst
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

public sealed class ImageConverterManager
{
	private readonly SettingsManager settings_manager;
	public ImageConverterManager (SettingsManager settingsManager)
	{
		settings_manager = settingsManager;
	}

	// Pinta.Brix note: upstream seeded this list from the toolkit's codec
	// registry plus its own formats; in the port every format (codec-backed
	// and native alike) is registered by the file-formats library / app at
	// startup via RegisterFormat.
	private readonly List<FormatDescriptor> formats = [];

	public IEnumerable<FormatDescriptor> Formats
		=> formats;

	/// <summary>
	/// The filter entries an "open image" dialog should offer: one per format
	/// that can be imported, carrying the lower case extensions it reads.
	/// </summary>
	public IReadOnlyList<FileDialogFilter> GetImportFilters ()
		=> BuildFilters (f => f.IsImportAvailable ());

	/// <summary>
	/// The filter entries a "save image as" dialog should offer: one per
	/// format that can be exported, carrying the lower case extensions it
	/// writes.
	/// </summary>
	public IReadOnlyList<FileDialogFilter> GetExportFilters ()
		=> BuildFilters (f => f.IsExportAvailable ());

	/// <summary>
	/// Every lower case extension an "open image" dialog should accept, in
	/// registration order and with no duplicates.
	/// </summary>
	public IReadOnlyList<string> GetImportExtensions ()
		=> [.. GetImportFilters ().SelectMany (f => f.Extensions).Distinct ()];

	/// <summary>
	/// Builds one filter entry per available format. The registry lists every
	/// extension in both cases so that matching is case-insensitive; a dialog
	/// only wants one of each, so the upper case spellings are dropped here
	/// rather than by every caller.
	/// </summary>
	private List<FileDialogFilter> BuildFilters (Func<FormatDescriptor, bool> isAvailable)
	{
		List<FileDialogFilter> filters = [];

		foreach (FormatDescriptor format in formats.Where (isAvailable)) {

			List<string> extensions = [
				.. format.Extensions
					.Where (x => x.All (char.IsLower))
					.Select (x => $".{x}")];

			if (extensions.Count > 0)
				filters.Add (new FileDialogFilter (format.FilterName, extensions));
		}

		return filters;
	}

	/// <summary>
	/// Registers a new file format.
	/// </summary>
	public void RegisterFormat (FormatDescriptor fd)
	{
		formats.Add (fd);
	}

	/// <summary>
	/// Unregisters the file format for the given extension.
	/// </summary>
	public void UnregisterFormatByExtension (string extension)
	{
		string normalized = NormalizeExtension (extension);
		formats.RemoveAll (f => f.Extensions.Contains (normalized));
	}

	/// <summary>
	/// Returns the default format that should be used when saving a file.
	/// This is normally the last format that was chosen by the user.
	/// </summary>
	public FormatDescriptor GetDefaultSaveFormat ()
	{
		string extension = settings_manager.GetSetting<string> (SettingNames.DEFAULT_IMAGE_TYPE, "jpeg");

		FormatDescriptor? fd = GetFormatByExtension (extension);

		// We found the last one we used
		if (fd != null)
			return fd;

		// Return any format we have
		foreach (var f in Formats)
			if (f.IsExportAvailable ())
				return f;

		// We don't have any formats
		throw new InvalidOperationException ("There are no image formats supported.");
	}

	/// <summary>
	/// Finds the correct exporter to use for opening the given file, or null
	/// if no exporter exists for the file.
	/// </summary>
	public IImageExporter? GetExporterByFile (string file)
	{
		string extension = Path.GetExtension (file);
		return GetExporterByExtension (extension);
	}

	/// <summary>
	/// Finds the file format for the given file name, or null
	/// if no file format exists for that file.
	/// </summary>
	public FormatDescriptor? GetFormatByFile (string file)
	{
		string extension = Path.GetExtension (file);
		return GetFormatByExtension (extension);
	}

	/// <summary>
	/// Finds the correct importer to use for opening the given file, or null
	/// if no importer exists for the file.
	/// </summary>
	public IImageImporter? GetImporterByFile (string file)
	{
		string extension = Path.GetExtension (file);
		return extension == null ? null : GetImporterByExtension (extension);
	}

	/// <summary>
	/// Sets the default format used when saving files to the given extension.
	/// </summary>
	public void SetDefaultFormat (string extension)
	{
		string normalized = NormalizeExtension (extension);
		settings_manager.PutSetting (SettingNames.DEFAULT_IMAGE_TYPE, normalized);
	}

	/// <summary>
	/// Finds the correct exporter to use for the given file extension, or null
	/// if no exporter exists for that extension.
	/// </summary>
	private IImageExporter? GetExporterByExtension (string extension)
	{
		FormatDescriptor? format = GetFormatByExtension (extension);
		return format?.Exporter;
	}

	/// <summary>
	/// Finds the correct importer to use for the given file extension, or null
	/// if no importer exists for that extension.
	/// </summary>
	private IImageImporter? GetImporterByExtension (string extension)
	{
		FormatDescriptor? format = GetFormatByExtension (extension);
		return format?.Importer;
	}

	/// <summary>
	/// Finds the file format for the given file extension, or null
	/// if no file format exists for that extension.
	/// </summary>
	public FormatDescriptor? GetFormatByExtension (string extension)
	{
		string normalized = NormalizeExtension (extension);
		return Formats.Where (p => p.Extensions.Contains (normalized)).FirstOrDefault ();
	}

	/// <summary>
	/// Normalizes the extension.
	/// </summary>
	private static string NormalizeExtension (string extension)
		=> extension.ToLowerInvariant ().TrimStart ('.').Trim ();
}
