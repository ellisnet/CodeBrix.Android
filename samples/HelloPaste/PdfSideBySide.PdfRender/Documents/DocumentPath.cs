using System;
using System.IO;

namespace PdfSideBySide.PdfRender.Documents;

/// <summary>
/// Path normalization and comparison shared by the document types, so "the same file" means
/// the same thing everywhere (full path, trailing separators dropped, case-insensitive on the
/// operating systems whose file systems are).
/// </summary>
internal static class DocumentPath
{
    private static readonly StringComparison Comparison =
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

    /// <summary>Returns the absolute, separator-trimmed form of path.</summary>
    public static string Normalize(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    }

    /// <summary>Whether pathA and pathB refer to the same file.</summary>
    public static bool AreSame(string pathA, string pathB) =>
        string.Equals(Normalize(pathA), Normalize(pathB), Comparison);
}
