using System;

namespace CodeBrixVideoTool.Processing.Operations;

/// <summary>How far a conversion has got.</summary>
/// <remarks>
/// Every stage that runs FFmpeg reports a real percentage, because FFmpeg says where in the media it
/// has reached and the source's duration is known. A stage that does not run FFmpeg - reading a
/// bespoke container, muxing an intermediate - reports no percentage at all, and the progress bar
/// shows that it is working rather than inventing a number.
/// </remarks>
public sealed class ConversionProgress
{
    /// <summary>Creates the report.</summary>
    /// <param name="stage">What is happening now, in a few words.</param>
    /// <param name="stageNumber">Which stage this is, from one.</param>
    /// <param name="stageCount">How many stages the conversion has.</param>
    /// <param name="stagePercent">How far through this stage, from 0 to 100, or null when it cannot be known.</param>
    public ConversionProgress(string stage, int stageNumber, int stageCount, double? stagePercent)
    {
        Stage = stage;
        StageNumber = stageNumber;
        StageCount = stageCount < 1 ? 1 : stageCount;
        StagePercent = stagePercent;
    }

    /// <summary>What is happening now, in a few words.</summary>
    public string Stage { get; }

    /// <summary>Which stage this is, from one.</summary>
    public int StageNumber { get; }

    /// <summary>How many stages the conversion has.</summary>
    public int StageCount { get; }

    /// <summary>How far through this stage, from 0 to 100, or null when it cannot be known.</summary>
    public double? StagePercent { get; }

    /// <summary>True when no percentage can be given and the bar should show activity instead.</summary>
    public bool IsIndeterminate => StagePercent is null;

    /// <summary>How far through the whole conversion, from 0 to 100.</summary>
    /// <remarks>
    /// A stage with no percentage of its own counts as half-done, so the bar still moves forward
    /// when one finishes rather than sitting still until the last stage starts.
    /// </remarks>
    public double OverallPercent
    {
        get
        {
            var within = Math.Clamp(StagePercent ?? 50d, 0d, 100d);
            var completed = Math.Max(0, StageNumber - 1);
            return Math.Clamp(((completed * 100d) + within) / StageCount, 0d, 100d);
        }
    }

    /// <summary>A line for the status bar.</summary>
    /// <returns>The stage, its number, and its percentage when there is one.</returns>
    public override string ToString() => StagePercent is null
        ? $"{Stage} ({StageNumber} of {StageCount})"
        : $"{Stage} ({StageNumber} of {StageCount}) - {StagePercent:F0}%";
}
