namespace CodeBrixVideoTool.Smoke;

/// <summary>
/// The few things a scripted run can only learn by looking at the screen: what the player element
/// itself says, what the two panels are showing, and the opacity a file's row is really being drawn
/// at. The page implements this over its own controls; everything else a run does it does through
/// the view models' own commands and properties.
/// </summary>
public interface ISmokeSurface
{
    /// <summary>How many stops the quality drop-down is offering.</summary>
    int QualityChoiceCount { get; }

    /// <summary>The stop the quality drop-down is showing as chosen.</summary>
    object SelectedQualityChoice { get; }

    /// <summary>Whether the last-run notes panel under the status bar is on screen.</summary>
    bool NotesPanelIsShown { get; }

    /// <summary>How many lines the last-run notes panel is showing.</summary>
    int NotesPanelLineCount { get; }

    /// <summary>How long the player element says the open file is, in seconds.</summary>
    double PlayerDurationSeconds { get; }

    /// <summary>Where the player element says it is in the open file, in seconds.</summary>
    double PlayerPositionSeconds { get; }

    /// <summary>How many chapters the player element read out of the open file.</summary>
    int PlayerChapterCount { get; }

    /// <summary>How many caption tracks the player element read out of the open file.</summary>
    int PlayerCaptionTrackCount { get; }

    /// <summary>
    /// The player element's frame counters, read together so the three of them belong to one moment.
    /// </summary>
    SmokeFrameCounts FrameCounts { get; }

    /// <summary>
    /// Lays the file list out, so a row that was only just added has a container to read.
    /// </summary>
    void LayOutLibraryList();

    /// <summary>The opacity one file's row is really being drawn at.</summary>
    /// <param name="item">The file whose row to read.</param>
    /// <returns>The row's opacity, or null when the list has not built a row for that file.</returns>
    double? ShownRowOpacity(object item);
}
