using CodeBrix.Graphics3D.Gltf2.Schema2;

namespace KenneyAssetBrowser.Rendering;

/// <summary>
/// A loaded model that keeps its glTF document alive so animation clips can be CPU-baked on
/// demand: pick a name from <see cref="AnimationNames"/>, call <see cref="BakeClip"/>, and
/// hand the clip to the preview canvas for playback.
/// </summary>
public sealed class AnimatedModel
{
    private readonly ModelRoot _root;
    private readonly IReadOnlyList<Material?> _materialOrder;

    internal AnimatedModel(ModelRoot root, LoadedModel model,
        IReadOnlyList<string> animationNames, IReadOnlyList<Material?> materialOrder)
    {
        _root = root;
        Model = model;
        AnimationNames = animationNames;
        _materialOrder = materialOrder;
    }

    /// <summary>The renderer-ready model (the rest pose, for animated sources).</summary>
    public LoadedModel Model { get; }

    /// <summary>The names of the model's animations (empty for a static model).</summary>
    public IReadOnlyList<string> AnimationNames { get; }

    /// <summary>Whether the source model carries any animations.</summary>
    public bool HasAnimations => AnimationNames.Count > 0;

    /// <summary>
    /// Bakes one animation into per-frame vertex data aligned with <see cref="Model"/>'s
    /// primitives. Cheap for Kenney-scale models (tens of triangles); call it off the UI thread
    /// for large ones.
    /// </summary>
    /// <param name="animationName">A name from <see cref="AnimationNames"/>.</param>
    /// <param name="frameRate">The bake rate, in frames per second.</param>
    /// <returns>The baked clip.</returns>
    /// <exception cref="ArgumentException">The model has no animation with that name.</exception>
    /// <exception cref="InvalidDataException">The animation evaluates to a different geometry
    /// layout than the rest pose (not expected for well-formed models).</exception>
    public ModelAnimationClip BakeClip(string animationName, int frameRate = 24)
    {
        var index = AnimationNames.ToList().IndexOf(animationName);
        if (index < 0)
        {
            throw new ArgumentException($"The model has no animation named '{animationName}'.", nameof(animationName));
        }

        var animation = _root.LogicalAnimations[index];
        var duration = Math.Max(animation.Duration, 1f / frameRate);
        var frameCount = Math.Min(600, Math.Max(2, (int)MathF.Ceiling(duration * frameRate)));

        var frames = new List<ModelAnimationFrame>(frameCount);
        for (var frame = 0; frame < frameCount; frame++)
        {
            //Sample [0, duration) so a looping clip does not hold its duplicate end pose
            var time = duration * frame / frameCount;
            frames.Add(BakeFrame(animation, time));
        }

        return new ModelAnimationClip
        {
            Name = animationName,
            Duration = duration,
            FrameRate = frameRate,
            Frames = frames,
        };
    }

    private ModelAnimationFrame BakeFrame(Animation animation, float time)
    {
        var groups = GltfModelLoader.EvaluateGroups(_root, animation, time);

        var primitives = new List<ModelFramePrimitive>(_materialOrder.Count);
        for (var i = 0; i < _materialOrder.Count; i++)
        {
            var material = _materialOrder[i];
            var group = groups.FirstOrDefault(g => ReferenceEquals(g.Material, material));
            if (group == null || group.Positions.Count != Model.Primitives[i].Positions.Length)
            {
                throw new InvalidDataException(
                    $"Animation '{animation.Name}' evaluates to a different geometry layout than the rest pose.");
            }

            primitives.Add(new ModelFramePrimitive
            {
                Positions = group.Positions.ToArray(),
                Normals = group.Normals.ToArray(),
            });
        }

        return new ModelAnimationFrame { Primitives = primitives };
    }
}
