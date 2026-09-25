using CodeBrixVideoTool.Processing.Formats;
using System;
using System.Collections.Generic;

namespace CodeBrixVideoTool.Processing.Probing;

/// <summary>
/// What probing found in one media file: enough to name it, size its resolution ladder, and decide
/// which destinations it can go to.
/// </summary>
public sealed class SourceMediaInfo
{
    /// <summary>The full path of the file that was probed.</summary>
    public string Path { get; set; }

    /// <summary>The file's name without its folder, for showing in a list.</summary>
    public string FileName { get; set; }

    /// <summary>The container shape the file turned out to be.</summary>
    public MediaFormatKind Format { get; set; }

    /// <summary>How long the media runs.</summary>
    public TimeSpan Duration { get; set; }

    /// <summary>The video track's coded width, in pixels, or zero when there is no video.</summary>
    public int Width { get; set; }

    /// <summary>The video track's coded height, in pixels, or zero when there is no video.</summary>
    public int Height { get; set; }

    /// <summary>The video track's frame rate, in frames per second, or zero when it is not stated.</summary>
    public double FrameRate { get; set; }

    /// <summary>The video codec's name, as the prober reports it, or an empty string.</summary>
    public string VideoCodec { get; set; } = string.Empty;

    /// <summary>The audio codec's name, as the prober reports it, or an empty string.</summary>
    public string AudioCodec { get; set; } = string.Empty;

    /// <summary>The audio track's channel count, or zero when there is no audio.</summary>
    public int AudioChannels { get; set; }

    /// <summary>The audio track's sample rate in hertz, or zero when there is no audio.</summary>
    public int AudioSampleRateHz { get; set; }

    /// <summary>How many caption tracks the file carries.</summary>
    public int CaptionTrackCount { get; set; }

    /// <summary>How many chapters the file carries.</summary>
    public int ChapterCount { get; set; }

    /// <summary>The file's size on disk, in bytes.</summary>
    public long SizeInBytes { get; set; }

    /// <summary>Anything the prober or the container reader wanted to say about the file.</summary>
    public IReadOnlyList<string> Notices { get; set; } = [];

    /// <summary>True when the file carries a video track this application can work with.</summary>
    public bool HasVideo => Width > 0 && Height > 0;

    /// <summary>True when the file carries an audio track.</summary>
    public bool HasAudio => AudioChannels > 0;

    /// <summary>True when the in-application player can open this file.</summary>
    public bool IsPlayable => MediaFormats.IsPlayable(Format);

    /// <summary>A very short name for the format, for a badge in a list.</summary>
    public string FormatBadge => MediaFormats.ShortName(Format);

    /// <summary>The one-line summary, as a bindable property.</summary>
    public string Summary => ToString();

    /// <summary>The file's size on disk, written the way a person reads it.</summary>
    public string SizeText => SizeInBytes >= 1024L * 1024L
        ? $"{SizeInBytes / (1024d * 1024d):F1} MB"
        : $"{SizeInBytes / 1024d:F0} KB";

    /// <summary>A one-line summary for the status bar.</summary>
    /// <returns>Format, resolution, duration and track counts.</returns>
    public override string ToString()
    {
        var resolution = HasVideo ? $"{Width}x{Height}" : "no video";
        var audio = HasAudio ? $"{AudioCodec} {AudioChannels}ch" : "no audio";
        return $"{MediaFormats.DisplayName(Format)} - {resolution}, {Duration:hh\\:mm\\:ss}, {audio}";
    }
}
