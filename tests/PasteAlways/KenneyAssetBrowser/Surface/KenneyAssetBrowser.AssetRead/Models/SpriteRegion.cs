namespace KenneyAssetBrowser.AssetRead.Models;

/// <summary>
/// One named rectangular region inside a spritesheet image, as declared by a
/// Kenney <c>TextureAtlas</c> XML file.
/// </summary>
public class SpriteRegion
{
    /// <summary>
    /// Creates a spritesheet region.
    /// </summary>
    /// <param name="name">The region name (usually the original sprite file name).</param>
    /// <param name="x">The left edge of the region, in pixels.</param>
    /// <param name="y">The top edge of the region, in pixels.</param>
    /// <param name="width">The width of the region, in pixels.</param>
    /// <param name="height">The height of the region, in pixels.</param>
    public SpriteRegion(string name, int x, int y, int width, int height)
    {
        Name = name ?? string.Empty;
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    /// <summary>Gets the region name (usually the original sprite file name).</summary>
    public string Name { get; }

    /// <summary>Gets the left edge of the region, in pixels.</summary>
    public int X { get; }

    /// <summary>Gets the top edge of the region, in pixels.</summary>
    public int Y { get; }

    /// <summary>Gets the width of the region, in pixels.</summary>
    public int Width { get; }

    /// <summary>Gets the height of the region, in pixels.</summary>
    public int Height { get; }
}
