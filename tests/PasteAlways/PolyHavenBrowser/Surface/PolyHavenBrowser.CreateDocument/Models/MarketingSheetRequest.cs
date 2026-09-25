namespace PolyHavenBrowser.CreateDocument;

/// <summary>One label/value specification row of the sheet (mirrors the Model View's facts).</summary>
public sealed class MarketingSheetFact
{
    /// <summary>Creates a fact row.</summary>
    public MarketingSheetFact(string label, string value)
    {
        Label = label;
        Value = value;
    }

    /// <summary>The fact's label, e.g. <c>Triangles</c>.</summary>
    public string Label { get; }

    /// <summary>The fact's display value, e.g. <c>12,204</c>.</summary>
    public string Value { get; }
}

/// <summary>One rendered product shot for the sheet's gallery row.</summary>
public sealed class MarketingSheetShot
{
    /// <summary>Creates a gallery shot.</summary>
    public MarketingSheetShot(string caption, byte[] imageBytes)
    {
        Caption = caption;
        ImageBytes = imageBytes;
    }

    /// <summary>The small caption under the shot, e.g. <c>FRONT</c>.</summary>
    public string Caption { get; }

    /// <summary>The encoded (PNG or JPEG) shot image.</summary>
    public byte[] ImageBytes { get; }
}

/// <summary>
/// Everything the marketing one-sheet needs, gathered by the caller: the model's display
/// texts and facts from the Model View, the catalog thumbnail, and the rendered product
/// shots. This type is deliberately free of any rendering/GL concern so document creation
/// can be exercised headlessly.
/// </summary>
public sealed class MarketingSheetRequest
{
    /// <summary>The model's full display name, e.g. <c>Marble Bust 1</c>.</summary>
    public required string ModelName { get; init; }

    /// <summary>The creator credit line, e.g. <c>by ulrickwery   ·   Poly Haven</c>.</summary>
    public string AuthorLine { get; init; } = string.Empty;

    /// <summary>The full factual description paragraph (the Model View's ABOUT text).</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>The specification rows (the Model View's DETAILS facts).</summary>
    public IReadOnlyList<MarketingSheetFact> Facts { get; init; } = [];

    /// <summary>The model's tags, without <c>#</c> prefixes.</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>The model's Poly Haven page, e.g. <c>https://polyhaven.com/a/marble_bust_01</c>.</summary>
    public string AssetUrl { get; init; } = string.Empty;

    /// <summary>The model's first category, e.g. <c>decorative</c>; empty when uncategorized.</summary>
    public string Category { get; init; } = string.Empty;

    /// <summary>The encoded catalog-cell preview image, or <see langword="null"/> when unavailable.</summary>
    public byte[]? CatalogThumbnailBytes { get; init; }

    /// <summary>The encoded hero beauty shot, or <see langword="null"/> to lead with the thumbnail.</summary>
    public byte[]? HeroShotBytes { get; init; }

    /// <summary>The gallery shots (front/side/back/top), in display order.</summary>
    public IReadOnlyList<MarketingSheetShot> GalleryShots { get; init; } = [];

    /// <summary>The total triangle count, when known (feeds the persuasive copy).</summary>
    public int? TriangleCount { get; init; }

    /// <summary>The total vertex count, when known.</summary>
    public int? VertexCount { get; init; }

    /// <summary>The material count, when known.</summary>
    public int? MaterialCount { get; init; }

    /// <summary>The largest texture resolution label, e.g. <c>8k</c>, when known.</summary>
    public string MaxTextureLabel { get; init; } = string.Empty;

    /// <summary>The Poly Haven download count, when known.</summary>
    public long? DownloadCount { get; init; }

    /// <summary>The UTC publication date, when known.</summary>
    public DateTime? PublishedUtc { get; init; }
}
