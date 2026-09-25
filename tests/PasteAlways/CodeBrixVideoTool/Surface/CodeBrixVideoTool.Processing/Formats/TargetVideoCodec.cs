namespace CodeBrixVideoTool.Processing.Formats;

/// <summary>
/// The video codec a destination format is written with. The application chooses it from the
/// destination and never asks the user.
/// </summary>
public enum TargetVideoCodec
{
    /// <summary>AV1 - every one of the four supported formats.</summary>
    Av1 = 0,

    /// <summary>H.264 - the <c>.mp4</c> export, for the widest possible compatibility.</summary>
    H264 = 1,
}
