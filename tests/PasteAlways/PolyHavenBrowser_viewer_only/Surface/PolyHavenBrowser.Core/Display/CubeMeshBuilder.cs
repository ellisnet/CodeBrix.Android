using System;
using System.Numerics;
using SkiaSharp;
using PolyHavenBrowser.Rendering;

namespace PolyHavenBrowser.Display;

/// <summary>
/// Builds a unit cube as a renderer-ready <see cref="LoadedModel"/> with a Poly Haven texture
/// mapped flat (0..1) onto each of its six faces. Shown at a corner ("isometric") camera
/// angle, this presents the texture undistorted — the right way to preview a flat material
/// like plywood or brick.
/// </summary>
public static class CubeMeshBuilder
{
    // Each face: four corners (CCW seen from outside) + its outward normal. UVs are the
    // same (0,0)-(1,0)-(1,1)-(0,1) per face so the whole texture shows on every face.
    private static readonly (Vector3 A, Vector3 B, Vector3 C, Vector3 D, Vector3 Normal)[] Faces =
    [
        // +X
        (new(0.5f, -0.5f, 0.5f), new(0.5f, -0.5f, -0.5f), new(0.5f, 0.5f, -0.5f), new(0.5f, 0.5f, 0.5f), new(1, 0, 0)),
        // -X
        (new(-0.5f, -0.5f, -0.5f), new(-0.5f, -0.5f, 0.5f), new(-0.5f, 0.5f, 0.5f), new(-0.5f, 0.5f, -0.5f), new(-1, 0, 0)),
        // +Y
        (new(-0.5f, 0.5f, 0.5f), new(0.5f, 0.5f, 0.5f), new(0.5f, 0.5f, -0.5f), new(-0.5f, 0.5f, -0.5f), new(0, 1, 0)),
        // -Y
        (new(-0.5f, -0.5f, -0.5f), new(0.5f, -0.5f, -0.5f), new(0.5f, -0.5f, 0.5f), new(-0.5f, -0.5f, 0.5f), new(0, -1, 0)),
        // +Z
        (new(-0.5f, -0.5f, 0.5f), new(0.5f, -0.5f, 0.5f), new(0.5f, 0.5f, 0.5f), new(-0.5f, 0.5f, 0.5f), new(0, 0, 1)),
        // -Z
        (new(0.5f, -0.5f, -0.5f), new(-0.5f, -0.5f, -0.5f), new(-0.5f, 0.5f, -0.5f), new(0.5f, 0.5f, -0.5f), new(0, 0, -1)),
    ];

    private static readonly float[] FaceU = [0f, 1f, 1f, 0f];
    private static readonly float[] FaceV = [1f, 1f, 0f, 0f];

    /// <summary>Builds a unit cube textured with the given bitmap.</summary>
    /// <param name="texture">The decoded texture image; converted to RGBA if needed.</param>
    /// <param name="name">A display name for the model.</param>
    public static LoadedModel Build(SKBitmap texture, string name)
    {
        ArgumentNullException.ThrowIfNull(texture);
        var (rgba, width, height) = ToRgba(texture);

        var positions = new float[Faces.Length * 4 * 3];
        var normals = new float[Faces.Length * 4 * 3];
        var texCoords = new float[Faces.Length * 4 * 2];
        var indices = new uint[Faces.Length * 6];

        for (var f = 0; f < Faces.Length; f++)
        {
            var face = Faces[f];
            var corners = new[] { face.A, face.B, face.C, face.D };
            for (var c = 0; c < 4; c++)
            {
                var vertex = f * 4 + c;
                positions[vertex * 3] = corners[c].X;
                positions[vertex * 3 + 1] = corners[c].Y;
                positions[vertex * 3 + 2] = corners[c].Z;
                normals[vertex * 3] = face.Normal.X;
                normals[vertex * 3 + 1] = face.Normal.Y;
                normals[vertex * 3 + 2] = face.Normal.Z;
                texCoords[vertex * 2] = FaceU[c];
                texCoords[vertex * 2 + 1] = FaceV[c];
            }

            var baseVertex = (uint)(f * 4);
            var i = f * 6;
            indices[i] = baseVertex;
            indices[i + 1] = baseVertex + 1;
            indices[i + 2] = baseVertex + 2;
            indices[i + 3] = baseVertex;
            indices[i + 4] = baseVertex + 2;
            indices[i + 5] = baseVertex + 3;
        }

        var material = new ModelMaterial
        {
            Name = name,
            BaseColorTextureRgba = rgba,
            BaseColorTextureWidth = width,
            BaseColorTextureHeight = height,
        };

        var primitive = new ModelPrimitive
        {
            Positions = positions,
            Normals = normals,
            TexCoords = texCoords,
            Indices = indices,
            MaterialIndex = 0,
        };

        return new LoadedModel
        {
            Name = name,
            Primitives = [primitive],
            Materials = [material],
            BoundsMin = new Vector3(-0.5f, -0.5f, -0.5f),
            BoundsMax = new Vector3(0.5f, 0.5f, 0.5f),
        };
    }

    private static (byte[] Rgba, int Width, int Height) ToRgba(SKBitmap bitmap)
    {
        if (bitmap.ColorType == SKColorType.Rgba8888)
        {
            return (bitmap.Bytes, bitmap.Width, bitmap.Height);
        }

        using var converted = new SKBitmap(new SKImageInfo(bitmap.Width, bitmap.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
        if (!bitmap.CopyTo(converted, SKColorType.Rgba8888))
        {
            throw new InvalidOperationException("Unable to convert the texture image to RGBA.");
        }

        return (converted.Bytes, converted.Width, converted.Height);
    }
}
