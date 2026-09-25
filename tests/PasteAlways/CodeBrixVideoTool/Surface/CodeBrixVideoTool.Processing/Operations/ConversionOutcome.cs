using System;
using System.Collections.Generic;

namespace CodeBrixVideoTool.Processing.Operations;

/// <summary>What a conversion did.</summary>
public sealed class ConversionOutcome
{
    private ConversionOutcome(bool succeeded, bool wasCancelled, string outputPath, long sizeInBytes,
        TimeSpan elapsed, string profileVerdict, bool passesProfile,
        IReadOnlyList<string> notes, IReadOnlyList<string> commands, string failure)
    {
        Succeeded = succeeded;
        WasCancelled = wasCancelled;
        OutputPath = outputPath;
        SizeInBytes = sizeInBytes;
        Elapsed = elapsed;
        ProfileVerdict = profileVerdict;
        PassesProfile = passesProfile;
        Notes = notes ?? [];
        Commands = commands ?? [];
        Failure = failure;
    }

    /// <summary>True when a file was written.</summary>
    public bool Succeeded { get; }

    /// <summary>True when the person asked for it to stop.</summary>
    public bool WasCancelled { get; }

    /// <summary>Where the result went, or null when nothing was written.</summary>
    public string OutputPath { get; }

    /// <summary>How big the result is, in bytes.</summary>
    public long SizeInBytes { get; }

    /// <summary>How long the whole conversion took.</summary>
    public TimeSpan Elapsed { get; }

    /// <summary>
    /// What the streamable-profile check said, or null when the destination has no profile to check.
    /// </summary>
    public string ProfileVerdict { get; }

    /// <summary>True when the result passed the streamable-profile check.</summary>
    public bool PassesProfile { get; }

    /// <summary>Anything worth telling the person about what was and was not carried across.</summary>
    public IReadOnlyList<string> Notes { get; }

    /// <summary>The FFmpeg command lines that were run, for the record.</summary>
    public IReadOnlyList<string> Commands { get; }

    /// <summary>Why it failed, or null when it did not.</summary>
    public string Failure { get; }

    /// <summary>Builds the outcome of a conversion that wrote a file.</summary>
    /// <param name="outputPath">Where the result went.</param>
    /// <param name="sizeInBytes">How big it is.</param>
    /// <param name="elapsed">How long it took.</param>
    /// <param name="profileVerdict">What the profile check said, or null.</param>
    /// <param name="passesProfile">Whether it passed the profile check.</param>
    /// <param name="notes">Anything worth reporting.</param>
    /// <param name="commands">The FFmpeg command lines that were run.</param>
    /// <returns>A successful outcome.</returns>
    public static ConversionOutcome Success(string outputPath, long sizeInBytes, TimeSpan elapsed,
        string profileVerdict, bool passesProfile, IReadOnlyList<string> notes, IReadOnlyList<string> commands) =>
        new(true, false, outputPath, sizeInBytes, elapsed, profileVerdict, passesProfile, notes, commands, null);

    /// <summary>Builds the outcome of a conversion the person stopped.</summary>
    /// <param name="elapsed">How long it ran before it stopped.</param>
    /// <param name="notes">Anything worth reporting.</param>
    /// <returns>A cancelled outcome.</returns>
    public static ConversionOutcome Cancelled(TimeSpan elapsed, IReadOnlyList<string> notes) =>
        new(false, true, null, 0, elapsed, null, false, notes, [], null);

    /// <summary>Builds the outcome of a conversion that failed.</summary>
    /// <param name="failure">Why it failed, in a sentence a person can act on.</param>
    /// <param name="elapsed">How long it ran before it failed.</param>
    /// <param name="notes">Anything worth reporting.</param>
    /// <returns>A failed outcome.</returns>
    public static ConversionOutcome Failed(string failure, TimeSpan elapsed, IReadOnlyList<string> notes) =>
        new(false, false, null, 0, elapsed, null, false, notes, [], failure);

    /// <summary>A line for the status bar.</summary>
    /// <returns>What happened.</returns>
    public override string ToString()
    {
        if (WasCancelled) { return "Cancelled."; }
        if (!Succeeded) { return "Failed: " + Failure; }

        var megabytes = SizeInBytes / (1024d * 1024d);
        return $"Wrote {OutputPath} ({megabytes:F1} MB) in {Elapsed:mm\\:ss}.";
    }
}
