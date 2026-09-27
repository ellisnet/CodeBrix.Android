namespace PalmVisualizer.Rendering;

/// <summary>
/// Builds the Visualize Mode scene for a page's game canvas. Registered with the
/// dependency-injection container at startup and resolved by the view model, which then owns
/// the session it is handed without knowing how one is made.
/// </summary>
public interface IVisualizerSessionFactory
{
    /// <summary>
    /// Creates the visualizer session that renders into the host's canvas. Call at the
    /// canvas's first real layout size; the session is not started.
    /// </summary>
    /// <param name="host">The page that owns the game canvas.</param>
    /// <returns>The session, ready to be started.</returns>
    IVisualizerSession CreateSession(IGameCanvasHost host);
}
