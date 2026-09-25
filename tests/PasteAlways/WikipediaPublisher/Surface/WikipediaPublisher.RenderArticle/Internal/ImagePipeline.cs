using CodeBrix.Imaging;
using CodeBrix.Imaging.Formats;
using CodeBrix.Imaging.Formats.Gif;
using CodeBrix.Imaging.Formats.Jpeg;
using CodeBrix.Imaging.Formats.Png;
using CodeBrix.Imaging.Formats.Webp;
using CodeBrix.Imaging.Processing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WikipediaPublisher.RenderArticle.Models;

namespace WikipediaPublisher.RenderArticle.Internal;

/// <summary>
/// Downloads each article image at print resolution (falling back to the page
/// thumbnail when the high-resolution rendition is unavailable) and normalizes
/// it for PDF embedding: capped pixel width, JPEG for photographs, PNG for
/// graphics with transparency.
/// </summary>
internal sealed class ImagePipeline
{
    private const int MaxPixelWidth = 1800;
    private const int JpegQuality = 87;

    private readonly WikipediaClient _client;

    public ImagePipeline(WikipediaClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    /// <summary>
    /// Downloads and processes every image in the article (lead image included).
    /// Failed images are left without <see cref="ArticleImage.ProcessedBytes"/> and
    /// reported in the article's warnings; they never fail the render.
    /// </summary>
    public async Task<int> PrepareImagesAsync(
        ParsedArticle article,
        Action<string> reportProgress,
        CancellationToken cancellationToken = default)
    {
        var images = new List<ArticleImage>();
        if (article.LeadImage is not null) { images.Add(article.LeadImage); }
        images.AddRange(article.Blocks
            .Where(b => b.Type == ArticleBlockType.Image && b.Image is not null)
            .Select(b => b.Image));

        var succeeded = 0;
        for (var i = 0; i < images.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var image = images[i];
            reportProgress?.Invoke($"Image {i + 1} of {images.Count}: {image.FileName}");

            if (await TryPrepareOneAsync(image, cancellationToken))
            {
                succeeded++;
            }
            else
            {
                article.Warnings.Add($"Image could not be downloaded or decoded: {image.FileName}");
            }
        }

        return succeeded;
    }

    private async Task<bool> TryPrepareOneAsync(ArticleImage image, CancellationToken cancellationToken)
    {
        var bytes = await _client.TryDownloadMediaAsync(image.PrintUrl, cancellationToken);

        //High-resolution rendition may 404 (e.g. odd file types) — fall back to the page thumbnail
        if (bytes is null && (!image.PrintUrl.Equals(image.ThumbUrl, StringComparison.Ordinal)))
        {
            bytes = await _client.TryDownloadMediaAsync(image.ThumbUrl, cancellationToken);
        }

        if (bytes is null || bytes.Length == 0) { return false; }

        try
        {
            ProcessForPrint(image, bytes);
            return true;
        }
        catch (Exception)
        {
            //Undecodable image data (corrupt or unsupported encoding)
            return false;
        }
    }

    private static void ProcessForPrint(ArticleImage articleImage, byte[] bytes)
    {
        using var image = Image.Load(bytes, out IImageFormat format);

        var keepsTransparency = format is PngFormat or WebpFormat or GifFormat;
        var needsResize = image.Width > MaxPixelWidth;

        //Untouched JPEG/PNG bytes embed best — only re-encode when we must
        if (!needsResize && (format is JpegFormat || format is PngFormat))
        {
            articleImage.ProcessedBytes = bytes;
            articleImage.ProcessedWidth = image.Width;
            articleImage.ProcessedHeight = image.Height;
            return;
        }

        if (needsResize)
        {
            image.Mutate(x => x.Resize(MaxPixelWidth, 0));
        }

        using var output = new MemoryStream();
        if (keepsTransparency)
        {
            image.Save(output, new PngEncoder());
        }
        else
        {
            image.Save(output, new JpegEncoder { Quality = JpegQuality });
        }

        articleImage.ProcessedBytes = output.ToArray();
        articleImage.ProcessedWidth = image.Width;
        articleImage.ProcessedHeight = image.Height;
    }
}
