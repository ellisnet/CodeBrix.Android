namespace KenneyAssetBrowser.AssetRead.Models;

/// <summary>
/// The broad kind of a file found inside a Kenney asset bundle, derived from its file extension.
/// </summary>
public enum AssetKind
{
    /// <summary>A kind that could not be determined from the file extension.</summary>
    Unknown,

    /// <summary>A raster image (PNG, JPEG, GIF, BMP or WebP).</summary>
    Image,

    /// <summary>A vector drawing (SVG).</summary>
    Vector,

    /// <summary>A 3D model (GLB, glTF, FBX, OBJ, DAE or STL).</summary>
    Model3D,

    /// <summary>A material definition that accompanies a 3D model (MTL).</summary>
    Material,

    /// <summary>An audio clip (OGG, WAV or MP3).</summary>
    Audio,

    /// <summary>A font file (TTF or OTF).</summary>
    Font,

    /// <summary>A text or markup document (TXT, HTML, XML, MD, URL or PDF).</summary>
    Document,

    /// <summary>A nested archive (ZIP).</summary>
    Archive,

    /// <summary>A Tiled map or tileset definition (TMX or TSX).</summary>
    TiledMap,

    /// <summary>An obsolete Flash companion file (SWF).</summary>
    Flash,

    /// <summary>An authoring source file (BLEND, 3DS, SKP, AI, MAT, WOFF/WOFF2, …).</summary>
    SourceFile,

    /// <summary>A game-engine package or project file (Unity, Godot, Construct).</summary>
    EnginePackage
}
