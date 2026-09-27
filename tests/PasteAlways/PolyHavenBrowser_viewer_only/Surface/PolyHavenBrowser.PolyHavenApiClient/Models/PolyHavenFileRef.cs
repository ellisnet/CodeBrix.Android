namespace PolyHavenBrowser.PolyHavenApiClient;

/// <summary>
/// A reference to a single downloadable file belonging to a Poly Haven asset, as returned
/// by the <c>/files/{id}</c> endpoint. Pass instances to
/// <see cref="IPolyHavenApiClient.DownloadFileAsync(PolyHavenFileRef, Stream, IProgress{PolyHavenDownloadProgress}?, bool, CancellationToken)"/>
/// (or one of its overloads) to download the file.
/// </summary>
public sealed class PolyHavenFileRef
{
    /// <summary>The absolute download URL of the file.</summary>
    public required string Url { get; init; }

    /// <summary>The MD5 checksum of the file contents (lowercase hex), when provided.</summary>
    public string? Md5 { get; init; }

    /// <summary>The size of the file in bytes, or 0 when not provided.</summary>
    public long Size { get; init; }

    /// <summary>
    /// For bundle files such as <c>.blend</c> or <c>.gltf</c>, the additional files the
    /// bundle requires, keyed by the relative path they should be saved to (for example
    /// <c>textures/rock_diff_1k.jpg</c>).
    /// </summary>
    public IReadOnlyDictionary<string, PolyHavenFileRef>? Include { get; init; }
}
