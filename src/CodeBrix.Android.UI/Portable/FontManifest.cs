using System;
using System.Collections.Generic;
using System.Text.Json;

namespace CodeBrix.Android.UI.Portable;

/// <summary>
/// One face listed by a CodeBrix font package <c>.ttf.manifest</c> file.
/// </summary>
/// <param name="Weight">The OpenType weight (100-950).</param>
/// <param name="IsItalic">True for italic and oblique faces.</param>
/// <param name="Stretch">The stretch as the WinUI FontStretch value (1 = UltraCondensed ... 5 = Normal ... 9 = UltraExpanded).</param>
/// <param name="Source">The face's ms-appx URI (the manifest's family_name).</param>
internal readonly record struct FontFace(int Weight, bool IsItalic, int Stretch, string Source);

/// <summary>
/// The <c>.ttf.manifest</c> format the CodeBrix font packages ship beside a font family
/// (<c>{"fonts":[{"font_style":"Normal","font_weight":400,"font_stretch":"Normal",
/// "family_name":"ms-appx:///..."}]}</c>), and the face selection the desktop heads apply
/// (CSS font matching: style first, then stretch, then weight).
/// </summary>
internal sealed class FontManifest
{
    /// <summary>FontStretch.Normal.</summary>
    internal const int NormalStretch = 5;

    private FontManifest(IReadOnlyList<FontFace> faces) => Faces = faces;

    /// <summary>The faces listed by the manifest.</summary>
    internal IReadOnlyList<FontFace> Faces { get; }

    /// <summary>Parses manifest JSON. Throws <see cref="FormatException"/> for invalid content.</summary>
    internal static FontManifest Parse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json ?? string.Empty);
            var faces = new List<FontFace>();
            if (document.RootElement.TryGetProperty("fonts", out var fonts) && fonts.ValueKind == JsonValueKind.Array)
            {
                foreach (var font in fonts.EnumerateArray())
                {
                    var source = GetString(font, "family_name");
                    if (string.IsNullOrWhiteSpace(source))
                    {
                        continue;
                    }

                    var weight = font.TryGetProperty("font_weight", out var w) && w.ValueKind == JsonValueKind.Number ? w.GetInt32() : 400;
                    var style = GetString(font, "font_style");
                    var isItalic = string.Equals(style, "Italic", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(style, "Oblique", StringComparison.OrdinalIgnoreCase);
                    faces.Add(new FontFace(weight, isItalic, ParseStretch(GetString(font, "font_stretch")), source));
                }
            }

            return new FontManifest(faces);
        }
        catch (JsonException e)
        {
            throw new FormatException("The font manifest is not valid JSON.", e);
        }
    }

    /// <summary>Converts a manifest font_stretch name to the WinUI FontStretch value.</summary>
    internal static int ParseStretch(string stretch) => stretch?.Trim().ToLowerInvariant() switch
    {
        "ultracondensed" => 1,
        "extracondensed" => 2,
        "condensed" => 3,
        "semicondensed" => 4,
        "semiexpanded" => 6,
        "expanded" => 7,
        "extraexpanded" => 8,
        "ultraexpanded" => 9,
        _ => NormalStretch,
    };

    /// <summary>
    /// Selects the best face for the requested weight, style and stretch (CSS Fonts level 3
    /// matching order: style, then stretch, then weight), or null when the manifest is empty.
    /// </summary>
    internal FontFace? Select(int weight, bool italic, int stretch)
    {
        if (Faces.Count == 0)
        {
            return null;
        }

        if (stretch <= 0)
        {
            stretch = NormalStretch;
        }

        // 1. Style: the requested style if any face has it, otherwise the other one.
        var candidates = Filter(f => f.IsItalic == italic);
        if (candidates.Count == 0)
        {
            candidates = new List<FontFace>(Faces);
        }

        // 2. Stretch: exact; else narrower first for <= Normal requests, wider first otherwise.
        var stretchChoice = ChooseClosest(candidates, f => f.Stretch, stretch, preferLower: stretch <= NormalStretch);
        candidates = candidates.FindAll(f => f.Stretch == stretchChoice);

        // 3. Weight (CSS): 400 tries 500 next; 500 tries 400 next; then lighter for < 400, heavier for > 500.
        var weightChoice = ChooseWeight(candidates, weight);
        return candidates.Find(f => f.Weight == weightChoice);
    }

    private List<FontFace> Filter(Predicate<FontFace> predicate)
    {
        var result = new List<FontFace>();
        foreach (var face in Faces)
        {
            if (predicate(face))
            {
                result.Add(face);
            }
        }

        return result;
    }

    private static int ChooseClosest(List<FontFace> faces, Func<FontFace, int> value, int desired, bool preferLower)
    {
        int? bestLower = null;
        int? bestHigher = null;
        foreach (var face in faces)
        {
            var v = value(face);
            if (v == desired)
            {
                return v;
            }

            if (v < desired && (bestLower == null || v > bestLower))
            {
                bestLower = v;
            }

            if (v > desired && (bestHigher == null || v < bestHigher))
            {
                bestHigher = v;
            }
        }

        return preferLower ? bestLower ?? bestHigher.Value : bestHigher ?? bestLower.Value;
    }

    private static int ChooseWeight(List<FontFace> faces, int desired)
    {
        var weights = new SortedSet<int>();
        foreach (var face in faces)
        {
            weights.Add(face.Weight);
        }

        if (weights.Contains(desired))
        {
            return desired;
        }

        if (desired >= 400 && desired <= 500)
        {
            // Heavier weights up to 500 first, then lighter, then heavier than 500.
            foreach (var w in weights)
            {
                if (w > desired && w <= 500)
                {
                    return w;
                }
            }

            int? lighter = null;
            foreach (var w in weights)
            {
                if (w < desired)
                {
                    lighter = w;
                }
            }

            if (lighter != null)
            {
                return lighter.Value;
            }

            foreach (var w in weights)
            {
                if (w > desired)
                {
                    return w;
                }
            }
        }

        if (desired < 400)
        {
            int? lighter = null;
            foreach (var w in weights)
            {
                if (w < desired)
                {
                    lighter = w;
                }
            }

            if (lighter != null)
            {
                return lighter.Value;
            }

            foreach (var w in weights)
            {
                if (w > desired)
                {
                    return w;
                }
            }
        }

        // desired > 500: heavier first (ascending), then lighter (descending).
        foreach (var w in weights)
        {
            if (w > desired)
            {
                return w;
            }
        }

        return weights.Max;
    }

    private static string GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
