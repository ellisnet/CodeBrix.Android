using CodeBrixVideoTool.Processing.Formats;
using CodeBrixVideoTool.Processing.Probing;
using CodeBrixVideoTool.Processing.Resolution;
using System;
using System.Collections.Generic;
using System.IO;

namespace CodeBrixVideoTool.Processing.Planning;

/// <summary>Settles what a conversion will do before any of it is done.</summary>
public static class ConversionPlanner
{
    /// <summary>Builds the plan for one conversion.</summary>
    /// <param name="source">What probing found in the file being converted.</param>
    /// <param name="destination">The format to write.</param>
    /// <param name="outputPath">Where the result goes.</param>
    /// <param name="resolution">
    /// The rung of the resolution ladder to use, or null to keep the source's own size.
    /// </param>
    /// <param name="quality">
    /// The quality stop to encode at. <see cref="QualityLevel.Good" /> when it is not stated, which is
    /// what the application has always written.
    /// </param>
    /// <returns>The settled plan.</returns>
    /// <exception cref="ArgumentNullException">The source is null.</exception>
    /// <exception cref="VideoToolProcessingException">
    /// The conversion is not one this application offers, or the output would overwrite the source.
    /// </exception>
    public static ConversionPlan Create(
        SourceMediaInfo source,
        MediaFormatKind destination,
        string outputPath,
        ResolutionOption resolution,
        QualityLevel quality = QualityLevel.Good)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new VideoToolProcessingException("A conversion needs somewhere to put its result.");
        }

        if (source.Format == destination)
        {
            throw new VideoToolProcessingException(
                $"'{source.FileName}' is already {MediaFormats.DisplayName(destination)}, so there is nothing to convert.");
        }

        ConversionOperationKind operation;
        try
        {
            operation = MediaFormats.OperationFor(source.Format, destination);
        }
        catch (ArgumentException exception)
        {
            throw new VideoToolProcessingException(exception.Message, exception);
        }

        if (PathsMatch(source.Path, outputPath))
        {
            throw new VideoToolProcessingException("A conversion cannot write over the file it is reading.");
        }

        var chosen = resolution ?? ResolutionOption.Original(
            ResolutionLadder.MakeEven(source.Width), ResolutionLadder.MakeEven(source.Height));

        return new ConversionPlan(source, destination, outputPath, chosen, quality, operation,
            DescribeSteps(source, destination, operation, chosen, quality));
    }

    /// <summary>
    /// A sensible file name for the result: the source's name, the destination's own suffix when the
    /// two would otherwise collide, and the destination's extension.
    /// </summary>
    /// <param name="source">The file being converted.</param>
    /// <param name="destination">The format to write.</param>
    /// <returns>A bare file name.</returns>
    /// <exception cref="ArgumentNullException">The source is null.</exception>
    public static string SuggestOutputFileName(SourceMediaInfo source, MediaFormatKind destination)
    {
        ArgumentNullException.ThrowIfNull(source);

        var stem = Path.GetFileNameWithoutExtension(source.FileName);
        var suffix = destination switch
        {
            MediaFormatKind.CodeBrixMode1 => "-mode1",
            MediaFormatKind.CodeBrixMode2 => "-mode2",
            _ => string.Empty,
        };

        return stem + suffix + MediaFormats.Extension(destination);
    }

    private static IReadOnlyList<string> DescribeSteps(
        SourceMediaInfo source,
        MediaFormatKind destination,
        ConversionOperationKind operation,
        ResolutionOption resolution,
        QualityLevel quality)
    {
        var steps = new List<string>();

        if (source.Format == MediaFormatKind.CodeBrixMode2)
        {
            steps.Add("Demultiplex the bespoke CBVF container, re-wrap its AV1 stream in IVF and its " +
                      "audio stream in Ogg, and mux those into one intermediate file without re-encoding.");
        }

        if (source.CaptionTrackCount > 0 || source.ChapterCount > 0)
        {
            steps.Add($"Lift out {source.CaptionTrackCount} caption track(s) and {source.ChapterCount} chapter(s) " +
                      "so the conversion can carry them across.");
        }

        var size = resolution.IsOriginal
            ? "at its own size"
            : $"scaled to {resolution.Width} x {resolution.Height}";

        var audioCodec = MediaFormats.AudioCodecFor(destination);

        //Every destination but the .mp4 export is capped at stereo, whichever codec it carries: this
        //application writes mono or stereo audio only. Nothing is ever upmixed, so a mono source stays
        //mono and a stereo one is never called a downmix.
        var sound = source.HasAudio && MediaFormats.AudioChannelsFor(destination, source.AudioChannels) < source.AudioChannels
            ? $"{audioCodec} audio downmixed to stereo (this application writes mono or stereo audio only)"
            : $"{audioCodec} audio";

        steps.Add(destination == MediaFormatKind.Mp4
            ? $"Encode H.264 video {size} and AAC audio into an MP4 file, at {quality} quality."
            : $"Encode AV1 video {size} and {sound} into {MediaFormats.DisplayName(destination)}, at {quality} quality.");

        if (MediaFormats.IsCodeBrixContainer(destination))
        {
            steps.Add("Check the result against the streamable profile.");
        }

        steps.Add($"({operation} complete.)");
        return steps;
    }

    private static bool PathsMatch(string first, string second)
    {
        try
        {
            return string.Equals(Path.GetFullPath(first), Path.GetFullPath(second), StringComparison.Ordinal);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
