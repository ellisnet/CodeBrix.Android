namespace KenneyAssetBrowser.Rendering;

/// <summary>
/// One CPU-baked animation of a model: evaluated vertex data for every frame, aligned with
/// the primitives of the <see cref="LoadedModel"/> it was baked against. Baking trades a
/// little memory for a renderer that never has to know about skinning or node animation —
/// playback is just swapping vertex buffers.
/// </summary>
public sealed class ModelAnimationClip
{
    /// <summary>The animation name, e.g. <c>walk</c>.</summary>
    public required string Name { get; init; }

    /// <summary>The animation duration, in seconds.</summary>
    public required float Duration { get; init; }

    /// <summary>The rate the frames were baked at, in frames per second.</summary>
    public required int FrameRate { get; init; }

    /// <summary>The baked frames, first to last (the clip loops).</summary>
    public required IReadOnlyList<ModelAnimationFrame> Frames { get; init; }
}

/// <summary>One baked frame of a <see cref="ModelAnimationClip"/>.</summary>
public sealed class ModelAnimationFrame
{
    /// <summary>Per-primitive vertex data, aligned with <see cref="LoadedModel.Primitives"/>.</summary>
    public required IReadOnlyList<ModelFramePrimitive> Primitives { get; init; }
}

/// <summary>The evaluated vertex data of one primitive at one animation frame.</summary>
public sealed class ModelFramePrimitive
{
    /// <summary>Vertex positions: 3 floats per vertex, same layout as the base primitive.</summary>
    public required float[] Positions { get; init; }

    /// <summary>Vertex normals: 3 floats per vertex, same layout as the base primitive.</summary>
    public required float[] Normals { get; init; }
}
