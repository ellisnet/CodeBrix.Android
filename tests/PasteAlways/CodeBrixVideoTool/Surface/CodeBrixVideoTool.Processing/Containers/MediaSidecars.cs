using System;
using System.Collections.Generic;
using System.IO;

namespace CodeBrixVideoTool.Processing.Containers;

/// <summary>
/// The chapters and caption tracks lifted out of a source file and written beside it, ready to be
/// handed to the authoring pass as inputs of their own.
/// </summary>
/// <remarks>
/// The authoring library takes captions and chapters only as files: it maps the source's video and
/// audio, and every caption track it writes comes from a separate input. A source's own embedded
/// captions and chapters therefore have to be extracted first, which is what produces this.
/// </remarks>
public sealed class MediaSidecars
{
    /// <summary>Creates the record.</summary>
    /// <param name="chaptersPath">The FFmpeg metadata file holding the chapters, or null.</param>
    /// <param name="captions">The extracted caption tracks.</param>
    /// <param name="notes">Anything that could not be carried across, in a sentence each.</param>
    public MediaSidecars(string chaptersPath, IReadOnlyList<ExtractedCaption> captions, IReadOnlyList<string> notes)
    {
        ChaptersPath = chaptersPath;
        Captions = captions ?? [];
        Notes = notes ?? [];
    }

    /// <summary>An empty set: no chapters, no captions, nothing to report.</summary>
    public static MediaSidecars None { get; } = new(null, [], []);

    /// <summary>The FFmpeg metadata file holding the chapters, or null when there are none.</summary>
    public string ChaptersPath { get; }

    /// <summary>The extracted caption tracks, each a WebVTT file.</summary>
    public IReadOnlyList<ExtractedCaption> Captions { get; }

    /// <summary>Anything that could not be carried across, in a sentence each.</summary>
    public IReadOnlyList<string> Notes { get; }

    /// <summary>True when there is at least one chapter.</summary>
    public bool HasChapters => !string.IsNullOrEmpty(ChaptersPath) && File.Exists(ChaptersPath);

    /// <summary>How many caption tracks were extracted.</summary>
    public int CaptionCount => Captions.Count;

    /// <summary>A one-line summary for the run notes.</summary>
    /// <returns>What was carried across.</returns>
    public override string ToString() =>
        $"{CaptionCount} caption track(s), chapters {(HasChapters ? "carried" : "none")}";
}
