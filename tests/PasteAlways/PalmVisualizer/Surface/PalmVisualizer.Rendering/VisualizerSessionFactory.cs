using System;

namespace PalmVisualizer.Rendering;

/// <summary>
/// The <see cref="IVisualizerSessionFactory"/> the application registers: hands back a
/// <see cref="VisualizerSession"/> bound to the host page's game canvas.
/// </summary>
public sealed class VisualizerSessionFactory : IVisualizerSessionFactory
{
    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="host"/> is null,
    /// or when the host has no canvas yet.</exception>
    public IVisualizerSession CreateSession(IGameCanvasHost host)
    {
        if (host == null) { throw new ArgumentNullException(nameof(host)); }

        return new VisualizerSession(host.Canvas);
    }
}
