using System.Collections.Generic;

namespace CodeBrixVideoTool.Processing.Containers;

/// <summary>
/// What came out of a Mode 2 file: its elementary streams re-wrapped into containers FFmpeg reads,
/// the one intermediate file those two were muxed into, and its chapters and captions.
/// </summary>
public sealed class Mode2Extraction
{
    /// <summary>Creates the record.</summary>
    /// <param name="intermediatePath">The Matroska file the streams were muxed into.</param>
    /// <param name="videoIvfPath">The AV1 elementary stream, wrapped in IVF.</param>
    /// <param name="audioOggPath">The audio stream, wrapped in Ogg, or null when there is no audio.</param>
    /// <param name="audioCodecId">The audio track's codec id, or an empty string.</param>
    /// <param name="videoFrameCount">How many coded video frames were carried across.</param>
    /// <param name="audioPacketCount">How many coded audio packets were carried across.</param>
    /// <param name="sidecars">The chapters and captions taken out of the file's header.</param>
    public Mode2Extraction(
        string intermediatePath,
        string videoIvfPath,
        string audioOggPath,
        string audioCodecId,
        int videoFrameCount,
        int audioPacketCount,
        MediaSidecars sidecars)
    {
        IntermediatePath = intermediatePath;
        VideoIvfPath = videoIvfPath;
        AudioOggPath = audioOggPath;
        AudioCodecId = audioCodecId ?? string.Empty;
        VideoFrameCount = videoFrameCount;
        AudioPacketCount = audioPacketCount;
        Sidecars = sidecars ?? MediaSidecars.None;
    }

    /// <summary>
    /// The Matroska file the elementary streams were muxed into with no re-encoding. This is the
    /// file the rest of the pipeline treats as the source, so a Mode 2 conversion is an ordinary
    /// conversion from that point on.
    /// </summary>
    public string IntermediatePath { get; }

    /// <summary>The AV1 elementary stream, wrapped in IVF.</summary>
    public string VideoIvfPath { get; }

    /// <summary>The audio stream, wrapped in Ogg, or null when the file carried no audio.</summary>
    public string AudioOggPath { get; }

    /// <summary>The audio track's codec id - "vorbis" for a file this application wrote.</summary>
    public string AudioCodecId { get; }

    /// <summary>How many coded video frames were carried across.</summary>
    public int VideoFrameCount { get; }

    /// <summary>How many coded audio packets were carried across.</summary>
    public int AudioPacketCount { get; }

    /// <summary>The chapters and captions taken out of the file's header.</summary>
    public MediaSidecars Sidecars { get; }

    /// <summary>True when an audio stream was carried across.</summary>
    public bool HasAudio => !string.IsNullOrEmpty(AudioOggPath);

    /// <summary>Anything worth saying about the extraction, for the run notes.</summary>
    public IReadOnlyList<string> Notes => Sidecars.Notes;

    /// <summary>A one-line summary for the run notes.</summary>
    /// <returns>What was carried across.</returns>
    public override string ToString() =>
        $"{VideoFrameCount} video frame(s), {AudioPacketCount} {AudioCodecId} packet(s), {Sidecars}";
}
