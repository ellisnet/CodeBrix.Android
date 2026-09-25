using CodeBrix.VideoPlayback.Captions;
using CodeBrix.VideoPlayback.Chapters;
using CodeBrixVideoTool.Playback.Models;
using CodeBrixVideoTool.Processing.Formats;
using CodeBrixVideoTool.Processing.Probing;
using System;
using System.Collections.Generic;

namespace CodeBrixVideoTool.Playback.Services;

/// <summary>
/// Every decision the player half of the application makes, with none of the machinery a view model
/// needs to make them observable.
/// </summary>
/// <remarks>
/// The rules live here rather than inside the view model because a view model derived from the
/// platform's SimpleViewModel cannot be constructed without a running application host, and rules
/// that cannot be tested are rules that quietly stop being true. The view model is a thin observable
/// wrapper over this.
/// </remarks>
public static class PlaybackSelection
{
    /// <summary>Whether the in-application player can open a file at all.</summary>
    /// <param name="item">What probing found in the file, or null.</param>
    /// <returns>True when the player can open it.</returns>
    public static bool CanOpen(SourceMediaInfo item) =>
        item is not null && MediaFormats.IsPlayable(item.Format);

    /// <summary>
    /// Why a file is not being played, in a sentence that says what to do about it instead of only
    /// what went wrong.
    /// </summary>
    /// <param name="item">What probing found in the file.</param>
    /// <returns>The sentence to show.</returns>
    /// <exception cref="ArgumentNullException">The item is null.</exception>
    public static string DescribeUnplayable(SourceMediaInfo item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return $"{MediaFormats.DisplayName(item.Format)} is not played in this application - " +
               "import it to one of the four CodeBrix formats first.";
    }

    /// <summary>Builds the rows of the chapter drop-down.</summary>
    /// <param name="chapters">The chapters the open file carries, or null.</param>
    /// <returns>One row per chapter, in the file's own order.</returns>
    public static IReadOnlyList<ChapterEntry> BuildChapterRows(IReadOnlyList<Chapter> chapters)
    {
        if (chapters is not { Count: > 0 })
        {
            return [];
        }

        var rows = new List<ChapterEntry>(chapters.Count);
        foreach (var chapter in chapters)
        {
            rows.Add(ChapterEntry.From(chapter));
        }

        return rows;
    }

    /// <summary>
    /// Builds the rows of the caption drop-down: an "off" row first, then one row per track. The
    /// "off" row is always there, so a person can always turn captions off again.
    /// </summary>
    /// <param name="tracks">The caption tracks the open file carries, or null.</param>
    /// <returns>The rows, always at least one.</returns>
    public static IReadOnlyList<CaptionEntry> BuildCaptionRows(IReadOnlyList<CaptionTrack> tracks)
    {
        var rows = new List<CaptionEntry> { CaptionEntry.Off };
        if (tracks is null)
        {
            return rows;
        }

        for (var index = 0; index < tracks.Count; index++)
        {
            rows.Add(CaptionEntry.From(index, tracks[index]));
        }

        return rows;
    }

    /// <summary>Whether the chapter drop-down is worth showing.</summary>
    /// <param name="chapterRowCount">How many rows <see cref="BuildChapterRows" /> produced.</param>
    /// <returns>True when there is at least one chapter.</returns>
    public static bool ShouldShowChapters(int chapterRowCount) => chapterRowCount > 0;

    /// <summary>Whether the caption drop-down is worth showing.</summary>
    /// <param name="captionRowCount">How many rows <see cref="BuildCaptionRows" /> produced.</param>
    /// <returns>True when there is at least one track behind the "off" row.</returns>
    public static bool ShouldShowCaptions(int captionRowCount) => captionRowCount > 1;

    /// <summary>The line the status bar shows once a file is open.</summary>
    /// <param name="fileName">The open file's name.</param>
    /// <param name="duration">How long it runs.</param>
    /// <param name="chapterRowCount">How many chapter rows there are.</param>
    /// <param name="captionRowCount">How many caption rows there are, counting the "off" row.</param>
    /// <returns>The line to show.</returns>
    public static string DescribeOpened(
        string fileName, TimeSpan duration, int chapterRowCount, int captionRowCount)
    {
        var chapters = ShouldShowChapters(chapterRowCount) ? $", {chapterRowCount} chapters" : string.Empty;
        var captions = ShouldShowCaptions(captionRowCount)
            ? $", {captionRowCount - 1} caption track(s)"
            : string.Empty;

        return $"{fileName} - {duration:hh\\:mm\\:ss}{chapters}{captions}.";
    }
}
