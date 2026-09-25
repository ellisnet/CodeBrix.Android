using System.Globalization;

namespace CodeBrixVideoTool.Processing.Resolution;

/// <summary>
/// One rung of the resolution ladder: either the source's own size, or a smaller size derived from
/// it proportionally.
/// </summary>
public sealed class ResolutionOption
{
    private ResolutionOption(string label, int width, int height, bool isOriginal)
    {
        Label = label;
        Width = width;
        Height = height;
        IsOriginal = isOriginal;
    }

    /// <summary>What to show a person: "Original (1920 x 1080)" or "1080p (1920 x 1080)".</summary>
    public string Label { get; }

    /// <summary>The coded width this rung asks for, in pixels. Always even.</summary>
    public int Width { get; }

    /// <summary>The coded height this rung asks for, in pixels. Always even.</summary>
    public int Height { get; }

    /// <summary>True for the rung that keeps the source's own size.</summary>
    public bool IsOriginal { get; }

    /// <summary>Builds the rung that keeps the source's own size.</summary>
    /// <param name="width">The source's coded width.</param>
    /// <param name="height">The source's coded height.</param>
    /// <returns>The "Original" rung.</returns>
    public static ResolutionOption Original(int width, int height) =>
        new(Describe("Original", width, height), width, height, true);

    /// <summary>Builds a rung that reduces the source proportionally.</summary>
    /// <param name="name">The rung's name, such as "1080p".</param>
    /// <param name="width">The reduced coded width.</param>
    /// <param name="height">The reduced coded height.</param>
    /// <returns>A reduction rung.</returns>
    public static ResolutionOption Reduced(string name, int width, int height) =>
        new(Describe(name, width, height), width, height, false);

    private static string Describe(string name, int width, int height) =>
        string.Create(CultureInfo.InvariantCulture, $"{name} ({width} x {height})");

    /// <summary>Returns the rung's label.</summary>
    /// <returns>The label.</returns>
    public override string ToString() => Label;
}
