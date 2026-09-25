using CodeBrix.VideoPlayback.Captions;

namespace CodeBrixVideoTool.Processing.Containers;

/// <summary>One caption track written out beside the media as a WebVTT file.</summary>
public sealed class ExtractedCaption
{
    /// <summary>Creates the record.</summary>
    /// <param name="path">Where the WebVTT file was written.</param>
    /// <param name="language">The track's BCP 47 language tag.</param>
    /// <param name="name">The track's name, or an empty string.</param>
    /// <param name="flags">Whether the track is default, forced or for the hearing impaired.</param>
    /// <param name="cueCount">How many cues the file carries.</param>
    public ExtractedCaption(string path, string language, string name, CaptionTrackFlags flags, int cueCount)
    {
        Path = path;
        Language = language;
        Name = name;
        Flags = flags;
        CueCount = cueCount;
    }

    /// <summary>Where the WebVTT file was written.</summary>
    public string Path { get; }

    /// <summary>The track's BCP 47 language tag; "und" when the source did not state one.</summary>
    public string Language { get; }

    /// <summary>The track's name, or an empty string.</summary>
    public string Name { get; }

    /// <summary>Whether the track is default, forced or for the hearing impaired.</summary>
    public CaptionTrackFlags Flags { get; }

    /// <summary>How many cues the file carries.</summary>
    public int CueCount { get; }
}
