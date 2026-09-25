namespace KenneyAssetBrowser.AssetRead.Models;

/// <summary>
/// One file inside a Kenney asset bundle (.zip) archive.
/// </summary>
public class AssetEntry
{
    /// <summary>
    /// Creates an entry for a single file inside a bundle archive.
    /// </summary>
    /// <param name="entryPath">The forward-slash path of the file inside the archive.</param>
    /// <param name="sizeBytes">The uncompressed size of the file, in bytes.</param>
    /// <param name="kind">The broad kind of the file, derived from its extension.</param>
    public AssetEntry(string entryPath, long sizeBytes, AssetKind kind)
    {
        EntryPath = entryPath ?? string.Empty;
        SizeBytes = sizeBytes;
        Kind = kind;

        var lastSlash = EntryPath.LastIndexOf('/');
        FileName = lastSlash < 0 ? EntryPath : EntryPath.Substring(lastSlash + 1);
        Category = lastSlash < 0
            ? string.Empty
            : EntryPath.Substring(0, lastSlash).Replace("/", " / ");

        var lastDot = FileName.LastIndexOf('.');
        Name = lastDot <= 0 ? FileName : FileName.Substring(0, lastDot);
        Extension = lastDot < 0 ? string.Empty : FileName.Substring(lastDot + 1).ToLowerInvariant();
    }

    /// <summary>Gets the forward-slash path of the file inside the archive.</summary>
    public string EntryPath { get; }

    /// <summary>Gets the file name (last path segment) including its extension.</summary>
    public string FileName { get; }

    /// <summary>Gets the file name without its extension.</summary>
    public string Name { get; }

    /// <summary>Gets the lower-case file extension without the leading dot, or an empty string.</summary>
    public string Extension { get; }

    /// <summary>
    /// Gets the folder chain the file lives in, rendered for display
    /// (e.g. <c>Models / GLB format</c>), or an empty string for a root file.
    /// </summary>
    public string Category { get; }

    /// <summary>Gets the uncompressed size of the file, in bytes.</summary>
    public long SizeBytes { get; }

    /// <summary>Gets the broad kind of the file, derived from its extension.</summary>
    public AssetKind Kind { get; }
}
