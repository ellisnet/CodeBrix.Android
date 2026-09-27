namespace PolyHavenBrowser.PolyHavenApiClient;

/// <summary>
/// A single file discovered while enumerating a <see cref="PolyHavenFileTree"/>.
/// </summary>
/// <param name="Path">The file's location in the tree as node names joined by <c>/</c> (for example <c>hdri/4k/hdr</c>).</param>
/// <param name="File">The downloadable file reference.</param>
public sealed record PolyHavenFileEntry(string Path, PolyHavenFileRef File);
