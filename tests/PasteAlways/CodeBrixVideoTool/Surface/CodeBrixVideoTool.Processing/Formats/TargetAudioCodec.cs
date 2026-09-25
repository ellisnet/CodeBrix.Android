namespace CodeBrixVideoTool.Processing.Formats;

/// <summary>
/// The audio codec a destination format is written with. The application chooses it from the
/// destination and never asks the user.
/// </summary>
public enum TargetAudioCodec
{
    /// <summary>Opus - standard MKV, standard WebM and CodeBrix Mode 1.</summary>
    Opus = 0,

    /// <summary>
    /// Vorbis - CodeBrix Mode 2, always. A bespoke CBVF file this application writes never contains
    /// an Opus track.
    /// </summary>
    Vorbis = 1,

    /// <summary>AAC - the <c>.mp4</c> export, for the widest possible compatibility.</summary>
    Aac = 2,
}
