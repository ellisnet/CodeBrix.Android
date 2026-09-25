using System.Collections.Generic;

namespace KenneyAssetBrowser.AssetRead.Models;

/// <summary>
/// A spritesheet definition from a Kenney asset bundle: the archive path of the sheet
/// image plus the named regions its <c>TextureAtlas</c> XML file declares.
/// </summary>
public class SpriteAtlas
{
    /// <summary>
    /// Creates a spritesheet definition.
    /// </summary>
    /// <param name="xmlEntryPath">The archive path of the TextureAtlas XML file.</param>
    /// <param name="imageEntryPath">The archive path of the spritesheet image the XML refers to.</param>
    /// <param name="regions">The named regions declared by the XML file.</param>
    public SpriteAtlas(string xmlEntryPath, string imageEntryPath, IReadOnlyList<SpriteRegion> regions)
    {
        XmlEntryPath = xmlEntryPath ?? string.Empty;
        ImageEntryPath = imageEntryPath ?? string.Empty;
        Regions = regions ?? [];

        var lastSlash = XmlEntryPath.LastIndexOf('/');
        var fileName = lastSlash < 0 ? XmlEntryPath : XmlEntryPath.Substring(lastSlash + 1);
        var lastDot = fileName.LastIndexOf('.');
        Name = lastDot <= 0 ? fileName : fileName.Substring(0, lastDot);
    }

    /// <summary>Gets the atlas name (the XML file name without its extension).</summary>
    public string Name { get; }

    /// <summary>Gets the archive path of the TextureAtlas XML file.</summary>
    public string XmlEntryPath { get; }

    /// <summary>Gets the archive path of the spritesheet image the XML refers to.</summary>
    public string ImageEntryPath { get; }

    /// <summary>Gets the named regions declared by the XML file.</summary>
    public IReadOnlyList<SpriteRegion> Regions { get; }
}
