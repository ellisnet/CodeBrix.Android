using System.Collections.Generic;

namespace PalmVisualizer.Rendering;

/// <summary>
/// The Visualize Mode scene's lifecycle as its consumer drives it: start it once, park it
/// while the user is elsewhere, wake it again, feed it palms from any thread, and stop it at
/// teardown. Obtained from <see cref="IVisualizerSessionFactory"/>, so the view model that
/// drives the scene never constructs an engine surface itself.
/// </summary>
public interface IVisualizerSession
{
    /// <summary>Indicates whether <see cref="Start"/> has run.</summary>
    bool IsStarted { get; }

    /// <summary>
    /// Starts the engine and builds the shader scene. Must be called on the UI thread once
    /// the canvas has a non-zero size, and only once per process - use <see cref="Pause"/>
    /// and <see cref="Resume"/> to leave and re-enter Visualize Mode.
    /// </summary>
    void Start();

    /// <summary>
    /// Parks the visual at ~zero cost while the user is elsewhere, releasing every palm
    /// attractor so a later <see cref="Resume"/> starts from the undisturbed visual. Safe to
    /// call when not started or already paused.
    /// </summary>
    void Pause();

    /// <summary>
    /// Wakes the visual after a <see cref="Pause"/>. Safe to call when not started or not
    /// paused.
    /// </summary>
    void Resume();

    /// <summary>
    /// Sets the open palms currently attracting the visual. Positions are normalized 0..1
    /// across the visual (already mirrored by the caller when the user is watching a
    /// mirror-style view). Safe to call from any thread.
    /// </summary>
    /// <param name="palms">The attracting palms; empty (or null) releases them all.</param>
    void UpdatePalms(IReadOnlyList<PalmAttractor> palms);

    /// <summary>Stops the engine. Call when the hosting page is closing.</summary>
    void Stop();
}
