using CodeBrixVideoTool.Processing.Formats;
using CodeBrixVideoTool.Processing.Probing;
using CodeBrixVideoTool.Processing.Resolution;
using System.Collections.Generic;

namespace CodeBrixVideoTool.Processing.Planning;

/// <summary>
/// One settled conversion: what is being converted, into what, at what size, with which codecs, and
/// by which route.
/// </summary>
public sealed class ConversionPlan
{
    /// <summary>Creates the plan. Use <see cref="ConversionPlanner" /> rather than this directly.</summary>
    /// <param name="source">What probing found in the file being converted.</param>
    /// <param name="destination">The format being written.</param>
    /// <param name="outputPath">Where the result goes.</param>
    /// <param name="resolution">The rung of the resolution ladder that was chosen.</param>
    /// <param name="quality">The quality stop that was chosen.</param>
    /// <param name="operation">What the conversion is called.</param>
    /// <param name="steps">The route, in a sentence per step.</param>
    public ConversionPlan(
        SourceMediaInfo source,
        MediaFormatKind destination,
        string outputPath,
        ResolutionOption resolution,
        QualityLevel quality,
        ConversionOperationKind operation,
        IReadOnlyList<string> steps)
    {
        Source = source;
        Destination = destination;
        OutputPath = outputPath;
        Resolution = resolution;
        Quality = quality;
        Operation = operation;
        Steps = steps ?? [];
    }

    /// <summary>What probing found in the file being converted.</summary>
    public SourceMediaInfo Source { get; }

    /// <summary>The format being written.</summary>
    public MediaFormatKind Destination { get; }

    /// <summary>Where the result goes.</summary>
    public string OutputPath { get; }

    /// <summary>The rung of the resolution ladder that was chosen.</summary>
    public ResolutionOption Resolution { get; }

    /// <summary>
    /// The quality stop that was chosen. It settles the encoder's constant rate factor and nothing
    /// else - see <see cref="Operations.ConversionRunner" />.
    /// </summary>
    public QualityLevel Quality { get; }

    /// <summary>What the conversion is called: Import, Transcode or Export.</summary>
    public ConversionOperationKind Operation { get; }

    /// <summary>The route, in a sentence per step, for the status bar and the run notes.</summary>
    public IReadOnlyList<string> Steps { get; }

    /// <summary>The audio codec the destination is written with, chosen from the destination alone.</summary>
    public TargetAudioCodec AudioCodec => MediaFormats.AudioCodecFor(Destination);

    /// <summary>
    /// How many audio channels the destination is written with: the source's own count, capped at what
    /// this application writes to that destination. See <see cref="MediaFormats.AudioChannelsFor" />.
    /// </summary>
    public int AudioChannels => MediaFormats.AudioChannelsFor(Destination, Source.AudioChannels);

    /// <summary>True when the destination carries fewer audio channels than the source does.</summary>
    public bool DownmixesAudio => Source.HasAudio && AudioChannels < Source.AudioChannels;

    /// <summary>The video codec the destination is written with, chosen from the destination alone.</summary>
    public TargetVideoCodec VideoCodec => MediaFormats.VideoCodecFor(Destination);

    /// <summary>
    /// True when the source is a Mode 2 file, which FFmpeg cannot open and which therefore has to be
    /// demultiplexed and re-wrapped before anything else can happen.
    /// </summary>
    public bool RequiresMode2Extraction => Source.Format == MediaFormatKind.CodeBrixMode2;

    /// <summary>True when the size is being reduced rather than kept.</summary>
    public bool IsResized => Resolution is { IsOriginal: false };

    /// <summary>The verb for the action button: "Import", "Transcode" or "Export".</summary>
    public string ActionVerb => MediaFormats.ActionVerb(Operation);

    /// <summary>A one-line summary for the status bar.</summary>
    /// <returns>What is about to happen.</returns>
    public override string ToString() =>
        $"{ActionVerb} {Source.FileName} to {MediaFormats.DisplayName(Destination)} at {Resolution.Width}x{Resolution.Height}";
}
