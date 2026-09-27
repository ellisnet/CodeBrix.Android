namespace PolyHavenBrowser.Rendering;

/// <summary>
/// One renderable triangle batch of a loaded model: flat vertex arrays (already
/// transformed into scene space, with node transforms baked in) plus triangle indices and
/// a material reference.
/// </summary>
public sealed class ModelPrimitive
{
    /// <summary>Vertex positions: 3 floats (x, y, z) per vertex, in scene space.</summary>
    public required float[] Positions { get; init; }

    /// <summary>Vertex normals: 3 floats per vertex, unit length, in scene space.</summary>
    public required float[] Normals { get; init; }

    /// <summary>Vertex texture coordinates: 2 floats (u, v) per vertex; zeros when the source has none.</summary>
    public required float[] TexCoords { get; init; }

    /// <summary>Triangle indices into the vertex arrays (three per triangle).</summary>
    public required uint[] Indices { get; init; }

    /// <summary>The index of this primitive's material in <see cref="LoadedModel.Materials"/>, or -1 for the default material.</summary>
    public int MaterialIndex { get; init; } = -1;

    /// <summary>The number of vertices.</summary>
    public int VertexCount => Positions.Length / 3;

    /// <summary>The number of triangles.</summary>
    public int TriangleCount => Indices.Length / 3;
}
