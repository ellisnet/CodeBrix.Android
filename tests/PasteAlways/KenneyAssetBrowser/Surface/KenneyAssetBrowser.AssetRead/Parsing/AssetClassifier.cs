using KenneyAssetBrowser.AssetRead.Models;
using System;
using System.Collections.Generic;

namespace KenneyAssetBrowser.AssetRead.Parsing;

/// <summary>
/// Maps file extensions found inside Kenney asset bundles to an <see cref="AssetKind"/>.
/// </summary>
public static class AssetClassifier
{
    private static readonly Dictionary<string, AssetKind> KindsByExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        ["png"] = AssetKind.Image,
        ["jpg"] = AssetKind.Image,
        ["jpeg"] = AssetKind.Image,
        ["gif"] = AssetKind.Image,
        ["bmp"] = AssetKind.Image,
        ["webp"] = AssetKind.Image,
        ["svg"] = AssetKind.Vector,
        ["glb"] = AssetKind.Model3D,
        ["gltf"] = AssetKind.Model3D,
        ["fbx"] = AssetKind.Model3D,
        ["obj"] = AssetKind.Model3D,
        ["dae"] = AssetKind.Model3D,
        ["stl"] = AssetKind.Model3D,
        ["mtl"] = AssetKind.Material,
        ["ogg"] = AssetKind.Audio,
        ["wav"] = AssetKind.Audio,
        ["mp3"] = AssetKind.Audio,
        ["ttf"] = AssetKind.Font,
        ["otf"] = AssetKind.Font,
        ["txt"] = AssetKind.Document,
        ["html"] = AssetKind.Document,
        ["htm"] = AssetKind.Document,
        ["xml"] = AssetKind.Document,
        ["md"] = AssetKind.Document,
        ["url"] = AssetKind.Document,
        ["pdf"] = AssetKind.Document,
        ["zip"] = AssetKind.Archive,
        ["tmx"] = AssetKind.TiledMap,
        ["tsx"] = AssetKind.TiledMap,
        ["swf"] = AssetKind.Flash,
        ["blend"] = AssetKind.SourceFile,
        ["3ds"] = AssetKind.SourceFile,
        ["skp"] = AssetKind.SourceFile,
        ["ai"] = AssetKind.SourceFile,
        ["mat"] = AssetKind.SourceFile,
        ["woff"] = AssetKind.SourceFile,
        ["woff2"] = AssetKind.SourceFile,
        ["unitypackage"] = AssetKind.EnginePackage,
        ["capx"] = AssetKind.EnginePackage,
        ["c3p"] = AssetKind.EnginePackage,
        ["tres"] = AssetKind.EnginePackage,
        ["tscn"] = AssetKind.EnginePackage,
        ["gd"] = AssetKind.EnginePackage,
        ["godot"] = AssetKind.EnginePackage,
        ["stex"] = AssetKind.EnginePackage,
        ["oggstr"] = AssetKind.EnginePackage,
        ["import"] = AssetKind.EnginePackage,
    };

    /// <summary>
    /// Classifies a file inside a bundle archive by its extension.
    /// </summary>
    /// <param name="entryPath">The forward-slash path of the file inside the archive.</param>
    /// <returns>The kind the extension maps to, or <see cref="AssetKind.Unknown"/>.</returns>
    public static AssetKind Classify(string entryPath)
    {
        if (string.IsNullOrEmpty(entryPath)) { return AssetKind.Unknown; }

        var lastDot = entryPath.LastIndexOf('.');
        if (lastDot < 0 || lastDot == entryPath.Length - 1) { return AssetKind.Unknown; }

        var extension = entryPath.Substring(lastDot + 1);
        return KindsByExtension.TryGetValue(extension, out var kind) ? kind : AssetKind.Unknown;
    }
}
