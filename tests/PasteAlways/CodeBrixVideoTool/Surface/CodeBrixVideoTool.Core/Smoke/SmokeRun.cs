using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CodeBrixVideoTool.Playback.Services;
using CodeBrixVideoTool.Processing.Formats;
using CodeBrixVideoTool.Processing.Operations;
using CodeBrixVideoTool.Processing.Samples;
using CodeBrixVideoTool.Processing.ViewModels;
using CodeBrixVideoTool.ViewModels;

namespace CodeBrixVideoTool.Smoke;

/// <summary>
/// A scripted verification run: it generates a source clip, drives the view models' own commands and
/// properties through a conversion, an export and a spell of playback, and prints one
/// <c>CBVT-SMOKE:</c> line per fact and per check before exiting with 0 or 1. It is not application
/// behavior and nothing starts it unless <c>CODEBRIXVIDEOTOOL_SMOKE</c> is set.
/// </summary>
/// <remarks>
/// The run owns the sequence of checks and the reporting format. The few things it can only learn by
/// looking at the screen come from <see cref="ISmokeSurface" />, which the hosting page implements
/// over its own controls.
/// </remarks>
public sealed class SmokeRun
{
    private readonly SmokeOptions options;
    private readonly ISmokeSurface surface;
    private int failures;

    /// <summary>Creates a run against one page's controls.</summary>
    /// <param name="options">What the run was asked for.</param>
    /// <param name="surface">The page, behind the little it is asked for.</param>
    /// <exception cref="ArgumentNullException">Either argument is null.</exception>
    public SmokeRun(SmokeOptions options, ISmokeSurface surface)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.surface = surface ?? throw new ArgumentNullException(nameof(surface));
    }

    /// <summary>
    /// Says a run ended in something nobody expected, in the run's own reporting format, and exits.
    /// </summary>
    /// <param name="exception">What went wrong.</param>
    public static void ReportFailure(Exception exception)
    {
        Console.WriteLine($"CBVT-SMOKE: RESULT FAIL (exception: {exception?.Message})");
        Console.Out.Flush();
        Environment.Exit(1);
    }

    /// <summary>
    /// Runs the whole script against one live view model and does not return: every exit from here
    /// ends the process with 0 for a run in which everything passed and 1 for one in which anything
    /// did not.
    /// </summary>
    /// <param name="viewModel">The application's own view model, as the page's data context.</param>
    /// <returns>A task that only completes if the process somehow outlives the run.</returns>
    public async Task RunAsync(MainViewModel viewModel)
    {
        try
        {
            Check("view-model-ready", viewModel is not null, viewModel is null ? "no data context" : "ready");
            if (failures > 0)
            {
                Finish();
                return;
            }

            Directory.CreateDirectory(options.WorkFolder);
            Fact("destination", options.Destination);
            Fact("workFolder", options.WorkFolder);

            var sourcePath = await SampleClipFactory
                .WriteRichMp4Async(options.WorkFolder, TimeSpan.FromSeconds(3));
            Fact("source", sourcePath);

            var info = await viewModel.AddAsync(sourcePath, CancellationToken.None);
            Check("source-probed", info is not null, info?.Summary ?? viewModel.StatusText);
            if (failures > 0)
            {
                Finish();
                return;
            }

            Fact("sourceFormat", info.Format);
            Fact("sourceCaptionTracks", info.CaptionTrackCount);
            Fact("sourceChapters", info.ChapterCount);
            Check("mp4-is-not-playable-here", !info.IsPlayable, "the player decodes AV1 only");

            //Quality is Good until a person says otherwise, which is what every conversion was
            //written at before there was a choice.
            Check("quality-defaults-to-good",
                viewModel.Conversion.SelectedQuality == QualityLevel.Good,
                viewModel.Conversion.SelectedQuality.ToString());

            //A stop that is NOT the default, so what follows proves the knob reaches the encoder.
            viewModel.Conversion.SelectedQuality = QualityLevel.Better;
            Fact("quality", viewModel.Conversion.SelectedQuality);
            Check("the-quality-drop-down-offers-the-four-stops",
                surface.QualityChoiceCount == MediaFormats.QualityLevels.Count &&
                Equals(surface.SelectedQualityChoice, QualityLevel.Better),
                $"{surface.QualityChoiceCount} stop(s), showing {surface.SelectedQualityChoice}");

            var destination = viewModel.Conversion.Destinations.FirstOrDefault(d => d.Kind == options.Destination);
            Check("destination-offered", destination is not null, options.Destination.ToString());
            if (failures > 0)
            {
                Finish();
                return;
            }

            viewModel.Conversion.SelectedDestination = destination;
            Fact("action", viewModel.Conversion.ActionLabel);
            Fact("resolutions", viewModel.Conversion.Resolutions.Count);

            var outputPath = Path.Combine(
                options.WorkFolder, "smoke" + MediaFormats.Extension(options.Destination));
            SetOutputPath(viewModel.Conversion, outputPath);

            var outcome = await RunConversionAsync(viewModel.Conversion);

            Check("conversion-succeeded", outcome.Succeeded, outcome.Failure ?? outcome.ToString());
            if (failures > 0)
            {
                Finish();
                return;
            }

            Fact("output", outcome.OutputPath);
            Fact("outputBytes", outcome.SizeInBytes);
            Fact("profileVerdict", outcome.ProfileVerdict ?? "(not checked)");
            Fact("elapsedSeconds", outcome.Elapsed.TotalSeconds.ToString("F1"));

            //A standard MKV is written with its cues at the end, so it is EXPECTED not to pass. It is
            //checked and reported on all the same, and that failure is not an error.
            var profileShouldPass = options.Destination != MediaFormatKind.Matroska;
            Check("streamable-profile-is-as-expected", outcome.PassesProfile == profileShouldPass,
                (profileShouldPass ? "expected to pass: " : "a standard MKV is expected not to pass: ") +
                (outcome.ProfileVerdict ?? "(not checked)"));

            Check("quality-reached-the-encoder",
                outcome.Commands.Any(command => command.Contains("-crf 24", StringComparison.Ordinal)),
                "AV1 rate factor 24 is Better");

            CheckLastRunNotes(viewModel, outcome, options.Destination);

            //The library add and the player open both happen off the conversion-finished event, so
            //give them a moment to land before asking the player anything.
            for (var attempt = 0; attempt < 100 && surface.PlayerDurationSeconds <= 0; attempt++)
            {
                await Task.Delay(100);
            }

            var produced = viewModel.SelectedItem;
            Check("output-added-to-the-list", produced is not null && produced.Path == outcome.OutputPath,
                produced?.FileName ?? "(nothing selected)");

            if (produced is not null)
            {
                Fact("outputFormat", produced.Format);
                Fact("outputAudioCodec", produced.AudioCodec);
                Fact("outputVideoCodec", produced.VideoCodec);

                if (options.Destination == MediaFormatKind.CodeBrixMode2)
                {
                    Check("mode2-audio-is-vorbis", produced.AudioCodec == "vorbis", produced.AudioCodec);
                }
            }

            Check("player-opened", surface.PlayerDurationSeconds > 0,
                $"DurationSeconds={surface.PlayerDurationSeconds:F2}");

            Fact("chapters", surface.PlayerChapterCount);
            Fact("captionTracks", surface.PlayerCaptionTrackCount);
            Check("chapters-survived", surface.PlayerChapterCount == info.ChapterCount,
                $"{surface.PlayerChapterCount} of {info.ChapterCount}");
            Check("captions-survived", surface.PlayerCaptionTrackCount == info.CaptionTrackCount,
                $"{surface.PlayerCaptionTrackCount} of {info.CaptionTrackCount}");

            var startPosition = surface.PlayerPositionSeconds;
            viewModel.Playback.PlayCommand.Execute(null);
            await Task.Delay(3000);
            var endPosition = surface.PlayerPositionSeconds;

            Fact("positionAtStart", startPosition.ToString("F2"));
            Fact("positionAtEnd", endPosition.ToString("F2"));
            Check("position-advances", endPosition - startPosition > 1.0,
                $"advanced {endPosition - startPosition:F2} s in 3 s");

            var frames = surface.FrameCounts;
            Fact("framesPosted", frames.Posted);
            Fact("framesPresented", frames.Presented);
            Fact("framesDropped", frames.Dropped);
            Check("frames-were-presented", frames.Presented > 0, frames.Presented.ToString());

            viewModel.Playback.StopCommand.Execute(null);

            //An exported .mp4 joins the list like every other output: dimmed, refused by the player,
            //and still a conversion source in its own right.
            await RunMp4ExportAsync(viewModel);

            //Leaves the window up with everything loaded, for a scripted run that wants to look at
            //it rather than only read what it printed.
            if (options.HoldSeconds > 0)
            {
                Fact("holdingSeconds", options.HoldSeconds);
                if (viewModel.Playback.PlayCommand.CanExecute(null))
                {
                    viewModel.Playback.PlayCommand.Execute(null);
                }

                await Task.Delay(options.HoldSeconds * 1000);
            }

            if (!options.KeepFiles)
            {
                viewModel.Playback.Close();
                await Task.Delay(200);
                TryDeleteFolder(options.WorkFolder);
            }

            Finish();
        }
        catch (Exception exception)
        {
            ReportFailure(exception);
        }
    }

    /// <summary>
    /// Exports whatever the run has just produced to an <c>.mp4</c> and proves that it joins the file
    /// list as a dimmed, unplayable, still-selectable row.
    /// </summary>
    private async Task RunMp4ExportAsync(MainViewModel viewModel)
    {
        var export = viewModel.Conversion.Destinations.FirstOrDefault(d => d.Kind == MediaFormatKind.Mp4);
        Check("mp4-export-is-offered", export is not null, "Export to MP4");
        if (export is null)
        {
            return;
        }

        var exportPath = Path.Combine(options.WorkFolder, "smoke-export.mp4");
        viewModel.Conversion.SelectedDestination = export;
        SetOutputPath(viewModel.Conversion, exportPath);
        Fact("exportAction", viewModel.Conversion.ActionLabel);

        var outcome = await RunConversionAsync(viewModel.Conversion);

        Check("mp4-export-succeeded", outcome.Succeeded, outcome.Failure ?? outcome.ToString());
        if (!outcome.Succeeded)
        {
            return;
        }

        Fact("exportOutput", outcome.OutputPath);
        Fact("exportBytes", outcome.SizeInBytes);
        Check("export-quality-reached-the-encoder",
            outcome.Commands.Any(command => command.Contains("-crf 17", StringComparison.Ordinal)),
            "H.264 rate factor 17 is Better");

        //The list add happens off the conversion-finished event, so give it a moment to land.
        for (var attempt = 0; attempt < 100 &&
             !string.Equals(viewModel.SelectedItem?.Path, exportPath, StringComparison.Ordinal); attempt++)
        {
            await Task.Delay(50);
        }

        var exported = viewModel.SelectedItem;
        Check("export-joins-the-file-list",
            viewModel.Library.Any(item => string.Equals(item.Path, exportPath, StringComparison.Ordinal)),
            $"{viewModel.Library.Count} file(s) listed");
        Check("export-is-the-selected-file",
            exported is not null && string.Equals(exported.Path, exportPath, StringComparison.Ordinal),
            exported?.FileName ?? "(nothing selected)");
        Check("export-is-listed-as-not-playable",
            exported is { IsPlayable: false }, exported?.Format.ToString() ?? "(nothing selected)");
        Check("export-is-still-a-conversion-source",
            viewModel.Conversion.Destinations.Count > 0,
            $"{viewModel.Conversion.Destinations.Count} destination(s) offered");
        Check("the-player-refuses-the-export",
            exported is not null &&
            string.Equals(viewModel.Playback.StatusText, PlaybackSelection.DescribeUnplayable(exported), StringComparison.Ordinal),
            viewModel.Playback.StatusText);
        Check("export-status-is-the-outcome-itself",
            string.Equals(viewModel.StatusText, outcome.ToString(), StringComparison.Ordinal),
            viewModel.StatusText);

        //The dimming is a property of the row itself, not only of the converter behind it: the row the
        //list built for the file it cannot play is shown fainter than the row beside it.
        surface.LayOutLibraryList();
        var dimmedRow = surface.ShownRowOpacity(exported);
        var fullRow = surface.ShownRowOpacity(viewModel.Library.FirstOrDefault(item => item.IsPlayable));
        Check("the-exported-row-is-shown-dimmed",
            dimmedRow is { } dim && fullRow is { } full && dim > 0d && dim < full,
            $"{Describe(dimmedRow)} against {Describe(fullRow)}");
    }

    /// <summary>
    /// Proves the operation panel is showing the last run's own notes: the streamable-profile verdict
    /// first when there was one, then whatever the run had to say, line for line.
    /// </summary>
    private void CheckLastRunNotes(
        MainViewModel viewModel, ConversionOutcome outcome, MediaFormatKind destination)
    {
        var shown = viewModel.Conversion.LastRunNotes;
        Fact("lastRunNoteCount", shown.Count);
        foreach (var line in shown)
        {
            Fact("lastRunNote", line);
        }

        Check("the-last-run-left-notes-on-screen", shown.Count > 0, $"{shown.Count} line(s)");

        var expected = ConversionViewModel.DescribeOutcome(outcome, destination);
        Check("the-notes-on-screen-match-the-outcome",
            shown.Count == expected.Count && shown.SequenceEqual(expected, StringComparer.Ordinal),
            $"{shown.Count} shown, {expected.Count} expected");

        //A standard MKV is expected to fail the profile and says so in the same breath; everything
        //else this application writes passes.
        var head = destination == MediaFormatKind.Matroska
            ? "Streamable profile: FAIL"
            : "Streamable profile: PASS";
        var mkvIsExcused = destination != MediaFormatKind.Matroska ||
                           (shown.Count > 0 && shown[0].EndsWith("(expected for a standard MKV)", StringComparison.Ordinal));

        Check("the-profile-verdict-heads-the-notes",
            shown.Count > 0 && shown[0].StartsWith(head, StringComparison.Ordinal) && mkvIsExcused,
            shown.Count > 0 ? shown[0] : "(nothing shown)");

        //And the panel under the status bar is really showing them, a line at a time.
        Check("the-notes-panel-under-the-status-bar-shows-them",
            surface.NotesPanelIsShown && surface.NotesPanelLineCount == shown.Count,
            $"{(surface.NotesPanelIsShown ? "shown" : "hidden")}, {surface.NotesPanelLineCount} line(s)");
    }

    /// <summary>
    /// Presses the action button and waits for the run to report itself finished. The outcome arrives
    /// on an event rather than from the command, so the wait is a completion source subscribed before
    /// the command is executed.
    /// </summary>
    private static async Task<ConversionOutcome> RunConversionAsync(ConversionViewModel conversion)
    {
        var finished = new TaskCompletionSource<ConversionOutcome>();
        void OnFinished(object _, ConversionOutcome result) => finished.TrySetResult(result);
        conversion.ConversionFinished += OnFinished;
        conversion.RunCommand.Execute(null);
        var outcome = await finished.Task;
        conversion.ConversionFinished -= OnFinished;
        return outcome;
    }

    /// <summary>
    /// Puts a fixed destination in place of the save dialog, through the bridge interface the
    /// conversion view model implements. It is the only dialog in the way of an unattended run.
    /// </summary>
    private static void SetOutputPath(IOutputPathBridge bridge, string path) =>
        bridge.PickOutputPathAsync = (_, _) => Task.FromResult(path);

    private static void Fact(string name, object value) =>
        Console.WriteLine($"CBVT-SMOKE: {name}={value?.ToString() ?? "(null)"}");

    private void Check(string step, bool ok, string detail)
    {
        Console.WriteLine($"CBVT-SMOKE: {(ok ? "PASS" : "FAIL")} {step} ({detail})");
        if (!ok)
        {
            failures++;
        }
    }

    private void Finish()
    {
        Console.WriteLine($"CBVT-SMOKE: RESULT {(failures == 0 ? "PASS" : $"FAIL ({failures})")}");
        Console.Out.Flush();
        Environment.Exit(failures == 0 ? 0 : 1);
    }

    private static string Describe(double? opacity) =>
        opacity?.ToString("F2", CultureInfo.InvariantCulture) ?? "(unreadable)";

    private static void TryDeleteFolder(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
        catch (IOException)
        {
            //A temporary folder that will not delete is not worth failing a scripted run over.
        }
    }
}
