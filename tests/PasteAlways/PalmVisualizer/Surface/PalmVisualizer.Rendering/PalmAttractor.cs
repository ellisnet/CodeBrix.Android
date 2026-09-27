namespace PalmVisualizer.Rendering;

/// <summary>
/// One open palm the visualization should be drawn toward, in normalized screen
/// coordinates. This is the whole seam between the vision pipeline and the rendering:
/// the consumer maps whatever it tracks into these points (mirroring the X coordinate
/// when the user watches a mirror-style view) and hands them to
/// <see cref="VisualizerSession.UpdatePalms"/>.
/// </summary>
public readonly struct PalmAttractor
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PalmAttractor"/> struct.
    /// </summary>
    /// <param name="id">A stable identifier for the palm while it stays in view.</param>
    /// <param name="x">The palm's horizontal position, 0 (left) .. 1 (right) across the visual.</param>
    /// <param name="y">The palm's vertical position, 0 (top) .. 1 (bottom) down the visual.</param>
    public PalmAttractor(int id, float x, float y)
    {
        Id = id;
        X = x;
        Y = y;
    }

    /// <summary>
    /// A stable identifier for the palm. The same physical hand should keep the same id
    /// from update to update so its glow follows it instead of re-fading in.
    /// </summary>
    public int Id { get; }

    /// <summary>The palm's horizontal position, 0 (left) .. 1 (right) across the visual.</summary>
    public float X { get; }

    /// <summary>The palm's vertical position, 0 (top) .. 1 (bottom) down the visual.</summary>
    public float Y { get; }
}
