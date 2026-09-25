namespace PolyHavenBrowser.Rendering;

/// <summary>
/// Loads a 3D model file into renderer-ready <see cref="LoadedModel"/> form. The default
/// implementation is <see cref="GltfModelLoader"/> (glTF is the format Poly Haven serves
/// for every model); the interface exists so the loading technology can be swapped or
/// mocked without touching the renderer.
/// </summary>
public interface IModelLoader
{
    /// <summary>Loads a self-contained model (e.g. a .glb binary) from a stream.</summary>
    /// <exception cref="InvalidDataException">The stream does not contain a loadable model.</exception>
    LoadedModel Load(Stream stream);

    /// <summary>Loads a model from a file path, resolving any side-car files (textures, .bin buffers) relative to it.</summary>
    /// <exception cref="InvalidDataException">The file is not a loadable model.</exception>
    LoadedModel LoadFile(string path);
}
