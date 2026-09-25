using CodeBrix.VideoPlayback.Chapters;
using System;

namespace CodeBrixVideoTool.Playback.Models;

/// <summary>One row of the chapter drop-down.</summary>
public sealed class ChapterEntry
{
    /// <summary>Creates the row.</summary>
    /// <param name="index">The chapter's position in the file, from zero.</param>
    /// <param name="start">Where the chapter begins.</param>
    /// <param name="title">The chapter's title.</param>
    public ChapterEntry(int index, TimeSpan start, string title)
    {
        Index = index;
        Start = start;
        Title = title;
    }

    /// <summary>Builds a row from a chapter the player reported.</summary>
    /// <param name="chapter">The chapter.</param>
    /// <returns>The row to show.</returns>
    /// <exception cref="ArgumentNullException">The chapter is null.</exception>
    public static ChapterEntry From(Chapter chapter)
    {
        ArgumentNullException.ThrowIfNull(chapter);
        var title = string.IsNullOrWhiteSpace(chapter.Title) ? $"Chapter {chapter.Index + 1}" : chapter.Title;
        return new ChapterEntry(chapter.Index, chapter.Start, title);
    }

    /// <summary>The chapter's position in the file, from zero.</summary>
    public int Index { get; }

    /// <summary>Where the chapter begins.</summary>
    public TimeSpan Start { get; }

    /// <summary>The chapter's title.</summary>
    public string Title { get; }

    /// <summary>What the drop-down shows: the start time and the title.</summary>
    public string Label => $"{Start:mm\\:ss}  {Title}";

    /// <summary>Returns the label.</summary>
    /// <returns>The label.</returns>
    public override string ToString() => Label;
}
