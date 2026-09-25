using System.Numerics;
using CodeBrix.Graphics3D.Gltf2.Geometry;
using CodeBrix.Graphics3D.Gltf2.Schema2;

namespace KenneyAssetBrowser.Rendering;

/// <summary>
/// The default <see cref="IModelLoader"/>, backed by CodeBrix.Graphics3D.Gltf2. Loads .glb and .gltf
/// files (Kenney 3D kits ship a GLB per model, whose colormap texture sits beside it),
/// bakes node transforms into the vertex data, generates smooth normals when the source
/// has none, and decodes base color textures via CodeBrix.Imaging.
/// </summary>
public sealed class GltfModelLoader : IModelLoader
{
    /// <inheritdoc />
    public LoadedModel Load(Stream stream) => Load(stream, resolveDependency: null);

    /// <summary>
    /// Loads a .glb binary from a stream, resolving any external references (textures) through
    /// resolveDependency — needed because some exporters (Kenney kits included) ship a GLB that
    /// still references its texture as a sibling file rather than embedding it.
    /// </summary>
    /// <param name="stream">The stream holding the .glb bytes.</param>
    /// <param name="resolveDependency">Returns the bytes of a referenced resource given its
    /// (GLB-relative) URI, or <c>null</c> when the resource cannot be found; <c>null</c> to
    /// refuse all external references.</param>
    /// <exception cref="InvalidDataException">The stream does not contain a loadable model.</exception>
    public LoadedModel Load(Stream stream, Func<string, byte[]?>? resolveDependency)
    {
        ArgumentNullException.ThrowIfNull(stream);
        return Convert(ReadRoot(stream, resolveDependency));
    }

    /// <summary>
    /// Loads a .glb binary from a stream like <see cref="Load(Stream, Func{string, byte[]})"/>,
    /// but keeps the model's animations available: the result can CPU-bake any of its
    /// animation clips for playback in the preview.
    /// </summary>
    /// <param name="stream">The stream holding the .glb bytes.</param>
    /// <param name="resolveDependency">Returns the bytes of a referenced resource given its
    /// (GLB-relative) URI, or <c>null</c> when the resource cannot be found; <c>null</c> to
    /// refuse all external references.</param>
    /// <exception cref="InvalidDataException">The stream does not contain a loadable model.</exception>
    public AnimatedModel LoadAnimated(Stream stream, Func<string, byte[]?>? resolveDependency)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var root = ReadRoot(stream, resolveDependency);

        if (root.LogicalAnimations.Count == 0)
        {
            return new AnimatedModel(root, Convert(root), [], []);
        }

        //Animated models are built from an evaluated rest pose instead of the static node
        //walk, so the primitive layout matches the per-frame evaluations exactly.
        var (model, materialOrder) = ConvertEvaluated(root, animation: null, time: 0f);
        var names = root.LogicalAnimations
            .Select((animation, index) =>
                string.IsNullOrWhiteSpace(animation.Name) ? $"animation {index + 1}" : animation.Name)
            .ToList();
        return new AnimatedModel(root, model, names, materialOrder);
    }

    private static ModelRoot ReadRoot(Stream stream, Func<string, byte[]?>? resolveDependency)
    {
        try
        {
            if (resolveDependency == null)
            {
                return ModelRoot.ReadGLB(stream);
            }

            var context = ReadContext.Create(assetName =>
            {
                var bytes = resolveDependency(Uri.UnescapeDataString(assetName));
                return bytes == null
                    ? throw new FileNotFoundException($"The model references '{assetName}', which was not found.")
                    : new ArraySegment<byte>(bytes);
            });
            return context.ReadBinarySchema2(stream);
        }
        catch (Exception ex) when (ex is not InvalidDataException)
        {
            throw new InvalidDataException("The stream does not contain a loadable glTF binary (.glb) model.", ex);
        }
    }

    //One material bucket of an evaluated scene: flat triangle-soup vertex lists
    internal sealed class EvaluatedGroup(Material? material)
    {
        public Material? Material { get; } = material;
        public List<float> Positions { get; } = [];
        public List<float> Normals { get; } = [];
        public List<float> TexCoords { get; } = [];
    }

    //Evaluates the scene (optionally under an animation at a point in time) into per-material
    //vertex buckets. The bucket order is deterministic for a given model, so frames baked at
    //different times stay aligned with the rest-pose primitives.
    internal static List<EvaluatedGroup> EvaluateGroups(ModelRoot root, Animation? animation, float time)
    {
        var scene = root.DefaultScene ?? (root.LogicalScenes.Count > 0 ? root.LogicalScenes[0] : null);
        if (scene is null)
        {
            throw new InvalidDataException("The glTF model has no scene.");
        }

        var groups = new List<EvaluatedGroup>();
        var indexByMaterial = new Dictionary<Material, int>();
        var nullMaterialIndex = -1;

        foreach (var (a, b, c, material) in Toolkit.EvaluateTriangles(scene, null, animation, time))
        {
            int groupIndex;
            if (material == null)
            {
                if (nullMaterialIndex < 0)
                {
                    nullMaterialIndex = groups.Count;
                    groups.Add(new EvaluatedGroup(null));
                }
                groupIndex = nullMaterialIndex;
            }
            else if (!indexByMaterial.TryGetValue(material, out groupIndex))
            {
                groupIndex = groups.Count;
                indexByMaterial[material] = groupIndex;
                groups.Add(new EvaluatedGroup(material));
            }

            var group = groups[groupIndex];
            AppendVertex(group, a);
            AppendVertex(group, b);
            AppendVertex(group, c);
        }

        return groups;
    }

    private static void AppendVertex(EvaluatedGroup group, IVertexBuilder vertex)
    {
        var geometry = vertex.GetGeometry();
        var position = geometry.GetPosition();
        group.Positions.Add(position.X);
        group.Positions.Add(position.Y);
        group.Positions.Add(position.Z);

        var normal = geometry.TryGetNormal(out var n) ? n : Vector3.UnitY;
        group.Normals.Add(normal.X);
        group.Normals.Add(normal.Y);
        group.Normals.Add(normal.Z);

        var material = vertex.GetMaterial();
        var uv = material.MaxTextCoords > 0 ? material.GetTexCoord(0) : Vector2.Zero;
        group.TexCoords.Add(uv.X);
        group.TexCoords.Add(uv.Y);
    }

    //Builds a LoadedModel from evaluated buckets, returning the material order so animation
    //frames baked later can be matched back to the primitives.
    private static (LoadedModel Model, IReadOnlyList<Material?> MaterialOrder) ConvertEvaluated(
        ModelRoot root, Animation? animation, float time)
    {
        var groups = EvaluateGroups(root, animation, time);
        if (groups.Count == 0)
        {
            throw new InvalidDataException("The glTF model contains no triangle geometry.");
        }

        var materials = new List<ModelMaterial>();
        var materialOrder = new List<Material?>();
        var primitives = new List<ModelPrimitive>();
        var boundsMin = new Vector3(float.PositiveInfinity);
        var boundsMax = new Vector3(float.NegativeInfinity);
        var vertexSum = Vector3.Zero;
        var vertexCount = 0L;

        foreach (var group in groups)
        {
            var materialIndex = -1;
            if (group.Material != null)
            {
                materialIndex = materials.Count;
                materials.Add(ConvertMaterial(group.Material));
            }
            materialOrder.Add(group.Material);

            var positions = group.Positions.ToArray();
            var indices = new uint[positions.Length / 3];
            for (var i = 0; i < indices.Length; i++) { indices[i] = (uint)i; }

            primitives.Add(new ModelPrimitive
            {
                Positions = positions,
                Normals = group.Normals.ToArray(),
                TexCoords = group.TexCoords.ToArray(),
                Indices = indices,
                MaterialIndex = materialIndex,
            });

            for (var i = 0; i < positions.Length; i += 3)
            {
                var p = new Vector3(positions[i], positions[i + 1], positions[i + 2]);
                boundsMin = Vector3.Min(boundsMin, p);
                boundsMax = Vector3.Max(boundsMax, p);
                vertexSum += p;
                vertexCount++;
            }
        }

        var model = new LoadedModel
        {
            Name = root.DefaultScene?.Name,
            Primitives = primitives,
            Materials = materials,
            BoundsMin = boundsMin,
            BoundsMax = boundsMax,
            Pivot = vertexCount > 0 ? vertexSum / vertexCount : null,
        };
        return (model, materialOrder);
    }

    /// <inheritdoc />
    public LoadedModel LoadFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ModelRoot model;
        try
        {
            model = ModelRoot.Load(path);
        }
        catch (Exception ex) when (ex is not InvalidDataException)
        {
            throw new InvalidDataException($"'{path}' is not a loadable glTF model.", ex);
        }

        return Convert(model);
    }

    private static LoadedModel Convert(ModelRoot model)
    {
        var materials = new List<ModelMaterial>(model.LogicalMaterials.Count);
        foreach (var material in model.LogicalMaterials)
        {
            materials.Add(ConvertMaterial(material));
        }

        var primitives = new List<ModelPrimitive>();
        var boundsMin = new Vector3(float.PositiveInfinity);
        var boundsMax = new Vector3(float.NegativeInfinity);
        var vertexSum = Vector3.Zero;
        var vertexCount = 0L;

        var scene = model.DefaultScene ?? (model.LogicalScenes.Count > 0 ? model.LogicalScenes[0] : null);
        if (scene is null)
        {
            throw new InvalidDataException("The glTF model has no scene.");
        }

        // glTF is a tree of nodes, each with a local transform; a mesh's final world position is
        // its node's accumulated transform. We flatten the tree (Walk) and bake each node's world
        // matrix into its vertices, so the renderer can draw everything with one shared MVP (no
        // per-node transforms). While baking, we accumulate the bounding box (for camera framing)
        // and the vertex centroid (for the orbit pivot).
        foreach (var node in Walk(scene.VisualChildren))
        {
            if (node.Mesh is null)
            {
                continue;
            }

            var worldMatrix = node.WorldMatrix;
            foreach (var primitive in node.Mesh.Primitives)
            {
                var converted = ConvertPrimitive(primitive, worldMatrix);
                if (converted is null)
                {
                    continue;
                }

                primitives.Add(converted);
                for (var i = 0; i < converted.Positions.Length; i += 3)
                {
                    var p = new Vector3(converted.Positions[i], converted.Positions[i + 1], converted.Positions[i + 2]);
                    boundsMin = Vector3.Min(boundsMin, p);
                    boundsMax = Vector3.Max(boundsMax, p);
                    vertexSum += p;
                    vertexCount++;
                }
            }
        }

        if (primitives.Count == 0)
        {
            throw new InvalidDataException("The glTF model contains no triangle geometry.");
        }

        return new LoadedModel
        {
            Name = model.Asset?.Copyright is null ? model.DefaultScene?.Name : model.DefaultScene?.Name,
            Primitives = primitives,
            Materials = materials,
            BoundsMin = boundsMin,
            BoundsMax = boundsMax,
            Pivot = vertexCount > 0 ? vertexSum / vertexCount : null,
        };
    }

    private static IEnumerable<Node> Walk(IEnumerable<Node> nodes)
    {
        foreach (var node in nodes)
        {
            yield return node;
            foreach (var child in Walk(node.VisualChildren))
            {
                yield return child;
            }
        }
    }

    private static ModelPrimitive? ConvertPrimitive(MeshPrimitive primitive, Matrix4x4 worldMatrix)
    {
        var positionAccessor = primitive.GetVertexAccessor("POSITION");
        if (positionAccessor is null)
        {
            return null;
        }

        var triangles = primitive.GetTriangleIndices().ToList();
        if (triangles.Count == 0)
        {
            return null;
        }

        var sourcePositions = positionAccessor.AsVector3Array();
        var sourceNormals = primitive.GetVertexAccessor("NORMAL")?.AsVector3Array();
        var sourceTexCoords = primitive.GetVertexAccessor("TEXCOORD_0")?.AsVector2Array();
        var vertexCount = sourcePositions.Count;

        var positions = new float[vertexCount * 3];
        var texCoords = new float[vertexCount * 2];
        for (var i = 0; i < vertexCount; i++)
        {
            var world = Vector3.Transform(sourcePositions[i], worldMatrix);
            positions[i * 3] = world.X;
            positions[i * 3 + 1] = world.Y;
            positions[i * 3 + 2] = world.Z;

            if (sourceTexCoords is not null)
            {
                texCoords[i * 2] = sourceTexCoords[i].X;
                texCoords[i * 2 + 1] = sourceTexCoords[i].Y;
            }
        }

        var indices = new uint[triangles.Count * 3];
        for (var i = 0; i < triangles.Count; i++)
        {
            indices[i * 3] = (uint)triangles[i].A;
            indices[i * 3 + 1] = (uint)triangles[i].B;
            indices[i * 3 + 2] = (uint)triangles[i].C;
        }

        float[] normals;
        if (sourceNormals is not null)
        {
            // TransformNormal is exact for rigid transforms and uniform scale — the cases
            // that occur in practice in real-world glTF exports.
            normals = new float[vertexCount * 3];
            for (var i = 0; i < vertexCount; i++)
            {
                var n = Vector3.TransformNormal(sourceNormals[i], worldMatrix);
                if (n.LengthSquared() > 0f)
                {
                    n = Vector3.Normalize(n);
                }
                normals[i * 3] = n.X;
                normals[i * 3 + 1] = n.Y;
                normals[i * 3 + 2] = n.Z;
            }
        }
        else
        {
            normals = GenerateSmoothNormals(positions, indices);
        }

        return new ModelPrimitive
        {
            Positions = positions,
            Normals = normals,
            TexCoords = texCoords,
            Indices = indices,
            MaterialIndex = primitive.Material?.LogicalIndex ?? -1,
        };
    }

    /// <summary>Generates per-vertex normals by area-weighted accumulation of face normals.</summary>
    internal static float[] GenerateSmoothNormals(float[] positions, uint[] indices)
    {
        var normals = new float[positions.Length];
        for (var i = 0; i < indices.Length; i += 3)
        {
            var ia = (int)indices[i] * 3;
            var ib = (int)indices[i + 1] * 3;
            var ic = (int)indices[i + 2] * 3;

            var a = new Vector3(positions[ia], positions[ia + 1], positions[ia + 2]);
            var b = new Vector3(positions[ib], positions[ib + 1], positions[ib + 2]);
            var c = new Vector3(positions[ic], positions[ic + 1], positions[ic + 2]);
            var faceNormal = Vector3.Cross(b - a, c - a); // length ∝ face area → area weighting

            foreach (var offset in (ReadOnlySpan<int>)[ia, ib, ic])
            {
                normals[offset] += faceNormal.X;
                normals[offset + 1] += faceNormal.Y;
                normals[offset + 2] += faceNormal.Z;
            }
        }

        for (var i = 0; i < normals.Length; i += 3)
        {
            var n = new Vector3(normals[i], normals[i + 1], normals[i + 2]);
            n = n.LengthSquared() > 0f ? Vector3.Normalize(n) : Vector3.UnitY;
            normals[i] = n.X;
            normals[i + 1] = n.Y;
            normals[i + 2] = n.Z;
        }

        return normals;
    }

    private static ModelMaterial ConvertMaterial(Material material)
    {
        var baseColor = material.FindChannel("BaseColor");

        byte[]? textureRgba = null;
        var textureWidth = 0;
        var textureHeight = 0;
        var imageContent = baseColor?.Texture?.PrimaryImage?.Content;
        if (imageContent is { Content.Length: > 0 } content)
        {
            try
            {
                (textureRgba, textureWidth, textureHeight) = LdrImageDecoder.DecodeToRgbaBytes(content.Content.ToArray());
            }
            catch (InvalidDataException)
            {
                // An undecodable texture degrades to the base color factor rather than failing the load.
            }
        }

        // The standard glTF alpha mode...
        var alphaMode = material.Alpha switch
        {
            AlphaMode.MASK => ModelAlphaMode.Mask,
            AlphaMode.BLEND => ModelAlphaMode.Blend,
            _ => ModelAlphaMode.Opaque,
        };

        // ...plus KHR_materials_transmission glass (e.g. a camera's lens/flash/viewfinder),
        // which is alphaMode OPAQUE yet see-through. This preview doesn't implement real
        // transmission/refraction, so treat any transmissive material as translucent and render it
        // with the same fixed preview opacity as BLEND surfaces, rather than as an opaque solid.
        // FindChannel("Transmission") returns a channel only when the extension is present (glTF
        // exporters write it only for actual glass), so its presence is a reliable glass signal.
        if (alphaMode == ModelAlphaMode.Opaque && material.FindChannel("Transmission") is not null)
        {
            alphaMode = ModelAlphaMode.Blend;
        }

        return new ModelMaterial
        {
            Name = material.Name,
            AlphaMode = alphaMode,
            BaseColorFactor = baseColor?.Color ?? Vector4.One,
            BaseColorTextureRgba = textureRgba,
            BaseColorTextureWidth = textureWidth,
            BaseColorTextureHeight = textureHeight,
            DoubleSided = material.DoubleSided,
        };
    }
}
