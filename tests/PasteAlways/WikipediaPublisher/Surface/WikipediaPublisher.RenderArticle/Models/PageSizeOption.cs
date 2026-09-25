using System;
using System.Collections.Generic;

namespace WikipediaPublisher.RenderArticle.Models;

/// <summary>
/// The trim (page) sizes that a published article book can be rendered at.
/// </summary>
public enum PageSizeOption
{
    /// <summary>8 in × 10 in — classic coffee-table trim (the default).</summary>
    EightByTen = 0,

    /// <summary>6 in × 9 in — standard trade-book trim.</summary>
    SixByNine,

    /// <summary>8.5 in × 11 in — US Letter.</summary>
    Letter,

    /// <summary>210 mm × 297 mm — ISO A4.</summary>
    A4
}

/// <summary>
/// Describes a selectable page size, including its display name and dimensions in points.
/// </summary>
public sealed class PageSizeInfo
{
    private PageSizeInfo(PageSizeOption option, string displayName, double widthPoints, double heightPoints)
    {
        Option = option;
        DisplayName = displayName;
        WidthPoints = widthPoints;
        HeightPoints = heightPoints;
    }

    /// <summary>The page size option this info describes.</summary>
    public PageSizeOption Option { get; }

    /// <summary>A friendly name suitable for display in a picker (e.g. drop-down).</summary>
    public string DisplayName { get; }

    /// <summary>Page width in typographic points (1/72 inch).</summary>
    public double WidthPoints { get; }

    /// <summary>Page height in typographic points (1/72 inch).</summary>
    public double HeightPoints { get; }

    /// <summary>All selectable page sizes, in display order (the first entry is the default).</summary>
    public static IReadOnlyList<PageSizeInfo> All { get; } =
    [
        new PageSizeInfo(PageSizeOption.EightByTen, "8\" × 10\" (coffee table)", 576, 720),
        new PageSizeInfo(PageSizeOption.SixByNine, "6\" × 9\" (trade book)", 432, 648),
        new PageSizeInfo(PageSizeOption.Letter, "8.5\" × 11\" (US Letter)", 612, 792),
        new PageSizeInfo(PageSizeOption.A4, "A4 (210 × 297 mm)", 595, 842)
    ];

    /// <summary>Gets the info record for the given page size option.</summary>
    public static PageSizeInfo For(PageSizeOption option)
    {
        foreach (var info in All)
        {
            if (info.Option == option) { return info; }
        }
        throw new ArgumentOutOfRangeException(nameof(option), option, "Unknown page size option.");
    }
}
