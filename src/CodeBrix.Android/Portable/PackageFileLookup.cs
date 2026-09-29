using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;

namespace CodeBrix.Android.Portable;

/// <summary>
/// The application package's files by package-relative path (AP1.12, WPE1-13 contract IApplicationPackageFilesPlatform):
/// pure C# over two delegates - open a file, list a folder - so the path rules and the existence answer are tested
/// host-free; on the device the delegates are the APK's AssetManager (ApplicationPackageFilesAndroidPlatform).
/// </summary>
/// <remarks>
/// A path is '/'-separated and package-relative ("Assets/pulse.json"; the ms-appx URI's host, as written, first).
/// A leading '/' or '\' separators are tolerated; an empty path, an empty segment, "." or ".." names no file.
/// Folder listings are cached (an APK's assets never change while the app runs); every member is thread-safe.
/// </remarks>
internal sealed class PackageFileLookup
{
    private readonly Func<string, Stream> _open;
    private readonly Func<string, string[]> _list;
    private readonly ConcurrentDictionary<string, HashSet<string>> _folders = new(StringComparer.Ordinal);

    /// <summary>Creates the lookup.</summary>
    /// <param name="open">Opens a package file for reading; returns null when there is no such file.</param>
    /// <param name="list">Lists the entry names (files and folders) of a package folder ("" = the root); an empty array
    /// for a file or a missing folder.</param>
    internal PackageFileLookup(Func<string, Stream> open, Func<string, string[]> list)
    {
        _open = open ?? throw new ArgumentNullException(nameof(open));
        _list = list ?? throw new ArgumentNullException(nameof(list));
    }

    /// <summary>Normalizes a package-relative path.</summary>
    /// <param name="relativePath">The path Core passed.</param>
    /// <param name="path">The normalized path ('/'-separated, no leading '/').</param>
    /// <returns>False when the path cannot name a package file.</returns>
    internal static bool TryNormalize(string relativePath, out string path)
    {
        path = null;
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return false;
        }

        var candidate = relativePath.Replace('\\', '/').TrimStart('/');
        if (candidate.Length == 0)
        {
            return false;
        }

        foreach (var segment in candidate.Split('/'))
        {
            if (segment.Length == 0 || segment == "." || segment == "..")
            {
                return false;
            }
        }

        path = candidate;
        return true;
    }

    /// <summary>Whether the package holds a FILE at the path (a folder is not a file).</summary>
    /// <param name="relativePath">The package-relative path.</param>
    /// <returns>True when the file exists.</returns>
    internal bool FileExists(string relativePath)
    {
        if (!TryNormalize(relativePath, out var path))
        {
            return false;
        }

        var (folder, name) = Split(path);

        // Listed in its folder, and not a folder itself (an APK holds no empty folders, so a folder lists entries).
        return Folder(folder).Contains(name) && Folder(path).Count == 0;
    }

    /// <summary>Opens a package file for reading.</summary>
    /// <param name="relativePath">The package-relative path.</param>
    /// <returns>The stream (the caller disposes it), or null when there is no such file.</returns>
    internal Stream OpenRead(string relativePath)
    {
        if (!TryNormalize(relativePath, out var path))
        {
            return null;
        }

        var (folder, name) = Split(path);
        return Folder(folder).Contains(name) ? _open(path) : null;
    }

    private static (string Folder, string Name) Split(string path)
    {
        var slash = path.LastIndexOf('/');
        return slash < 0 ? (string.Empty, path) : (path.Substring(0, slash), path.Substring(slash + 1));
    }

    private HashSet<string> Folder(string folder) =>
        _folders.GetOrAdd(folder, f => new HashSet<string>(_list(f) ?? Array.Empty<string>(), StringComparer.Ordinal));
}
