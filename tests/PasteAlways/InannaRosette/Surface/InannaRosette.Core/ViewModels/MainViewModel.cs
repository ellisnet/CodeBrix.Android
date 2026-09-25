using CodeBrix.Platform.Extensions;
using CodeBrix.Platform.Simple;
using InannaRosette.Reading.Data;
using InannaRosette.Reading.Models;
using InannaRosette.Reading.Services;
using InannaRosette.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace InannaRosette.ViewModels;

/// <summary>
/// The whole reading: the deck, the cards waiting in the tray, what sits on each of the nine
/// stations and which way up it reads, who is asking and what they asked, and the
/// interpretation that was written from it. Every decision lives here — what a command is
/// allowed to do, where a card may be laid, what the status strip says. The page owns only the
/// pictures: it is told what changed through <see cref="IReadingTableBridge"/> and it hands
/// back the two things a view model cannot do for itself, file dialogs and dialogs.
/// </summary>
[Microsoft.UI.Xaml.Data.Bindable]
public class MainViewModel : SimpleViewModel, IReadingTableBridge, IReadingFileBridge, IReadingDialogBridge
{
    /// <summary>The nine stations of the rosette: the heart, and eight petals around it.</summary>
    public const int StationCount = 9;

    /// <summary>
    /// What is said when the deck's own goddess comes out of the stack. One constant, so the
    /// blessing is written once and the page is handed it rather than keeping its own copy.
    /// </summary>
    public const string InannaBlessing = "Blessed are You, Queen of Heaven!";

    /// <summary>The card the blessing belongs to: Inanna, the first card of the deck.</summary>
    private const int InannaCardId = 1;

    //The library's default implementations stand in until the container's registrations
    //  replace them in the constructor, so design mode - which returns before that - still has
    //  working objects behind every property and nothing has to null-check them.
    private IReadingInterpreter _interpreter = new ReadingInterpreter();
    private IReadingSerializer _serializer = new ReadingSerializer();
    private IPdfReportBuilder _pdfBuilder = new PdfReportBuilder();
    private ILogger _log = NullLogger.Instance;

    //A fresh Deck is already shuffled; the cards that have left it live in _cards, and
    //  _stations is the same set again, indexed by the station each one was laid on.
    private Deck _deck = new();
    private readonly List<ReadingCard> _cards = new();
    private readonly ReadingCard?[] _stations = new ReadingCard?[StationCount];

    public MainViewModel()
    {
        if (IsDesignMode(true)) { return; } //Leave as the first line of constructor

        //The logger factory App.InitializeLogging() wired into the platform, when there is one
        var loggerFactory = LogExtensionPoint.AmbientLoggerFactory;
        if (loggerFactory != null) { _log = loggerFactory.CreateLogger<MainViewModel>(); }
        _log.LogInformation("Main view model startup.");

        _interpreter = GetService<IReadingInterpreter>() ?? _interpreter;
        _serializer = GetService<IReadingSerializer>() ?? _serializer;
        _pdfBuilder = GetService<IPdfReportBuilder>() ?? _pdfBuilder;

        RefreshCounts();
        RefreshGuidance();
    }

    #region | Bindable properties |

    /// <summary>Who the reading is for; may be left empty.</summary>
    public string Querent
    {
        get;
        set => SetProperty(ref field, value ?? string.Empty);
    } = string.Empty;

    /// <summary>The question put to the rosette; may be left empty.</summary>
    public string Question
    {
        get;
        set => SetProperty(ref field, value ?? string.Empty);
    } = string.Empty;

    /// <summary>The guiding line along the foot of the window.</summary>
    public string StatusText
    {
        get;
        private set => SetProperty(ref field, value ?? string.Empty);
    } = "Shuffle the deck, then draw a card from the stack and lay it on a petal.";

    /// <summary>How many cards are still in the stack.</summary>
    [AffectsCommands(nameof(DrawCommand), nameof(AutoLayCommand))]
    [AffectsProperties(nameof(DeckCountText), nameof(CanDraw), nameof(CanAutoLay))]
    public int DeckRemaining
    {
        get;
        private set => SetProperty(ref field, value);
    }

    /// <summary>How many drawn cards are waiting in the tray.</summary>
    [AffectsCommands(nameof(ClearCommand))]
    [AffectsProperties(nameof(TrayCaption), nameof(CanClear))]
    public int TrayCount
    {
        get;
        private set => SetProperty(ref field, value);
    }

    /// <summary>How many of the nine stations hold a card.</summary>
    [AffectsCommands(nameof(AutoLayCommand), nameof(ClearCommand), nameof(InterpretCommand),
        nameof(SaveReadingCommand))]
    [AffectsProperties(nameof(StationsText), nameof(CanAutoLay), nameof(CanClear), nameof(CanInterpret))]
    public int FilledStations
    {
        get;
        private set => SetProperty(ref field, value);
    }

    /// <summary>True while a command is working; every button waits for it.</summary>
    [AffectsAllCommands]
    [AffectsProperties(nameof(CanDraw), nameof(CanAutoLay), nameof(CanClear),
        nameof(CanInterpret), nameof(CanCreatePdf))]
    public bool IsBusy
    {
        get;
        private set => SetProperty(ref field, value);
    }

    /// <summary>The prose the interpreter wrote, or null while nothing has been read yet.</summary>
    [AffectsProperties(nameof(InterpretationTitle), nameof(InterpretationSubtitle))]
    public ReadingInterpretation? Interpretation
    {
        get;
        private set => SetProperty(ref field, value);
    }

    /// <summary>True once an interpretation exists, whether or not the panel is showing.</summary>
    [AffectsCommands(nameof(CreatePdfCommand))]
    [AffectsProperties(nameof(StaleVisibility), nameof(CanCreatePdf))]
    public bool HasInterpretation
    {
        get;
        private set => SetProperty(ref field, value);
    }

    /// <summary>True once the table changed after an interpretation was produced.</summary>
    [AffectsProperties(nameof(StaleVisibility))]
    public bool IsInterpretationStale
    {
        get;
        private set => SetProperty(ref field, value);
    }

    /// <summary>True while the interpretation panel covers the deck rail.</summary>
    [AffectsProperties(nameof(DeckPanelVisibility), nameof(InterpretationVisibility))]
    public bool ShowInterpretationPanel
    {
        get;
        private set => SetProperty(ref field, value);
    }

    /// <summary>The PDF report written most recently, or empty when none has been saved.</summary>
    [AffectsCommands(nameof(OpenSavedReportCommand))]
    public string LastSavedReportPath
    {
        get;
        private set => SetProperty(ref field, value ?? string.Empty);
    } = string.Empty;

    #endregion

    #region | Computed properties |

    /// <summary>True when there is a card left to draw and nothing is in flight.</summary>
    public bool CanDraw => !IsBusy && DeckRemaining > 0;

    /// <summary>True when there is both an empty station and a card to fill it with.</summary>
    public bool CanAutoLay => !IsBusy && FilledStations < StationCount && DeckRemaining > 0;

    /// <summary>True when there is anything on the table or in the tray to clear.</summary>
    public bool CanClear => !IsBusy && (FilledStations > 0 || TrayCount > 0);

    /// <summary>True once at least one card is laid; a partial rosette still reads.</summary>
    public bool CanInterpret => !IsBusy && FilledStations >= 1;

    /// <summary>True once there is an interpretation the report can be built from.</summary>
    public bool CanCreatePdf => !IsBusy && HasInterpretation;

    /// <summary>"4 of 9 stations filled", for the right of the status strip.</summary>
    public string StationsText => $"{FilledStations} of {StationCount} stations filled";

    /// <summary>"37 cards remain", under the deck stack.</summary>
    public string DeckCountText => DeckRemaining == 1 ? "1 card remains" : $"{DeckRemaining} cards remain";

    /// <summary>"2 cards waiting", under the tray.</summary>
    public string TrayCaption => TrayCount == 0
        ? "No cards drawn yet"
        : TrayCount == 1 ? "1 card waiting" : $"{TrayCount} cards waiting";

    /// <summary>The heading over the interpretation panel; empty while nothing has been read.</summary>
    public string InterpretationTitle => Interpretation?.Title ?? string.Empty;

    /// <summary>"A reading for Ninshubur · 14 September 2026", under the heading.</summary>
    public string InterpretationSubtitle
    {
        get
        {
            var reading = Interpretation?.Reading;
            if (reading == null) { return string.Empty; }

            var who = string.IsNullOrWhiteSpace(reading.Querent) ? "the querent" : reading.Querent;
            return $"A reading for {who} · {reading.Created:d MMMM yyyy}";
        }
    }

    /// <summary>The deck rail shows whenever the interpretation panel does not.</summary>
    public Microsoft.UI.Xaml.Visibility DeckPanelVisibility =>
        ShowInterpretationPanel ? Microsoft.UI.Xaml.Visibility.Collapsed : Microsoft.UI.Xaml.Visibility.Visible;

    /// <summary>The interpretation panel's own visibility.</summary>
    public Microsoft.UI.Xaml.Visibility InterpretationVisibility =>
        ShowInterpretationPanel ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

    /// <summary>The "reading out of date" badge, shown only when a reading exists and has gone stale.</summary>
    public Microsoft.UI.Xaml.Visibility StaleVisibility =>
        HasInterpretation && IsInterpretationStale
            ? Microsoft.UI.Xaml.Visibility.Visible
            : Microsoft.UI.Xaml.Visibility.Collapsed;

    #endregion

    #region | What is on the table |

    /// <summary>Every card that has left the deck, laid or waiting.</summary>
    public IReadOnlyList<ReadingCard> Cards => _cards;

    /// <summary>The card laid at a station, or null when the station is empty.</summary>
    /// <param name="index">The station index, 0..8.</param>
    public ReadingCard? CardAt(int index) =>
        index >= 0 && index < StationCount ? _stations[index] : null;

    /// <summary>
    /// Lays a card on a station, which the page calls when a drag is dropped on one. Whatever
    /// was there is displaced back into the tray, and the interpretation goes stale.
    /// </summary>
    /// <param name="card">The card being laid; it may be coming from the tray or another station.</param>
    /// <param name="station">The station index, 0..8.</param>
    public void PlaceOnStation(ReadingCard? card, int station)
    {
        if (card == null || station < 0 || station >= StationCount || !_cards.Contains(card)) { return; }

        var occupant = _stations[station];
        if (occupant != null && !ReferenceEquals(occupant, card))
        {
            occupant.Station = -1;
            CardMoved?.Invoke(occupant, false);
            SetStatus($"{occupant.Card.Name} was displaced and waits in the tray.");
        }

        if (card.Station >= 0 && card.Station < StationCount && ReferenceEquals(_stations[card.Station], card))
        {
            _stations[card.Station] = null;
        }

        card.Station = station;
        _stations[station] = card;
        CardMoved?.Invoke(card, false);

        if (occupant == null || ReferenceEquals(occupant, card))
        {
            SetStatus($"{card.Card.Name} laid at {TitleOf(station)}.");
        }

        //The status line already names the card and its station, so it is not overwritten with
        //  the general guidance
        InvalidateInterpretation(hidePanel: false);
        RefreshCounts(refreshGuidance: false);
    }

    /// <summary>
    /// Sends a card back to the tray, which the page calls when a drag is dropped on no station
    /// and when the close badge on a laid card is pressed.
    /// </summary>
    /// <param name="card">The card to return.</param>
    /// <param name="announce">True to name the card in the status line, as the close badge does.</param>
    public void ReturnToTray(ReadingCard? card, bool announce)
    {
        if (card == null || !_cards.Contains(card)) { return; }

        if (card.Station >= 0 && card.Station < StationCount && ReferenceEquals(_stations[card.Station], card))
        {
            _stations[card.Station] = null;
        }

        card.Station = -1;
        CardMoved?.Invoke(card, false);
        if (announce) { SetStatus($"{card.Card.Name} returned to the tray."); }

        InvalidateInterpretation(hidePanel: false);
        RefreshCounts(refreshGuidance: !announce);
    }

    /// <summary>
    /// Turns a card the other way up, which the page calls on a double click. A reversed card
    /// reads its reversed meaning, so the interpretation goes stale.
    /// </summary>
    /// <param name="card">The card to turn.</param>
    public void ToggleReversed(ReadingCard? card)
    {
        if (card == null || !_cards.Contains(card)) { return; }

        card.IsReversed = !card.IsReversed;
        CardFlipped?.Invoke(card);
        InvalidateInterpretation(hidePanel: false);
        SetStatus($"{card.Card.Name} is now {(card.IsReversed ? "reversed" : "upright")}.");
        RefreshCounts(refreshGuidance: false);
    }

    #endregion

    #region | Commands and their implementations |

    private SimpleCommand? _shuffleCommand;

    /// <summary>
    /// Sends the tray back into the deck and shuffles it. Whatever is laid on the rosette stays
    /// exactly where it is, the way it is, so the interpretation is left standing.
    /// </summary>
    public SimpleCommand ShuffleCommand =>
        (_shuffleCommand ??= new SimpleCommand(CanShuffle, DoShuffle));

    private SimpleCommand? _drawCommand;

    /// <summary>Takes the top card of the deck into the tray.</summary>
    public SimpleCommand DrawCommand =>
        (_drawCommand ??= new SimpleCommand(() => CanDraw, DoDraw));

    private SimpleCommand? _autoLayCommand;

    /// <summary>Fills every empty station, using the tray first and then the deck.</summary>
    public SimpleCommand AutoLayCommand =>
        (_autoLayCommand ??= new SimpleCommand(() => CanAutoLay, DoAutoLay));

    private SimpleCommand? _clearCommand;

    /// <summary>
    /// Empties the rosette and the tray, every card going back under the deck in the order it
    /// was laid. The deck is not shuffled, so a card just cleared is the last one to come round
    /// again.
    /// </summary>
    public SimpleCommand ClearCommand =>
        (_clearCommand ??= new SimpleCommand(() => CanClear, DoClear));

    private SimpleCommand? _interpretCommand;

    /// <summary>Writes the reading from whatever is laid, complete or partial.</summary>
    public SimpleCommand InterpretCommand =>
        (_interpretCommand ??= new SimpleCommand(() => CanInterpret, DoInterpret));

    private SimpleCommand? _createPdfCommand;

    /// <summary>Composes the printable report and writes it where the person chooses.</summary>
    public SimpleCommand CreatePdfCommand =>
        (_createPdfCommand ??= new SimpleCommand(() => CanCreatePdf, DoCreatePdf));

    private SimpleCommand? _backToDeckCommand;

    /// <summary>Closes the interpretation panel and shows the deck rail again.</summary>
    public SimpleCommand BackToDeckCommand =>
        (_backToDeckCommand ??= new SimpleCommand(DoBackToDeck));

    private SimpleCommand? _saveReadingCommand;

    /// <summary>Writes the laid rosette to a JSON file.</summary>
    public SimpleCommand SaveReadingCommand =>
        (_saveReadingCommand ??= new SimpleCommand(CanSaveReading, DoSaveReading));

    private SimpleCommand? _openReadingCommand;

    /// <summary>Reads a saved rosette back and lays it out again.</summary>
    public SimpleCommand OpenReadingCommand =>
        (_openReadingCommand ??= new SimpleCommand(() => !IsBusy, DoOpenReading));

    private SimpleCommand? _openSavedReportCommand;

    /// <summary>Hands the PDF that was saved most recently to the operating system.</summary>
    public SimpleCommand OpenSavedReportCommand =>
        (_openSavedReportCommand ??= new SimpleCommand(CanOpenSavedReport, DoOpenSavedReport));

    private bool CanShuffle() => !IsBusy;

    private bool CanSaveReading() => !IsBusy && FilledStations > 0;

    private bool CanOpenSavedReport() => !string.IsNullOrWhiteSpace(LastSavedReportPath);

    //Nothing on the rosette is disturbed, so there is nothing to ask about: the tray goes back
    //  under the deck, the deck is shuffled, and the page is given the length of its animation
    //  with IsBusy held true so no other command can run over the top of it
    private async Task DoShuffle()
    {
        if (!CanShuffle()) { return; }

        IsBusy = true;
        try
        {
            var returning = _cards.Where(c => c.InTray).ToList();
            foreach (var card in returning)
            {
                _cards.Remove(card);
                _deck.ReturnToBottom(card.Card);
            }

            _deck.Shuffle();
            RefreshCounts(refreshGuidance: false);
            SetStatus(ShuffleReport(returning.Count));

            var shuffled = DeckShuffled;
            if (shuffled != null) { await shuffled(returning); }
        }
        catch (Exception e)
        {
            _log.LogWarning(e, "Shuffle failed.");
        }
        finally
        {
            IsBusy = false;
        }

        RefreshCounts(refreshGuidance: false);
    }

    //"31 cards remain" is the thing a person actually wants to know after a shuffle; how many
    //  came back off the tray is what tells them the shuffle did anything at all
    private string ShuffleReport(int returned) => returned switch
    {
        0 => $"The deck is shuffled; {DeckRemaining} cards remain.",
        1 => $"One card returned to the deck and it is shuffled; {DeckRemaining} cards remain.",
        _ => $"The tray returned to the deck and it is shuffled; {DeckRemaining} cards remain.",
    };

    private async Task DoDraw()
    {
        if (!CanDraw) { return; }

        if (DrawToTray() == null)
        {
            SetStatus("The deck is empty. Press Clear to send every card back under it.");
            return;
        }

        //One turn of the loop so the flip animation the page started has a frame to begin in
        await Task.Delay(1);
        RefreshCounts();
    }

    private async Task DoAutoLay()
    {
        if (!CanAutoLay) { return; }

        IsBusy = true;
        try
        {
            for (var i = 0; i < StationCount; i++)
            {
                if (_stations[i] != null) { continue; }

                //Prefer a card already waiting in the tray over one still in the deck
                var card = _cards.FirstOrDefault(c => c.InTray) ?? DrawToTray();
                if (card == null) { break; }

                card.IsReversed = Deck.RollReversed(_deck.Rng);
                card.Station = i;
                _stations[i] = card;
                CardMoved?.Invoke(card, true);
                RefreshCounts(refreshGuidance: false);
                await Task.Delay(85);
            }
        }
        finally
        {
            IsBusy = false;
        }

        InvalidateInterpretation(hidePanel: false);
        RefreshCounts();
    }

    private async Task DoClear()
    {
        if (!CanClear) { return; }

        var ok = await Confirm("Clear the rosette?",
            "Every card goes back under the deck and the spread is emptied. The deck is not shuffled.",
            "Clear");
        if (!ok) { return; }

        GatherToDeck();
        RefreshCounts(refreshGuidance: false);
        SetStatus($"The rosette is clear and every card is back under the deck; {DeckRemaining} cards remain.");
    }

    private async Task DoInterpret()
    {
        if (!CanInterpret) { return; }

        ReadingInterpretation interpretation;
        IsBusy = true;
        try
        {
            var reading = BuildReading();
            interpretation = await Task.Run(() => _interpreter.Interpret(reading));
        }
        catch (Exception e)
        {
            _log.LogError(e, "Interpret failed.");
            InvokeOnMainThread(() => IsBusy = false);
            await ShowMessage("The reading could not be composed", e.Message);
            return;
        }

        InvokeOnMainThread(() =>
        {
            IsBusy = false;
            Interpretation = interpretation;
            HasInterpretation = true;
            IsInterpretationStale = false;
            ShowInterpretationPanel = true;
            RefreshGuidance();
        });
    }

    private Task DoBackToDeck()
    {
        ShowInterpretationPanel = false;
        RefreshGuidance();
        return Task.CompletedTask;
    }

    private async Task DoCreatePdf()
    {
        var interpretation = Interpretation;
        if (interpretation == null)
        {
            await ShowMessage("Nothing to report yet", "Press Interpret first, then create the report.");
            return;
        }

        byte[] bytes;
        string suggested;
        IsBusy = true;
        SetStatus("Composing the report…");
        try
        {
            //The report draws every card's art and embeds the fonts, which is far too much
            //  work for the UI thread
            suggested = _pdfBuilder.SuggestedFileName(interpretation);
            bytes = await Task.Run(() => _pdfBuilder.Build(interpretation));
        }
        catch (Exception e)
        {
            _log.LogError(e, "PDF build failed.");
            InvokeOnMainThread(() => IsBusy = false);
            await ShowMessage("The report could not be composed", e.Message);
            return;
        }

        InvokeOnMainThread(() => IsBusy = false);

        string? path;
        try
        {
            path = await PickSavePath(suggested, "PDF document", ".pdf");
        }
        catch (Exception e)
        {
            _log.LogError(e, "Save picker failed.");
            await ShowMessage("The file dialog is unavailable", e.Message);
            return;
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            InvokeOnMainThread(() => SetStatus("The report was not saved."));
            return;
        }

        try
        {
            await File.WriteAllBytesAsync(path, bytes);
        }
        catch (Exception e)
        {
            await ShowMessage("The report could not be written", e.Message);
            return;
        }

        InvokeOnMainThread(() =>
        {
            LastSavedReportPath = path;
            SetStatus($"Report saved to {path}");
        });

        var savedDialog = ShowReportSavedAsync;
        if (savedDialog != null) { await savedDialog(path); }
    }

    private async Task DoSaveReading()
    {
        if (!CanSaveReading()) { return; }

        var reading = BuildReading();
        string json;
        try
        {
            json = _serializer.ToJson(reading);
        }
        catch (Exception e)
        {
            await ShowMessage("The reading could not be saved", e.Message);
            return;
        }

        var name = string.IsNullOrWhiteSpace(reading.Querent) ? "Reading" : reading.Querent;
        var path = await PickSavePath($"{Sanitise(name)}-rosette.json", "Rosette reading", ".json");
        if (string.IsNullOrWhiteSpace(path)) { return; }

        try
        {
            await File.WriteAllTextAsync(path, json);
            InvokeOnMainThread(() => SetStatus($"Reading saved to {path}"));
        }
        catch (Exception e)
        {
            await ShowMessage("The reading could not be written", e.Message);
        }
    }

    private async Task DoOpenReading()
    {
        if (IsBusy) { return; }

        var picker = PickReadingPathAsync;
        if (picker == null)
        {
            await ShowMessage("The file dialog is unavailable",
                "This head cannot browse for files.");
            return;
        }

        string? path;
        try
        {
            path = await picker();
        }
        catch (Exception e)
        {
            await ShowMessage("The file dialog is unavailable", e.Message);
            return;
        }

        if (string.IsNullOrWhiteSpace(path)) { return; }

        RosetteReading reading;
        try
        {
            var json = await File.ReadAllTextAsync(path);
            reading = _serializer.FromJson(json);
        }
        catch (Exception e)
        {
            await ShowMessage("That reading could not be opened", e.Message);
            return;
        }

        InvokeOnMainThread(() => ApplyReading(reading));
    }

    //One place in the whole application asks the host to open a file. A refusal is a status
    //  line, never an exception that reaches the user.
    private async Task DoOpenSavedReport()
    {
        if (!CanOpenSavedReport()) { return; }

        var path = LastSavedReportPath;
        try
        {
            //Uri.TryCreate(path, UriKind.Absolute) rejects a POSIX path such as /home/me/a.pdf,
            //  which is exactly what the Linux and macOS heads hand back; the Uri constructor
            //  over a full path accepts both shapes
            var uri = new Uri(Path.GetFullPath(path));

            var opened = await Windows.System.Launcher.LaunchUriAsync(uri);
            if (!opened)
            {
                InvokeOnMainThread(() => SetStatus("No application was available to open that report."));
            }
        }
        catch (Exception e)
        {
            InvokeOnMainThread(() => SetStatus($"That report could not be opened: {e.Message}"));
        }
    }

    #endregion

    #region | The deck and the reading |

    //Takes the top card of the deck into the tray and tells the page to show it leaving the
    //  stack. Returns null when the deck is empty. Every draw goes through here - the deck stack,
    //  the Draw button and each card Auto-lay takes - which is what makes it the one place the
    //  goddess's own card can be recognised as having been drawn.
    private ReadingCard? DrawToTray()
    {
        Card? card;
        try
        {
            card = _deck.Draw();
        }
        catch (Exception e)
        {
            _log.LogWarning(e, "Draw failed.");
            return null;
        }

        if (card == null) { return null; }

        var drawn = new ReadingCard(card);
        _cards.Add(drawn);
        CardAdded?.Invoke(drawn);

        //The card exists on screen before the blessing is asked for, so the radiance has a card
        //  to bloom out of. A reading opened from a file lays its cards through ApplyReading and
        //  never comes through here, which is exactly right: nothing was drawn.
        if (card.Id == InannaCardId) { CardCelebrated?.Invoke(drawn, InannaBlessing); }

        return drawn;
    }

    //Every drawn card back under the deck in a settled order - the nine stations first, then the
    //  tray as it stands - the table emptied, the panel closed and the interpretation gone. The
    //  deck is deliberately NOT shuffled: what was just cleared is what the deck reaches last.
    private void GatherToDeck()
    {
        var tray = _cards.Where(c => c.InTray).ToList();

        for (var i = 0; i < _stations.Length; i++)
        {
            var laid = _stations[i];
            _stations[i] = null;
            if (laid != null) { _deck.ReturnToBottom(laid.Card); }
        }

        foreach (var waiting in tray) { _deck.ReturnToBottom(waiting.Card); }

        _cards.Clear();
        TableCleared?.Invoke();

        InvalidateInterpretation(hidePanel: true);
    }

    //The reading as the library wants it: whatever is laid, in station order
    private RosetteReading BuildReading()
    {
        var reading = new RosetteReading { Querent = Querent.Trim(), Question = Question.Trim() };
        for (var i = 0; i < StationCount; i++)
        {
            var card = _stations[i];
            if (card == null) { continue; }
            reading.Placements.Add(new PlacedCard(RosetteSpread.Positions[i], card.Card, card.IsReversed));
        }

        return reading;
    }

    //Lays a reading that was read back from a file: the table starts empty and each placement
    //  arrives already on its station, so the page places it rather than dealing it to the tray
    private void ApplyReading(RosetteReading reading)
    {
        GatherToDeck();

        Querent = reading.Querent ?? string.Empty;
        Question = reading.Question ?? string.Empty;

        var laid = 0;
        foreach (var placement in reading.Placements)
        {
            var index = placement.Position.Index;
            if (index < 0 || index >= StationCount || _stations[index] != null) { continue; }

            //A card on the table is never also in the stack, so the deck hands it over rather
            //  than keeping a second copy of it; a file naming the same card twice is refused
            //  here rather than being drawn again later
            if (!_deck.Remove(placement.Card)) { continue; }

            var card = new ReadingCard(placement.Card)
            {
                IsReversed = placement.IsReversed,
                Station = index,
            };
            _cards.Add(card);
            _stations[index] = card;
            laid++;
            CardAdded?.Invoke(card);
        }

        //Whatever was not laid is the deck now, and a reading just opened deserves a fresh order
        _deck.Shuffle();

        RefreshCounts(refreshGuidance: false);
        SetStatus($"Opened a reading with {laid} cards laid; {DeckRemaining} cards remain in the deck.");
    }

    private void InvalidateInterpretation(bool hidePanel)
    {
        if (Interpretation != null) { IsInterpretationStale = true; }
        if (!hidePanel) { return; }

        Interpretation = null;
        HasInterpretation = false;
        IsInterpretationStale = false;
        ShowInterpretationPanel = false;
    }

    private static string TitleOf(int index)
    {
        var positions = RosetteSpread.Positions;
        return index >= 0 && index < positions.Count ? positions[index].Title : $"station {index}";
    }

    private static string Sanitise(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars()) { name = name.Replace(c, '-'); }
        return name.Trim();
    }

    #endregion

    #region | The status line |

    /// <summary>Sets the status line explicitly, because an action just happened.</summary>
    /// <param name="text">The line to show.</param>
    public void SetStatus(string text) => StatusText = text;

    /// <summary>Works the guiding line out from the current state of the table.</summary>
    public void RefreshGuidance()
    {
        if (ShowInterpretationPanel)
        {
            StatusText = IsInterpretationStale
                ? "The table has changed since this reading was drawn. Press Interpret again to refresh it."
                : "Read the interpretation, then create a PDF report to keep or share it.";
            return;
        }

        if (FilledStations >= StationCount)
        {
            StatusText = "All nine stations are filled. Press Interpret to read the rosette.";
        }
        else if (FilledStations > 0)
        {
            StatusText = TrayCount > 0
                ? "Drag a drawn card onto a petal, or press Interpret to read what is already laid."
                : "Draw another card, or press Auto-lay to fill the remaining stations.";
        }
        else if (TrayCount > 0)
        {
            StatusText = "Drag a card from the tray onto a petal of the rosette. Double-click a laid card to reverse it.";
        }
        else if (DeckRemaining <= 0)
        {
            StatusText = "The deck is empty. Press Clear to send every card back under it.";
        }
        else
        {
            StatusText = "Click the deck (or press Draw) to take a card, then drag it onto a petal.";
        }
    }

    //The three counts every button and caption is gated on, recomputed from the one source of
    //  truth after anything moves
    private void RefreshCounts(bool refreshGuidance = true)
    {
        DeckRemaining = _deck.Remaining;
        TrayCount = _cards.Count(c => c.InTray);
        FilledStations = _stations.Count(s => s != null);
        AssertWholePack();
        if (refreshGuidance) { RefreshGuidance(); }
    }

    //The deck, the tray and the rosette are three parts of one pack: a card is in exactly one of
    //  them, always, and the three counts add up to the forty. Anything else means a card has
    //  been lost or copied, which is worth saying the moment it happens rather than when it turns
    //  up twice in the same reading. Debug builds only; the check costs nothing in Release.
    [System.Diagnostics.Conditional("DEBUG")]
    private void AssertWholePack()
    {
        var total = DeckRemaining + TrayCount + FilledStations;
        System.Diagnostics.Debug.Assert(total == DeckData.Cards.Count,
            $"The pack does not add up: {DeckRemaining} in the deck + {TrayCount} in the tray + " +
            $"{FilledStations} laid = {total}, not {DeckData.Cards.Count}.");
    }

    #endregion

    #region | Head-capability bridges |

    /// <inheritdoc/>
    public Action<ReadingCard>? CardAdded { get; set; }

    /// <inheritdoc/>
    public Action<ReadingCard, bool>? CardMoved { get; set; }

    /// <inheritdoc/>
    public Action<ReadingCard>? CardFlipped { get; set; }

    /// <inheritdoc/>
    public Action<ReadingCard, string>? CardCelebrated { get; set; }

    /// <inheritdoc/>
    public Action? TableCleared { get; set; }

    /// <inheritdoc/>
    public Func<IReadOnlyList<ReadingCard>, Task>? DeckShuffled { get; set; }

    /// <inheritdoc/>
    public Func<string, string, string, Task<string?>>? PickSavePathAsync { get; set; }

    /// <inheritdoc/>
    public Func<Task<string?>>? PickReadingPathAsync { get; set; }

    /// <inheritdoc/>
    public Func<string, string, string, Task<bool>>? ConfirmAsync { get; set; }

    /// <inheritdoc/>
    public Func<string, string, Task>? ShowMessageAsync { get; set; }

    /// <inheritdoc/>
    public Func<string, Task>? ShowReportSavedAsync { get; set; }

    //A head with no dialog cannot ask, so the answer is yes - exactly what the page's own
    //  dialog did when ContentDialog.ShowAsync threw
    private async Task<bool> Confirm(string title, string message, string primary)
    {
        var confirm = ConfirmAsync;
        return confirm == null || await confirm(title, message, primary);
    }

    private async Task ShowMessage(string title, string message)
    {
        var show = ShowMessageAsync;
        if (show == null)
        {
            InvokeOnMainThread(() => SetStatus($"{title}: {message}"));
            return;
        }

        await show(title, message);
    }

    private async Task<string?> PickSavePath(string suggestedFileName, string typeName, string extension)
    {
        var picker = PickSavePathAsync;
        if (picker == null)
        {
            await ShowMessage("The file dialog is unavailable", "This head cannot save files.");
            return null;
        }

        return await picker(suggestedFileName, typeName, extension);
    }

    #endregion

    #region | IDisposable implementation |

    /// <inheritdoc/>
    public override void Dispose()
    {
        _shuffleCommand?.Dispose();
        _shuffleCommand = null;
        _drawCommand?.Dispose();
        _drawCommand = null;
        _autoLayCommand?.Dispose();
        _autoLayCommand = null;
        _clearCommand?.Dispose();
        _clearCommand = null;
        _interpretCommand?.Dispose();
        _interpretCommand = null;
        _createPdfCommand?.Dispose();
        _createPdfCommand = null;
        _backToDeckCommand?.Dispose();
        _backToDeckCommand = null;
        _saveReadingCommand?.Dispose();
        _saveReadingCommand = null;
        _openReadingCommand?.Dispose();
        _openReadingCommand = null;
        _openSavedReportCommand?.Dispose();
        _openSavedReportCommand = null;

        //Releases the page: every one of these delegates captures it and would otherwise keep
        //  it alive through the view model
        CardAdded = null;
        CardMoved = null;
        CardFlipped = null;
        CardCelebrated = null;
        TableCleared = null;
        DeckShuffled = null;
        PickSavePathAsync = null;
        PickReadingPathAsync = null;
        ConfirmAsync = null;
        ShowMessageAsync = null;
        ShowReportSavedAsync = null;

        base.Dispose();
    }

    #endregion
}
