namespace CodeBrixVideoTool.Processing.Formats;

/// <summary>One row of the destination-format drop-down.</summary>
public sealed class DestinationOption
{
    /// <summary>Creates the row.</summary>
    /// <param name="kind">The format this row writes.</param>
    public DestinationOption(MediaFormatKind kind)
    {
        Kind = kind;
        Label = MediaFormats.DisplayName(kind);
    }

    /// <summary>The format this row writes.</summary>
    public MediaFormatKind Kind { get; }

    /// <summary>What the drop-down shows.</summary>
    public string Label { get; }

    /// <summary>Returns the label.</summary>
    /// <returns>The label.</returns>
    public override string ToString() => Label;
}
