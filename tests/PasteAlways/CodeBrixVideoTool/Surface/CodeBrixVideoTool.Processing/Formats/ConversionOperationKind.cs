namespace CodeBrixVideoTool.Processing.Formats;

/// <summary>
/// What a conversion is called, which follows entirely from where its source and destination sit
/// relative to the <c>.mp4</c> boundary.
/// </summary>
public enum ConversionOperationKind
{
    /// <summary>An <c>.mp4</c>-family file coming in, to one of the four supported formats.</summary>
    Import = 0,

    /// <summary>One of the four supported formats to another of them.</summary>
    Transcode = 1,

    /// <summary>One of the four supported formats going out, to <c>.mp4</c>.</summary>
    Export = 2,
}
