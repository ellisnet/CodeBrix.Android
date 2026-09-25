using System.Globalization;
using System.Text;

namespace PolyHavenBrowser.CreateDocument.Internal;

/// <summary>
/// Writes the sheet's persuasive copy from the model's real facts: the kicker above the
/// title, the tagline under it, the first-person pull quote, and the sales paragraph that
/// runs beside the factual description. Deterministic — the same request always produces
/// the same words — and every sentence degrades gracefully when its fact is missing.
/// </summary>
internal static class SalesCopyBuilder
{
    /// <summary>The ALL-CAPS kicker above the title, e.g. <c>POLY HAVEN COLLECTION · DECORATIVE</c>.</summary>
    public static string BuildKicker(MarketingSheetRequest request)
    {
        var category = request.Category?.Trim();
        return string.IsNullOrEmpty(category)
            ? "POLY HAVEN COLLECTION"
            : $"POLY HAVEN COLLECTION · {category.ToUpperInvariant()}";
    }

    /// <summary>The persuasive one-liner under the title.</summary>
    public static string BuildTagline(MarketingSheetRequest request)
    {
        return string.IsNullOrEmpty(request.MaxTextureLabel)
            ? "Photoreal. Production-ready. Free forever."
            : $"Photoreal to {request.MaxTextureLabel}. Production-ready. Free forever.";
    }

    /// <summary>
    /// The first-person pull quote: <c>“I am the Marble Bust model you have been looking
    /// for.”</c> The sentence uses the display name with trailing numbers stripped.
    /// </summary>
    public static string BuildPullQuote(MarketingSheetRequest request)
    {
        var name = ModelNameFormatter.StripTrailingNumbers(request.ModelName);
        return string.IsNullOrEmpty(name)
            ? "“I am the model you have been looking for.”"
            : $"“I am the {name} model you have been looking for.”";
    }

    /// <summary>The pull quote's signature line — the model signs with its full name.</summary>
    public static string BuildPullQuoteSignature(MarketingSheetRequest request) =>
        $"— {request.ModelName.Trim()}";

    /// <summary>
    /// The sales paragraph: the model's real numbers, delivered like a product launch.
    /// </summary>
    public static string BuildSalesParagraph(MarketingSheetRequest request)
    {
        var text = new StringBuilder();

        if (request.TriangleCount is > 0)
        {
            text.Append(CultureInfo.InvariantCulture,
                $"{request.TriangleCount:N0} meticulously placed triangles");
            text.Append(request.VertexCount is > 0
                ? string.Create(CultureInfo.InvariantCulture,
                    $" across {request.VertexCount:N0} vertices — every one of them earning its place. ")
                : " — every one of them earning its place. ");
        }

        if (request.MaterialCount is > 0)
        {
            text.Append(request.MaterialCount == 1
                ? "One PBR material, dialed in"
                : string.Create(CultureInfo.InvariantCulture, $"{request.MaterialCount:N0} PBR materials, dialed in"));
            text.Append(string.IsNullOrEmpty(request.MaxTextureLabel)
                ? " and ready for close-ups. "
                : $" with textures up to {request.MaxTextureLabel} — ready for close-ups. ");
        }
        else if (!string.IsNullOrEmpty(request.MaxTextureLabel))
        {
            text.Append(CultureInfo.InvariantCulture,
                $"Textures up to {request.MaxTextureLabel} — ready for close-ups. ");
        }

        if (request.DownloadCount is > 0)
        {
            text.Append(CultureInfo.InvariantCulture, $"Downloaded {request.DownloadCount:N0} times");
            text.Append(request.PublishedUtc is { } published
                ? string.Create(CultureInfo.InvariantCulture,
                    $" since {published.ToString("MMMM yyyy", CultureInfo.InvariantCulture)}, and every scene it joins looks more expensive. ")
                : ", and every scene it joins looks more expensive. ");
        }

        text.Append("A CC0 license that asks nothing in return: no attribution, no royalties, no fine print. ");

        var name = ModelNameFormatter.StripTrailingNumbers(request.ModelName);
        text.Append(string.IsNullOrEmpty(name)
            ? "This is not a model you audition. This is a model you cast."
            : $"The {name} is not a model you audition. It is a model you cast.");

        return text.ToString();
    }
}
