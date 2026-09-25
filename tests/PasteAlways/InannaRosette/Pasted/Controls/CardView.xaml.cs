using CodeBrix.Platform.Extensions;
using InannaRosette.Reading.Data;
using InannaRosette.Reading.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using Windows.UI;

namespace InannaRosette.Controls;

/// <summary>
/// One oracle card, drawn procedurally: the face (gold double border, corner flourishes,
/// numeral/suit band, emblem medallion, name, epithet, rosette dots) or the back
/// (Venus star, ring of rosettes, tracked title). Everything is vector; no bitmaps.
/// </summary>
public sealed partial class CardView : UserControl
{
    /// <summary>The design box the card art is authored in; a Viewbox scales it to the real size.</summary>
    private const double DW = 250;

    private const double DH = 400;

    //The path parser is static, so the logger it reports through has to be too
    private static readonly ILogger Log = CreateLog();

    private static ILogger CreateLog()
    {
        //The logger factory App.InitializeLogging() wired into the platform, when there is one
        var loggerFactory = LogExtensionPoint.AmbientLoggerFactory;
        if (loggerFactory != null) { return loggerFactory.CreateLogger<CardView>(); }
        return NullLogger.Instance;
    }

    private bool _hovering;

    public CardView()
    {
        InitializeComponent();
        PointerEntered += OnPointerEnteredCard;
        PointerExited += OnPointerExitedCard;
        Loaded += (_, _) => Rebuild();
    }

    /// <summary>Raised when the small × in the corner is clicked.</summary>
    public event EventHandler? CloseRequested;

    // ------------------------------------------------------------------ properties

    public static readonly DependencyProperty CardProperty = DependencyProperty.Register(
        nameof(Card), typeof(Card), typeof(CardView),
        new PropertyMetadata(null, (d, _) => ((CardView)d).Rebuild()));

    public Card? Card
    {
        get => (Card?)GetValue(CardProperty);
        set => SetValue(CardProperty, value);
    }

    public static readonly DependencyProperty IsFaceUpProperty = DependencyProperty.Register(
        nameof(IsFaceUp), typeof(bool), typeof(CardView),
        new PropertyMetadata(true, (d, _) => ((CardView)d).ApplyFacing()));

    public bool IsFaceUp
    {
        get => (bool)GetValue(IsFaceUpProperty);
        set => SetValue(IsFaceUpProperty, value);
    }

    public static readonly DependencyProperty IsReversedProperty = DependencyProperty.Register(
        nameof(IsReversed), typeof(bool), typeof(CardView),
        new PropertyMetadata(false, (d, _) => ((CardView)d).ApplyOrientation(false)));

    public bool IsReversed
    {
        get => (bool)GetValue(IsReversedProperty);
        set => SetValue(IsReversedProperty, value);
    }

    public static readonly DependencyProperty CardWidthProperty = DependencyProperty.Register(
        nameof(CardWidth), typeof(double), typeof(CardView),
        new PropertyMetadata(104d, (d, _) => ((CardView)d).ApplySize()));

    public double CardWidth
    {
        get => (double)GetValue(CardWidthProperty);
        set => SetValue(CardWidthProperty, value);
    }

    public static readonly DependencyProperty CardHeightProperty = DependencyProperty.Register(
        nameof(CardHeight), typeof(double), typeof(CardView),
        new PropertyMetadata(166d, (d, _) => ((CardView)d).ApplySize()));

    public double CardHeight
    {
        get => (double)GetValue(CardHeightProperty);
        set => SetValue(CardHeightProperty, value);
    }

    public static readonly DependencyProperty ShowCloseButtonProperty = DependencyProperty.Register(
        nameof(ShowCloseButton), typeof(bool), typeof(CardView),
        new PropertyMetadata(false, (d, _) => ((CardView)d).UpdateOverlayVisibility()));

    /// <summary>Whether the hover × (return to tray) is offered at all.</summary>
    public bool ShowCloseButton
    {
        get => (bool)GetValue(ShowCloseButtonProperty);
        set => SetValue(ShowCloseButtonProperty, value);
    }

    public static readonly DependencyProperty EnableHoverProperty = DependencyProperty.Register(
        nameof(EnableHover), typeof(bool), typeof(CardView), new PropertyMetadata(true));

    public bool EnableHover
    {
        get => (bool)GetValue(EnableHoverProperty);
        set => SetValue(EnableHoverProperty, value);
    }

    /// <summary>A lapis ring around the card (used for "this is the snap target" / selection).</summary>
    public bool IsHighlighted
    {
        get => HighlightRect.Opacity > 0.5;
        set => HighlightRect.Opacity = value ? 1 : 0;
    }

    /// <summary>Extra rotation applied on top of the reversed flip (tray fan).</summary>
    public double FanAngle
    {
        get => LiftTransform.Rotation;
        set => LiftTransform.Rotation = value;
    }

    public double LiftScale
    {
        get => LiftTransform.ScaleX;
        set { LiftTransform.ScaleX = value; LiftTransform.ScaleY = value; }
    }

    // ------------------------------------------------------------------ animation

    /// <summary>Flip a face-down card up: ScaleX 1 to 0, swap the face, then 0 to 1.</summary>
    public void FlipToFaceUp()
    {
        if (IsFaceUp) { return; }
        try
        {
            void OnDone(object? s, object e)
            {
                FlipOutStory.Completed -= OnDone;
                IsFaceUp = true;
                try { FlipInStory.Begin(); } catch (Exception) { FaceTransform.ScaleX = 1; }
            }
            FlipOutStory.Completed += OnDone;
            FlipOutStory.Begin();
        }
        catch (Exception)
        {
            IsFaceUp = true;
            FaceTransform.ScaleX = 1;
        }
    }

    /// <summary>Fade/slide the card in (used by auto-lay and by drawing into the tray).</summary>
    public void PlayAppear()
    {
        try { AppearStory.Begin(); }
        catch (Exception) { Shell.Opacity = 1; }
    }

    /// <summary>Toggle reversed with a short rotate; falls back to a direct set.</summary>
    public void ToggleReversed()
    {
        var target = !IsReversed;
        SetValue(IsReversedProperty, target);
        ApplyOrientation(animate: true);
    }

    private void ApplyOrientation(bool animate)
    {
        var to = IsReversed ? 180d : 0d;
        if (!animate)
        {
            FaceTransform.Rotation = to;
            UpdateOverlayVisibility();
            return;
        }

        try
        {
            RotateStep.From = IsReversed ? 0 : 180;
            RotateStep.To = to;
            RotateStory.Begin();
        }
        catch (Exception)
        {
            FaceTransform.Rotation = to;
        }
        UpdateOverlayVisibility();
    }

    // ------------------------------------------------------------------ hover

    private void OnPointerEnteredCard(object sender, PointerRoutedEventArgs e)
    {
        _hovering = true;
        UpdateOverlayVisibility();
        if (!EnableHover) { return; }
        try { HoverInStory.Begin(); }
        catch (Exception) { LiftTransform.TranslateY = -4; GlowRect.Opacity = 1; }
    }

    private void OnPointerExitedCard(object sender, PointerRoutedEventArgs e)
    {
        _hovering = false;
        UpdateOverlayVisibility();
        if (!EnableHover) { return; }
        try { HoverOutStory.Begin(); }
        catch (Exception) { LiftTransform.TranslateY = 0; GlowRect.Opacity = 0; }
    }

    /// <summary>Force the hover treatment off (called when a drag starts).</summary>
    public void ClearHover()
    {
        _hovering = false;
        LiftTransform.TranslateY = 0;
        GlowRect.Opacity = 0;
        UpdateOverlayVisibility();
    }

    // ------------------------------------------------------------------ layout / build

    private void ApplySize()
    {
        var w = Math.Max(20, CardWidth);
        var h = Math.Max(32, CardHeight);
        Shell.Width = w;
        Shell.Height = h;
        Width = w;
        Height = h;
        GlowRect.Width = w + 6;
        GlowRect.Height = h + 6;
        HighlightRect.Width = w + 10;
        HighlightRect.Height = h + 10;
        FaceTransform.CenterX = w / 2;
        FaceTransform.CenterY = h / 2;
        LiftTransform.CenterX = w / 2;
        LiftTransform.CenterY = h / 2;
        BuildOverlay();
    }

    private void ApplyFacing()
    {
        FaceCanvas.Visibility = IsFaceUp ? Visibility.Visible : Visibility.Collapsed;
        BackCanvas.Visibility = IsFaceUp ? Visibility.Collapsed : Visibility.Visible;
        UpdateOverlayVisibility();
    }

    /// <summary>Rebuild all the vector art. Safe to call repeatedly.</summary>
    public void Rebuild()
    {
        FaceCanvas.Children.Clear();
        BackCanvas.Children.Clear();
        BuildBack();
        if (Card is not null) { BuildFace(Card); }
        ApplySize();
        ApplyFacing();
        ApplyOrientation(animate: false);
    }

    // ------------------------------------------------------------------ helpers

    private static Rectangle Rect(double x, double y, double w, double h, double radius,
                                  Brush? fill, Brush? stroke = null, double thickness = 0)
    {
        var r = new Rectangle
        {
            Width = w,
            Height = h,
            RadiusX = radius,
            RadiusY = radius,
            Fill = fill,
            Stroke = stroke,
            StrokeThickness = thickness,
        };
        Canvas.SetLeft(r, x);
        Canvas.SetTop(r, y);
        return r;
    }

    private static Ellipse Circle(double cx, double cy, double radius, Brush? fill,
                                  Brush? stroke = null, double thickness = 0)
    {
        var e = new Ellipse
        {
            Width = radius * 2,
            Height = radius * 2,
            Fill = fill,
            Stroke = stroke,
            StrokeThickness = thickness,
        };
        Canvas.SetLeft(e, cx - radius);
        Canvas.SetTop(e, cy - radius);
        return e;
    }

    private static TextBlock Label(string text, double x, double y, double width, double size,
                                   Brush foreground, TextAlignment align = TextAlignment.Left,
                                   bool bold = false, double lineHeight = 0, bool wrap = false)
    {
        var t = new TextBlock
        {
            Text = text,
            Width = width,
            FontSize = size,
            Foreground = foreground,
            TextAlignment = align,
            TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap,
            FontWeight = bold ? Microsoft.UI.Text.FontWeights.Bold : Microsoft.UI.Text.FontWeights.Normal,
            FontFamily = new FontFamily(bold ? Fonts.Bold : Fonts.Regular),
        };
        if (lineHeight > 0) { t.LineHeight = lineHeight; }
        Canvas.SetLeft(t, x);
        Canvas.SetTop(t, y);
        return t;
    }

    /// <summary>
    /// Render a stack of emblem layers authored in a 0..<paramref name="box"/> coordinate box
    /// into a fixed-size Canvas, ready for a Viewbox. Filled layers take <paramref name="fill"/>
    /// (accent layers take <paramref name="accent"/>); stroked layers take <paramref name="stroke"/>.
    /// </summary>
    internal static Canvas LayerCanvas(IReadOnlyList<EmblemLayer> layers, double box,
                                       Brush? fill, Brush? accent, Brush? stroke)
    {
        var canvas = new Canvas { Width = box, Height = box };
        if (layers is null) { return canvas; }

        foreach (var layer in layers)
        {
            if (layer is null || string.IsNullOrWhiteSpace(layer.PathData)) { continue; }
            var brush = layer.Accent ? (accent ?? fill) : fill;
            canvas.Children.Add(new Microsoft.UI.Xaml.Shapes.Path
            {
                Data = ParseGeometry(layer.PathData),
                Fill = layer.Filled ? brush : null,
                Stroke = layer.Filled ? null : (layer.Accent ? (accent ?? stroke) : stroke),
                StrokeThickness = layer.Filled ? 0 : Math.Max(0.4, layer.StrokeWidth),
                StrokeLineJoin = PenLineJoin.Round,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                Opacity = layer.Opacity <= 0 ? 1 : layer.Opacity,
            });
        }
        return canvas;
    }

    /// <summary>An ornament layer stack placed at a point, scaled into a square of the given size.</summary>
    internal static Viewbox PathBox(IReadOnlyList<EmblemLayer> layers, double cx, double cy, double size,
                                    double box, Brush? fill, Brush? accent = null, Brush? stroke = null,
                                    double opacity = 1, double scaleX = 1, double scaleY = 1)
    {
        var canvas = LayerCanvas(layers, box, fill, accent, stroke ?? fill);

        var vb = new Viewbox
        {
            Width = size,
            Height = size,
            Stretch = Stretch.Uniform,
            Child = canvas,
            Opacity = opacity,
            RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5),
        };
        if (scaleX != 1 || scaleY != 1)
        {
            vb.RenderTransform = new ScaleTransform { ScaleX = scaleX, ScaleY = scaleY };
        }
        Canvas.SetLeft(vb, cx - size / 2);
        Canvas.SetTop(vb, cy - size / 2);
        return vb;
    }

    /// <summary>Parse path mini-language into a Geometry (the XAML parser route, kept in one place).</summary>
    internal static Geometry ParseGeometry(string data)
    {
        if (string.IsNullOrWhiteSpace(data)) { return new PathGeometry(); }

        var parsed = TryParseGeometry(data);
        if (parsed is not null) { return parsed; }

        // This head's parser only accepts circular arcs; flatten every arc to beziers and retry.
        var flattened = Ornament.ArcsToBeziers(data);
        if (!ReferenceEquals(flattened, data))
        {
            parsed = TryParseGeometry(flattened);
            if (parsed is not null)
            {
                if (!_geometryFallbackLogged)
                {
                    _geometryFallbackLogged = true;
                    Log.LogWarning("Arcs flattened to beziers for this head's path parser.");
                }
                return parsed;
            }
        }

        Log.LogError("Geometry parse failed for \"{PathData}\".",
            data.Length <= 60 ? data : data[..60] + "\u2026");
        return new PathGeometry();
    }

    private static Geometry? TryParseGeometry(string data)
    {
        try { return (Geometry)Microsoft.UI.Xaml.Markup.XamlBindingHelper.ConvertValue(typeof(Geometry), data); }
        catch (Exception) { /* try the reader */ }

        try
        {
            const string ns = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
            var xaml = $"<Path xmlns=\"{ns}\" Data=\"{System.Security.SecurityElement.Escape(data)}\" />";
            if (Microsoft.UI.Xaml.Markup.XamlReader.Load(xaml) is Microsoft.UI.Xaml.Shapes.Path p && p.Data is not null)
            {
                return p.Data;
            }
        }
        catch (Exception) { /* caller reports */ }

        return null;
    }

    private static bool _geometryFallbackLogged;

    // ------------------------------------------------------------------ the face

    private void BuildFace(Card card)
    {
        var gold = Ornament.Brush(Ornament.Gold);
        var goldDeep = Ornament.Brush(Ornament.GoldDeep);
        var goldRule = Ornament.Brush(Ornament.GoldDeep, 0.55);
        var goldFaint = Ornament.Brush(Ornament.GoldDeep, 0.30);
        var secondary = Ornament.ToColor(card.SecondaryColor);

        // body + the lapis glow falling from the top
        FaceCanvas.Children.Add(Rect(0, 0, DW, DH, 16, Ornament.Brush(Ornament.Night2)));
        var glow = new Ellipse
        {
            Width = 232,
            Height = 196,
            Fill = new RadialGradientBrush
            {
                Center = new Windows.Foundation.Point(0.5, 0.5),
                GradientOrigin = new Windows.Foundation.Point(0.5, 0.45),
                RadiusX = 0.5,
                RadiusY = 0.5,
                GradientStops =
                {
                    new GradientStop { Color = Color.FromArgb(0x72, 0x3E, 0x59, 0xBE), Offset = 0 },
                    new GradientStop { Color = Color.FromArgb(0x00, 0x1A, 0x10, 0x30), Offset = 1 },
                },
            },
        };
        Canvas.SetLeft(glow, 9);
        Canvas.SetTop(glow, 6);
        FaceCanvas.Children.Add(glow);

        // double gold border
        FaceCanvas.Children.Add(Rect(4, 4, DW - 8, DH - 8, 13, null, gold, 2.5));
        FaceCanvas.Children.Add(Rect(12, 12, DW - 24, DH - 24, 8, null, goldRule, 1));

        AddCornerFlourishes();

        // numeral / suit band
        FaceCanvas.Children.Add(Label(Ornament.Track(card.NumeralLabel), 24, 30, 96, 10, goldDeep));
        FaceCanvas.Children.Add(Label(Ornament.TrackLight(card.SuitName), 96, 31, 130, 8.6, goldDeep, TextAlignment.Right));
        FaceCanvas.Children.Add(Rect(24, 52, DW - 48, 1.4, 0, goldRule));
        FaceCanvas.Children.Add(Rect(24, 56.5, DW - 48, 0.8, 0, goldFaint));

        // medallion
        const double mx = DW / 2, my = 166, mr = 74;
        FaceCanvas.Children.Add(Circle(mx, my, mr, new RadialGradientBrush
        {
            Center = new Windows.Foundation.Point(0.5, 0.5),
            GradientOrigin = new Windows.Foundation.Point(0.5, 0.42),
            RadiusX = 0.5,
            RadiusY = 0.5,
            GradientStops =
            {
                new GradientStop { Color = Ornament.Lighten(secondary, 0.22), Offset = 0 },
                new GradientStop { Color = secondary, Offset = 0.55 },
                new GradientStop { Color = Ornament.ToColor(Ornament.Night), Offset = 1 },
            },
        }, gold, 1.8));
        FaceCanvas.Children.Add(Circle(mx, my, mr - 7, null, goldFaint, 0.9));

        AddEmblem(card, mx, my, mr * 1.46);

        // name + epithet; the name shrinks for long names so the block always clears the
        // bottom band, whether it sets on one line or two
        var nameLength = card.Name?.Length ?? 0;
        var nameSize = nameLength > 18 ? 18.5 : nameLength > 13 ? 21.5 : 25;

        var stack = new StackPanel { Width = DW - 40, Spacing = 4 };
        stack.Children.Add(new TextBlock
        {
            Text = card.Name,
            FontSize = nameSize,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            FontFamily = new FontFamily(Fonts.Bold),
            Foreground = gold,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            LineHeight = Math.Round(nameSize * 1.16),
        });
        stack.Children.Add(new TextBlock
        {
            Text = card.Epithet,
            FontSize = 11.8,
            FontFamily = new FontFamily(Fonts.Regular),
            Foreground = Ornament.Brush(Ornament.Ivory, 0.68),
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 15,
        });
        Canvas.SetLeft(stack, 20);
        Canvas.SetTop(stack, 252);
        FaceCanvas.Children.Add(stack);

        // bottom band: hairline + three rosette dots
        FaceCanvas.Children.Add(Rect(24, 344, DW - 48, 0.8, 0, goldFaint));
        var dots = new Microsoft.UI.Xaml.Shapes.Path
        {
            Data = ParseGeometry(
                Ornament.RosettePath(105, 364, 7) + " " +
                Ornament.RosettePath(125, 364, 8.5) + " " +
                Ornament.RosettePath(145, 364, 7)),
            Fill = Ornament.Brush(Ornament.Gold, 0.8),
        };
        FaceCanvas.Children.Add(dots);
    }

    private void AddCornerFlourishes()
    {
        const double inset = 28;
        const double size = 32;
        var brush = Ornament.Brush(Ornament.GoldDeep, 0.9);
        var accent = Ornament.Brush(Ornament.Gold, 0.9);
        var art = EmblemArt.CornerFlourish;
        // top-left, top-right, bottom-left, bottom-right — mirrored into place
        FaceCanvas.Children.Add(PathBox(art, inset, inset, size, 30, brush, accent, brush, 1, 1, 1));
        FaceCanvas.Children.Add(PathBox(art, DW - inset, inset, size, 30, brush, accent, brush, 1, -1, 1));
        FaceCanvas.Children.Add(PathBox(art, inset, DH - inset, size, 30, brush, accent, brush, 1, 1, -1));
        FaceCanvas.Children.Add(PathBox(art, DW - inset, DH - inset, size, 30, brush, accent, brush, 1, -1, -1));
    }

    private void AddEmblem(Card card, double cx, double cy, double size)
    {
        IReadOnlyList<EmblemLayer> layers;
        try { layers = EmblemArt.Layers(card.Emblem); }
        catch (Exception) { return; }
        if (layers is null || layers.Count == 0) { return; }

        var accentBrush = new SolidColorBrush(Ornament.ToColor(card.AccentColor));
        var altBrush = Ornament.Brush(Ornament.GoldPale);
        var strokeBrush = Ornament.Brush(Ornament.Gold);

        var canvas = LayerCanvas(layers, 100, accentBrush, altBrush, strokeBrush);
        var vb = new Viewbox { Width = size, Height = size, Stretch = Stretch.Uniform, Child = canvas };
        Canvas.SetLeft(vb, cx - size / 2);
        Canvas.SetTop(vb, cy - size / 2);
        FaceCanvas.Children.Add(vb);
    }

    // ------------------------------------------------------------------ the back

    private void BuildBack()
    {
        var gold = Ornament.Brush(Ornament.Gold);
        var goldRule = Ornament.Brush(Ornament.GoldDeep, 0.55);
        var goldFaint = Ornament.Brush(Ornament.GoldDeep, 0.32);

        BackCanvas.Children.Add(Rect(0, 0, DW, DH, 16, Ornament.Brush(Ornament.Night)));
        BackCanvas.Children.Add(Rect(4, 4, DW - 8, DH - 8, 13, null, gold, 2.5));
        BackCanvas.Children.Add(Rect(12, 12, DW - 24, DH - 24, 8, null, goldRule, 1));

        const double cx = DW / 2, cy = 178;

        // lapis inner disc
        BackCanvas.Children.Add(Circle(cx, cy, 74, new RadialGradientBrush
        {
            Center = new Windows.Foundation.Point(0.5, 0.5),
            GradientOrigin = new Windows.Foundation.Point(0.5, 0.4),
            RadiusX = 0.5,
            RadiusY = 0.5,
            GradientStops =
            {
                new GradientStop { Color = Ornament.ToColor(Ornament.LapisLight), Offset = 0 },
                new GradientStop { Color = Ornament.ToColor(Ornament.Lapis), Offset = 0.6 },
                new GradientStop { Color = Ornament.ToColor(Ornament.Night), Offset = 1 },
            },
        }, goldRule, 1.2));

        // the Venus star
        BackCanvas.Children.Add(PathBox(EmblemArt.VenusStar, cx, cy, 128, 100,
            gold, Ornament.Brush(Ornament.GoldPale), gold));

        // ring of eight rosettes
        for (int i = 0; i < 8; i++)
        {
            var p = Ornament.Polar(cx, cy, 104, i * 45);
            BackCanvas.Children.Add(PathBox(EmblemArt.RosetteMotif, p.X, p.Y, 26, 100,
                Ornament.Brush(Ornament.GoldDeep, 0.9), Ornament.Brush(Ornament.Lapis), Ornament.Brush(Ornament.GoldDeep, 0.9)));
        }

        // faint outer circle
        BackCanvas.Children.Add(Circle(cx, cy, 122, null, goldFaint, 0.8));

        BackCanvas.Children.Add(Rect(46, 330, DW - 92, 0.8, 0, goldFaint));
        BackCanvas.Children.Add(Label(Ornament.Track("Rosette of Inanna"), 20, 344, DW - 40, 10.5,
            Ornament.Brush(Ornament.Gold, 0.92), TextAlignment.Center));
    }

    // ------------------------------------------------------------------ overlay

    private Border? _closeButton;
    private Microsoft.UI.Xaml.Shapes.Path? _reversedMarker;

    private void BuildOverlay()
    {
        Overlay.Children.Clear();
        _closeButton = null;
        _reversedMarker = null;

        var w = Shell.Width;

        // reversed marker: a small inverted triangle, top-left, never rotated with the card
        var marker = new Microsoft.UI.Xaml.Shapes.Path
        {
            Data = ParseGeometry("M 0,0 L 11,0 L 5.5,9 Z"),
            Fill = Ornament.Brush(Ornament.CarnelianLight),
            Stroke = Ornament.Brush(Ornament.GoldPale, 0.8),
            StrokeThickness = 0.8,
            Visibility = Visibility.Collapsed,
        };
        Canvas.SetLeft(marker, 7);
        Canvas.SetTop(marker, 7);
        Overlay.Children.Add(marker);
        _reversedMarker = marker;

        // hover × returns the card to the tray
        var close = new Border
        {
            Width = 18,
            Height = 18,
            CornerRadius = new CornerRadius(9),
            Background = Ornament.Brush(Ornament.Kohl, 0.88),
            BorderBrush = Ornament.Brush(Ornament.Gold, 0.9),
            BorderThickness = new Thickness(1),
            Visibility = Visibility.Collapsed,
            Child = new TextBlock
            {
                Text = "×",
                FontSize = 12,
                FontFamily = new FontFamily(Fonts.Regular),
                Foreground = Ornament.Brush(Ornament.Gold),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };
        close.PointerPressed += (_, e) =>
        {
            e.Handled = true;
            CloseRequested?.Invoke(this, EventArgs.Empty);
        };
        Canvas.SetLeft(close, w - 22);
        Canvas.SetTop(close, 4);
        Overlay.Children.Add(close);
        _closeButton = close;

        UpdateOverlayVisibility();
    }

    private void UpdateOverlayVisibility()
    {
        if (_reversedMarker is not null)
        {
            _reversedMarker.Visibility = IsReversed && IsFaceUp ? Visibility.Visible : Visibility.Collapsed;
        }
        if (_closeButton is not null)
        {
            _closeButton.Visibility = ShowCloseButton && _hovering ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}

/// <summary>Font URIs, in one place.</summary>
internal static class Fonts
{
    public const string Regular = "ms-appx:///CodeBrix.Platform.Fonts.Merriweather/Fonts/Merriweather.ttf";
    public const string Bold = "ms-appx:///CodeBrix.Platform.Fonts.Merriweather/Fonts/Merriweather-Bold.ttf";
}
