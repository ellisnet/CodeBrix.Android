using CodeBrix.Compression.Zip;
using KenneyAssetBrowser.AssetRead.Models;
using KenneyAssetBrowser.AssetRead.Parsing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace KenneyAssetBrowser.AssetRead;

/// <summary>
/// An open Kenney asset bundle (.zip) archive that serves individual entry reads on demand,
/// without extracting the whole archive. Keep one instance open while browsing a bundle and
/// dispose it when the user moves on.
/// </summary>
public class BundleArchive : IDisposable
{
    private readonly ZipFile _zipFile;
    private readonly Dictionary<string, long> _indexesByEntryPath;

    //ZipFile entry streams share the underlying FileStream, so reads are serialized
    private readonly object _gate = new();

    /// <summary>
    /// Opens a bundle zip file for random-access entry reads.
    /// </summary>
    /// <param name="zipPath">The full path of the bundle zip file on disk.</param>
    public BundleArchive(string zipPath)
    {
        ZipPath = zipPath ?? throw new ArgumentNullException(nameof(zipPath));
        _zipFile = new ZipFile(zipPath);

        //ZipFile.GetEntry is an O(n) scan per lookup; build a name index once instead
        _indexesByEntryPath = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        var entries = new List<AssetEntry>();
        foreach (ZipEntry entry in _zipFile)
        {
            if (entry.IsDirectory) { continue; }
            _indexesByEntryPath[entry.Name] = entry.ZipFileIndex;
            entries.Add(new AssetEntry(entry.Name, Math.Max(0, entry.Size), AssetClassifier.Classify(entry.Name)));
        }

        Entries = entries.OrderBy(e => e.EntryPath, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>Gets the full path of the bundle zip file on disk.</summary>
    public string ZipPath { get; }

    /// <summary>Gets every file entry in the archive, classified by extension.</summary>
    public IReadOnlyList<AssetEntry> Entries { get; }

    /// <summary>
    /// Determines whether the archive contains a file entry with the given path.
    /// </summary>
    /// <param name="entryPath">The forward-slash path of the file inside the archive.</param>
    public bool HasEntry(string entryPath) =>
        entryPath != null && _indexesByEntryPath.ContainsKey(entryPath);

    /// <summary>
    /// Reads the uncompressed bytes of one archive entry.
    /// </summary>
    /// <param name="entryPath">The forward-slash path of the file inside the archive.</param>
    /// <returns>The entry's bytes, or <c>null</c> when the archive has no such entry.</returns>
    public byte[] ReadEntryBytes(string entryPath)
    {
        if (entryPath == null || !_indexesByEntryPath.TryGetValue(entryPath, out var index))
        {
            return null;
        }

        lock (_gate)
        {
            using var input = _zipFile.GetInputStream(index);
            using var buffer = new MemoryStream();
            input.CopyTo(buffer);
            return buffer.ToArray();
        }
    }

    /// <summary>
    /// Reads one archive entry as UTF-8 text.
    /// </summary>
    /// <param name="entryPath">The forward-slash path of the file inside the archive.</param>
    /// <returns>The entry's text, or <c>null</c> when the archive has no such entry.</returns>
    public string ReadEntryText(string entryPath)
    {
        var bytes = ReadEntryBytes(entryPath);
        return bytes == null ? null : Encoding.UTF8.GetString(bytes);
    }

    /// <summary>
    /// Reads the bytes of a resource that another entry references by relative path — e.g. the
    /// <c>Textures/colormap.png</c> a Kenney GLB model references beside itself. The path is
    /// resolved against the referencing entry's folder first, then each parent folder up to the
    /// archive root, and finally by bare file name anywhere in the archive.
    /// </summary>
    /// <param name="baseEntryPath">The archive path of the entry doing the referencing.</param>
    /// <param name="relativeUri">The referenced resource's relative path.</param>
    /// <returns>The resource's bytes, or <c>null</c> when nothing matches.</returns>
    public byte[] ReadDependencyBytes(string baseEntryPath, string relativeUri) =>
        ReadEntryBytes(ResolveDependencyPath(baseEntryPath, relativeUri));

    /// <summary>
    /// Resolves the archive path of a resource that another entry references by relative path,
    /// using the same rules as <see cref="ReadDependencyBytes"/> — so a resolved path can in
    /// turn anchor further references (a .tmx map resolves its .tsx tileset, whose path then
    /// resolves the tileset image).
    /// </summary>
    /// <param name="baseEntryPath">The archive path of the entry doing the referencing.</param>
    /// <param name="relativeUri">The referenced resource's relative path.</param>
    /// <returns>The matching entry's archive path, or <c>null</c> when nothing matches.</returns>
    public string ResolveDependencyPath(string baseEntryPath, string relativeUri)
    {
        if (baseEntryPath == null || string.IsNullOrWhiteSpace(relativeUri)) { return null; }

        var relative = relativeUri.Replace('\\', '/').TrimStart('/');
        var lastSlash = baseEntryPath.Replace('\\', '/').LastIndexOf('/');
        var folder = lastSlash < 0 ? string.Empty : baseEntryPath.Substring(0, lastSlash);

        //Try the reference against the entry's own folder, then each parent up to the root
        while (true)
        {
            var candidate = NormalizePath(folder.Length == 0 ? relative : folder + "/" + relative);
            if (HasEntry(candidate)) { return candidate; }
            if (folder.Length == 0) { break; }

            var parentSlash = folder.LastIndexOf('/');
            folder = parentSlash < 0 ? string.Empty : folder.Substring(0, parentSlash);
        }

        //Last resort: match the bare file name anywhere in the archive
        var fileNameSlash = relative.LastIndexOf('/');
        var fileName = fileNameSlash < 0 ? relative : relative.Substring(fileNameSlash + 1);
        var match = Entries.FirstOrDefault(e =>
            e.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase));
        return match?.EntryPath;
    }

    //Collapses "." and ".." segments so relative references resolve to real entry paths
    private static string NormalizePath(string path)
    {
        var segments = new List<string>();
        foreach (var segment in path.Split('/'))
        {
            if (segment.Length == 0 || segment == ".") { continue; }
            if (segment == "..")
            {
                if (segments.Count > 0) { segments.RemoveAt(segments.Count - 1); }
                continue;
            }
            segments.Add(segment);
        }

        return string.Join("/", segments);
    }

    /// <summary>Closes the underlying zip file.</summary>
    public void Dispose() => _zipFile.Close();
}
