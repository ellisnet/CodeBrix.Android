using CodeBrix.Platform.Extensions;
using CodeBrix.Platform.Simple;
using InannaRosette.Controls;
using InannaRosette.Helpers;
using InannaRosette.Reading.Data;
using InannaRosette.Reading.Models;
using InannaRosette.Services;
using InannaRosette.ViewModels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using System; //Required: the IAsyncOperation GetAwaiter extension (awaiting the pickers) lives here
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Storage.Pickers;

namespace InannaRosette.Views;

/// <summary>
/// The whole screen: the header, the altar with the eight-petalled rosette, the deck rail,
/// the interpretation panel and the status strip. Every decision lives on
/// <see cref="MainViewModel"/>; this page owns the pictures — geometry, pointer mechanics,
/// animation — and reflects what the view model says has happened.
/// </summary>
public sealed partial class MainPage : Page
{
    // ------------------------------------------------------------- layout constants
    private const double Margin_ = 16;
    private const double Gap = 12;
    private const double RailWidth = 360;
    private const double DeckStackDepth = 4;

    // The ScrollViewer paints its vertical bar as an overlay along the right edge of the
    // scrolled area rather than taking a column of its own, so anything that has to stay
    // clickable — or readable — is held this far in from that edge.
    private const double ScrollBarTrack = 16;
    private const double ScrollBarGap = 4;
    private const double ScrollBarClearance = ScrollBarTrack + ScrollBarGap;
    private const double DetailCloseSize = 20;

    // the width the card detail panel's scrolled column had before the scrollbar was allowed for
    private const double DetailPanelTextWidth = 296;

    // ------------------------------------------------------------- state
    /// <summary>The visual for one card the view model says is on the table or in the tray.</summary>
    private sealed class CardItem
    {
        public required ReadingCard Model { get; init; }
        public required CardView View { get; init; }
    }

    private readonly List<CardItem> _items = new();
    private readonly List<CardView> _deckBacks = new();
    private readonly List<Rectangle> _slotRects = new();
    private readonly List<TextBlock> _slotLabels = new();

    private MainViewModel? _wiredViewModel;

    //Set once the page has unloaded and disposed its view model; everything that reaches for
    //  the view model after that gets null instead of a disposed object
    private bool _viewModelDisposed;

    private ILogger _log = NullLogger.Instance;

    // the hand-drawn rail captions, refreshed in place when the view model says they changed
    private TextBlock? _deckCountText;
    private TextBlock? _trayCaptionText;

    //A resize that lands while the interpretation panel is up leaves the rail chrome behind;
    //  this remembers that it has to be rebuilt when the panel goes away
    private bool _railChromeStale;

    //Which run of the shuffle animation the visuals on screen belong to. A resize rebuilds the
    //  deck stack from scratch and the page unloading throws it away altogether, so anything
    //  animating a frame at a time checks this before it touches a visual it no longer owns.
    private int _shuffleGeneration;

    //The same token, for the blessing. A resize moves the altar out from under it, Clear and
    //  Shuffle take the table away, and drawing Inanna again starts it over: each of those ends
    //  the run that was playing, and every frame checks that it still owns the layer.
    private int _celebrationGeneration;

    // geometry, recomputed on every resize
    private double _cardW = 104, _cardH = 166;
    private double _centreX, _centreY, _radius;
    private double _altarX, _altarY, _altarW, _altarH;
    private double _railX, _railY, _railH;
    private double _deckX, _deckY, _deckW, _deckH;
    private double _trayY, _trayW, _trayH;

    // drag state
    private CardItem? _dragItem;
    private Point _grab;
    private bool _dragging;
    private bool _dragMoved;
    private int _topZ = 20;
    private Rectangle? _dragShadow;
    private int _highlightStation = -1;
    private CardItem? _lastClickItem;
    private DateTime _lastClickAt = DateTime.MinValue;

    private Border? _detailPanel;

    public MainPage()
    {
        //The logger factory App.InitializeLogging() wired into the platform, when there is one
        var loggerFactory = LogExtensionPoint.AmbientLoggerFactory;
        if (loggerFactory != null) { _log = loggerFactory.CreateLogger<MainPage>(); }

        //Doing this before InitializeComponent() - InitializeComponent() is the thing that
        //  sets the data context, from the <Page.DataContext> element in the XAML.
        DataContextChanged += (_, _) =>
        {
            //Give the view model's SimpleDialog helpers a XamlRoot to attach dialogs to
            (DataContext as IXamlRootGetter)?.SetXamlRootGetter(() => XamlRoot);

            //Give the view model the pictures, the file dialogs and the dialogs — the three
            //  things only a page can supply
            if (DataContext is IReadingTableBridge table)
            {
                table.CardAdded = ShowCardAdded;
                table.CardMoved = ShowCardMoved;
                table.CardFlipped = ShowCardFlipped;
                table.CardCelebrated = ShowCardCelebrated;
                table.TableCleared = ShowTableCleared;
                table.DeckShuffled = PlayShuffleAsync;
            }

            if (DataContext is IReadingFileBridge files)
            {
                files.PickSavePathAsync = PickSavePathAsync;
                files.PickReadingPathAsync = PickReadingPathAsync;
            }

            if (DataContext is IReadingDialogBridge dialogs)
            {
                dialogs.ConfirmAsync = ConfirmAsync;
                dialogs.ShowMessageAsync = ShowMessageAsync;
                dialogs.ShowReportSavedAsync = ShowReportSavedAsync;
            }

            WireViewModel();
        };

        Loaded += (_, _) =>
        {
            WireViewModel();
            Relayout();
        };

        //Nothing else owns the view model — the XAML declares it in <Page.DataContext> — so the
        //  page is what runs its teardown: the commands disposed and the bridge delegates the
        //  page handed it dropped, every one of which captures this page
        Unloaded += (_, _) => OnPageUnloaded();

        InitializeComponent();

        AppTitleText.Text = Ornament.Track("Rosette of Inanna");
        StaleBadgeText.Text = Ornament.Track("reading out of date");

        BuildLogo();
    }

    private MainViewModel? ViewModel => _viewModelDisposed ? null : DataContext as MainViewModel;

    // ==================================================================== watching the view model

    /// <summary>
    /// The page is going. Anything still in flight — a detail panel, a drag, a fading
    /// animation, a dialog waiting to be closed — is let go of first, then the view model is
    /// disposed through <see cref="IDisposable"/>, so this line says nothing about what it
    /// happens to be holding; <c>MainViewModel.Dispose()</c> is the one place that knows.
    /// </summary>
    private void OnPageUnloaded()
    {
        CloseDetail();
        EndDrag();
        _shuffleGeneration++;
        EndCelebration();
        UnwireViewModel();

        _viewModelDisposed = true;
        (DataContext as IDisposable)?.Dispose();
    }

    private void WireViewModel()
    {
        var viewModel = ViewModel;
        if (ReferenceEquals(viewModel, _wiredViewModel)) { return; }

        UnwireViewModel();
        if (viewModel == null) { return; }

        _wiredViewModel = viewModel;
        _wiredViewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void UnwireViewModel()
    {
        if (_wiredViewModel == null) { return; }

        _wiredViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _wiredViewModel = null;
    }

    //The chrome that is drawn by hand onto the rail canvas cannot be bound, so the few pieces
    //  of it that follow view model state are refreshed from here
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MainViewModel.DeckRemaining):
                UpdateDeckStackVisibility();
                break;
            case nameof(MainViewModel.DeckCountText):
            case nameof(MainViewModel.TrayCaption):
                RefreshRailText();
                break;
            case nameof(MainViewModel.Interpretation):
                RenderInterpretation(ViewModel?.Interpretation);
                break;
            case nameof(MainViewModel.ShowInterpretationPanel):
                ApplyInterpretationPanel(ViewModel?.ShowInterpretationPanel == true);
                break;
        }
    }

    private void BuildLogo()
    {
        LogoBox.Child = CardView.LayerCanvas(
            EmblemArt.VenusStar, 100,
            Ornament.Brush(Ornament.Gold),
            Ornament.Brush(Ornament.LapisGlow),
            Ornament.Brush(Ornament.Gold, 0.9));
    }

    // ==================================================================== geometry

    private void OnContentSizeChanged(object sender, SizeChangedEventArgs e) => Relayout();

    private void Relayout()
    {
        if (_viewModelDisposed) { return; }

        var w = ContentCanvas.ActualWidth;
        var h = ContentCanvas.ActualHeight;
        if (w < 200 || h < 200) { return; }

        //Everything below is about to be recomputed, and a blessing in flight was drawn for the
        //  geometry that is going away; it ends here rather than finishing somewhere odd
        EndCelebration();

        _altarX = Margin_;
        _altarY = Gap;
        _altarH = h - Gap * 2;
        _railX = w - Margin_ - RailWidth;
        _altarW = Math.Max(320, _railX - Gap - _altarX);
        _railY = Gap;
        _railH = _altarH;

        Place(AltarBorder, _altarX, _altarY, _altarW, _altarH);
        Place(RailBorder, _railX, _railY, RailWidth, _railH);
        Place(InterpretationBorder, _railX, _railY, RailWidth, _railH);
        // the ScrollViewer on this head measures its child with an unbounded width, so the
        // text column is pinned explicitly or long paragraphs run off the panel edge
        InterpStack.Width = RailWidth - 50;

        // card size: the rosette must fit in the altar with room for the station labels
        var hFromHeight = (_altarH / 2 - 28) / 2.05;
        var hFromWidth = (_altarW / 2 - 64) / 1.8625;
        _cardH = Math.Clamp(Math.Min(hFromHeight, hFromWidth), 88, 176);
        _cardW = Math.Round(_cardH * 0.625);
        _cardH = Math.Round(_cardH);
        _radius = Math.Round(_cardH * 1.55);

        _centreX = _altarX + _altarW / 2;
        _centreY = _altarY + _altarH / 2;

        BuildOrnament();
        BuildSlots();

        //The tray and the deck stack are placed from this geometry whether or not the rail is
        //  the thing on show, so it is always current
        ComputeRailGeometry();

        if (ViewModel?.ShowInterpretationPanel == true)
        {
            //The rail layer is collapsed behind the interpretation panel: drawing chrome into
            //  it is work nobody sees, so it waits for "Back to deck"
            _railChromeStale = true;
        }
        else
        {
            BuildRailChrome();
        }

        RepositionAllCards();
    }

    private static void Place(FrameworkElement element, double x, double y, double w, double h)
    {
        Canvas.SetLeft(element, x);
        Canvas.SetTop(element, y);
        element.Width = Math.Max(0, w);
        element.Height = Math.Max(0, h);
    }

    /// <summary>Centre of station <paramref name="index"/> in ContentCanvas coordinates.</summary>
    private Point StationCentre(int index)
    {
        if (index <= 0) { return new Point(_centreX, _centreY); }
        var angle = AngleOf(index);
        var p = Ornament.Polar(_centreX, _centreY, _radius, angle);
        return new Point(p.X, p.Y);
    }

    private static double AngleOf(int index)
    {
        var positions = RosetteSpread.Positions;
        return index >= 0 && index < positions.Count
            ? positions[index].AngleDegrees
            : (index - 1) * 45.0;
    }

    // ==================================================================== the rosette ornament

    private void BuildOrnament()
    {
        OrnamentLayer.Children.Clear();

        var petalStroke = Ornament.Brush(Ornament.Gold, 0.44);
        var petalFill = Ornament.Brush(Ornament.Gold, 0.045);
        var hairline = Ornament.Brush(Ornament.Gold, 0.20);
        var ring = Ornament.Brush(Ornament.Gold, 0.5);

        var inner = _radius * 0.17;
        var petalLength = _radius * 0.80;
        var petalWidth = _radius * 0.215;

        // faint outer circle
        OrnamentLayer.Children.Add(new Ellipse
        {
            Width = (_radius + _cardH * 0.60) * 2,
            Height = (_radius + _cardH * 0.60) * 2,
            Stroke = Ornament.Brush(Ornament.GoldDeep, 0.22),
            StrokeThickness = 1,
        }.At(_centreX - (_radius + _cardH * 0.60), _centreY - (_radius + _cardH * 0.60)));

        OrnamentLayer.Children.Add(new Ellipse
        {
            Width = _radius * 2.06,
            Height = _radius * 2.06,
            Stroke = Ornament.Brush(Ornament.GoldDeep, 0.13),
            StrokeThickness = 0.8,
        }.At(_centreX - _radius * 1.03, _centreY - _radius * 1.03));

        // eight radial hairlines, between the petals
        for (int i = 0; i < 8; i++)
        {
            OrnamentLayer.Children.Add(new Microsoft.UI.Xaml.Shapes.Path
            {
                Data = CardView.ParseGeometry(Ornament.RadialLine(
                    _centreX, _centreY, inner * 1.25, _radius * 1.02, 22.5 + i * 45)),
                Stroke = hairline,
                StrokeThickness = 0.9,
            });
        }

        // eight bezier petals
        for (int i = 0; i < 8; i++)
        {
            OrnamentLayer.Children.Add(new Microsoft.UI.Xaml.Shapes.Path
            {
                Data = CardView.ParseGeometry(Ornament.PetalPath(
                    _centreX, _centreY, inner, petalLength, petalWidth, i * 45)),
                Stroke = petalStroke,
                StrokeThickness = 1.2,
                Fill = petalFill,
                StrokeLineJoin = PenLineJoin.Round,
            });
        }

        // centre ring
        OrnamentLayer.Children.Add(new Ellipse
        {
            Width = inner * 2,
            Height = inner * 2,
            Stroke = ring,
            StrokeThickness = 1.3,
        }.At(_centreX - inner, _centreY - inner));

        OrnamentLayer.Children.Add(new Ellipse
        {
            Width = inner * 1.32,
            Height = inner * 1.32,
            Stroke = Ornament.Brush(Ornament.Gold, 0.28),
            StrokeThickness = 0.9,
        }.At(_centreX - inner * 0.66, _centreY - inner * 0.66));

        OrnamentLayer.Children.Add(new Microsoft.UI.Xaml.Shapes.Path
        {
            Data = CardView.ParseGeometry(Ornament.RosettePath(_centreX, _centreY, inner * 0.52)),
            Fill = Ornament.Brush(Ornament.Gold, 0.22),
            Stroke = Ornament.Brush(Ornament.Gold, 0.35),
            StrokeThickness = 0.7,
        });
    }

    private void BuildSlots()
    {
        SlotLayer.Children.Clear();
        _slotRects.Clear();
        _slotLabels.Clear();

        for (int i = 0; i < 9; i++)
        {
            var centre = StationCentre(i);
            var rect = new Rectangle
            {
                Width = _cardW,
                Height = _cardH,
                RadiusX = 7,
                RadiusY = 7,
                Stroke = Ornament.Brush(Ornament.Gold, 0.55),
                StrokeThickness = 1.3,
                StrokeDashArray = new DoubleCollection { 4, 4 },
                Fill = Ornament.Brush(Ornament.Kohl, 0.3),
            };
            Canvas.SetLeft(rect, centre.X - _cardW / 2);
            Canvas.SetTop(rect, centre.Y - _cardH / 2);
            SlotLayer.Children.Add(rect);
            _slotRects.Add(rect);

            const double labelW = 176;
            var label = new TextBlock
            {
                Text = StationLabel(i),
                Width = labelW,
                FontSize = 9.2,
                FontFamily = new FontFamily(Fonts.Regular),
                Foreground = Ornament.Brush(Ornament.GoldDeep, 0.95),
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.NoWrap,
            };
            var anchor = i == 0
                ? new Point(_centreX, _centreY + _cardH / 2 + 16)
                : PointAt(LabelRadius(labelW, AngleOf(i)), AngleOf(i));
            Canvas.SetLeft(label, Math.Clamp(anchor.X - labelW / 2,
                _altarX + 5, Math.Max(_altarX + 5, _altarX + _altarW - labelW - 5)));
            Canvas.SetTop(label, Math.Clamp(anchor.Y - 7, _altarY + 5, _altarY + _altarH - 18));
            SlotLayer.Children.Add(label);
            _slotLabels.Add(label);
        }

        UpdateSlotVisibility();
    }

    /// <summary>
    /// How far out a station label must sit so its box clears the card box on that petal.
    /// The two rectangles are separated as soon as they clear on EITHER axis, so the smaller
    /// of the two required distances is enough — which keeps the diagonal labels tucked in.
    /// </summary>
    private double LabelRadius(double labelWidth, double angleDegrees)
    {
        var radians = angleDegrees * Math.PI / 180.0;
        var sin = Math.Abs(Math.Sin(radians));
        var cos = Math.Abs(Math.Cos(radians));
        var needX = sin > 0.02 ? (labelWidth / 2 + _cardW / 2 + 9) / sin : double.MaxValue;
        var needY = cos > 0.02 ? (9 + _cardH / 2 + 11) / cos : double.MaxValue;
        return _radius + Math.Min(needX, needY);
    }

    private Point PointAt(double radius, double angle)
    {
        var p = Ornament.Polar(_centreX, _centreY, radius, angle);
        return new Point(p.X, p.Y);
    }

    private static string StationLabel(int index)
    {
        var positions = RosetteSpread.Positions;
        if (index < 0 || index >= positions.Count)
        {
            return Ornament.Track(index == 0 ? "The Heart" : $"{Card.ToRoman(index)}");
        }

        var pos = positions[index];
        return index == 0
            ? Ornament.Track(pos.Title)
            : Ornament.Track($"{Card.ToRoman(index)} · {pos.Title}");
    }

    /// <summary>Fade a slot outline once a card sits on it.</summary>
    private void UpdateSlotVisibility()
    {
        for (int i = 0; i < _slotRects.Count && i < 9; i++)
        {
            var occupied = ItemAtStation(i) is not null;
            _slotRects[i].Opacity = occupied ? 0 : 1;
            _slotLabels[i].Opacity = occupied ? 0.75 : 1;
        }
    }

    private void SetStationHighlight(int index)
    {
        if (_highlightStation == index) { return; }
        if (_highlightStation >= 0 && _highlightStation < _slotRects.Count)
        {
            var old = _slotRects[_highlightStation];
            old.Stroke = Ornament.Brush(Ornament.Gold, 0.55);
            old.StrokeThickness = 1.3;
            old.Fill = Ornament.Brush(Ornament.Kohl, 0.3);
            old.Opacity = ItemAtStation(_highlightStation) is null ? 1 : 0;
        }
        _highlightStation = index;
        if (index >= 0 && index < _slotRects.Count)
        {
            var rect = _slotRects[index];
            rect.Opacity = 1;
            rect.Stroke = Ornament.Brush(Ornament.LapisGlow, 0.95);
            rect.StrokeThickness = 2.2;
            rect.Fill = Ornament.Brush(Ornament.LapisGlow, 0.14);
        }
    }

    // ==================================================================== the rail

    /// <summary>Where the deck stack and the tray sit; wanted even when the rail is hidden.</summary>
    private void ComputeRailGeometry()
    {
        _deckW = Math.Round(Math.Min(120, RailWidth * 0.33));
        _deckH = Math.Round(_deckW * 1.6);
        _deckX = _railX + (RailWidth - _deckW) / 2 - 6;
        _deckY = _railY + 44;

        _trayW = RailWidth - 36;
        _trayH = Math.Round(_cardH * 0.86) + 18;
        _trayY = _deckY + _deckH + 98;
    }

    private void BuildRailChrome()
    {
        RailLayer.Children.Clear();
        _railChromeStale = false;

        ComputeRailGeometry();

        RailLayer.Children.Add(Head(Ornament.Track("The Deck"), _railX + 18, _railY + 17, RailWidth - 36));
        RailLayer.Children.Add(Rule(_railX + 18, _railY + 33, RailWidth - 36));

        var count = Caption(ViewModel?.DeckCountText ?? string.Empty, _railX + 18, _deckY + _deckH + 12,
            RailWidth - 36, 12, Ornament.Gold);
        RailLayer.Children.Add(count);
        _deckCountText = count;

        RailLayer.Children.Add(Caption("Click the stack to draw a card", _railX + 18, _deckY + _deckH + 30,
            RailWidth - 36, 10.5, Ornament.Ivory, 0.55));

        RailLayer.Children.Add(Head(Ornament.Track("Drawn Cards"), _railX + 18, _trayY - 34, RailWidth - 36));
        RailLayer.Children.Add(Rule(_railX + 18, _trayY - 18, RailWidth - 36));

        var tray = Caption(ViewModel?.TrayCaption ?? string.Empty, _railX + 18, _trayY + _trayH + 6,
            RailWidth - 36, 10.5, Ornament.Ivory, 0.55);
        RailLayer.Children.Add(tray);
        _trayCaptionText = tray;

        // a closing ornament at the foot of the rail, if there is room
        var footY = _trayY + _trayH + 46;
        if (footY < _railY + _railH - 90)
        {
            var vb = new Viewbox { Width = 46, Height = 46, Stretch = Stretch.Uniform, Opacity = 0.42 };
            vb.Child = CardView.LayerCanvas(EmblemArt.RosetteMotif, 100,
                Ornament.Brush(Ornament.GoldDeep),
                Ornament.Brush(Ornament.Lapis),
                Ornament.Brush(Ornament.GoldDeep));
            Canvas.SetLeft(vb, _railX + RailWidth / 2 - 23);
            Canvas.SetTop(vb, footY);
            RailLayer.Children.Add(vb);

            RailLayer.Children.Add(Caption(
                "Drag a card onto a petal. Double-click to reverse it. Click for its lore.",
                _railX + 26, footY + 56, RailWidth - 52, 10.5, Ornament.Ivory, 0.45, wrap: true));
        }

        BuildDeckStack();
        RefreshRailText();
    }

    private static TextBlock Head(string text, double x, double y, double width)
    {
        var t = new TextBlock
        {
            Text = text,
            Width = width,
            FontSize = 10.5,
            FontFamily = new FontFamily(Fonts.Bold),
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            Foreground = Ornament.Brush(Ornament.Gold, 0.95),
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.NoWrap,
        };
        Canvas.SetLeft(t, x);
        Canvas.SetTop(t, y);
        return t;
    }

    private static Rectangle Rule(double x, double y, double width)
    {
        var r = new Rectangle { Width = width, Height = 0.8, Fill = Ornament.Brush(Ornament.GoldDeep, 0.35) };
        Canvas.SetLeft(r, x);
        Canvas.SetTop(r, y);
        return r;
    }

    private static TextBlock Caption(string text, double x, double y, double width, double size,
                                     string colour, double opacity = 1, bool wrap = false)
    {
        var t = new TextBlock
        {
            Text = text,
            Width = width,
            FontSize = size,
            FontFamily = new FontFamily(Fonts.Regular),
            Foreground = Ornament.Brush(colour, opacity),
            TextAlignment = TextAlignment.Center,
            TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap,
            LineHeight = wrap ? size * 1.55 : 0,
        };
        Canvas.SetLeft(t, x);
        Canvas.SetTop(t, y);
        return t;
    }

    private void BuildDeckStack()
    {
        //Every back on screen is about to be thrown away, so a riffle that is part way through
        //  stops here rather than going on moving visuals that are no longer in the tree
        _shuffleGeneration++;

        foreach (var back in _deckBacks) { CardLayer.Children.Remove(back); }
        _deckBacks.Clear();

        for (int i = 0; i < DeckStackDepth; i++)
        {
            var view = new CardView
            {
                IsFaceUp = false,
                CardWidth = _deckW,
                CardHeight = _deckH,
                EnableHover = i == DeckStackDepth - 1,
            };
            view.Rebuild();
            var offset = (DeckStackDepth - 1 - i) * 3.5;
            Canvas.SetLeft(view, _deckX + offset);
            Canvas.SetTop(view, _deckY - offset);
            Canvas.SetZIndex(view, 5 + i);
            view.PointerPressed += OnDeckPressed;
            CardLayer.Children.Add(view);
            _deckBacks.Add(view);
        }

        UpdateDeckStackVisibility();
    }

    private void UpdateDeckStackVisibility()
    {
        if (ViewModel?.ShowInterpretationPanel == true)
        {
            foreach (var back in _deckBacks) { back.Visibility = Visibility.Collapsed; }
            return;
        }

        var remaining = ViewModel?.DeckRemaining ?? 0;
        for (int i = 0; i < _deckBacks.Count; i++)
        {
            var needed = _deckBacks.Count - i;   // the bottom-most back needs 4 cards, the top 1
            _deckBacks[i].Visibility = remaining >= needed || (remaining > 0 && i == _deckBacks.Count - 1)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
    }

    private void OnDeckPressed(object sender, PointerRoutedEventArgs e)
    {
        e.Handled = true;
        CloseDetail();

        //The command owns the gate; the page only asks it
        var draw = ViewModel?.DrawCommand;
        if (draw?.CanExecute(null) == true) { draw.Execute(null); }
    }

    private void RefreshRailText()
    {
        var viewModel = ViewModel;
        if (viewModel is null) { return; }
        if (_deckCountText is not null) { _deckCountText.Text = viewModel.DeckCountText; }
        if (_trayCaptionText is not null) { _trayCaptionText.Text = viewModel.TrayCaption; }
    }

    // ==================================================================== what the view model says changed

    private CardItem? ItemFor(ReadingCard model) => _items.FirstOrDefault(i => ReferenceEquals(i.Model, model));

    private CardItem? ItemAtStation(int index) =>
        index < 0 ? null : _items.FirstOrDefault(i => i.Model.Station == index);

    /// <summary>A card has left the deck: build its visual where the view model says it belongs.</summary>
    private void ShowCardAdded(ReadingCard model)
    {
        var inTray = model.InTray;
        var view = new CardView
        {
            Card = model.Card,
            IsFaceUp = !inTray,
            CardWidth = inTray ? Math.Round(_cardW * 0.86) : _cardW,
            CardHeight = inTray ? Math.Round(_cardH * 0.86) : _cardH,
        };
        view.Rebuild();
        AttachCardHandlers(view);

        var item = new CardItem { Model = model, View = view };
        _items.Add(item);
        Canvas.SetLeft(view, _deckX);
        Canvas.SetTop(view, _deckY);
        Canvas.SetZIndex(view, ++_topZ);
        CardLayer.Children.Add(view);

        if (inTray)
        {
            // dealt onto the tray face down, then turned over where it lies
            LayoutTray();
            view.FlipToFaceUp();
            return;
        }

        // a reading that was opened from a file arrives already laid
        PlaceVisual(item, animate: true);
    }

    /// <summary>A card is now on a station, or back in the tray.</summary>
    private void ShowCardMoved(ReadingCard model, bool animate)
    {
        var item = ItemFor(model);
        if (item is null) { return; }

        if (model.InTray)
        {
            item.View.ShowCloseButton = false;
            LayoutTray();
            UpdateSlotVisibility();
            return;
        }

        PlaceVisual(item, animate);
    }

    /// <summary>A card was turned the other way up.</summary>
    private void ShowCardFlipped(ReadingCard model) => ItemFor(model)?.View.ToggleReversed();

    /// <summary>Every card went back into the deck.</summary>
    private void ShowTableCleared()
    {
        CloseDetail();
        EndDrag();
        EndCelebration();
        foreach (var item in _items) { CardLayer.Children.Remove(item.View); }
        _items.Clear();
        UpdateSlotVisibility();
    }

    // ==================================================================== the shuffle

    /// <summary>One tray card on its way home, with where it set off from.</summary>
    private sealed class Flight
    {
        public required CardView View { get; init; }
        public required double FromX { get; init; }
        public required double FromY { get; init; }
        public required double FromW { get; init; }
        public required double FromH { get; init; }
        public required double FromFan { get; init; }
    }

    // a riffle is three passes out and back, and the tray flies home before the first of them
    private const int FlyHomeMs = 220;
    private const int RifflePassMs = 110;
    private const int RifflePasses = 3;

    /// <summary>
    /// The view model has shuffled the deck. The cards it hands over were in the tray and are
    /// already back in the stack, so their visuals fly home and are dropped; then the stack
    /// riffles — three passes fanned out and gathered again — and the task completes, which is
    /// what lets the view model put its busy flag down.
    /// </summary>
    /// <param name="returned">The tray cards that went back into the deck; may be empty.</param>
    private async Task PlayShuffleAsync(IReadOnlyList<ReadingCard> returned)
    {
        CloseDetail();
        EndDrag();
        EndCelebration();

        var generation = ++_shuffleGeneration;
        try
        {
            await FlyTrayHome(returned, generation);
            await RiffleDeckStack(generation);
        }
        catch (Exception e)
        {
            //A dropped animation is never worth failing the command over; the finally below is
            //  what actually leaves the scene tidy
            _log.LogWarning(e, "The shuffle animation did not finish.");
        }
        finally
        {
            DropReturnedVisuals(returned);
            SettleDeckStack();
            LayoutTray();
            UpdateSlotVisibility();
        }
    }

    //The tray cards slide and shrink into the deck stack, turning face down half way, which is
    //  what says "these went back" rather than "these vanished"
    private async Task FlyTrayHome(IReadOnlyList<ReadingCard> returned, int generation)
    {
        var flights = new List<Flight>();
        foreach (var model in returned ?? Array.Empty<ReadingCard>())
        {
            var item = ItemFor(model);
            if (item is null) { continue; }

            item.View.ShowCloseButton = false;
            item.View.ClearHover();
            item.View.IsHitTestVisible = false;
            flights.Add(new Flight
            {
                View = item.View,
                FromX = Canvas.GetLeft(item.View),
                FromY = Canvas.GetTop(item.View),
                FromW = item.View.CardWidth,
                FromH = item.View.CardHeight,
                FromFan = item.View.FanAngle,
            });
        }

        if (flights.Count == 0) { return; }

        await Ease(FlyHomeMs, t =>
        {
            if (!ShuffleLives(generation)) { return; }
            foreach (var flight in flights)
            {
                var view = flight.View;
                Canvas.SetLeft(view, flight.FromX + (_deckX - flight.FromX) * t);
                Canvas.SetTop(view, flight.FromY + (_deckY - flight.FromY) * t);
                view.CardWidth = flight.FromW + (_deckW - flight.FromW) * t;
                view.CardHeight = flight.FromH + (_deckH - flight.FromH) * t;
                view.FanAngle = flight.FromFan * (1 - t);
                if (t >= 0.5) { view.IsFaceUp = false; }
            }
        });
    }

    //Two or three passes of the same shape: the backs fan out from the middle of the stack, each
    //  one further and turned a little more than the last, then come back square. The fan flips
    //  side on every pass, which is what makes it read as a riffle rather than a single spread.
    private async Task RiffleDeckStack(int generation)
    {
        var backs = _deckBacks.Where(b => b.Visibility == Visibility.Visible).ToList();
        var middle = (backs.Count - 1) / 2.0;
        var spread = Math.Max(12, _deckW * 0.40);

        for (var pass = 0; pass < RifflePasses; pass++)
        {
            var side = pass % 2 == 0 ? 1 : -1;
            await Ease(RifflePassMs, t => ApplyRiffle(generation, backs, middle, spread, side, t));
            await Ease(RifflePassMs, t => ApplyRiffle(generation, backs, middle, spread, side, 1 - t));
        }
    }

    private void ApplyRiffle(int generation, List<CardView> backs, double middle, double spread,
                             int side, double t)
    {
        if (!ShuffleLives(generation)) { return; }

        for (var i = 0; i < backs.Count; i++)
        {
            var view = backs[i];
            var index = _deckBacks.IndexOf(view);
            if (index < 0) { continue; }

            var rest = (DeckStackDepth - 1 - index) * 3.5;
            var fromMiddle = i - middle;
            Canvas.SetLeft(view, _deckX + rest + fromMiddle * spread * side * t);
            Canvas.SetTop(view, _deckY - rest - Math.Abs(fromMiddle) * 9 * t);
            view.FanAngle = fromMiddle * 9 * side * t;
        }
    }

    //The visuals for the cards that went back; the view model has already forgotten them
    private void DropReturnedVisuals(IReadOnlyList<ReadingCard> returned)
    {
        foreach (var model in returned ?? Array.Empty<ReadingCard>())
        {
            var item = ItemFor(model);
            if (item is null) { continue; }
            CardLayer.Children.Remove(item.View);
            _items.Remove(item);
        }
    }

    /// <summary>Puts every back of the deck stack square again, wherever a riffle left it.</summary>
    private void SettleDeckStack()
    {
        for (var i = 0; i < _deckBacks.Count; i++)
        {
            var view = _deckBacks[i];
            var offset = (DeckStackDepth - 1 - i) * 3.5;
            Canvas.SetLeft(view, _deckX + offset);
            Canvas.SetTop(view, _deckY - offset);
            view.FanAngle = 0;
            view.LiftScale = 1.0;
        }

        UpdateDeckStackVisibility();
    }

    //False once the page has gone, or once a resize has rebuilt the stack under a running riffle
    private bool ShuffleLives(int generation) => !_viewModelDisposed && _shuffleGeneration == generation;

    /// <summary>
    /// Runs <paramref name="onFrame"/> once a frame for <paramref name="milliseconds"/>, handing
    /// it an eased 0..1. The awaits resume on the UI thread the command came in on, which is
    /// what makes it safe to move visuals from inside the callback.
    /// </summary>
    private static async Task Ease(int milliseconds, Action<double> onFrame)
    {
        const int frameMs = 16;

        //A delay is only ever at least as long as it was asked for - the system timer is coarser
        //  than a frame - so how far along the animation is comes off the clock, not off a count
        //  of frames, and the whole thing takes the time it said it would
        var clock = System.Diagnostics.Stopwatch.StartNew();
        while (clock.ElapsedMilliseconds < milliseconds)
        {
            await Task.Delay(frameMs);
            var linear = Math.Min(1, clock.ElapsedMilliseconds / (double)milliseconds);
            //ease-out cubic: quick away, slow into place
            onFrame(1 - Math.Pow(1 - linear, 3));
        }

        onFrame(1);
    }

    // ==================================================================== the blessing

    // The radiance and the sparks take this long; the words are timed separately and are what
    // makes the whole celebration a little over two seconds. The two run together.
    private const int BloomMs = 1100;
    private const int BlessingWaitMs = 140;
    private const int BlessingRiseMs = 520;
    private const int BlessingHoldMs = 950;
    private const int BlessingFadeMs = 480;

    // how many sparks leave the card, and how far the words travel as they come and go
    private const int SparkCount = 9;
    private const double BlessingRise = 26;

    /// <summary>One spark on its way out of the card, with the line it follows.</summary>
    private sealed class Spark
    {
        public required Microsoft.UI.Xaml.Shapes.Path Shape { get; init; }
        public required double AngleDegrees { get; init; }
        public required double Reach { get; init; }
        public required double Size { get; init; }
    }

    /// <summary>
    /// The deck's own goddess has been drawn. The view model does not wait for this, so it is
    /// started here and observed here: a dropped animation is a picture that did not finish, not
    /// a failed draw, and the layer is left empty either way.
    /// </summary>
    /// <param name="model">The card that was drawn; the radiance blooms out of its visual.</param>
    /// <param name="blessing">The words to say over the altar, as the view model wrote them.</param>
    private void ShowCardCelebrated(ReadingCard model, string blessing)
    {
        _ = PlayCelebrationAsync(model, blessing);
    }

    private async Task PlayCelebrationAsync(ReadingCard model, string blessing)
    {
        if (_viewModelDisposed || model is null) { return; }

        //Whatever was playing is over: drawing Inanna twice in a row shows the second blessing
        //  from the beginning rather than two of them on top of one another
        EndCelebration();
        var generation = _celebrationGeneration;

        var pieces = new List<UIElement>();
        try
        {
            await Task.WhenAll(
                PlayBloomAsync(generation, model, pieces),
                PlayBlessingAsync(generation, blessing, pieces));
        }
        catch (Exception e)
        {
            //Exactly as the shuffle does: the finally below is what leaves the layer tidy
            _log.LogWarning(e, "The blessing animation did not finish.");
        }
        finally
        {
            foreach (var piece in pieces) { RemoveCelebrationPiece(piece); }
        }
    }

    /// <summary>
    /// Ends whatever is playing and empties the layer. Called by anything that moves the ground
    /// under it — a resize, a clear, a shuffle, the page unloading, the next blessing.
    /// </summary>
    private void EndCelebration()
    {
        _celebrationGeneration++;
        CelebrationLayer.Children.Clear();
    }

    //False once the page has gone, or once anything at all has taken the layer over
    private bool CelebrationLives(int generation) =>
        !_viewModelDisposed && _celebrationGeneration == generation;

    private void RemoveCelebrationPiece(UIElement piece)
    {
        //EndCelebration may have emptied the layer already; removing twice is not an error
        try { CelebrationLayer.Children.Remove(piece); }
        catch (Exception) { /* the layer no longer holds it */ }
    }

    private T AddCelebrationPiece<T>(T piece, List<UIElement> pieces) where T : UIElement
    {
        piece.IsHitTestVisible = false;
        piece.Opacity = 0;
        CelebrationLayer.Children.Add(piece);
        pieces.Add(piece);
        return piece;
    }

    /// <summary>The centre of the drawn card while it is still on the table; its last place after.</summary>
    private Point CelebrationOrigin(ReadingCard model, Point fallback)
    {
        var view = ItemFor(model)?.View;
        if (view is null) { return fallback; }

        var x = Canvas.GetLeft(view);
        var y = Canvas.GetTop(view);
        if (double.IsNaN(x) || double.IsNaN(y)) { return fallback; }

        return new Point(x + view.CardWidth / 2, y + view.CardHeight / 2);
    }

    /// <summary>
    /// Gold breaks out of the card that was drawn: two rings widening and thinning, the
    /// eight-pointed star of the goddess opening through them, and a handful of sparks carried
    /// outwards. The centre is read again every frame, so an Auto-lay that carries the card off
    /// to its petal takes the radiance with it.
    /// </summary>
    private async Task PlayBloomAsync(int generation, ReadingCard model, List<UIElement> pieces)
    {
        var span = Math.Max(52, _cardH * 0.92);
        var origin = CelebrationOrigin(model, new Point(_deckX + _deckW / 2, _deckY + _deckH / 2));

        var rings = new List<Ellipse>();
        for (var i = 0; i < 2; i++)
        {
            rings.Add(AddCelebrationPiece(new Ellipse
            {
                Stroke = Ornament.Brush(i == 0 ? Ornament.GoldPale : Ornament.Gold),
                StrokeThickness = 3,
            }, pieces));
        }

        var star = AddCelebrationPiece(new Viewbox
        {
            Stretch = Stretch.Uniform,
            Child = CardView.LayerCanvas(EmblemArt.VenusStar, 100,
                Ornament.Brush(Ornament.Gold),
                Ornament.Brush(Ornament.GoldPale),
                Ornament.Brush(Ornament.GoldPale, 0.9)),
        }, pieces);

        var sparks = new List<Spark>();
        for (var i = 0; i < SparkCount; i++)
        {
            var size = i % 2 == 0 ? 7.0 : 4.6;
            sparks.Add(new Spark
            {
                Shape = AddCelebrationPiece(new Microsoft.UI.Xaml.Shapes.Path
                {
                    Data = CardView.ParseGeometry(
                        Ornament.StarPath(size, size, size, size * 0.34, 4, i * 9.0)),
                    Fill = Ornament.Brush(i % 3 == 0 ? Ornament.GoldPale : Ornament.Gold),
                }, pieces),
                AngleDegrees = i * (360.0 / SparkCount) + 12,
                Reach = span * (i % 3 == 0 ? 2.3 : i % 3 == 1 ? 1.85 : 2.6),
                Size = size,
            });
        }

        await Ease(BloomMs, t =>
        {
            if (!CelebrationLives(generation)) { return; }

            //Ease() hands out an eased 0..1, which is the right curve for how far a thing has
            //  travelled and the wrong one for how bright it still is: undo it for the fades, so
            //  the gold goes out evenly over the whole second rather than in the first quarter.
            var p = 1 - Math.Cbrt(Math.Max(0, 1 - t));
            var centre = CelebrationOrigin(model, origin);

            for (var i = 0; i < rings.Count; i++)
            {
                var ring = rings[i];
                var grow = i == 0 ? t : Math.Clamp((t - 0.16) / 0.84, 0, 1);
                var fade = i == 0 ? p : Math.Clamp((p - 0.18) / 0.82, 0, 1);
                var r = span * (0.30 + 1.55 * grow);
                ring.Width = r * 2;
                ring.Height = r * 2;
                ring.StrokeThickness = Math.Max(0.6, 3.4 * (1 - grow));
                ring.Opacity = (i == 0 ? 0.95 : 0.6) * (1 - fade);
                Canvas.SetLeft(ring, centre.X - r);
                Canvas.SetTop(ring, centre.Y - r);
            }

            var starSize = span * (0.55 + 1.75 * t);
            star.Width = starSize;
            star.Height = starSize;
            star.Opacity = p < 0.28 ? p / 0.28 : Math.Max(0, 1 - (p - 0.28) / 0.72);
            Canvas.SetLeft(star, centre.X - starSize / 2);
            Canvas.SetTop(star, centre.Y - starSize / 2);

            foreach (var spark in sparks)
            {
                var distance = spark.Reach * (0.18 + 0.82 * t);
                var at = Ornament.Polar(centre.X, centre.Y, distance, spark.AngleDegrees);
                Canvas.SetLeft(spark.Shape, at.X - spark.Size);
                Canvas.SetTop(spark.Shape, at.Y - spark.Size);
                spark.Shape.Opacity = p < 0.12 ? p / 0.12 : Math.Max(0, 1 - (p - 0.12) / 0.88);
            }
        });
    }

    /// <summary>
    /// The words, over the altar: they wait for the radiance to open, fade and rise into place,
    /// hold, and go up and out. The dark backing is what lets gold read over the rosette.
    /// </summary>
    private async Task PlayBlessingAsync(int generation, string blessing, List<UIElement> pieces)
    {
        if (string.IsNullOrWhiteSpace(blessing)) { return; }

        var banner = AddCelebrationPiece(BuildBlessingBanner(blessing), pieces);

        //A Canvas child is never measured for us, so the height the words actually set to has to
        //  be asked for before the banner can be centred on the altar
        banner.Measure(new Windows.Foundation.Size(banner.Width, double.PositiveInfinity));
        var height = banner.DesiredSize.Height > 8 ? banner.DesiredSize.Height : 74;

        var left = _centreX - banner.Width / 2;
        var settled = _centreY - height / 2;
        Canvas.SetLeft(banner, left);
        Canvas.SetTop(banner, settled + BlessingRise);

        await Task.Delay(BlessingWaitMs);
        if (!CelebrationLives(generation)) { return; }

        await Ease(BlessingRiseMs, t =>
        {
            if (!CelebrationLives(generation)) { return; }
            banner.Opacity = t;
            Canvas.SetTop(banner, settled + BlessingRise * (1 - t));
        });
        if (!CelebrationLives(generation)) { return; }

        await Task.Delay(BlessingHoldMs);
        if (!CelebrationLives(generation)) { return; }

        await Ease(BlessingFadeMs, t =>
        {
            if (!CelebrationLives(generation)) { return; }
            banner.Opacity = 1 - t;
            Canvas.SetTop(banner, settled - BlessingRise * 0.55 * t);
        });
    }

    private Border BuildBlessingBanner(string blessing)
    {
        //As wide as the altar allows, and never so wide that the line loses its middle
        var width = Math.Clamp(_altarW - 48, 240, 620);
        var size = Math.Clamp(width / 20.5, 13, 27);
        var inner = width - 56;

        var stack = new StackPanel { Spacing = 8, Width = inner };
        stack.Children.Add(new Rectangle { Height = 1, Fill = Ornament.Brush(Ornament.GoldDeep, 0.6) });
        stack.Children.Add(new TextBlock
        {
            Text = blessing,
            Width = inner,
            FontSize = size,
            FontFamily = new FontFamily(Fonts.Bold),
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            Foreground = Ornament.Brush(Ornament.Gold),
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            LineHeight = Math.Round(size * 1.34),
        });
        stack.Children.Add(new Rectangle { Height = 1, Fill = Ornament.Brush(Ornament.GoldDeep, 0.6) });

        return new Border
        {
            Width = width,
            //The rosette is behind the words; a dark ground and a gold hairline are what keep
            //  them legible over it, exactly as the card detail panel does over the altar
            Background = Ornament.Brush(Ornament.Kohl, 0.88),
            BorderBrush = Ornament.Brush(Ornament.Gold, 0.7),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(5),
            Padding = new Thickness(28, 15, 28, 17),
            Child = stack,
        };
    }

    // ==================================================================== placement

    private void PlaceVisual(CardItem item, bool animate)
    {
        var index = item.Model.Station;
        if (index < 0 || index >= 9) { return; }

        item.View.IsFaceUp = true;
        item.View.ShowCloseButton = true;
        item.View.CardWidth = _cardW;
        item.View.CardHeight = _cardH;
        item.View.FanAngle = 0;
        item.View.Rebuild();
        item.View.IsReversed = item.Model.IsReversed;

        var centre = StationCentre(index);
        Canvas.SetLeft(item.View, centre.X - _cardW / 2);
        Canvas.SetTop(item.View, centre.Y - _cardH / 2);
        Canvas.SetZIndex(item.View, ++_topZ);
        if (animate) { item.View.PlayAppear(); }

        LayoutTray();
        UpdateSlotVisibility();
    }

    private void LayoutTray()
    {
        var tray = _items.Where(i => i.Model.InTray).ToList();
        var cardW = Math.Round(_cardW * 0.86);
        var cardH = Math.Round(_cardH * 0.86);
        var available = _trayW;
        var n = tray.Count;

        for (int i = 0; i < n; i++)
        {
            var item = tray[i];
            item.View.CardWidth = cardW;
            item.View.CardHeight = cardH;
            item.View.ShowCloseButton = false;

            double spacing = n <= 1 ? 0 : Math.Min(cardW + 10, (available - cardW) / (n - 1));
            double totalWidth = cardW + spacing * (n - 1);
            double startX = _railX + 18 + Math.Max(0, (available - totalWidth) / 2);
            double x = startX + spacing * i;

            double mid = (n - 1) / 2.0;
            double fan = n <= 1 ? 0 : (i - mid) * 3.2;
            double lift = n <= 1 ? 0 : Math.Abs(i - mid) * 2.2;

            Canvas.SetLeft(item.View, x);
            Canvas.SetTop(item.View, _trayY + 8 + lift);
            Canvas.SetZIndex(item.View, 12 + i);
            item.View.FanAngle = fan;
            item.View.IsReversed = item.Model.IsReversed;
        }
    }

    private void RepositionAllCards()
    {
        foreach (var item in _items)
        {
            if (item.Model.InTray) { continue; }
            item.View.CardWidth = _cardW;
            item.View.CardHeight = _cardH;
            item.View.FanAngle = 0;
            var centre = StationCentre(item.Model.Station);
            Canvas.SetLeft(item.View, centre.X - _cardW / 2);
            Canvas.SetTop(item.View, centre.Y - _cardH / 2);
        }
        LayoutTray();
        UpdateSlotVisibility();
    }

    // ==================================================================== drag & drop

    private void AttachCardHandlers(CardView view)
    {
        view.PointerPressed += OnCardPressed;
        view.PointerMoved += OnCardMoved;
        view.PointerReleased += OnCardReleased;
        view.PointerCaptureLost += OnCardCaptureLost;
        view.CloseRequested += OnCardCloseRequested;
    }

    private CardItem? ItemOf(object sender) => _items.FirstOrDefault(i => ReferenceEquals(i.View, sender));

    private void OnCardCloseRequested(object? sender, EventArgs e)
    {
        var item = ItemOf(sender!);
        if (item is null || item.Model.InTray) { return; }
        CloseDetail();
        ViewModel?.ReturnToTray(item.Model, announce: true);
    }

    private void OnCardPressed(object sender, PointerRoutedEventArgs e)
    {
        var item = ItemOf(sender);
        if (item is null) { return; }

        var pp = e.GetCurrentPoint(ContentCanvas);
        if (!pp.Properties.IsLeftButtonPressed) { return; }
        e.Handled = true;

        // double click toggles the orientation
        var now = DateTime.UtcNow;
        if (ReferenceEquals(_lastClickItem, item) && (now - _lastClickAt).TotalMilliseconds < 420)
        {
            _lastClickItem = null;
            _lastClickAt = DateTime.MinValue;
            CloseDetail();
            ViewModel?.ToggleReversed(item.Model);
            return;
        }
        _lastClickItem = item;
        _lastClickAt = now;

        CloseDetail();

        _dragItem = item;
        _dragMoved = false;
        _grab = new Point(pp.Position.X - Canvas.GetLeft(item.View), pp.Position.Y - Canvas.GetTop(item.View));
        Canvas.SetZIndex(item.View, 900);
        _dragging = item.View.CapturePointer(e.Pointer);
    }

    private void OnCardMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_dragging || _dragItem is null) { return; }
        e.Handled = true;

        var p = e.GetCurrentPoint(ContentCanvas).Position;
        var x = p.X - _grab.X;
        var y = p.Y - _grab.Y;

        if (!_dragMoved)
        {
            var dx = x - Canvas.GetLeft(_dragItem.View);
            var dy = y - Canvas.GetTop(_dragItem.View);
            if (Math.Abs(dx) + Math.Abs(dy) < 4) { return; }
            BeginDragVisuals(_dragItem);
        }

        Canvas.SetLeft(_dragItem.View, x);
        Canvas.SetTop(_dragItem.View, y);
        if (_dragShadow is not null)
        {
            Canvas.SetLeft(_dragShadow, x + 5);
            Canvas.SetTop(_dragShadow, y + 8);
        }

        SetStationHighlight(NearestStation(x + _dragItem.View.CardWidth / 2, y + _dragItem.View.CardHeight / 2));
    }

    private void BeginDragVisuals(CardItem item)
    {
        _dragMoved = true;
        item.View.ClearHover();
        item.View.LiftScale = 1.05;
        item.View.CardWidth = _cardW;
        item.View.CardHeight = _cardH;
        item.View.FanAngle = 0;
        item.View.IsFaceUp = true;

        _dragShadow = new Rectangle
        {
            Width = _cardW,
            Height = _cardH,
            RadiusX = 9,
            RadiusY = 9,
            Fill = Ornament.Brush(Ornament.Kohl, 0.55),
            IsHitTestVisible = false,
        };
        Canvas.SetZIndex(_dragShadow, 899);
        CardLayer.Children.Add(_dragShadow);
    }

    private int NearestStation(double x, double y)
    {
        var best = -1;
        var bestDistance = double.MaxValue;
        var snap = _cardH * 0.8;
        for (int i = 0; i < 9; i++)
        {
            var c = StationCentre(i);
            var d = Math.Sqrt((c.X - x) * (c.X - x) + (c.Y - y) * (c.Y - y));
            if (d < bestDistance) { bestDistance = d; best = i; }
        }
        return bestDistance <= snap ? best : -1;
    }

    private void OnCardReleased(object sender, PointerRoutedEventArgs e)
    {
        var item = ItemOf(sender);
        e.Handled = true;
        try { ((UIElement)sender).ReleasePointerCapture(e.Pointer); }
        catch (Exception) { /* capture may already be gone */ }

        if (item is null || !_dragging) { EndDrag(); return; }

        if (!_dragMoved)
        {
            EndDrag();
            ShowDetail(item);
            return;
        }

        var x = Canvas.GetLeft(item.View);
        var y = Canvas.GetTop(item.View);
        var target = NearestStation(x + _cardW / 2, y + _cardH / 2);
        EndDrag();

        // the view model decides what a drop on that station means; this page only reports it
        if (target >= 0)
        {
            ViewModel?.PlaceOnStation(item.Model, target);
        }
        else
        {
            ViewModel?.ReturnToTray(item.Model, announce: false);
        }
    }

    private void OnCardCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        if (_dragging && _dragMoved && _dragItem is not null)
        {
            var item = _dragItem;
            EndDrag();
            if (item.Model.InTray) { LayoutTray(); } else { RepositionAllCards(); }
        }
        else
        {
            EndDrag();
        }
    }

    private void EndDrag()
    {
        if (_dragItem is not null)
        {
            _dragItem.View.LiftScale = 1.0;
            Canvas.SetZIndex(_dragItem.View, ++_topZ);
        }
        if (_dragShadow is not null)
        {
            CardLayer.Children.Remove(_dragShadow);
            _dragShadow = null;
        }
        SetStationHighlight(-1);
        _dragging = false;
        _dragMoved = false;
        _dragItem = null;
    }

    // ==================================================================== the detail panel

    private void OnScenePressed(object sender, PointerRoutedEventArgs e) => CloseDetail();

    /// <summary>
    /// An unhandled pointer move bubbles out to the window manager, which on some heads then
    /// drags the window instead of leaving the scene alone. Card drags mark their own moves
    /// handled; this catches every other move over the app's own chrome.
    /// </summary>
    private void OnRootPointerMoved(object sender, PointerRoutedEventArgs e) => e.Handled = true;

    private void CloseDetail()
    {
        if (_detailPanel is not null)
        {
            DetailLayer.Children.Remove(_detailPanel);
            _detailPanel = null;
        }
    }

    private void ShowDetail(CardItem item)
    {
        CloseDetail();

        const double panelW = 330;
        var panel = BuildDetailPanel(item);
        panel.Width = panelW;
        panel.MaxHeight = Math.Max(240, ContentCanvas.ActualHeight - 40);

        var cardX = Canvas.GetLeft(item.View);
        var cardY = Canvas.GetTop(item.View);
        var cardW = item.View.CardWidth;

        var x = cardX + cardW + 14;
        if (x + panelW > _railX - 6) { x = cardX - panelW - 14; }
        x = Math.Clamp(x, _altarX + 6, Math.Max(_altarX + 6, _railX - panelW - 6));

        var y = Math.Clamp(cardY - 30, 8, Math.Max(8, ContentCanvas.ActualHeight - 380));

        Canvas.SetLeft(panel, x);
        Canvas.SetTop(panel, y);
        DetailLayer.Children.Add(panel);
        _detailPanel = panel;
        try { panel.Opacity = 0; FadeIn(panel, true); } catch (Exception) { panel.Opacity = 1; }
    }

    private Border BuildDetailPanel(CardItem item)
    {
        var card = item.Model.Card;
        var reversed = item.Model.IsReversed;

        //The panel is the same width as it always was; what the text gets is what is left of it
        //  once the scrollbar's strip down the right-hand edge is taken out
        const double textWidth = DetailPanelTextWidth - ScrollBarClearance;

        var stack = new StackPanel { Spacing = 7 };

        //The close button is NOT part of the scrolled content: it lives on the body grid,
        //  above the scroller, so the scrollbar overlay cannot take its clicks. The heads are
        //  held clear of where it sits so a long card name never runs underneath it.
        var heads = new StackPanel
        {
            Spacing = 2,
            Margin = new Thickness(0, 0, DetailCloseSize + 6, 0),
        };
        heads.Children.Add(Text(card.Name, 17, Ornament.Gold, bold: true, wrap: true));
        heads.Children.Add(Text(card.Epithet, 11.5, Ornament.Ivory, opacity: 0.72, wrap: true));
        heads.Children.Add(Text(card.Transliteration, 10.5, Ornament.GoldDeep, opacity: 0.9, wrap: true));
        stack.Children.Add(heads);

        stack.Children.Add(new Rectangle { Height = 1, Fill = Ornament.Brush(Ornament.GoldDeep, 0.5), Margin = new Thickness(0, 4, 0, 2) });

        stack.Children.Add(Text(Ornament.Track("Domains"), 9.5, Ornament.GoldDeep));
        stack.Children.Add(Text(string.Join("  ·  ", card.Domains), 12, Ornament.Ivory, opacity: 0.9, wrap: true));

        stack.Children.Add(Text(Ornament.Track(reversed ? "Reversed" : "Upright"), 9.5, Ornament.GoldDeep, margin: new Thickness(0, 6, 0, 0)));
        var keywords = reversed ? card.ReversedKeywords : card.UprightKeywords;
        stack.Children.Add(BuildChips(keywords, textWidth));
        stack.Children.Add(Text(reversed ? card.ReversedMeaning : card.UprightMeaning, 12, Ornament.Ivory, opacity: 0.9, wrap: true, lineHeight: 19));

        stack.Children.Add(Text(Ornament.Track("Lore"), 9.5, Ornament.GoldDeep, margin: new Thickness(0, 6, 0, 0)));
        stack.Children.Add(Text(card.Lore, 11.5, Ornament.Ivory, opacity: 0.72, wrap: true, lineHeight: 18));

        stack.Width = textWidth;
        var scroller = new ScrollViewer
        {
            Content = stack,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            HorizontalScrollMode = ScrollMode.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = 400,
        };

        // a soft fade at the foot so a clipped paragraph reads as "there is more below"
        var fade = new Rectangle
        {
            Height = 26,
            VerticalAlignment = VerticalAlignment.Bottom,
            IsHitTestVisible = false,
            Fill = new LinearGradientBrush
            {
                StartPoint = new Windows.Foundation.Point(0, 0),
                EndPoint = new Windows.Foundation.Point(0, 1),
                GradientStops =
                {
                    new Microsoft.UI.Xaml.Media.GradientStop { Color = Ornament.ToColor("#00130B26"), Offset = 0 },
                    new Microsoft.UI.Xaml.Media.GradientStop { Color = Ornament.ToColor("#FA130B26"), Offset = 1 },
                },
            },
        };
        var close = new Border
        {
            Width = DetailCloseSize,
            Height = DetailCloseSize,
            CornerRadius = new CornerRadius(DetailCloseSize / 2),
            BorderBrush = Ornament.Brush(Ornament.Gold, 0.7),
            BorderThickness = new Thickness(1),
            Background = Ornament.Brush(Ornament.Kohl, 0.6),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            //Clear of the scrollbar's strip, so the bar never takes a click meant for this
            Margin = new Thickness(0, 0, ScrollBarClearance, 0),
            Child = new TextBlock
            {
                Text = "×",
                FontSize = 13,
                FontFamily = new FontFamily(Fonts.Regular),
                Foreground = Ornament.Brush(Ornament.Gold),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };
        close.PointerPressed += (_, e) => { e.Handled = true; CloseDetail(); };

        var body = new Grid();
        body.Children.Add(scroller);
        body.Children.Add(fade);

        //Last child, so it is on top of both the scrolled content and the scrollbar overlay and
        //  stays where it is however far the content is scrolled
        body.Children.Add(close);

        var border = new Border
        {
            Background = Ornament.Brush("#FA130B26"),
            BorderBrush = Ornament.Brush(Ornament.GoldDeep, 0.85),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(16, 14, 14, 14),
            Child = body,
        };
        border.PointerPressed += (_, e) => e.Handled = true;
        return border;
    }

    // ==================================================================== text helpers

    private static TextBlock Text(string text, double size, string colour, bool bold = false,
                                  double opacity = 1, bool wrap = false, double lineHeight = 0,
                                  Thickness margin = default)
    {
        var t = new TextBlock
        {
            Text = text ?? string.Empty,
            FontSize = size,
            FontFamily = new FontFamily(bold ? Fonts.Bold : Fonts.Regular),
            FontWeight = bold ? Microsoft.UI.Text.FontWeights.Bold : Microsoft.UI.Text.FontWeights.Normal,
            Foreground = Ornament.Brush(colour, opacity),
            TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap,
            Margin = margin,
        };
        if (lineHeight > 0) { t.LineHeight = lineHeight; }
        return t;
    }

    /// <summary>Keyword chips, wrapped by hand (there is no WrapPanel in this XAML surface).</summary>
    private static StackPanel BuildChips(IReadOnlyList<string> keywords, double width)
    {
        var outer = new StackPanel { Spacing = 5, Margin = new Thickness(0, 3, 0, 4) };
        StackPanel row = NewRow();
        double used = 0;

        foreach (var keyword in keywords ?? Array.Empty<string>())
        {
            var text = Ornament.Track(keyword);
            var estimate = keyword.Length * 7.2 + 20;
            if (used > 0 && used + estimate > width)
            {
                outer.Children.Add(row);
                row = NewRow();
                used = 0;
            }
            row.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(2),
                BorderBrush = Ornament.Brush(Ornament.GoldDeep, 0.55),
                BorderThickness = new Thickness(1),
                Background = Ornament.Brush(Ornament.Gold, 0.08),
                Padding = new Thickness(6, 2, 6, 3),
                Child = new TextBlock
                {
                    Text = text,
                    FontSize = 8.8,
                    FontFamily = new FontFamily(Fonts.Regular),
                    Foreground = Ornament.Brush(Ornament.GoldPale, 0.95),
                },
            });
            used += estimate;
        }

        if (row.Children.Count > 0) { outer.Children.Add(row); }
        return outer;

        static StackPanel NewRow() => new() { Orientation = Orientation.Horizontal, Spacing = 5 };
    }

    // ==================================================================== the interpretation panel

    private void ApplyInterpretationPanel(bool show)
    {
        //The rail, the rail chrome and the panel are bound to DeckPanelVisibility and
        //  InterpretationVisibility; what is left here is the fade and the cards, which are
        //  built in code and so have nothing to bind
        if (!show && _railChromeStale)
        {
            //The window was resized while the panel was up, so the rail is being shown again
            //  at a size its chrome was never drawn for
            BuildRailChrome();
            LayoutTray();
        }

        UpdateDeckStackVisibility();

        foreach (var item in _items.Where(i => i.Model.InTray))
        {
            item.View.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
        }

        FadeIn(InterpretationBorder, show);
    }

    private static void FadeIn(UIElement element, bool visible)
    {
        try
        {
            var story = new Microsoft.UI.Xaml.Media.Animation.Storyboard();
            var animation = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
            {
                From = visible ? 0 : 1,
                To = visible ? 1 : 0,
                Duration = new Duration(TimeSpan.FromMilliseconds(220)),
            };
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTarget(animation, element);
            Microsoft.UI.Xaml.Media.Animation.Storyboard.SetTargetProperty(animation, "Opacity");
            story.Children.Add(animation);
            story.Begin();
            element.Opacity = visible ? 1 : 0;
        }
        catch (Exception)
        {
            element.Opacity = visible ? 1 : 0;
        }
    }

    private void RenderInterpretation(ReadingInterpretation? interpretation)
    {
        //The title and the subtitle are bound to the view model; the body below is built by hand
        InterpStack.Children.Clear();
        if (interpretation is null) { return; }

        if (!string.IsNullOrWhiteSpace(interpretation.Reading.Question))
        {
            InterpStack.Children.Add(Quote($"“{interpretation.Reading.Question}”"));
        }

        InterpStack.Children.Add(Text(interpretation.Opening, 12, Ornament.Ivory, opacity: 0.92, wrap: true, lineHeight: 19));

        foreach (var position in interpretation.Positions)
        {
            InterpStack.Children.Add(new Rectangle
            {
                Height = 0.8,
                Fill = Ornament.Brush(Ornament.GoldDeep, 0.3),
                Margin = new Thickness(0, 8, 0, 2),
            });
            InterpStack.Children.Add(Text(position.Heading, 13.5, Ornament.Gold, bold: true, wrap: true));
            InterpStack.Children.Add(Text(
                $"{position.Placed.Card.Name} · {position.Placed.OrientationLabel}",
                11, Ornament.Ivory, opacity: 0.7, wrap: true));
            InterpStack.Children.Add(BuildChips(position.Keywords, 296));
            foreach (var paragraph in position.Paragraphs)
            {
                InterpStack.Children.Add(Text(paragraph, 12, Ornament.Ivory, opacity: 0.92, wrap: true, lineHeight: 19));
            }
            if (!string.IsNullOrWhiteSpace(position.Placed.Card.Invocation))
            {
                InterpStack.Children.Add(Quote(position.Placed.Card.Invocation));
            }
        }

        if (interpretation.Insights.Count > 0)
        {
            InterpStack.Children.Add(Heading(Ornament.Track("What the rosette shows")));
            foreach (var insight in interpretation.Insights)
            {
                var body = new StackPanel { Spacing = 4 };
                body.Children.Add(Text(insight.Title, 12, Ornament.GoldPale, bold: true, wrap: true));
                body.Children.Add(Text(insight.Text, 11.5, Ornament.Ivory, opacity: 0.88, wrap: true, lineHeight: 18));
                InterpStack.Children.Add(new Border
                {
                    Background = Ornament.Brush(Ornament.Night2, 0.85),
                    BorderBrush = Ornament.Brush(Ornament.GoldDeep, 0.4),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(12, 10, 12, 12),
                    Child = body,
                });
            }
        }

        if (!string.IsNullOrWhiteSpace(interpretation.Counsel))
        {
            InterpStack.Children.Add(Heading(Ornament.Track("Counsel")));
            InterpStack.Children.Add(Text(interpretation.Counsel, 12, Ornament.Ivory, opacity: 0.92, wrap: true, lineHeight: 19));
        }

        if (!string.IsNullOrWhiteSpace(interpretation.ClosingInvocation))
        {
            InterpStack.Children.Add(Heading(Ornament.Track("Closing invocation")));
            InterpStack.Children.Add(Quote(interpretation.ClosingInvocation));
        }

        //The last thing in the panel, whatever came before it: the one line every reading ends
        //  with. It is set exactly as the library wrote it - no tracking, no capitals - because
        //  the report prints the same characters.
        if (!string.IsNullOrWhiteSpace(interpretation.Closing))
        {
            InterpStack.Children.Add(new Rectangle
            {
                Height = 0.8,
                Width = 120,
                HorizontalAlignment = HorizontalAlignment.Center,
                Fill = Ornament.Brush(Ornament.GoldDeep, 0.55),
                Margin = new Thickness(0, 14, 0, 2),
            });

            var closing = Text(interpretation.Closing, 11, Ornament.Gold, opacity: 0.92, wrap: true,
                lineHeight: 18, margin: new Thickness(0, 0, 0, 6));
            closing.TextAlignment = TextAlignment.Center;
            InterpStack.Children.Add(closing);
        }
    }

    private static TextBlock Heading(string text) =>
        Text(text, 10.5, Ornament.Gold, bold: true, margin: new Thickness(0, 12, 0, 0));

    private static Border Quote(string text) => new()
    {
        BorderBrush = Ornament.Brush(Ornament.Gold, 0.75),
        BorderThickness = new Thickness(2, 0, 0, 0),
        Padding = new Thickness(12, 3, 4, 4),
        Margin = new Thickness(2, 4, 0, 4),
        Child = Text(text, 12, Ornament.GoldPale, opacity: 0.88, wrap: true, lineHeight: 19),
    };

    // ==================================================================== file dialogs

    private static async Task<string?> PickSavePathAsync(string suggestedFileName, string typeName, string extension)
    {
        var picker = new FileSavePicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            SuggestedFileName = suggestedFileName,
            DefaultFileExtension = extension,
        };
        picker.FileTypeChoices.Add(typeName, new List<string> { extension });

        var file = await picker.PickSaveFileAsync();
        if (file is null) { return null; }

        //Some heads percent-encode the path they return, which would save "My Reading.pdf" as
        //  "My%20Reading.pdf"; decode it before anything touches the disk
        var path = FileDialogHelper.ToFileSystemPath(file.Path);
        FileDialogHelper.RemoveEmptyPlaceholder(path);
        return path;
    }

    private static async Task<string?> PickReadingPathAsync()
    {
        var picker = new FileOpenPicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
        };
        picker.FileTypeFilter.Add(".json");

        var file = await picker.PickSingleFileAsync();
        return file is null ? null : FileDialogHelper.ToFileSystemPath(file.Path);
    }

    // ==================================================================== dialogs

    private async Task<bool> ConfirmAsync(string title, string message, string primary)
    {
        try
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = Text(message, 12.5, Ornament.Ivory, opacity: 0.9, wrap: true),
                PrimaryButtonText = primary,
                CloseButtonText = "Cancel",
                PrimaryButtonStyle = (Style)Application.Current.Resources["TemplePrimaryButtonStyle"],
                CloseButtonStyle = (Style)Application.Current.Resources["TempleButtonStyle"],
                XamlRoot = XamlRoot,
            };
            return await dialog.ShowAsync() == ContentDialogResult.Primary;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Confirm dialog failed.");
            return true;
        }
    }

    private async Task ShowMessageAsync(string title, string message)
    {
        try
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = Text(message, 12.5, Ornament.Ivory, opacity: 0.9, wrap: true),
                CloseButtonText = "Close",
                CloseButtonStyle = (Style)Application.Current.Resources["TempleButtonStyle"],
                XamlRoot = XamlRoot,
            };
            await dialog.ShowAsync();
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Message dialog failed.");
        }
    }

    private async Task ShowReportSavedAsync(string path)
    {
        var stack = new StackPanel { Spacing = 12, Width = 430 };
        stack.Children.Add(Text("The report has been written to:", 12, Ornament.Ivory, opacity: 0.9, wrap: true));
        stack.Children.Add(Text(path, 11, Ornament.GoldPale, wrap: true));

        //Handing the file to the operating system is the view model's job, so the button runs
        //  its command rather than starting anything itself
        var open = new Button
        {
            Content = "Open",
            Style = (Style)Application.Current.Resources["TempleButtonStyle"],
            Command = ViewModel?.OpenSavedReportCommand,
        };
        stack.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Children = { open },
        });

        var dialog = new ContentDialog
        {
            Title = "Report saved",
            Content = stack,
            CloseButtonText = "Done",
            CloseButtonStyle = (Style)Application.Current.Resources["TemplePrimaryButtonStyle"],
            XamlRoot = XamlRoot,
        };
        try { await dialog.ShowAsync(); }
        catch (Exception ex) { _log.LogWarning(ex, "Saved dialog failed."); }
    }
}

/// <summary>Small positioning sugar for canvas children.</summary>
internal static class CanvasExtensions
{
    public static T At<T>(this T element, double x, double y) where T : UIElement
    {
        Canvas.SetLeft(element, x);
        Canvas.SetTop(element, y);
        return element;
    }
}
