using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Windows.Storage.Pickers;

namespace CodeBrix.Android.Services;

/// <summary>
/// The pure parts of the Storage Access Framework pickers: the MIME types an open request asks for, the
/// document name a save request suggests, and safe local names for picked documents.
/// </summary>
internal static class PickerRequests
{
    /// <summary>The broad MIME type of an open request: image/* or video/* for those libraries, else */*.</summary>
    internal static string BaseMimeType(PickerLocationId location) => location switch
    {
        PickerLocationId.PicturesLibrary => "image/*",
        PickerLocationId.VideosLibrary => "video/*",
        _ => "*/*",
    };

    /// <summary>
    /// The MIME types (EXTRA_MIME_TYPES) of a FileTypeFilter: null - no restriction - when the filter is empty,
    /// holds "*", or names an extension the map does not know (the user must still be able to pick it).
    /// </summary>
    /// <param name="fileTypeFilter">The extensions (".pdf" or "pdf").</param>
    /// <param name="mimeTypeOfExtension">The platform map (extension without the dot -> MIME type, or null).</param>
    internal static string[] FilterMimeTypes(IEnumerable<string> fileTypeFilter, Func<string, string> mimeTypeOfExtension)
    {
        var filter = fileTypeFilter?.Where(f => !string.IsNullOrWhiteSpace(f)).ToList() ?? new List<string>();
        if (filter.Count == 0 || filter.Any(f => f.Trim() == "*"))
        {
            return null;
        }

        var mimeTypes = new List<string>();
        foreach (var extension in filter)
        {
            var mime = mimeTypeOfExtension(extension.Trim().TrimStart('.').ToLowerInvariant());
            if (string.IsNullOrEmpty(mime))
            {
                return null;
            }

            if (!mimeTypes.Contains(mime))
            {
                mimeTypes.Add(mime);
            }
        }

        return mimeTypes.ToArray();
    }

    /// <summary>
    /// The extension a save request creates: DefaultFileExtension, else the first extension of the first
    /// FileTypeChoices entry, else none ("").
    /// </summary>
    internal static string SaveExtension(string defaultFileExtension, IDictionary<string, IList<string>> fileTypeChoices)
    {
        if (!string.IsNullOrWhiteSpace(defaultFileExtension))
        {
            return Normalize(defaultFileExtension);
        }

        var first = fileTypeChoices?.Values.SelectMany(v => v ?? Array.Empty<string>()).FirstOrDefault(e => !string.IsNullOrWhiteSpace(e) && e.Trim() != "*");
        return first == null ? string.Empty : Normalize(first);

        static string Normalize(string extension) => "." + extension.Trim().TrimStart('.');
    }

    /// <summary>The document title a save request suggests: the suggested name with the save extension added when missing.</summary>
    internal static string SaveTitle(string suggestedFileName, string extension)
    {
        var name = string.IsNullOrWhiteSpace(suggestedFileName) ? "Untitled" : suggestedFileName.Trim();
        return string.IsNullOrEmpty(extension) || name.EndsWith(extension, StringComparison.OrdinalIgnoreCase) ? name : name + extension;
    }

    /// <summary>A file name safe for the local copy of a picked document (no path separators or reserved characters).</summary>
    internal static string SafeFileName(string displayName, string fallback)
    {
        var name = string.IsNullOrWhiteSpace(displayName) ? fallback : displayName.Trim();
        var invalid = Path.GetInvalidFileNameChars().Concat(new[] { '/', '\\', ':' }).ToHashSet();
        var safe = new string(name.Select(c => invalid.Contains(c) || char.IsControl(c) ? '_' : c).ToArray()).Trim('.', ' ');
        return string.IsNullOrEmpty(safe) ? fallback : safe;
    }
}
