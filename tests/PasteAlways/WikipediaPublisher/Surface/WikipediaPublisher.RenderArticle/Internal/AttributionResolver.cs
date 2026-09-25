using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WikipediaPublisher.RenderArticle.Models;

namespace WikipediaPublisher.RenderArticle.Internal;

/// <summary>
/// Fills in each image's <see cref="ArticleImage.Attribution"/> credit line by looking up the
/// media file's authorship and licence in Wikimedia's image metadata. Best-effort: any image
/// whose metadata is missing or whose request fails simply keeps an empty attribution.
/// </summary>
internal sealed class AttributionResolver
{
    private readonly WikipediaClient _client;

    public AttributionResolver(WikipediaClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    /// <summary>
    /// Resolves credits for every downloaded image in the article (lead image included).
    /// Images that failed to download are skipped — they are never placed in the book.
    /// </summary>
    public async Task ResolveAsync(ParsedArticle article, CancellationToken cancellationToken = default)
    {
        if (article is null) { return; }

        var images = new List<ArticleImage>();
        if (article.LeadImage is not null) { images.Add(article.LeadImage); }
        images.AddRange(article.Blocks
            .Where(b => b.Type == ArticleBlockType.Image && b.Image is not null)
            .Select(b => b.Image));

        //Only fetch for images that will actually appear (downloaded) and that we can identify
        var placed = images
            .Where(i => i.ProcessedBytes is { Length: > 0 }
                        && !string.IsNullOrWhiteSpace(i.MediaPageTitle))
            .ToList();
        if (placed.Count == 0) { return; }

        var titles = placed
            .Select(i => i.MediaPageTitle)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var host = GetHost(article.SourceUrl);
        var metadata = await _client.GetImageMetadataAsync(titles, host, cancellationToken);
        if (metadata.Count == 0) { return; }

        foreach (var image in placed)
        {
            if (metadata.TryGetValue(image.MediaPageTitle, out var fields))
            {
                image.Attribution = AttributionFormatter.Format(fields);
            }
        }
    }

    private static string GetHost(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host : "en.wikipedia.org";
}
