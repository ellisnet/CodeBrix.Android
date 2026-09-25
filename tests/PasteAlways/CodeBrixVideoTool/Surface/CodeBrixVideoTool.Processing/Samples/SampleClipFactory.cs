using CodeBrix.VideoProcessing;
using CodeBrix.VideoProcessing.Enums;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CodeBrixVideoTool.Processing.Samples;

/// <summary>
/// Makes the small synthetic clips the tests and the application's scripted smoke run need, so that
/// neither has to carry a media file around with it.
/// </summary>
/// <remarks>
/// The picture and the tone come from FFmpeg's own test sources, generated through
/// CodeBrix.VideoProcessing like everything else this application asks FFmpeg to do. Nothing is
/// copied from anywhere and nothing is left behind: every clip is written where the caller asks.
/// </remarks>
public static class SampleClipFactory
{
    /// <summary>Writes a small H.264 and AAC MP4 file, of the kind an import starts from.</summary>
    /// <param name="path">The file to write, overwriting anything already there.</param>
    /// <param name="width">The picture's width in pixels. Must be even.</param>
    /// <param name="height">The picture's height in pixels. Must be even.</param>
    /// <param name="duration">How long the clip runs.</param>
    /// <param name="frameRate">The picture's frame rate.</param>
    /// <param name="cancellationToken">Stops the generation.</param>
    /// <returns>The path that was written.</returns>
    /// <exception cref="VideoToolProcessingException">FFmpeg could not write the clip.</exception>
    public static async Task<string> WriteMp4Async(
        string path,
        int width = 320,
        int height = 240,
        TimeSpan duration = default,
        double frameRate = 25d,
        CancellationToken cancellationToken = default)
    {
        var length = duration <= TimeSpan.Zero ? TimeSpan.FromSeconds(3) : duration;
        var filterGraph = string.Create(CultureInfo.InvariantCulture,
            $"testsrc2=size={width}x{height}:rate={frameRate}[out0]; sine=frequency=440:sample_rate=48000[out1]");

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));

        var errors = new List<string>();
        var succeeded = await FFMpegArguments
            .FromFileInput(filterGraph, false, input => input.ForceFormat("lavfi"))
            .OutputToFile(path, true, options => options
                .WithDuration(length)
                .WithVideoCodec("libx264")
                .WithConstantRateFactor(28)
                .WithSpeedPreset(Speed.UltraFast)
                .ForcePixelFormat("yuv420p")
                .WithAudioCodec("aac")
                .WithAudioBitrate(96)
                .ForceFormat("mp4"))
            .NotifyOnError(errors.Add)
            .CancellableThrough(cancellationToken)
            .ProcessAsynchronously(false)
            .ConfigureAwait(false);

        if (!succeeded || !File.Exists(path))
        {
            throw new VideoToolProcessingException(
                "A sample clip could not be generated. " + string.Join(" ", errors.TakeLast(5)));
        }

        return path;
    }

    /// <summary>
    /// Writes a small MP4 that carries a caption track and chapters as well as picture and sound -
    /// the kind of file an import has something to carry across.
    /// </summary>
    /// <param name="folder">The folder to write the clip and its two working files into.</param>
    /// <param name="duration">How long the clip runs.</param>
    /// <param name="cancellationToken">Stops the generation.</param>
    /// <returns>The path of the MP4 that was written.</returns>
    /// <exception cref="VideoToolProcessingException">FFmpeg could not write the clip.</exception>
    public static async Task<string> WriteRichMp4Async(
        string folder, TimeSpan duration = default, CancellationToken cancellationToken = default)
    {
        var length = duration <= TimeSpan.Zero ? TimeSpan.FromSeconds(3) : duration;
        Directory.CreateDirectory(folder);

        var plain = Path.Combine(folder, "sample-plain.mp4");
        var captions = Path.Combine(folder, "sample.en.vtt");
        var chapters = Path.Combine(folder, "sample.ffmetadata");
        var rich = Path.Combine(folder, "sample.mp4");

        await WriteMp4Async(plain, 320, 240, length, 25d, cancellationToken).ConfigureAwait(false);
        WriteWebVtt(captions, length);
        WriteChapterMetadata(chapters, length);

        var errors = new List<string>();
        var succeeded = await FFMpegArguments
            .FromFileInput(plain)
            .AddFileInput(captions, false)
            .AddFileInput(chapters, false)
            .MapMetaData(2)
            .OutputToFile(rich, true, options => options
                .SelectStream(0, 0, Channel.Video)
                .SelectStream(0, 0, Channel.Audio)
                .SelectStream(0, 1, Channel.Subtitle)
                .CopyChannel(Channel.Both)
                .WithSubtitleCodec("mov_text")
                .WithStreamMetadata(Channel.Subtitle, 0, "language", "eng")
                .ForceFormat("mp4"))
            .NotifyOnError(errors.Add)
            .CancellableThrough(cancellationToken)
            .ProcessAsynchronously(false)
            .ConfigureAwait(false);

        if (!succeeded || !File.Exists(rich))
        {
            throw new VideoToolProcessingException(
                "A sample clip with captions and chapters could not be generated. " + string.Join(" ", errors.TakeLast(5)));
        }

        return rich;
    }

    /// <summary>Writes a small WebVTT caption file with a few cues spread over a duration.</summary>
    /// <param name="path">The file to write, overwriting anything already there.</param>
    /// <param name="duration">The media the cues belong to.</param>
    /// <param name="cueCount">How many cues to write.</param>
    /// <returns>The path that was written.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The cue count is not positive.</exception>
    public static string WriteWebVtt(string path, TimeSpan duration, int cueCount = 3)
    {
        if (cueCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(cueCount), cueCount, "A caption file needs at least one cue.");
        }

        var length = duration <= TimeSpan.Zero ? TimeSpan.FromSeconds(3) : duration;
        var slot = length / cueCount;

        var text = new StringBuilder("WEBVTT\n\n");
        for (var index = 0; index < cueCount; index++)
        {
            var start = slot * index;
            var end = start + (slot * 0.8);
            text.Append(CultureInfo.InvariantCulture, $"cue{index + 1}\n")
                .Append(Format(start)).Append(" --> ").Append(Format(end)).Append('\n')
                .Append(CultureInfo.InvariantCulture, $"Sample cue {index + 1}\n\n");
        }

        File.WriteAllText(path, text.ToString(), new UTF8Encoding(false));
        return path;
    }

    /// <summary>Writes a small FFmpeg metadata file describing a few chapters.</summary>
    /// <param name="path">The file to write, overwriting anything already there.</param>
    /// <param name="duration">The media the chapters belong to.</param>
    /// <param name="chapterCount">How many chapters to write.</param>
    /// <returns>The path that was written.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The chapter count is not positive.</exception>
    public static string WriteChapterMetadata(string path, TimeSpan duration, int chapterCount = 3)
    {
        if (chapterCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(chapterCount), chapterCount, "A chapter file needs at least one chapter.");
        }

        var length = duration <= TimeSpan.Zero ? TimeSpan.FromSeconds(3) : duration;
        var slot = length / chapterCount;

        var text = new StringBuilder(";FFMETADATA1\n");
        for (var index = 0; index < chapterCount; index++)
        {
            var start = (long)(slot * index).TotalMilliseconds;
            var end = (long)(slot * (index + 1)).TotalMilliseconds;
            text.Append("\n[CHAPTER]\nTIMEBASE=1/1000\n")
                .Append(CultureInfo.InvariantCulture, $"START={start}\n")
                .Append(CultureInfo.InvariantCulture, $"END={end}\n")
                .Append(CultureInfo.InvariantCulture, $"title=Part {index + 1}\n");
        }

        File.WriteAllText(path, text.ToString(), new UTF8Encoding(false));
        return path;
    }

    private static string Format(TimeSpan value) =>
        value.ToString(@"hh\:mm\:ss\.fff", CultureInfo.InvariantCulture);
}
