using System;
using CodeBrix.Android.UI.Portable;
using CodeBrix.Platform.UI.Contracts;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;
using ALayout = global::Android.Text.Layout;
using APaintFlags = global::Android.Graphics.PaintFlags;
using AStaticLayout = global::Android.Text.StaticLayout;
using ATextPaint = global::Android.Text.TextPaint;
using ATruncateAt = global::Android.Text.TextUtils.TruncateAt;

namespace CodeBrix.Android.UI.Android;

/// <summary>
/// The Android implementation of <see cref="ITextPlatform"/>. <see cref="CreateLayout"/> is the
/// TextBlock leaf measure: a <see cref="ATextPaint"/> from the resolved typeface, font size
/// and character spacing, and a <see cref="AStaticLayout"/> with the TextBlock's wrapping,
/// trimming, maximum line count and alignment; the result is converted to DIPs. The native
/// TextView that shows the TextBlock (AP3a) is configured from the same paint so measure and
/// draw agree. TextBox gets the minimal <see cref="TextBoxAndroidPlatform"/>.
/// </summary>
internal sealed class TextAndroidPlatform : ITextPlatform
{
    private readonly FontAndroidPlatform _fonts;
    private readonly Func<double> _defaultDensity;

    internal TextAndroidPlatform(FontAndroidPlatform fonts, Func<double> defaultDensity)
    {
        _fonts = fonts;
        _defaultDensity = defaultDensity;
    }

    /// <inheritdoc />
    public ITextLayout EmptyLayout => EmptyTextLayout.Instance;

    /// <inheritdoc />
    public ITextLayout CreateLayout(TextBlock textBlock, Size availableSize, out Size desiredSize)
    {
        ArgumentNullException.ThrowIfNull(textBlock);

        var density = GetDensity(textBlock);
        var text = textBlock.Text ?? string.Empty;
        using var paint = CreatePaint(textBlock, density);

        var wraps = textBlock.TextWrapping != TextWrapping.NoWrap;
        var trims = textBlock.TextTrimming != TextTrimming.None;
        var availablePx = TextMeasureMath.AvailableWidthToPx(availableSize.Width, density);
        var naturalWidthPx = (int)Math.Ceiling(ALayout.GetDesiredWidth(text, paint));

        // Unwrapped text is as wide as its longest line, clipped (and ellipsized when trimming) to the available width.
        var layoutWidthPx = wraps ? Math.Min(naturalWidthPx, availablePx) : naturalWidthPx;
        var maxLines = textBlock.MaxLines > 0 ? textBlock.MaxLines : int.MaxValue;
        if (!wraps && trims && naturalWidthPx > availablePx)
        {
            layoutWidthPx = availablePx;
        }

        layoutWidthPx = Math.Max(0, layoutWidthPx);
        var builder = AStaticLayout.Builder.Obtain(text, 0, text.Length, paint, layoutWidthPx)
            .SetAlignment(ToAndroidAlignment(textBlock.TextAlignment))
            .SetIncludePad(false)
            .SetMaxLines(wraps ? maxLines : 1);

        if (trims)
        {
            builder.SetEllipsize(ATruncateAt.End);
        }

        var layout = builder.Build();

        var widthPx = 0f;
        for (var line = 0; line < layout.LineCount; line++)
        {
            widthPx = Math.Max(widthPx, layout.GetLineWidth(line));
        }

        if (!wraps && !trims)
        {
            widthPx = naturalWidthPx;
        }

        desiredSize = new Size(
            TextMeasureMath.PxToDip(Math.Ceiling(widthPx), density),
            TextMeasureMath.PxToDip(layout.Height, density));

        return new AndroidTextLayout(layout, text, density);
    }

    /// <inheritdoc />
    public ITextBoxPlatform CreateTextBoxPlatform(TextBox textBox) => new TextBoxAndroidPlatform(textBox);

    /// <summary>Builds the paint a TextBlock is measured (and drawn) with.</summary>
    internal ATextPaint CreatePaint(TextBlock textBlock, double density)
    {
        var paint = new ATextPaint(APaintFlags.AntiAlias)
        {
            TextSize = TextMeasureMath.DipToPx(textBlock.FontSize, density),
            LetterSpacing = TextMeasureMath.CharacterSpacingToEm(textBlock.CharacterSpacing),
        };
        paint.SetTypeface(_fonts.Resolve(textBlock.FontFamily, textBlock.FontWeight, textBlock.FontStyle, textBlock.FontStretch));
        return paint;
    }

    private double GetDensity(UIElement element)
    {
        var scale = element.XamlRoot?.RasterizationScale ?? 0;
        return scale > 0 ? scale : _defaultDensity();
    }

    private static ALayout.Alignment ToAndroidAlignment(TextAlignment alignment) => alignment switch
    {
        TextAlignment.Center => ALayout.Alignment.AlignCenter,
        TextAlignment.Right or TextAlignment.End => ALayout.Alignment.AlignOpposite,
        _ => ALayout.Alignment.AlignNormal,
    };
}
