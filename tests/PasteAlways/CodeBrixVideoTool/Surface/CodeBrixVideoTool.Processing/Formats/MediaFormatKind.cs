namespace CodeBrixVideoTool.Processing.Formats;

/// <summary>
/// The container shapes this application knows about: the four formats it can write and play, plus
/// the <c>.mp4</c> family it imports from and exports to.
/// </summary>
public enum MediaFormatKind
{
    /// <summary>Nothing recognisable, or a file that has not been probed yet.</summary>
    Unknown = 0,

    /// <summary>
    /// The <c>.mp4</c> family: the FFmpeg-readable interchange containers that sit outside the four
    /// supported formats. Import reads one of these; export writes an <c>.mp4</c>. Never playable
    /// inside this application, because the in-application player decodes AV1 and nothing else.
    /// </summary>
    Mp4 = 1,

    /// <summary>A standard Matroska file: <c>.mkv</c>, AV1 video and Opus audio.</summary>
    Matroska = 2,

    /// <summary>A standard WebM file: <c>.webm</c>, AV1 video and Opus audio.</summary>
    WebM = 3,

    /// <summary>
    /// CodeBrix Mode 1: a <c>.cbv</c> file that is a WebM file constrained to the streamable
    /// profile - AV1 video, Opus audio, cues in front of the first cluster.
    /// </summary>
    CodeBrixMode1 = 4,

    /// <summary>
    /// CodeBrix Mode 2: a <c>.cbv</c> file in the bespoke CBVF container - AV1 video and Vorbis
    /// audio, with every index entry and every caption cue ahead of the media data.
    /// </summary>
    CodeBrixMode2 = 5,
}
