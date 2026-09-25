using KenneyAssetBrowser.AssetRead.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

namespace KenneyAssetBrowser.AssetRead.Parsing;

/// <summary>
/// Parses the <c>TextureAtlas</c> XML files that accompany Kenney spritesheet images.
/// </summary>
public static class SpriteAtlasParser
{
    /// <summary>
    /// Attempts to parse a TextureAtlas XML document.
    /// </summary>
    /// <param name="xmlText">The full text of the XML file.</param>
    /// <param name="xmlEntryPath">The archive path of the XML file (used to resolve the
    /// relative <c>imagePath</c> attribute and to name the atlas).</param>
    /// <param name="atlas">The parsed atlas, or <c>null</c> when parsing fails.</param>
    /// <returns><c>true</c> when the text is a TextureAtlas document with at least one region.</returns>
    public static bool TryParse(string xmlText, string xmlEntryPath, out SpriteAtlas atlas)
    {
        atlas = null;
        if (string.IsNullOrWhiteSpace(xmlText)) { return false; }

        XElement root;
        try
        {
            root = XDocument.Parse(xmlText).Root;
        }
        catch (Exception)
        {
            return false;
        }

        if (root == null || !root.Name.LocalName.Equals("TextureAtlas", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var imagePath = (string)root.Attribute("imagePath") ?? string.Empty;
        var regions = new List<SpriteRegion>();

        foreach (var subTexture in root.Elements()
                     .Where(e => e.Name.LocalName.Equals("SubTexture", StringComparison.OrdinalIgnoreCase)))
        {
            var name = (string)subTexture.Attribute("name") ?? string.Empty;
            if (name.Length == 0) { continue; }

            if (TryReadInt(subTexture, "x", out var x) &&
                TryReadInt(subTexture, "y", out var y) &&
                TryReadInt(subTexture, "width", out var width) &&
                TryReadInt(subTexture, "height", out var height))
            {
                regions.Add(new SpriteRegion(name, x, y, width, height));
            }
        }

        if (regions.Count == 0) { return false; }

        atlas = new SpriteAtlas(xmlEntryPath, ResolveImagePath(xmlEntryPath, imagePath), regions);
        return true;
    }

    private static bool TryReadInt(XElement element, string attributeName, out int value)
    {
        value = 0;
        var text = (string)element.Attribute(attributeName);
        return text != null && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    //The imagePath attribute is relative to the folder holding the XML file
    private static string ResolveImagePath(string xmlEntryPath, string imagePath)
    {
        if (string.IsNullOrEmpty(imagePath)) { return string.Empty; }
        if (imagePath.Contains('/')) { return imagePath; }

        var lastSlash = (xmlEntryPath ?? string.Empty).LastIndexOf('/');
        return lastSlash < 0 ? imagePath : xmlEntryPath.Substring(0, lastSlash + 1) + imagePath;
    }
}
