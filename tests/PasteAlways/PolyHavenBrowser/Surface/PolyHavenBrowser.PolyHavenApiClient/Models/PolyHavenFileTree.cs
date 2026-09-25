using System.Text.Json;

namespace PolyHavenBrowser.PolyHavenApiClient;

/// <summary>
/// The complete file catalog of a Poly Haven asset, as returned by the <c>/files/{id}</c>
/// endpoint. The API returns an arbitrarily nested structure (for example
/// <c>hdri → 4k → hdr → file</c> for HDRIs, or <c>Diffuse → 4k → jpg → file</c> for
/// textures), which this type exposes as a traversable tree of <see cref="PolyHavenFileNode"/>
/// instances with <see cref="PolyHavenFileRef"/> leaves.
/// </summary>
public sealed class PolyHavenFileTree
{
    /// <summary>Creates a file tree from its top-level nodes.</summary>
    /// <param name="children">The top-level nodes keyed by name (for example <c>hdri</c> or <c>Diffuse</c>).</param>
    public PolyHavenFileTree(IReadOnlyDictionary<string, PolyHavenFileNode> children)
    {
        Children = children ?? throw new ArgumentNullException(nameof(children));
    }

    /// <summary>The top-level nodes keyed by name (for example <c>hdri</c>, <c>tonemapped</c>, or <c>Diffuse</c>).</summary>
    public IReadOnlyDictionary<string, PolyHavenFileNode> Children { get; }

    /// <summary>
    /// Finds the node at the given path, or returns <see langword="null"/> when any
    /// segment does not exist. Example: <c>Find("hdri", "4k")</c>.
    /// </summary>
    /// <param name="pathSegments">The node names to follow from the root.</param>
    public PolyHavenFileNode? Find(params string[] pathSegments)
    {
        ArgumentNullException.ThrowIfNull(pathSegments);
        if (pathSegments.Length == 0)
        {
            return null;
        }

        PolyHavenFileNode? current = null;
        var children = Children;
        foreach (var segment in pathSegments)
        {
            if (!children.TryGetValue(segment, out current))
            {
                return null;
            }

            children = current.Children;
        }

        return current;
    }

    /// <summary>
    /// Finds the file at the given path, or returns <see langword="null"/> when the path
    /// does not exist or refers to a grouping node rather than a file.
    /// Example: <c>FindFile("hdri", "4k", "hdr")</c>.
    /// </summary>
    /// <param name="pathSegments">The node names to follow from the root.</param>
    public PolyHavenFileRef? FindFile(params string[] pathSegments) => Find(pathSegments)?.File;

    /// <summary>
    /// Enumerates every file in the tree (depth-first), with each file's path expressed
    /// as its node names joined by <c>/</c> (for example <c>hdri/4k/hdr</c>).
    /// </summary>
    public IEnumerable<PolyHavenFileEntry> EnumerateFiles()
    {
        foreach (var entry in EnumerateFiles(Children, prefix: null))
        {
            yield return entry;
        }
    }

    private static IEnumerable<PolyHavenFileEntry> EnumerateFiles(
        IReadOnlyDictionary<string, PolyHavenFileNode> children, string? prefix)
    {
        foreach (var (name, node) in children)
        {
            var path = prefix is null ? name : $"{prefix}/{name}";
            if (node.File is { } file)
            {
                yield return new PolyHavenFileEntry(path, file);
            }
            else
            {
                foreach (var entry in EnumerateFiles(node.Children, path))
                {
                    yield return entry;
                }
            }
        }
    }

    /// <summary>Parses the raw JSON of a <c>/files/{id}</c> response into a tree.</summary>
    internal static PolyHavenFileTree Parse(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new PolyHavenApiException(
                $"Unexpected Poly Haven file list format: expected a JSON object but got {element.ValueKind}.");
        }

        return new PolyHavenFileTree(ParseChildren(element));
    }

    private static Dictionary<string, PolyHavenFileNode> ParseChildren(JsonElement obj)
    {
        var children = new Dictionary<string, PolyHavenFileNode>(StringComparer.Ordinal);
        foreach (var property in obj.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            children[property.Name] = IsFileObject(property.Value)
                ? new PolyHavenFileNode(property.Name, ParseFileRef(property.Value))
                : new PolyHavenFileNode(property.Name, ParseChildren(property.Value));
        }

        return children;
    }

    private static bool IsFileObject(JsonElement element) =>
        element.TryGetProperty("url", out var url) && url.ValueKind == JsonValueKind.String;

    private static PolyHavenFileRef ParseFileRef(JsonElement element)
    {
        var url = element.GetProperty("url").GetString()!;
        var md5 = element.TryGetProperty("md5", out var md5Element) && md5Element.ValueKind == JsonValueKind.String
            ? md5Element.GetString()
            : null;
        var size = element.TryGetProperty("size", out var sizeElement) && sizeElement.ValueKind == JsonValueKind.Number
            ? sizeElement.GetInt64()
            : 0;

        Dictionary<string, PolyHavenFileRef>? include = null;
        if (element.TryGetProperty("include", out var includeElement)
            && includeElement.ValueKind == JsonValueKind.Object)
        {
            include = new Dictionary<string, PolyHavenFileRef>(StringComparer.Ordinal);
            foreach (var property in includeElement.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.Object && IsFileObject(property.Value))
                {
                    include[property.Name] = ParseFileRef(property.Value);
                }
            }
        }

        return new PolyHavenFileRef { Url = url, Md5 = md5, Size = size, Include = include };
    }
}
