using System;
using CodeBrix.Android.UI.Android;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI.Text;
using ABitmapDrawable = global::Android.Graphics.Drawables.BitmapDrawable;
using ACanvas = global::Android.Graphics.Canvas;
using AColor = global::Android.Graphics.Color;
using AColorFilter = global::Android.Graphics.ColorFilter;
using AContext = global::Android.Content.Context;
using ADrawable = global::Android.Graphics.Drawables.Drawable;
using AFormat = global::Android.Graphics.Format;
using APaint = global::Android.Graphics.Paint;
using APaintFlags = global::Android.Graphics.PaintFlags;
using ARect = global::Android.Graphics.Rect;
using ATypeface = global::Android.Graphics.Typeface;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>
/// A glyph of an icon font as an Android drawable (plan 2.13 FontIconDrawable): what a native icon slot
/// (a menu item, a Material button icon, a text field end icon) shows for a FontIcon / SymbolIcon /
/// FontIconSource. The glyph is centred in the drawable's bounds, as large as the icon's font size.
/// </summary>
internal sealed class FontIconDrawable : ADrawable
{
    private readonly APaint _paint = new(APaintFlags.AntiAlias);
    private readonly string _glyph;
    private readonly int _size;

    /// <summary>Creates the drawable.</summary>
    /// <param name="glyph">The glyph text.</param>
    /// <param name="typeface">The icon font.</param>
    /// <param name="sizePx">The font size (and intrinsic size) in pixels.</param>
    /// <param name="color">The ARGB colour.</param>
    internal FontIconDrawable(string glyph, ATypeface typeface, int sizePx, int color)
    {
        _glyph = glyph ?? string.Empty;
        _size = Math.Max(1, sizePx);
        _paint.TextSize = _size;
        _paint.TextAlign = APaint.Align.Center;
        _paint.Color = new AColor(color);
        if (typeface != null)
        {
            _paint.SetTypeface(typeface);
        }
    }

    /// <summary>The glyph drawn.</summary>
    internal string Glyph => _glyph;

    /// <inheritdoc />
    public override int IntrinsicWidth => _size;

    /// <inheritdoc />
    public override int IntrinsicHeight => _size;

    /// <inheritdoc />
    public override int Opacity => (int)AFormat.Translucent;

    /// <inheritdoc />
    public override void Draw(ACanvas canvas)
    {
        var bounds = Bounds;
        var metrics = _paint.GetFontMetrics();
        var x = bounds.ExactCenterX();
        var y = bounds.ExactCenterY() - ((metrics.Ascent + metrics.Descent) / 2f);
        canvas.DrawText(_glyph, x, y, _paint);
    }

    /// <inheritdoc />
    public override void SetAlpha(int alpha)
    {
        _paint.Alpha = alpha;
        InvalidateSelf();
    }

    /// <inheritdoc />
    public override void SetColorFilter(AColorFilter colorFilter)
    {
        _paint.SetColorFilter(colorFilter);
        InvalidateSelf();
    }
}

/// <summary>
/// Drawables for the IconElement / IconSource family (plan 3 rows IconElement, IconSourceElement,
/// FontIcon, SymbolIcon): a FontIcon / SymbolIcon / FontIconSource / SymbolIconSource glyph in its icon
/// font (the Fluent symbols font for symbols), a BitmapIcon / BitmapIconSource / ImageIconSource bitmap
/// when Core has decoded it. (The icon ELEMENTS themselves render through Core's own composition - a Grid
/// holding a TextBlock glyph - which the panel and TextBlock handlers show natively.)
/// </summary>
internal static class IconDrawables
{
    /// <summary>The drawable of an icon element or icon source, or null when it has none on Android.</summary>
    /// <param name="icon">An IconElement or IconSource.</param>
    /// <param name="context">The context.</param>
    /// <param name="density">Pixels per DIP.</param>
    /// <param name="defaultColor">The ARGB colour when the icon sets no Foreground.</param>
    /// <param name="defaultSizeDips">The size when the icon sets none, in DIPs.</param>
    /// <returns>The drawable, or null.</returns>
    internal static ADrawable Create(object icon, AContext context, double density, int defaultColor, double defaultSizeDips = 20)
    {
        switch (icon)
        {
            case FontIcon font:
                return Glyph(font.Glyph, font.FontFamily ?? SymbolFont(font), font.FontWeight, font.FontStyle, font.FontSize, font.Foreground, density, defaultColor, defaultSizeDips);
            case SymbolIcon symbol:
                return Glyph(SymbolGlyph(symbol.Symbol), SymbolFont(symbol), FontWeights.Normal, FontStyle.Normal, double.NaN, symbol.Foreground, density, defaultColor, defaultSizeDips);
            case FontIconSource fontSource:
                return Glyph(fontSource.Glyph, fontSource.FontFamily ?? SymbolFont(null), fontSource.FontWeight, fontSource.FontStyle, fontSource.FontSize, fontSource.Foreground, density, defaultColor, defaultSizeDips);
            case SymbolIconSource symbolSource:
                return Glyph(SymbolGlyph(symbolSource.Symbol), SymbolFont(null), FontWeights.Normal, FontStyle.Normal, double.NaN, symbolSource.Foreground, density, defaultColor, defaultSizeDips);
            case BitmapIcon bitmapIcon when ImageHandler.BitmapOf(bitmapIcon) is { } bitmap:
                return new ABitmapDrawable(context.Resources, bitmap);
            default:
                return null;
        }
    }

    /// <summary>The glyph of a Symbol in the symbols font.</summary>
    /// <param name="symbol">The symbol.</param>
    /// <returns>The glyph text.</returns>
    internal static string SymbolGlyph(Symbol symbol) => SymbolIcon.ConvertSymbolValueToGlyph((int)symbol).ToString();

    private static FontFamily SymbolFont(DependencyObject scope) =>
        ThemeResources.TryFind(scope, "SymbolThemeFontFamily", out var value) && value is FontFamily family ? family : new FontFamily("Segoe Fluent Icons");

    private static ADrawable Glyph(string glyph, FontFamily family, FontWeight weight, FontStyle style, double size, Brush foreground, double density, int defaultColor, double defaultSize)
    {
        if (string.IsNullOrEmpty(glyph))
        {
            return null;
        }

        var typeface = AndroidPlatformBootstrap.Fonts?.Resolve(family, weight, style, FontStretch.Normal);
        var dips = double.IsNaN(size) || size <= 0 ? defaultSize : size;
        var color = ThemeResources.ColorOf(foreground) ?? defaultColor;
        return new FontIconDrawable(glyph, typeface, (int)Math.Round(dips * density), color);
    }
}
