using System;
using System.IO;

namespace PolyHavenBrowser.Helpers;

/// <summary>
/// Small helpers for the native "Save PDF as…" file dialog.
/// </summary>
public static class FileDialogHelper
{
    /// <summary>
    /// Turns the path a picker hands back into a real file-system path. The Linux Skia heads
    /// build theirs out of the desktop portal's <c>file://</c> URI and leave it
    /// percent-encoded, so a name with a space in it arrives as <c>My%20Model.pdf</c> and
    /// would be written to disk under that literal name; accented names fare worse still
    /// (<c>Ölberg</c> arrives as <c>%C3%96lberg</c>). Nothing is decoded unless the text
    /// really does carry escapes, so paths from heads that already return a plain one — the
    /// Win32 and WPF save dialogs — pass through untouched.
    /// </summary>
    public static string ToFileSystemPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) { return path; }

        //A head that hands back the whole URI rather than just its path.
        if (path.StartsWith("file:", StringComparison.OrdinalIgnoreCase)
            && Uri.TryCreate(path, UriKind.Absolute, out var uri)
            && uri.IsFile)
        {
            return uri.LocalPath;
        }

        return HasPercentEscape(path) ? Uri.UnescapeDataString(path) : path;
    }

    //True when the text holds at least one "%" followed by two hex digits. A literal percent
    //  sign that is not the start of an escape (say "100% done.pdf") leaves the path alone.
    private static bool HasPercentEscape(string text)
    {
        for (var i = 0; i + 2 < text.Length; i++)
        {
            if (text[i] == '%' && Uri.IsHexDigit(text[i + 1]) && Uri.IsHexDigit(text[i + 2]))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The WinRT <c>FileSavePicker</c> (Skia heads and native WinUI) creates an empty
    /// placeholder file at the chosen path for a brand-new name. Remove it — but only when it
    /// is genuinely empty — so a chosen path behaves like a pure destination. A file that has
    /// content is never deleted; the user explicitly chose to save over it.
    /// </summary>
    public static void RemoveEmptyPlaceholder(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) { return; }

        try
        {
            var info = new FileInfo(path);
            if (info.Exists && info.Length == 0)
            {
                info.Delete();
            }
        }
        catch
        {
            //Leave the file in place if it cannot be removed; it will simply be overwritten.
        }
    }
}
