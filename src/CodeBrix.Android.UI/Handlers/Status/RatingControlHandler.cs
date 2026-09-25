using System;
using System.Collections.Generic;
using CodeBrix.Android.UI.Android;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using AAppCompatRatingBar = AndroidX.AppCompat.Widget.AppCompatRatingBar;
using ABitmap = global::Android.Graphics.Bitmap;
using ABitmapShader = global::Android.Graphics.BitmapShader;
using ACanvas = global::Android.Graphics.Canvas;
using AClipDrawable = global::Android.Graphics.Drawables.ClipDrawable;
using AColor = global::Android.Graphics.Color;
using AContext = global::Android.Content.Context;
using ADrawable = global::Android.Graphics.Drawables.Drawable;
using AGravityFlags = global::Android.Views.GravityFlags;
using ALayerDrawable = global::Android.Graphics.Drawables.LayerDrawable;
using APaint = global::Android.Graphics.Paint;
using APaintFlags = global::Android.Graphics.PaintFlags;
using ARectShape = global::Android.Graphics.Drawables.Shapes.RectShape;
using AShaderTileMode = global::Android.Graphics.Shader.TileMode;
using AShapeDrawable = global::Android.Graphics.Drawables.ShapeDrawable;
using ATypeface = global::Android.Graphics.Typeface;
using AView = global::Android.Views.View;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>
/// AP10-B: the handler of RatingControl (tsv row RatingControl: "AppCompatRatingBar"). The RatingControl keeps its
/// Fluent template (Value, PlaceholderValue, ValueChanged, the Caption, keyboard and the template's stars answering
/// Core input stay Core's); its two rows of stars (RatingBackgroundStackPanel and RatingForegroundStackPanel, laid out
/// by Core, not drawn) are shown by a native AppCompatRatingBar over them. The bar's stars are the template's own star
/// glyphs, fonts and colours rendered into its tiles, one tile per star cell of the template's row, so the native bar
/// is exactly as large as the row it replaces. A finger on the bar shows at once and lands, through Core, on the template's
/// stars under it, which set Value by RatingControl's own rules (Core raises ValueChanged; the bar then shows Core's
/// value); an accessibility adjustment of the bar sets Value itself. IsReadOnly makes it an indicator.
/// </summary>
internal sealed class RatingControlHandler : TemplateOverlayHandler<RatingControl, AAppCompatRatingBar>
{
    /// <summary>RatingControl's mapper.</summary>
    public static readonly PropertyMapper<RatingControl, RatingControlHandler> Mapper = new(ViewMappers.ViewMapper)
    {
        [RatingControl.ValueProperty] = MapOverlay,
        [RatingControl.PlaceholderValueProperty] = MapOverlay,
        [RatingControl.MaxRatingProperty] = MapOverlay,
        [RatingControl.IsReadOnlyProperty] = MapOverlay,
        [RatingControl.IsClearEnabledProperty] = MapOverlay,
        [RatingControl.ItemInfoProperty] = MapOverlay,
        [Control.IsEnabledProperty] = MapOverlay,
        [Control.ForegroundProperty] = MapOverlay,
    };

    private bool _updating;
    private long _lastTouch;
    private string _tileKey;

    /// <summary>Creates the handler.</summary>
    public RatingControlHandler()
        : base(Mapper)
    {
    }

    /// <summary>The handler, or the templated fallback for a re-templated RatingControl.</summary>
    /// <param name="element">The RatingControl.</param>
    /// <returns>The handler.</returns>
    internal static IAndroidElementHandler Create(UIElement element) =>
        element is RatingControl rating && NativeControlPolicy.IsNative(rating, typeof(RatingControl), new[] { "DefaultRatingControlStyle" }, out _)
            ? new RatingControlHandler()
            : new TemplatedFallbackHandler();

    /// <inheritdoc />
    protected override AAppCompatRatingBar CreateOverlay(AContext context)
    {
        var bar = new AAppCompatRatingBar(context)
        {
            StepSize = 1f,
            ContentDescription = "Rating",
        };
        bar.SetMinimumHeight(0);
        bar.SetMinimumWidth(0);
        bar.SetPadding(0, 0, 0, 0);
        bar.RatingBarChange += OnRatingBarChange;
        bar.Touch += OnWidgetTouch;
        return bar;
    }

    /// <inheritdoc />
    protected override bool IsOverlayShown(RatingControl element) => element.MaxRating > 0 && Background(element) != null;

    /// <inheritdoc />
    protected override IReadOnlyList<FrameworkElement> CoveredParts(RatingControl element) =>
        new[] { Background(element), FindNamed(element, "RatingForegroundStackPanel") };

    /// <inheritdoc />
    protected override void UpdateOverlay(RatingControl element, AAppCompatRatingBar bar)
    {
        var row = Background(element);
        var stars = Math.Max(1, element.MaxRating);
        var density = Density;
        var cellWidth = (int)Math.Round(row.ActualWidth / stars * density);
        var cellHeight = (int)Math.Round(row.ActualHeight * density);
        var unselected = FirstStar(row);
        var selected = FirstStar(FindNamed(element, "RatingForegroundStackPanel")) ?? unselected;
        var key = $"{cellWidth}x{cellHeight}|{Describe(unselected)}|{Describe(selected)}";
        if (key != _tileKey && cellWidth > 0 && cellHeight > 0)
        {
            _tileKey = key;
            bar.ProgressDrawable = Tiles(
                Star(unselected, cellWidth, cellHeight, density, PagingWidgets.Role(bar.Context, "colorOutline", unchecked((int)0xFF79747E))),
                Star(selected, cellWidth, cellHeight, density, PagingWidgets.Role(bar.Context, "colorPrimary", unchecked((int)0xFF6750A4))));
        }

        _updating = true;
        try
        {
            bar.NumStars = stars;
            bar.Max = stars;
            bar.Rating = StatusMath.ShownRating(element.Value, element.PlaceholderValue, stars);
            bar.IsIndicator = element.IsReadOnly || !element.IsEnabled;
            bar.Enabled = element.IsEnabled;
            bar.ContentDescription = $"Rating {Math.Max(0, element.Value)} of {stars}";
        }
        finally
        {
            _updating = false;
        }
    }

    /// <inheritdoc />
    protected override void DisconnectHandler(TemplateOverlayHostView platformView)
    {
        if (platformView.NativeOverlay is AAppCompatRatingBar bar)
        {
            bar.RatingBarChange -= OnRatingBarChange;
            bar.Touch -= OnWidgetTouch;
        }

        base.DisconnectHandler(platformView);
    }

    private static FrameworkElement Background(RatingControl element) => FindNamed(element, "RatingBackgroundStackPanel");

    private static TextBlock FirstStar(FrameworkElement row) =>
        row != null && VisualTreeHelper.GetChildrenCount(row) > 0 ? VisualTreeHelper.GetChild(row, 0) as TextBlock : null;

    private static string Describe(TextBlock star) =>
        star == null ? "-" : $"{star.Text}|{star.FontSize}|{ThemeResources.ColorOf(star.Foreground)}|{star.FontFamily?.Source}";

    private static ABitmap Star(TextBlock star, int width, int height, double density, int fallbackColor)
    {
        var bitmap = ABitmap.CreateBitmap(width, height, ABitmap.Config.Argb8888);
        using var canvas = new ACanvas(bitmap);
        using var paint = new APaint(APaintFlags.AntiAlias) { TextAlign = APaint.Align.Center };
        var glyph = string.IsNullOrEmpty(star?.Text) ? "\uE735" : star.Text;
        paint.Color = new AColor(ThemeResources.ColorOf(star?.Foreground) ?? fallbackColor);
        paint.TextSize = (float)((star?.FontSize ?? 20) * density);
        var family = star?.FontFamily ?? new FontFamily("Segoe Fluent Icons");
        paint.SetTypeface(AndroidPlatformBootstrap.Fonts?.Resolve(family, Microsoft.UI.Text.FontWeights.Normal, Windows.UI.Text.FontStyle.Normal, Windows.UI.Text.FontStretch.Normal) ?? ATypeface.Default);
        var baseline = (height / 2f) - ((paint.Descent() + paint.Ascent()) / 2f);
        canvas.DrawText(glyph, width / 2f, baseline, paint);
        return bitmap;
    }

    // ProgressBar tiles its drawables only when it is inflated from XML; one built in code is tiled here the same way
    // (a repeating bitmap shader, the progress layer clipped horizontally).
    private static ADrawable Tiles(ABitmap unselected, ABitmap selected)
    {
        var background = Tile(unselected);
        var progress = new AClipDrawable(Tile(selected), AGravityFlags.Left, global::Android.Graphics.Drawables.ClipDrawableOrientation.Horizontal);
        var secondary = new AClipDrawable(Tile(unselected), AGravityFlags.Left, global::Android.Graphics.Drawables.ClipDrawableOrientation.Horizontal);
        var layers = new ALayerDrawable(new ADrawable[] { background, secondary, progress });
        layers.SetId(0, global::Android.Resource.Id.Background);
        layers.SetId(1, global::Android.Resource.Id.SecondaryProgress);
        layers.SetId(2, global::Android.Resource.Id.Progress);
        return layers;
    }

    private static AShapeDrawable Tile(ABitmap bitmap)
    {
        var drawable = new AShapeDrawable(new ARectShape());
        drawable.Paint.SetShader(new ABitmapShader(bitmap, AShaderTileMode.Repeat, AShaderTileMode.Clamp));
        return drawable;
    }

    private void OnWidgetTouch(object sender, AView.TouchEventArgs e)
    {
        // The bar shows the finger at once; the same touch reaches Core, where it lands on the template's stars under
        // the bar, which set Value (RatingControl's own rules); the bar then shows Core's value.
        e.Handled = false;
        _lastTouch = global::Android.OS.SystemClock.UptimeMillis();
    }

    private void OnRatingBarChange(object sender, global::Android.Widget.RatingBar.RatingBarChangeEventArgs e)
    {
        if (_updating || !e.FromUser || Element is not RatingControl element || element.IsReadOnly)
        {
            return;
        }

        if (global::Android.OS.SystemClock.UptimeMillis() - _lastTouch < 1000)
        {
            // A touch: Core rates with the same touch.
            PostRefresh();
            return;
        }

        // Not a touch (an accessibility adjustment): rate here.

        element.Value = StatusMath.ValueFromRating(e.Rating, element.Value, element.IsClearEnabled);
        PostRefresh();
    }
}
