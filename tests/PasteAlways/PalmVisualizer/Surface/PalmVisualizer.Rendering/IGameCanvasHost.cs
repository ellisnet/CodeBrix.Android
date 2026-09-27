using CodeBrix.Platform.GameEngine.Host.Rendering;

namespace PalmVisualizer.Rendering;

/// <summary>
/// The page that owns the visualizer's game canvas, seen as the one thing this library needs
/// from it. The hosting page implements it and hands itself to the view model at the canvas's
/// first real layout size; the view model passes it to
/// <see cref="IVisualizerSessionFactory.CreateSession"/> without ever naming a view type.
/// </summary>
public interface IGameCanvasHost
{
    /// <summary>The game canvas the visualizer renders into.</summary>
    GameSurfaceCanvas Canvas { get; }
}
