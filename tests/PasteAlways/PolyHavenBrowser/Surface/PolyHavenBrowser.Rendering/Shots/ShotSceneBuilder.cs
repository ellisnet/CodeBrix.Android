using System.Numerics;

namespace PolyHavenBrowser.Rendering;

/// <summary>
/// A model standing on its stage, ready to shoot: the composite <see cref="LoadedModel"/>
/// (stage primitives plus the model's own, untouched) and the stage primitives by
/// themselves so their lighting can be re-aimed per shot.
/// </summary>
public sealed class ShotScene
{
    internal ShotScene(
        LoadedModel composite,
        IReadOnlyList<ModelPrimitive> stagePrimitives,
        IReadOnlyList<ModelPrimitive> subjectPrimitives)
    {
        Composite = composite;
        StagePrimitives = stagePrimitives;
        SubjectPrimitives = subjectPrimitives;
    }

    /// <summary>
    /// The composite scene. Its bounds and pivot are the <b>model's</b> (not the stage's),
    /// so <see cref="OrbitCamera.FitToModel"/> frames the product, not the furniture.
    /// </summary>
    public LoadedModel Composite { get; }

    /// <summary>The stage's own primitives (floor and backdrop cove).</summary>
    public IReadOnlyList<ModelPrimitive> StagePrimitives { get; }

    /// <summary>The model's own primitives — what the shot camera frames on.</summary>
    public IReadOnlyList<ModelPrimitive> SubjectPrimitives { get; }

    /// <summary>
    /// Points every stage normal straight at the light, so the stage renders fully,
    /// evenly lit (its look comes from its baked textures, not from shading) while the
    /// model keeps real form from the same light. Call before each shot, then re-set the
    /// scene on the renderer so the changed normals re-upload.
    /// </summary>
    public void AimStageAtLight(Vector3 lightDirection)
    {
        var direction = lightDirection == Vector3.Zero ? Vector3.UnitY : Vector3.Normalize(lightDirection);
        foreach (var primitive in StagePrimitives)
        {
            var normals = primitive.Normals;
            for (var i = 0; i < normals.Length; i += 3)
            {
                normals[i] = direction.X;
                normals[i + 1] = direction.Y;
                normals[i + 2] = direction.Z;
            }
        }
    }
}

/// <summary>
/// Builds the photography set around a loaded model: a floor the model stands on (with
/// the contact shadow baked into its texture) and an "infinity cove" cylinder whose
/// vertical gradient forms the backdrop. Everything is plain CPU geometry and generated
/// textures flowing through the ordinary <see cref="ModelPrimitive"/>/<see cref="ModelMaterial"/>
/// path, so one <see cref="GlModelSceneRenderer"/> pass draws model and stage together
/// with correct depth and blending. The model itself is never modified.
/// </summary>
public static class ShotSceneBuilder
{
    //Stage proportions, in multiples of the model's bounding radius. The camera (at
    //  FitMargin ~1.3 it sits ~3.5 radii out) stays well inside the cove.
    private const float FloorHalfSizeFactor = 5.8f;
    private const float CoveRadiusFactor = 6.0f;
    private const float CoveHeightFactor = 9.0f;
    private const int CoveSegments = 64;

    //Baked floor texture sizes: generous when a real texture is resampled, small for a
    //  plain color that only carries the shadow.
    private const int TexturedFloorBake = 2048;
    private const int PlainFloorBake = 512;

    private const int GradientWidth = 4;
    private const int GradientHeight = 256;

    /// <summary>Builds the scene for one model on one stage.</summary>
    public static ShotScene Build(LoadedModel model, ShotStage stage)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(stage);

        var radius = MathF.Max(model.BoundsRadius, 0.001f);
        var center = model.BoundsCenter;
        var floorY = model.BoundsMin.Y;

        //The stage materials are appended after the model's, so the model primitives'
        //  material indices stay valid untouched.
        var floorMaterialIndex = model.Materials.Count;
        var coveMaterialIndex = model.Materials.Count + 1;

        var (floorPrimitive, floorMaterial) = BuildFloor(
            model, stage, center, floorY, radius * FloorHalfSizeFactor, floorMaterialIndex);
        var (covePrimitive, coveMaterial) = BuildCove(
            stage, center, floorY, radius, coveMaterialIndex);

        var primitives = new List<ModelPrimitive>(model.Primitives.Count + 2)
        {
            floorPrimitive,
            covePrimitive,
        };
        primitives.AddRange(model.Primitives);

        var materials = new List<ModelMaterial>(model.Materials)
        {
            floorMaterial,
            coveMaterial,
        };

        var composite = new LoadedModel
        {
            Name = model.Name,
            Primitives = primitives,
            Materials = materials,
            //The model's own bounds and pivot, so cameras frame the product, not the set.
            BoundsMin = model.BoundsMin,
            BoundsMax = model.BoundsMax,
            Pivot = model.Pivot,
        };

        return new ShotScene(composite, [floorPrimitive, covePrimitive], model.Primitives);
    }

    // ── Floor ─────────────────────────────────────────────────────────────

    private static (ModelPrimitive Primitive, ModelMaterial Material) BuildFloor(
        LoadedModel model, ShotStage stage, Vector3 center, float floorY, float halfSize,
        int materialIndex)
    {
        var positions = new[]
        {
            center.X - halfSize, floorY, center.Z - halfSize,
            center.X + halfSize, floorY, center.Z - halfSize,
            center.X + halfSize, floorY, center.Z + halfSize,
            center.X - halfSize, floorY, center.Z + halfSize,
        };
        var normals = new float[] { 0, 1, 0, 0, 1, 0, 0, 1, 0, 0, 1, 0 };
        var texCoords = new float[] { 0, 0, 1, 0, 1, 1, 0, 1 };
        var indices = new uint[] { 0, 1, 2, 0, 2, 3 };

        var bakeSize = stage.FloorTextureRgba is { Length: > 0 } ? TexturedFloorBake : PlainFloorBake;

        var primitive = new ModelPrimitive
        {
            Positions = positions,
            Normals = normals,
            TexCoords = texCoords,
            Indices = indices,
            MaterialIndex = materialIndex,
        };
        var material = new ModelMaterial
        {
            Name = "shot-stage-floor",
            BaseColorTextureRgba = BakeFloorTexture(model, stage, center, halfSize, bakeSize),
            BaseColorTextureWidth = bakeSize,
            BaseColorTextureHeight = bakeSize,
            DoubleSided = true,
        };
        return (primitive, material);
    }

    //Resamples the stage's floor texture (tiled at its physical world size) across the
    //  whole floor quad and multiplies in the model's soft contact shadow, producing one
    //  non-repeating texture with the shadow baked exactly where the model stands.
    private static byte[] BakeFloorTexture(
        LoadedModel model, ShotStage stage, Vector3 center, float halfSize, int bakeSize)
    {
        var pixels = new byte[bakeSize * bakeSize * 4];

        var source = stage.FloorTextureRgba;
        var sourceWidth = stage.FloorTextureWidth;
        var sourceHeight = stage.FloorTextureHeight;
        var hasSource = source is { Length: > 0 } && sourceWidth > 0 && sourceHeight > 0;

        //Tile the source at its physical size — but for a model larger than the tile, scale
        //  the pattern up with the model so the floor never turns into repeating mush.
        var radius = MathF.Max(model.BoundsRadius, 0.001f);
        var worldSize = MathF.Max(stage.FloorTextureWorldSize, radius);

        //The contact shadow: an ellipse fitted to the model's footprint, smoothly fading
        //  out. A thin model (near-zero extent on one axis) still casts a plausible pool
        //  of shadow, so each radius is floored at a fraction of the model's radius.
        var minShadowRadius = radius * 0.35f;
        var shadowRadiusX = MathF.Max((model.BoundsMax.X - model.BoundsMin.X) * 0.5f * 1.3f, minShadowRadius);
        var shadowRadiusZ = MathF.Max((model.BoundsMax.Z - model.BoundsMin.Z) * 0.5f * 1.3f, minShadowRadius);

        for (var py = 0; py < bakeSize; py++)
        {
            var worldZ = ((py + 0.5f) / bakeSize * 2f - 1f) * halfSize;
            for (var px = 0; px < bakeSize; px++)
            {
                var worldX = ((px + 0.5f) / bakeSize * 2f - 1f) * halfSize;

                byte r, g, b;
                if (hasSource)
                {
                    (r, g, b) = SampleBilinearWrapped(
                        source!, sourceWidth, sourceHeight,
                        (center.X + worldX) / worldSize,
                        (center.Z + worldZ) / worldSize);
                }
                else
                {
                    (r, g, b) = (stage.FloorColor.R, stage.FloorColor.G, stage.FloorColor.B);
                }

                //Smoothstep shadow falloff from the footprint ellipse.
                var distance = MathF.Sqrt(
                    worldX * worldX / (shadowRadiusX * shadowRadiusX) +
                    worldZ * worldZ / (shadowRadiusZ * shadowRadiusZ));
                var t = Math.Clamp(1f - distance, 0f, 1f);
                var shade = 1f - stage.ShadowStrength * (t * t * (3f - 2f * t));

                var offset = (py * bakeSize + px) * 4;
                pixels[offset] = (byte)(r * shade);
                pixels[offset + 1] = (byte)(g * shade);
                pixels[offset + 2] = (byte)(b * shade);
                pixels[offset + 3] = 255;
            }
        }

        return pixels;
    }

    private static (byte R, byte G, byte B) SampleBilinearWrapped(
        byte[] rgba, int width, int height, float u, float v)
    {
        //Wrap into [0,1), then into texel space centered on texel centers.
        var x = (u - MathF.Floor(u)) * width - 0.5f;
        var y = (v - MathF.Floor(v)) * height - 0.5f;

        var x0 = (int)MathF.Floor(x);
        var y0 = (int)MathF.Floor(y);
        var fx = x - x0;
        var fy = y - y0;

        x0 = ((x0 % width) + width) % width;
        y0 = ((y0 % height) + height) % height;
        var x1 = (x0 + 1) % width;
        var y1 = (y0 + 1) % height;

        var p00 = (y0 * width + x0) * 4;
        var p10 = (y0 * width + x1) * 4;
        var p01 = (y1 * width + x0) * 4;
        var p11 = (y1 * width + x1) * 4;

        byte LerpChannel(int channel)
        {
            var top = rgba[p00 + channel] + (rgba[p10 + channel] - rgba[p00 + channel]) * fx;
            var bottom = rgba[p01 + channel] + (rgba[p11 + channel] - rgba[p01 + channel]) * fx;
            return (byte)Math.Clamp(top + (bottom - top) * fy, 0f, 255f);
        }

        return (LerpChannel(0), LerpChannel(1), LerpChannel(2));
    }

    // ── Backdrop cove ─────────────────────────────────────────────────────

    private static (ModelPrimitive Primitive, ModelMaterial Material) BuildCove(
        ShotStage stage, Vector3 center, float floorY, float radius, int materialIndex)
    {
        var coveRadius = radius * CoveRadiusFactor;
        var coveHeight = radius * CoveHeightFactor;

        var vertexCount = (CoveSegments + 1) * 2;
        var positions = new float[vertexCount * 3];
        var normals = new float[vertexCount * 3];
        var texCoords = new float[vertexCount * 2];
        var indices = new uint[CoveSegments * 6];

        for (var segment = 0; segment <= CoveSegments; segment++)
        {
            var angle = segment / (float)CoveSegments * 2f * MathF.PI;
            var x = center.X + coveRadius * MathF.Sin(angle);
            var z = center.Z + coveRadius * MathF.Cos(angle);

            var bottom = segment * 2;
            var top = bottom + 1;

            positions[bottom * 3] = x;
            positions[bottom * 3 + 1] = floorY;
            positions[bottom * 3 + 2] = z;
            positions[top * 3] = x;
            positions[top * 3 + 1] = floorY + coveHeight;
            positions[top * 3 + 2] = z;

            //Placeholder normals (up); ShotScene.AimStageAtLight re-aims them per shot.
            normals[bottom * 3 + 1] = 1f;
            normals[top * 3 + 1] = 1f;

            texCoords[bottom * 2] = segment / (float)CoveSegments;
            texCoords[bottom * 2 + 1] = 0f;
            texCoords[top * 2] = segment / (float)CoveSegments;
            texCoords[top * 2 + 1] = 1f;
        }

        for (var segment = 0; segment < CoveSegments; segment++)
        {
            var bottom = (uint)(segment * 2);
            var offset = segment * 6;
            indices[offset] = bottom;
            indices[offset + 1] = bottom + 1;
            indices[offset + 2] = bottom + 2;
            indices[offset + 3] = bottom + 1;
            indices[offset + 4] = bottom + 3;
            indices[offset + 5] = bottom + 2;
        }

        var primitive = new ModelPrimitive
        {
            Positions = positions,
            Normals = normals,
            TexCoords = texCoords,
            Indices = indices,
            MaterialIndex = materialIndex,
        };
        var material = new ModelMaterial
        {
            Name = "shot-stage-cove",
            BaseColorTextureRgba = BuildGradientTexture(stage),
            BaseColorTextureWidth = GradientWidth,
            BaseColorTextureHeight = GradientHeight,
            DoubleSided = true,
        };
        return (primitive, material);
    }

    //The cove's vertical gradient: BackdropBottom at v=0 (floor level) to BackdropTop at v=1.
    private static byte[] BuildGradientTexture(ShotStage stage)
    {
        var pixels = new byte[GradientWidth * GradientHeight * 4];
        for (var y = 0; y < GradientHeight; y++)
        {
            var t = y / (float)(GradientHeight - 1);
            var r = (byte)(stage.BackdropBottom.R + (stage.BackdropTop.R - stage.BackdropBottom.R) * t);
            var g = (byte)(stage.BackdropBottom.G + (stage.BackdropTop.G - stage.BackdropBottom.G) * t);
            var b = (byte)(stage.BackdropBottom.B + (stage.BackdropTop.B - stage.BackdropBottom.B) * t);

            for (var x = 0; x < GradientWidth; x++)
            {
                var offset = (y * GradientWidth + x) * 4;
                pixels[offset] = r;
                pixels[offset + 1] = g;
                pixels[offset + 2] = b;
                pixels[offset + 3] = 255;
            }
        }
        return pixels;
    }
}
