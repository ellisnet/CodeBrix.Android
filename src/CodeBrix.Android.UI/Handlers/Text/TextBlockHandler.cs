using System;
using System.Text;
using CodeBrix.Android.UI.Android;
using CodeBrix.Android.UI.Platform;
using CodeBrix.Android.UI.Portable.Drawing;
using CodeBrix.Android.UI.Portable.Layout;
using CodeBrix.Android.UI.Portable.Projection;
using CodeBrix.Platform.UI.Contracts;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Windows.UI.Text;
using ABreakStrategy = global::Android.Text.BreakStrategy;
using AColor = global::Android.Graphics.Color;
using AComplexUnitType = global::Android.Util.ComplexUnitType;
using AGravityFlags = global::Android.Views.GravityFlags;
using AJustificationMode = global::Android.Text.JustificationMode;
using ALinkMovementMethod = global::Android.Text.Method.LinkMovementMethod;
using AView = global::Android.Views.View;
using ATextView = global::Android.Widget.TextView;
using ATruncateAt = global::Android.Text.TextUtils.TruncateAt;
using AAbsoluteSizeSpan = global::Android.Text.Style.AbsoluteSizeSpan;
using AForegroundColorSpan = global::Android.Text.Style.ForegroundColorSpan;
using APaintFlags = global::Android.Graphics.PaintFlags;
using ASpannableStringBuilder = global::Android.Text.SpannableStringBuilder;
using ASpanTypes = global::Android.Text.SpanTypes;
using AStrikethroughSpan = global::Android.Text.Style.StrikethroughSpan;
using ATypefaceSpan = global::Android.Text.Style.TypefaceSpan;
using AUnderlineSpan = global::Android.Text.Style.UnderlineSpan;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>
/// The TextBlock handler: a natively MEASURED leaf (MeasuresNatively) - Core asks the TextView for its
/// size inside Core's measure and places it where Core arranged the TextBlock. Text, font, colour,
/// wrapping, trimming, alignment, padding, TextDecorations, LineHeight (+ LineStackingStrategy) and
/// IsTextSelectionEnabled are mapped; Inlines (Run, Span, Bold, Italic, Underline, LineBreak, Hyperlink)
/// become one Android spannable whose spans carry each run's effective Foreground, FontSize, font
/// (family/weight/style/stretch) and TextDecorations (Hyperlink: underlined unless its UnderlineStyle
/// says None; clickable - a ClickableSpan that runs Core's Hyperlink.OnClick, AP3a). The text view setup (simple break strategy, no font padding, no
/// fallback line spacing) is the AP1 projection's, which made native text agree with the StaticLayout
/// measure. Inlines are re-read at every native measure, because an inline change invalidates the
/// TextBlock's measure in Core (a Run is not an element: it has no handler of its own).
/// </summary>
internal sealed class TextBlockHandler : ViewHandler<TextBlock, ATextView>
{
    /// <summary>TextBlock's mapper.</summary>
    public static readonly PropertyMapper<TextBlock, TextBlockHandler> Mapper = new(ViewMappers.ViewMapper)
    {
        [TextBlock.TextProperty] = MapText,
        [TextBlock.ForegroundProperty] = MapForeground,
        [TextBlock.FontFamilyProperty] = MapFont,
        [TextBlock.FontSizeProperty] = MapFont,
        [TextBlock.FontWeightProperty] = MapFont,
        [TextBlock.FontStyleProperty] = MapFont,
        [TextBlock.FontStretchProperty] = MapFont,
        [TextBlock.CharacterSpacingProperty] = MapFont,
        [TextBlock.TextWrappingProperty] = MapTextStyle,
        [TextBlock.TextTrimmingProperty] = MapTextStyle,
        [TextBlock.MaxLinesProperty] = MapTextStyle,
        [TextBlock.TextAlignmentProperty] = MapTextStyle,
        [TextBlock.PaddingProperty] = MapPadding,
        [TextBlock.TextDecorationsProperty] = MapTextDecorations,
        [TextBlock.LineHeightProperty] = MapLineHeight,
        [TextBlock.LineStackingStrategyProperty] = MapLineHeight,
        [TextBlock.IsTextSelectionEnabledProperty] = MapIsTextSelectionEnabled,
    };

    private readonly BrushWatcher _foregroundWatcher;
    private readonly CorePointerBridge _linkBridge;
    private bool _hasLinks;

    /// <summary>Creates the handler.</summary>
    public TextBlockHandler()
        : base(Mapper)
    {
        _linkBridge = new CorePointerBridge(() => NativeView);
        _foregroundWatcher = new BrushWatcher(() =>
        {
            if (Element is TextBlock text)
            {
                MapForeground(this, text);
            }
        });
    }

    /// <inheritdoc />
    public override ElementHandlerCapabilities Capabilities => ElementHandlerCapabilities.MeasuresNatively;

    /// <summary>Maps Text (and the Inlines: formatted runs become a spannable).</summary>
    public static void MapText(TextBlockHandler handler, TextBlock element) => handler.UpdateText(element);

    /// <summary>Maps TextDecorations (underline / strikethrough of the whole block).</summary>
    public static void MapTextDecorations(TextBlockHandler handler, TextBlock element)
    {
        var view = handler.PlatformView;
        var flags = view.PaintFlags & ~(APaintFlags.UnderlineText | APaintFlags.StrikeThruText);
        if ((element.TextDecorations & TextDecorations.Underline) != 0)
        {
            flags |= APaintFlags.UnderlineText;
        }

        if ((element.TextDecorations & TextDecorations.Strikethrough) != 0)
        {
            flags |= APaintFlags.StrikeThruText;
        }

        view.PaintFlags = flags;
    }

    /// <summary>
    /// Maps LineHeight and LineStackingStrategy: BlockLineHeight and BaselineToBaseline give every line
    /// exactly LineHeight; MaxHeight gives a line at least LineHeight (never less than the font's own).
    /// </summary>
    public static void MapLineHeight(TextBlockHandler handler, TextBlock element)
    {
        var view = handler.PlatformView;
        var lineHeight = element.LineHeight;
        if (double.IsNaN(lineHeight) || lineHeight <= 0)
        {
            view.SetLineSpacing(0, 1);
            return;
        }

        var pixels = LayoutReplayMath.ToPixels(lineHeight, handler.Density);
        if (element.LineStackingStrategy == LineStackingStrategy.MaxHeight)
        {
            var natural = view.Paint.GetFontMetricsInt(null);
            if (pixels <= natural)
            {
                view.SetLineSpacing(0, 1);
                return;
            }
        }

        view.LineHeight = pixels;
    }

    /// <summary>Maps IsTextSelectionEnabled (native selection: long press, handles, the copy menu).</summary>
    public static void MapIsTextSelectionEnabled(TextBlockHandler handler, TextBlock element)
    {
        if (handler.PlatformView.IsTextSelectable != element.IsTextSelectionEnabled)
        {
            handler.PlatformView.SetTextIsSelectable(element.IsTextSelectionEnabled);
        }
    }

    /// <summary>Maps Foreground (the single colour of the brush; the brush's content is watched).</summary>
    public static void MapForeground(TextBlockHandler handler, TextBlock element)
    {
        handler._foregroundWatcher.Watch(element.Foreground);
        handler.PlatformView.SetTextColor(new AColor(BrushPaint.SingleColor(element.Foreground, unchecked((int)0xFF000000))));
    }

    /// <summary>Maps the font (family, size, weight, style, stretch) and CharacterSpacing.</summary>
    public static void MapFont(TextBlockHandler handler, TextBlock element)
    {
        var view = handler.PlatformView;
        var typeface = AndroidPlatformBootstrap.Fonts?.Resolve(element.FontFamily, element.FontWeight, element.FontStyle, element.FontStretch);
        if (typeface != null && !ReferenceEquals(view.Typeface, typeface))
        {
            view.Typeface = typeface;
        }

        view.SetTextSize(AComplexUnitType.Px, (float)(element.FontSize * handler.Density));
        view.LetterSpacing = element.CharacterSpacing / 1000f;
    }

    /// <summary>Maps wrapping, trimming, MaxLines and alignment.</summary>
    public static void MapTextStyle(TextBlockHandler handler, TextBlock element)
    {
        var view = handler.PlatformView;
        var style = ProjectionTextStyle.From(element.TextAlignment, element.TextWrapping, element.TextTrimming, element.MaxLines);
        var horizontal = style.Alignment switch
        {
            ProjectionTextAlignment.Center => AGravityFlags.CenterHorizontal,
            ProjectionTextAlignment.End => AGravityFlags.End,
            _ => AGravityFlags.Start,
        };
        view.Gravity = horizontal | AGravityFlags.Top;
        view.JustificationMode = element.TextAlignment == TextAlignment.Justify ? AJustificationMode.InterWord : AJustificationMode.None;
        view.SetHorizontallyScrolling(style.SingleLine);
        view.SetMaxLines(style.MaxLines);
        view.Ellipsize = style.Ellipsize ? ATruncateAt.End : null;
    }

    /// <summary>Maps Padding to the view's padding.</summary>
    public static void MapPadding(TextBlockHandler handler, TextBlock element)
    {
        var density = handler.Density;
        var padding = element.Padding;
        handler.PlatformView.SetPadding(
            LayoutReplayMath.ToPixels(padding.Left, density),
            LayoutReplayMath.ToPixels(padding.Top, density),
            LayoutReplayMath.ToPixels(padding.Right, density),
            LayoutReplayMath.ToPixels(padding.Bottom, density));
    }

    /// <inheritdoc />
    public override Size Measure(Size availableSize)
    {
        if (Element is TextBlock element)
        {
            // An inline change invalidates the TextBlock's measure: re-read the inlines first.
            UpdateText(element);
        }

        return ViewHandlerExtensions.GetDesiredSizeFromView(NativeView, availableSize, Density);
    }

    private string _plainText;
    private string _spannedSignature;
    private BuildState _building = new();

    private void UpdateText(TextBlock element)
    {
        var view = PlatformView;
        if (!TryBuildSpanned(element, out var spanned, out var signature))
        {
            var text = element.Text ?? string.Empty;
            if (_spannedSignature != null || !string.Equals(_plainText, text, StringComparison.Ordinal) || !string.Equals(view.Text, text, StringComparison.Ordinal))
            {
                _spannedSignature = null;
                _plainText = text;
                view.Text = text;
            }

            SetLinks(false);
            return;
        }

        if (!string.Equals(_spannedSignature, signature, StringComparison.Ordinal))
        {
            _spannedSignature = signature;
            _plainText = null;
            view.SetText(spanned, global::Android.Widget.TextView.BufferType.Spannable);
        }

        SetLinks(_building.HasLinks);
    }

    /// <summary>
    /// Makes the hyperlinks clickable (LinkMovementMethod), or restores the plain view. Core-only pointers
    /// on a TextBlock with hyperlinks are forwarded to the view so an injected tap follows the link too.
    /// </summary>
    private void SetLinks(bool hasLinks)
    {
        if (_hasLinks == hasLinks)
        {
            return;
        }

        _hasLinks = hasLinks;
        var view = PlatformView;
        if (hasLinks)
        {
            view.MovementMethod = ALinkMovementMethod.Instance;
            view.Focusable = false;
            view.FocusableInTouchMode = false;
            view.Touch += OnLinkTouch;
            if (Element != null)
            {
                _linkBridge.Attach(Element);
            }
        }
        else
        {
            _linkBridge.Detach();
            view.Touch -= OnLinkTouch;
            if (!view.IsTextSelectable)
            {
                view.MovementMethod = null;
                view.Clickable = false;
                view.LongClickable = false;
                view.Focusable = false;
            }
        }
    }

    private void OnLinkTouch(object sender, AView.TouchEventArgs e)
    {
        e.Handled = false;
        _linkBridge.OnNativeTouch(sender as AView, e.Event);
    }

    /// <summary>
    /// Builds the spannable of a TextBlock whose inlines carry formatting of their own; false for plain
    /// text (no inlines, or one Run that looks like the block).
    /// </summary>
    private bool TryBuildSpanned(TextBlock element, out ASpannableStringBuilder spanned, out string signature)
    {
        spanned = null;
        signature = null;
        InlineCollection inlines;
        try
        {
            inlines = element.Inlines;
        }
        catch (InvalidOperationException)
        {
            return false;
        }

        if (inlines == null || inlines.Count == 0 || (inlines.Count == 1 && inlines[0] is Run single && LooksLikeBlock(single, element)))
        {
            return false;
        }

        var builder = new ASpannableStringBuilder();
        var key = new StringBuilder();
        _building = new BuildState();
        foreach (var inline in inlines)
        {
            Append(builder, key, inline, element, hyperlink: null);
        }

        spanned = builder;
        signature = key.ToString();
        return true;
    }

    private void Append(ASpannableStringBuilder builder, StringBuilder key, Inline inline, TextBlock block, Hyperlink hyperlink)
    {
        switch (inline)
        {
            case Run run:
            {
                var text = run.Text ?? string.Empty;
                if (text.Length == 0)
                {
                    return;
                }

                var start = builder.Length();
                builder.Append(text);
                var end = builder.Length();
                var color = BrushPaint.SingleColor(run.Foreground, BrushPaint.SingleColor(block.Foreground, unchecked((int)0xFF000000)));
                var size = run.FontSize > 0 ? run.FontSize : block.FontSize;
                var decorations = run.TextDecorations;
                var underline = (decorations & TextDecorations.Underline) != 0
                    || (hyperlink != null && hyperlink.UnderlineStyle != UnderlineStyle.None);
                var strike = (decorations & TextDecorations.Strikethrough) != 0;
                builder.SetSpan(new AForegroundColorSpan(new AColor(color)), start, end, ASpanTypes.ExclusiveExclusive);
                builder.SetSpan(new AAbsoluteSizeSpan((int)Math.Round(size * Density)), start, end, ASpanTypes.ExclusiveExclusive);
                if (AndroidPlatformBootstrap.Fonts?.Resolve(run.FontFamily, run.FontWeight, run.FontStyle, run.FontStretch) is { } typeface)
                {
                    builder.SetSpan(new ATypefaceSpan(typeface), start, end, ASpanTypes.ExclusiveExclusive);
                }

                if (underline)
                {
                    builder.SetSpan(new AUnderlineSpan(), start, end, ASpanTypes.ExclusiveExclusive);
                }

                if (strike)
                {
                    builder.SetSpan(new AStrikethroughSpan(), start, end, ASpanTypes.ExclusiveExclusive);
                }

                key.Append('[').Append(text).Append('|').Append(color).Append('|').Append(size)
                    .Append('|').Append(run.FontFamily?.Source).Append('|').Append(run.FontWeight.Weight)
                    .Append('|').Append((int)run.FontStyle).Append('|').Append(underline).Append(strike).Append(']');
                break;
            }

            case LineBreak:
                builder.Append("\n");
                key.Append("[br]");
                break;

            case Span span:
            {
                var spanStart = builder.Length();
                foreach (var child in span.Inlines)
                {
                    Append(builder, key, child, block, span as Hyperlink ?? hyperlink);
                }

                if (span is Hyperlink link && builder.Length() > spanStart)
                {
                    builder.SetSpan(new HyperlinkSpan(link), spanStart, builder.Length(), ASpanTypes.ExclusiveExclusive);
                    _building.HasLinks = true;
                    key.Append("[link:").Append(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(link)).Append(']');
                }

                break;
            }
        }
    }

    private static bool LooksLikeBlock(Run run, TextBlock block) =>
        ReferenceEquals(run.Foreground, block.Foreground)
        && run.FontSize == block.FontSize
        && ReferenceEquals(run.FontFamily, block.FontFamily)
        && run.FontWeight.Weight == block.FontWeight.Weight
        && run.FontStyle == block.FontStyle
        && run.FontStretch == block.FontStretch
        && run.TextDecorations == TextDecorations.None;

    /// <inheritdoc />
    protected override ATextView CreatePlatformView()
    {
        var view = new ATextView(Context)
        {
            BreakStrategy = ABreakStrategy.Simple,
            HyphenationFrequency = global::Android.Text.HyphenationFrequency.None,
        };
        view.SetIncludeFontPadding(false);
        view.FallbackLineSpacing = false;
        view.SetPadding(0, 0, 0, 0);
        view.LayoutChange += OnTextViewLayoutChange;
        return view;
    }

    /// <summary>
    /// [AP8-S batch 2] Rebuilds and redraws the text whenever the replay lays the view out. A TextView makes its text
    /// layout for the width its frame has when the text is SET (TextView.checkForRelayout); a single-line trimmed
    /// TextBlock (TextTrimming + MaxLines 1: ellipsized to that width) whose text a native list re-bound while the view
    /// was 0 wide kept a layout of just the ellipsis, because the replay then measured it with the same exact spec as
    /// before (Android's measure cache skipped onMeasure) and only moved/resized its frame: the card titles of a
    /// switched catalog showed "..." or nothing (seen in a pasted app). Forcing the measure at the laid-out size makes
    /// the TextView rebuild its layout for its real width; the redraw re-records it. Once per arrange, never per frame.
    /// </summary>
    private static void OnTextViewLayoutChange(object sender, AView.LayoutChangeEventArgs e)
    {
        if (sender is not ATextView view)
        {
            return;
        }

        var width = e.Right - e.Left;
        var height = e.Bottom - e.Top;
        if (width > 0 && height > 0)
        {
            view.ForceLayout();
            view.Measure(MeasureSpecExtensions.Exactly(width), MeasureSpecExtensions.Exactly(height));
        }

        view.Invalidate();
    }

    /// <inheritdoc />
    protected override void DisconnectHandler(ATextView platformView)
    {
        _linkBridge.Detach();
        platformView.Touch -= OnLinkTouch;
        platformView.LayoutChange -= OnTextViewLayoutChange;
        _hasLinks = false;
        _foregroundWatcher.Clear();
        base.DisconnectHandler(platformView);
    }

    private sealed class BuildState
    {
        internal bool HasLinks { get; set; }
    }

    /// <summary>
    /// A hyperlink's range in the spannable: a click (LinkMovementMethod) runs Core's Hyperlink.OnClick
    /// (Click, then NavigateUri through the launcher). The link's look (colour, underline) comes from the
    /// run spans, not from Android's link style.
    /// </summary>
    private sealed class HyperlinkSpan : global::Android.Text.Style.ClickableSpan
    {
        private readonly WeakReference<Hyperlink> _hyperlink;

        internal HyperlinkSpan(Hyperlink hyperlink) => _hyperlink = new WeakReference<Hyperlink>(hyperlink);

        public override void OnClick(AView widget)
        {
            if (_hyperlink.TryGetTarget(out var hyperlink))
            {
                CodeBrix.Android.UI.Input.NativeInput.MarkHandled();
                hyperlink.OnClick();
            }
        }

        public override void UpdateDrawState(global::Android.Text.TextPaint ds)
        {
        }
    }
}
