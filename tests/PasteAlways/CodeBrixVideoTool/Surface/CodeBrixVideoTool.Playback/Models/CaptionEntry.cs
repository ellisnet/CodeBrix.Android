using CodeBrix.VideoPlayback.Captions;
using System;

namespace CodeBrixVideoTool.Playback.Models;

/// <summary>One row of the caption drop-down, including the "off" row.</summary>
public sealed class CaptionEntry
{
    private CaptionEntry(int index, string label, CaptionTrack track)
    {
        Index = index;
        Label = label;
        Track = track;
    }

    /// <summary>The row that turns captions off.</summary>
    public static CaptionEntry Off { get; } = new(-1, "Captions off", null);

    /// <summary>Builds a row from a caption track the player reported.</summary>
    /// <param name="index">The track's position in the file, from zero.</param>
    /// <param name="track">The track.</param>
    /// <returns>The row to show.</returns>
    /// <exception cref="ArgumentNullException">The track is null.</exception>
    public static CaptionEntry From(int index, CaptionTrack track)
    {
        ArgumentNullException.ThrowIfNull(track);

        var name = string.IsNullOrWhiteSpace(track.Name) ? null : track.Name;
        var language = string.IsNullOrWhiteSpace(track.Language) ? null : track.Language;
        var label = name ?? language ?? $"Captions {index + 1}";

        if (name is not null && language is not null)
        {
            label = $"{name} ({language})";
        }

        if (track.IsForced)
        {
            label += " - forced";
        }

        return new CaptionEntry(index, label, track);
    }

    /// <summary>The track's position in the file, or -1 for the "off" row.</summary>
    public int Index { get; }

    /// <summary>What the drop-down shows.</summary>
    public string Label { get; }

    /// <summary>The track this row selects, or null for the "off" row.</summary>
    public CaptionTrack Track { get; }

    /// <summary>True for the row that turns captions off.</summary>
    public bool IsOff => Track is null;

    /// <summary>Returns the label.</summary>
    /// <returns>The label.</returns>
    public override string ToString() => Label;
}
