using CodeBrix.VideoPlayback.Captions;
using CodeBrix.VideoPlayback.Chapters;
using CodeBrix.VideoPlayback.Containers;
using CodeBrix.VideoProcessing;
using CodeBrix.VideoProcessing.Enums;
using CodeBrixVideoTool.Processing.Formats;
using CodeBrixVideoTool.Processing.Probing;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Chapter = CodeBrix.VideoPlayback.Chapters.Chapter; //CodeBrix.VideoProcessing has a Chapter of its own

namespace CodeBrixVideoTool.Processing.Containers;

/// <summary>
/// Lifts a source file's chapters and caption tracks out into files beside it, so that the
/// authoring pass can be told to carry them across.
/// </summary>
/// <remarks>
/// <para>
/// The four supported formats are all read by the playback core's own container readers, which hand
/// over chapters and cues as objects and need no external tool. Only the <c>.mp4</c> family goes
/// through FFmpeg, and only for its text caption streams: an image-based caption track has no WebVTT
/// form at all and is reported rather than silently lost.
/// </para>
/// <para>
/// Every chapter written from here carries exactly ONE title, whichever path it came down: this
/// application supports one chapter-title language. See
/// <see cref="CollapseToOneTitlePerChapter" /> for the rule and for what is reported when a source
/// carried more.
/// </para>
/// </remarks>
public sealed class SidecarExtractor
{
    private static readonly string[] TextCaptionCodecs =
    [
        "subrip", "srt", "webvtt", "ass", "ssa", "mov_text", "text", "eia_608", "subviewer",
    ];

    /// <summary>Extracts whatever chapters and captions a source carries.</summary>
    /// <param name="source">What probing found in the file.</param>
    /// <param name="workingFolder">A folder the extracted files may be written into.</param>
    /// <param name="cancellationToken">Stops the extraction.</param>
    /// <returns>The files that were written, and anything that could not be carried across.</returns>
    /// <exception cref="VideoToolProcessingException">The source could not be read.</exception>
    public async Task<MediaSidecars> ExtractAsync(
        SourceMediaInfo source, string workingFolder, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        Directory.CreateDirectory(workingFolder);

        return MediaFormats.IsSupportedFormat(source.Format)
            ? ExtractFromContainerReader(source, workingFolder)
            : await ExtractWithFfmpegAsync(source, workingFolder, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Extracts the chapters and captions an already-open container reader is holding. Used by the
    /// Mode 2 route, which has the reader open anyway to demultiplex the media.
    /// </summary>
    /// <param name="reader">A reader whose packets have already been drained, if it needed draining.</param>
    /// <param name="workingFolder">A folder the extracted files may be written into.</param>
    /// <returns>The files that were written.</returns>
    public static MediaSidecars ExtractFromReader(IMediaContainerReader reader, string workingFolder)
    {
        ArgumentNullException.ThrowIfNull(reader);
        Directory.CreateDirectory(workingFolder);

        var notes = new List<string>();
        var captions = new List<ExtractedCaption>();

        for (var index = 0; index < reader.CaptionTracks.Count; index++)
        {
            var track = reader.CaptionTracks[index];
            if (track.CueCount == 0)
            {
                notes.Add($"Caption track {index} carried no cues and was left out.");
                continue;
            }

            var language = NormalizeLanguage(track.Language);
            var path = Path.Combine(workingFolder, WebVttFile.FileNameFor(index, language));
            WebVttFile.Write(track, path);
            captions.Add(new ExtractedCaption(path, language, track.Name ?? string.Empty, track.Flags, track.CueCount));
        }

        var chapters = CollapseToOneTitlePerChapter(reader.Chapters, notes);
        var chaptersPath = WriteChapters(chapters, workingFolder);
        return new MediaSidecars(chaptersPath, captions, notes);
    }

    /// <summary>
    /// Reduces every chapter to a single, untagged title, because this application carries one
    /// chapter-title language.
    /// </summary>
    /// <param name="chapters">The chapters a source declared, with whatever titles it declared.</param>
    /// <param name="notes">
    /// Collects one sentence when at least one language title was dropped. Nothing is added when every
    /// chapter already carried a single title, which is what an <c>.mp4</c> source always produces.
    /// </param>
    /// <returns>The same chapters, each with at most one title, keyed by the empty language tag.</returns>
    /// <remarks>
    /// The title kept is the untagged one when the source has it, and otherwise the FIRST title the
    /// source lists - the order the container reader hands them over in. The count reported is of
    /// DISTINCT languages dropped across the whole file, so a two-language chapter list drops one
    /// language however many chapters carry it.
    /// </remarks>
    public static IReadOnlyList<Chapter> CollapseToOneTitlePerChapter(
        IReadOnlyList<Chapter> chapters, IList<string> notes)
    {
        if (chapters is not { Count: > 0 })
        {
            return [];
        }

        var dropped = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var collapsed = new List<Chapter>(chapters.Count);

        foreach (var chapter in chapters)
        {
            var titles = chapter.Titles;
            if (titles is not { Count: > 0 })
            {
                collapsed.Add(chapter);
                continue;
            }

            var keptLanguage = titles.ContainsKey(string.Empty) ? string.Empty : titles.Keys.First();
            foreach (var language in titles.Keys)
            {
                if (!string.Equals(language, keptLanguage, StringComparison.Ordinal) && !string.IsNullOrEmpty(language))
                {
                    dropped.Add(language);
                }
            }

            collapsed.Add(titles.Count == 1 && keptLanguage.Length == 0
                ? chapter
                : new Chapter(chapter.Index, chapter.Start, chapter.End, chapter.IsHidden,
                    new Dictionary<string, string> { [string.Empty] = titles[keptLanguage] }));
        }

        if (dropped.Count > 0 && notes is not null)
        {
            notes.Add($"{dropped.Count} chapter-title language(s) dropped: this application carries one title per chapter.");
        }

        return collapsed;
    }

    private static MediaSidecars ExtractFromContainerReader(SourceMediaInfo source, string workingFolder)
    {
        try
        {
            using var reader = MediaContainers.Open(source.Path);

            //A Matroska or WebM file interleaves its subtitle cues with the video, so the cues are
            //complete only once the file has been read through. The bespoke container keeps every
            //cue in its header, so its tracks are complete the instant it is open.
            if (reader.CaptionTracks.Count > 0 && reader.CaptionTracks.Any(t => !t.AreCuesComplete))
            {
                while (reader.TryReadPacket(out _))
                {
                    //Draining for the cues; nothing is decoded and no packet is kept.
                }
            }

            return ExtractFromReader(reader, workingFolder);
        }
        catch (Exception exception) when (exception is not VideoToolProcessingException)
        {
            throw new VideoToolProcessingException(
                $"The chapters and captions in '{source.FileName}' could not be read: {exception.Message}", exception);
        }
    }

    private static async Task<MediaSidecars> ExtractWithFfmpegAsync(
        SourceMediaInfo source, string workingFolder, CancellationToken cancellationToken)
    {
        var notes = new List<string>();
        var captions = new List<ExtractedCaption>();

        IMediaAnalysis analysis;
        try
        {
            analysis = await FFProbe.AnalyseAsync(source.Path, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new VideoToolProcessingException(
                $"'{source.FileName}' could not be probed for its chapters and captions: {exception.Message}", exception);
        }

        var streams = analysis.SubtitleStreams ?? [];
        for (var index = 0; index < streams.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var stream = streams[index];
            var codec = stream.CodecName ?? string.Empty;
            if (!TextCaptionCodecs.Contains(codec, StringComparer.OrdinalIgnoreCase))
            {
                notes.Add($"Caption track {index} is '{codec}', which has no text form, so it was not carried across.");
                continue;
            }

            var language = NormalizeLanguage(stream.Language);
            var path = Path.Combine(workingFolder, WebVttFile.FileNameFor(index, language));

            var succeeded = await FFMpegArguments
                .FromFileInput(source.Path)
                .OutputToFile(path, true, options => options
                    .SelectStream(index, 0, Channel.Subtitle)
                    .WithSubtitleCodec("webvtt")
                    .ForceFormat("webvtt"))
                .CancellableThrough(cancellationToken)
                .ProcessAsynchronously(false)
                .ConfigureAwait(false);

            if (!succeeded || !File.Exists(path))
            {
                notes.Add($"Caption track {index} ('{codec}') could not be converted to WebVTT and was not carried across.");
                continue;
            }

            var read = CaptionFiles.ReadWebVttFile(path, index, language, stream.Language ?? string.Empty, CaptionTrackFlags.None);
            if (read.CueCount == 0)
            {
                notes.Add($"Caption track {index} converted to an empty WebVTT file and was left out.");
                continue;
            }

            captions.Add(new ExtractedCaption(path, language, string.Empty, CaptionTrackFlags.None, read.CueCount));
        }

        var chapters = CollapseToOneTitlePerChapter(ChaptersFrom(analysis), notes);
        var chaptersPath = WriteChapters(chapters, workingFolder);
        return new MediaSidecars(chaptersPath, captions, notes);
    }

    private static IReadOnlyList<Chapter> ChaptersFrom(IMediaAnalysis analysis)
    {
        var source = analysis.Chapters;
        if (source is not { Count: > 0 })
        {
            return [];
        }

        var chapters = new List<Chapter>(source.Count);
        for (var index = 0; index < source.Count; index++)
        {
            var chapter = source[index];
            var title = string.IsNullOrWhiteSpace(chapter.Title) || chapter.Title == "TitleValueNotSet"
                ? string.Create(CultureInfo.InvariantCulture, $"Chapter {index + 1}")
                : chapter.Title;

            chapters.Add(new Chapter(index, chapter.Start, chapter.End, false,
                new Dictionary<string, string> { [string.Empty] = title }));
        }

        return chapters;
    }

    private static string WriteChapters(IReadOnlyList<Chapter> chapters, string workingFolder)
    {
        if (chapters is not { Count: > 0 })
        {
            return null;
        }

        var path = Path.Combine(workingFolder, "chapters.ffmetadata");
        File.WriteAllText(path, FfMetadataChapters.Write(chapters));
        return path;
    }

    private static string NormalizeLanguage(string language) =>
        string.IsNullOrWhiteSpace(language) ? "und" : language.Trim();
}
