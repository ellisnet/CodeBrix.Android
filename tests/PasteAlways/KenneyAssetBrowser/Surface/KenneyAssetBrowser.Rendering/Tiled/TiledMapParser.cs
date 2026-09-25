using System.Globalization;
using System.Xml.Linq;

namespace KenneyAssetBrowser.Rendering;

/// <summary>
/// Parses Tiled .tmx maps and .tsx tilesets — the orthogonal, CSV-encoded subset that
/// Kenney's packs ship. Anything else (base64 layers, infinite maps) is rejected rather
/// than mis-rendered.
/// </summary>
public static class TiledMapParser
{
    /// <summary>
    /// Attempts to parse a .tmx map document.
    /// </summary>
    /// <param name="xmlText">The full text of the .tmx file.</param>
    /// <param name="map">The parsed map, or <see langword="null"/> when parsing fails.</param>
    /// <returns><see langword="true"/> for an orthogonal map with at least one CSV tile layer.</returns>
    public static bool TryParseMap(string? xmlText, out TiledMapDocument? map)
    {
        map = null;
        var root = ParseRoot(xmlText, "map");
        if (root == null) { return false; }

        var orientation = (string?)root.Attribute("orientation") ?? "orthogonal";
        if (!orientation.Equals("orthogonal", StringComparison.OrdinalIgnoreCase)) { return false; }

        if (!TryReadInt(root, "width", out var width) ||
            !TryReadInt(root, "height", out var height) ||
            !TryReadInt(root, "tilewidth", out var tileWidth) ||
            !TryReadInt(root, "tileheight", out var tileHeight))
        {
            return false;
        }

        var tilesets = new List<TiledTilesetRef>();
        foreach (var tileset in root.Elements().Where(e => e.Name.LocalName == "tileset"))
        {
            if (!TryReadInt(tileset, "firstgid", out var firstGid)) { continue; }

            var source = (string?)tileset.Attribute("source");
            if (source != null)
            {
                tilesets.Add(new TiledTilesetRef { FirstGid = firstGid, Source = source });
            }
            else if (TryParseTilesetElement(tileset, out var inline))
            {
                tilesets.Add(new TiledTilesetRef { FirstGid = firstGid, Inline = inline });
            }
        }

        var layers = new List<TiledLayer>();
        foreach (var layer in root.Elements().Where(e => e.Name.LocalName == "layer"))
        {
            if (TryParseLayer(layer, out var parsed)) { layers.Add(parsed!); }
        }

        if (tilesets.Count == 0 || layers.Count == 0) { return false; }

        map = new TiledMapDocument
        {
            Width = width,
            Height = height,
            TileWidth = tileWidth,
            TileHeight = tileHeight,
            Tilesets = tilesets,
            Layers = layers,
        };
        return true;
    }

    /// <summary>
    /// Attempts to parse a .tsx tileset document.
    /// </summary>
    /// <param name="xmlText">The full text of the .tsx file.</param>
    /// <param name="tileset">The parsed tileset, or <see langword="null"/> when parsing fails.</param>
    public static bool TryParseTileset(string? xmlText, out TiledTilesetInfo? tileset)
    {
        tileset = null;
        var root = ParseRoot(xmlText, "tileset");
        return root != null && TryParseTilesetElement(root, out tileset);
    }

    private static XElement? ParseRoot(string? xmlText, string expectedName)
    {
        if (string.IsNullOrWhiteSpace(xmlText)) { return null; }
        try
        {
            var root = XDocument.Parse(xmlText).Root;
            return root != null && root.Name.LocalName == expectedName ? root : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static bool TryParseTilesetElement(XElement element, out TiledTilesetInfo? tileset)
    {
        tileset = null;
        var image = element.Elements().FirstOrDefault(e => e.Name.LocalName == "image");
        var imagePath = (string?)image?.Attribute("source");
        if (imagePath == null) { return false; }

        if (!TryReadInt(element, "tilewidth", out var tileWidth) ||
            !TryReadInt(element, "tileheight", out var tileHeight) ||
            !TryReadInt(element, "tilecount", out var tileCount) ||
            !TryReadInt(element, "columns", out var columns) ||
            columns <= 0)
        {
            return false;
        }

        TryReadInt(element, "spacing", out var spacing);
        TryReadInt(element, "margin", out var margin);

        tileset = new TiledTilesetInfo
        {
            Name = (string?)element.Attribute("name"),
            TileWidth = tileWidth,
            TileHeight = tileHeight,
            TileCount = tileCount,
            Columns = columns,
            ImagePath = imagePath,
            Spacing = spacing,
            Margin = margin,
        };
        return true;
    }

    private static bool TryParseLayer(XElement layer, out TiledLayer? parsed)
    {
        parsed = null;
        if (!TryReadInt(layer, "width", out var width) ||
            !TryReadInt(layer, "height", out var height))
        {
            return false;
        }

        var data = layer.Elements().FirstOrDefault(e => e.Name.LocalName == "data");
        var encoding = (string?)data?.Attribute("encoding");
        if (data == null || !string.Equals(encoding, "csv", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var cells = data.Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (cells.Length != width * height) { return false; }

        var gids = new uint[cells.Length];
        for (var i = 0; i < cells.Length; i++)
        {
            if (!uint.TryParse(cells[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out gids[i]))
            {
                return false;
            }
        }

        var opacityText = (string?)layer.Attribute("opacity");
        var opacity = opacityText != null &&
            float.TryParse(opacityText, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedOpacity)
                ? parsedOpacity
                : 1f;

        parsed = new TiledLayer
        {
            Name = (string?)layer.Attribute("name"),
            Width = width,
            Height = height,
            Gids = gids,
            Visible = (string?)layer.Attribute("visible") != "0",
            Opacity = Math.Clamp(opacity, 0f, 1f),
        };
        return true;
    }

    private static bool TryReadInt(XElement element, string attributeName, out int value)
    {
        value = 0;
        var text = (string?)element.Attribute(attributeName);
        return text != null && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }
}
