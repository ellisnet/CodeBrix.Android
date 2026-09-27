// STUB (paste always): PalmVisualizer.Rendering.VisualizerSession (CodeBrix.Samples PalmVisualizer/src/libs/
// PalmVisualizer.Rendering/VisualizerSession.cs); the original drives CodeBrix.Platform.GameEngine (a package that
// depends on CodeBrix.Platform packages, not allowed in a paste-always head; GameEngine is D-O14 NotSupported on
// Android). Same namespace, type name and public members.
using System;
using System.Collections.Generic;
using CodeBrix.Platform.GameEngine.Host.Rendering;

namespace PalmVisualizer.Rendering;

/// <summary>Stand-in for the palm-reactive visualizer session (compile-only).</summary>
public sealed class VisualizerSession : IVisualizerSession
{
    /// <summary>Initializes a new instance of the <see cref="VisualizerSession"/> class.</summary>
    public VisualizerSession(GameSurfaceCanvas canvas)
    {
        if (canvas == null) { throw new ArgumentNullException(nameof(canvas)); }
    }

    /// <inheritdoc />
    public bool IsStarted { get; private set; }

    /// <inheritdoc />
    public void Start() => IsStarted = true;

    /// <inheritdoc />
    public void Pause() { }

    /// <inheritdoc />
    public void Resume() { }

    /// <inheritdoc />
    public void UpdatePalms(IReadOnlyList<PalmAttractor> palms) { }

    /// <inheritdoc />
    public void Stop() => IsStarted = false;
}
