namespace KenneyAssetBrowser.Rendering;

/// <summary>
/// A parsed Tiled (.tmx) map: dimensions, tileset references and tile layers — the subset
/// the read-only map preview needs (orthogonal maps with CSV layer data, which is what
/// Kenney's packs ship).
/// </summary>
public sealed class TiledMapDocument
{
    /// <summary>The map width, in tiles.</summary>
    public required int Width { get; init; }

    /// <summary>The map height, in tiles.</summary>
    public required int Height { get; init; }

    /// <summary>The width of one tile, in pixels.</summary>
    public required int TileWidth { get; init; }

    /// <summary>The height of one tile, in pixels.</summary>
    public required int TileHeight { get; init; }

    /// <summary>The tileset references, in document order.</summary>
    public required IReadOnlyList<TiledTilesetRef> Tilesets { get; init; }

    /// <summary>The tile layers, bottom-most first (document order).</summary>
    public required IReadOnlyList<TiledLayer> Layers { get; init; }
}

/// <summary>
/// One tileset reference of a .tmx map: its starting global tile id plus either the
/// relative path of an external .tsx file or the inline tileset definition.
/// </summary>
public sealed class TiledTilesetRef
{
    /// <summary>The global tile id the tileset's first tile maps to.</summary>
    public required int FirstGid { get; init; }

    /// <summary>The relative path of the external .tsx file, or <see langword="null"/> when inline.</summary>
    public string? Source { get; init; }

    /// <summary>The inline tileset definition, or <see langword="null"/> when external.</summary>
    public TiledTilesetInfo? Inline { get; init; }
}

/// <summary>
/// A tileset definition (from a .tsx file or inline in the map): the tile grid geometry
/// and the tileset image it cuts tiles from.
/// </summary>
public sealed class TiledTilesetInfo
{
    /// <summary>The tileset name.</summary>
    public string? Name { get; init; }

    /// <summary>The width of one tile, in pixels.</summary>
    public required int TileWidth { get; init; }

    /// <summary>The height of one tile, in pixels.</summary>
    public required int TileHeight { get; init; }

    /// <summary>The number of tiles in the tileset.</summary>
    public required int TileCount { get; init; }

    /// <summary>The number of tile columns in the tileset image.</summary>
    public required int Columns { get; init; }

    /// <summary>The tileset image's path, relative to the .tsx (or .tmx when inline).</summary>
    public required string ImagePath { get; init; }

    /// <summary>The spacing between tiles in the image, in pixels.</summary>
    public int Spacing { get; init; }

    /// <summary>The margin around the image's tile grid, in pixels.</summary>
    public int Margin { get; init; }
}

/// <summary>
/// One tile layer of a .tmx map: raw global tile ids (with the Tiled flip bits still set)
/// in row-major order.
/// </summary>
public sealed class TiledLayer
{
    /// <summary>The layer name.</summary>
    public string? Name { get; init; }

    /// <summary>The layer width, in tiles.</summary>
    public required int Width { get; init; }

    /// <summary>The layer height, in tiles.</summary>
    public required int Height { get; init; }

    /// <summary>The global tile ids in row-major order, flip bits included; 0 = empty cell.</summary>
    public required uint[] Gids { get; init; }

    /// <summary>Whether the layer is visible.</summary>
    public bool Visible { get; init; } = true;

    /// <summary>The layer opacity in [0, 1].</summary>
    public float Opacity { get; init; } = 1f;
}
