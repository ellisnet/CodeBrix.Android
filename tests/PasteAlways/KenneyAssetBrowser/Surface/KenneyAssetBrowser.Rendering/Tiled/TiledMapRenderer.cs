using SkiaSharp;

namespace KenneyAssetBrowser.Rendering;

/// <summary>
/// Composites a parsed Tiled map into a single bitmap: every visible tile layer in order,
/// honoring the per-cell horizontal/vertical/diagonal flip bits. The result feeds the same
/// 2D viewer that displays plain images.
/// </summary>
public static class TiledMapRenderer
{
    private const uint FlipHorizontally = 0x80000000;
    private const uint FlipVertically = 0x40000000;
    private const uint FlipDiagonally = 0x20000000;
    private const uint GidMask = 0x1FFFFFFF;

    //A preview guard: maps larger than this many output pixels are refused
    private const long MaxOutputPixels = 4096L * 4096L;

    /// <summary>
    /// Renders the map into a new bitmap. The caller owns the returned bitmap.
    /// </summary>
    /// <param name="map">The parsed map.</param>
    /// <param name="tilesets">One resolved entry per map tileset: its first global id, its
    /// definition, and its decoded tileset image (not disposed by this method).</param>
    /// <returns>The composited map bitmap.</returns>
    /// <exception cref="InvalidDataException">The map is too large to preview.</exception>
    public static SKBitmap Render(
        TiledMapDocument map,
        IReadOnlyList<(int FirstGid, TiledTilesetInfo Info, SKBitmap Image)> tilesets)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(tilesets);

        var pixelWidth = (long)map.Width * map.TileWidth;
        var pixelHeight = (long)map.Height * map.TileHeight;
        if (pixelWidth <= 0 || pixelHeight <= 0 || pixelWidth * pixelHeight > MaxOutputPixels)
        {
            throw new InvalidDataException(
                $"The map is {pixelWidth} × {pixelHeight} px — too large for the preview.");
        }

        //Highest FirstGid first, so the owning tileset of a gid is the first match
        var ordered = tilesets.OrderByDescending(t => t.FirstGid).ToList();

        var bitmap = new SKBitmap(new SKImageInfo((int)pixelWidth, (int)pixelHeight,
            SKColorType.Rgba8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);

        var sampling = new SKSamplingOptions(SKFilterMode.Nearest);
        foreach (var layer in map.Layers.Where(l => l.Visible && l.Opacity > 0f))
        {
            using var paint = new SKPaint { Color = SKColors.White.WithAlpha((byte)(layer.Opacity * 255)) };
            DrawLayer(canvas, map, layer, ordered, sampling, paint);
        }

        return bitmap;
    }

    private static void DrawLayer(
        SKCanvas canvas, TiledMapDocument map, TiledLayer layer,
        IReadOnlyList<(int FirstGid, TiledTilesetInfo Info, SKBitmap Image)> orderedTilesets,
        SKSamplingOptions sampling, SKPaint paint)
    {
        for (var row = 0; row < layer.Height; row++)
        {
            for (var column = 0; column < layer.Width; column++)
            {
                var rawGid = layer.Gids[(row * layer.Width) + column];
                var gid = (int)(rawGid & GidMask);
                if (gid == 0) { continue; }

                var (firstGid, info, image) = orderedTilesets.FirstOrDefault(t => t.FirstGid <= gid);
                if (info == null || image == null) { continue; }

                var index = gid - firstGid;
                if (index < 0 || index >= info.TileCount) { continue; }

                var sourceRect = SKRect.Create(
                    info.Margin + ((index % info.Columns) * (info.TileWidth + info.Spacing)),
                    info.Margin + ((index / info.Columns) * (info.TileHeight + info.Spacing)),
                    info.TileWidth, info.TileHeight);

                canvas.Save();

                //Center on the destination cell so the flip transforms pivot in place.
                //Tiled semantics: the diagonal flip (a transpose) applies first, then the
                //horizontal and vertical flips — outermost transform = first canvas call.
                canvas.Translate(
                    (column * map.TileWidth) + (map.TileWidth / 2f),
                    (row * map.TileHeight) + (map.TileHeight / 2f));
                canvas.Scale(
                    (rawGid & FlipHorizontally) != 0 ? -1f : 1f,
                    (rawGid & FlipVertically) != 0 ? -1f : 1f);
                if ((rawGid & FlipDiagonally) != 0)
                {
                    canvas.Scale(-1f, 1f);
                    canvas.RotateDegrees(90f);
                }

                canvas.DrawBitmap(image, sourceRect,
                    SKRect.Create(-map.TileWidth / 2f, -map.TileHeight / 2f, map.TileWidth, map.TileHeight),
                    sampling, paint);
                canvas.Restore();
            }
        }
    }
}
