namespace PolyHavenBrowser.PolyHavenApiClient;

/// <summary>
/// A node in a <see cref="PolyHavenFileTree"/>. A node is either a file
/// (<see cref="IsFile"/> is <see langword="true"/> and <see cref="File"/> is non-null) or a
/// folder-like grouping with <see cref="Children"/> (for example a texture map name,
/// a resolution such as <c>4k</c>, or a file format such as <c>hdr</c>).
/// </summary>
public sealed class PolyHavenFileNode
{
    private static readonly IReadOnlyDictionary<string, PolyHavenFileNode> EmptyChildren =
        new Dictionary<string, PolyHavenFileNode>();

    /// <summary>Creates a file (leaf) node.</summary>
    /// <param name="name">The key of the node within its parent (for example <c>hdr</c>).</param>
    /// <param name="file">The file reference this node represents.</param>
    public PolyHavenFileNode(string name, PolyHavenFileRef file)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        File = file ?? throw new ArgumentNullException(nameof(file));
        Children = EmptyChildren;
    }

    /// <summary>Creates a grouping (folder) node.</summary>
    /// <param name="name">The key of the node within its parent (for example <c>4k</c>).</param>
    /// <param name="children">The child nodes, keyed by name.</param>
    public PolyHavenFileNode(string name, IReadOnlyDictionary<string, PolyHavenFileNode> children)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Children = children ?? throw new ArgumentNullException(nameof(children));
    }

    /// <summary>The key of this node within its parent (for example <c>hdri</c>, <c>4k</c>, or <c>exr</c>).</summary>
    public string Name { get; }

    /// <summary>The file reference when this node is a file; otherwise <see langword="null"/>.</summary>
    public PolyHavenFileRef? File { get; }

    /// <summary>The child nodes keyed by name; empty when this node is a file.</summary>
    public IReadOnlyDictionary<string, PolyHavenFileNode> Children { get; }

    /// <summary>Whether this node is a downloadable file rather than a grouping.</summary>
    public bool IsFile => File is not null;
}
