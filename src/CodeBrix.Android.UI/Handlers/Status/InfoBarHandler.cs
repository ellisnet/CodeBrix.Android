using System;
using System.Collections.Generic;
using CodeBrix.Android.UI.Android;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using AColor = global::Android.Graphics.Color;
using AComplexUnitType = global::Android.Util.ComplexUnitType;
using AContext = global::Android.Content.Context;
using AFrameLayout = global::Android.Widget.FrameLayout;
using AGravityFlags = global::Android.Views.GravityFlags;
using ALinearLayout = global::Android.Widget.LinearLayout;
using AMaterialButton = Google.Android.Material.Button.MaterialButton;
using AMaterialCardView = Google.Android.Material.Card.MaterialCardView;
using ASpannableStringBuilder = global::Android.Text.SpannableStringBuilder;
using ASpanTypes = global::Android.Text.SpanTypes;
using AStyleSpan = global::Android.Text.Style.StyleSpan;
using ATextView = global::Android.Widget.TextView;
using ATypefaceStyle = global::Android.Graphics.TypefaceStyle;
using AView = global::Android.Views.View;
using AViewGroup = global::Android.Views.ViewGroup;
using AViewStates = global::Android.Views.ViewStates;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>
/// AP10-B: the native InfoBar: a Material card (12 dp corners) in the severity's fill and border, the severity icon
/// (its disc and glyph), the title (bold) followed by the message, and a Material icon button that closes it.
/// </summary>
internal sealed class InfoBarCardView : AMaterialCardView
{
    /// <summary>Creates the view.</summary>
    /// <param name="context">A Material 3 context.</param>
    internal InfoBarCardView(AContext context)
        : base(context)
    {
        var density = context.Resources?.DisplayMetrics?.Density ?? 1f;
        int Dp(double value) => (int)Math.Round(value * density);
        Radius = Dp(12);
        CardElevation = 0;
        UseCompatPadding = false;
        PreventCornerOverlap = false;

        var row = new ALinearLayout(context) { Orientation = global::Android.Widget.Orientation.Horizontal };
        row.SetGravity(AGravityFlags.CenterVertical);
        row.SetPadding(Dp(16), 0, Dp(4), 0);

        Icon = new AFrameLayout(context);
        IconDisc = Glyph(context);
        IconGlyph = Glyph(context);
        Icon.AddView(IconDisc, new AFrameLayout.LayoutParams(AViewGroup.LayoutParams.MatchParent, AViewGroup.LayoutParams.MatchParent));
        Icon.AddView(IconGlyph, new AFrameLayout.LayoutParams(AViewGroup.LayoutParams.MatchParent, AViewGroup.LayoutParams.MatchParent));
        row.AddView(Icon, new ALinearLayout.LayoutParams(Dp(20), Dp(20)) { RightMargin = Dp(12) });

        Text = new ATextView(context);
        Text.SetIncludeFontPadding(false);
        row.AddView(Text, new ALinearLayout.LayoutParams(0, AViewGroup.LayoutParams.WrapContent, 1f));

        Row = row;
        AddView(row, new AFrameLayout.LayoutParams(AViewGroup.LayoutParams.MatchParent, AViewGroup.LayoutParams.MatchParent));
        Close = PagingWidgets.IconButton(context, "\uE711", "Close", density);
        AddView(Close, new AFrameLayout.LayoutParams(Dp(40), Dp(40)) { Gravity = AGravityFlags.Top | AGravityFlags.Left });
    }

    /// <summary>The row of icon and text.</summary>
    internal ALinearLayout Row { get; }

    /// <summary>
    /// Puts the close button exactly over the template's CloseButton (pixels, relative to the card), so that the
    /// finger that presses it also lands on the part Core closes the bar with; the text stops short of it.
    /// </summary>
    internal void PlaceClose(int left, int top, int width, int height, int cardWidth)
    {
        if (Close.LayoutParameters is AFrameLayout.LayoutParams lp
            && (lp.LeftMargin != left || lp.TopMargin != top || lp.Width != width || lp.Height != height))
        {
            lp.LeftMargin = left;
            lp.TopMargin = top;
            lp.Width = width;
            lp.Height = height;
            Close.LayoutParameters = lp;
        }

        var right = Math.Max(0, cardWidth - left);
        if (Row.PaddingRight != right)
        {
            Row.SetPadding(Row.PaddingLeft, 0, right, 0);
        }
    }

    /// <summary>The severity icon (disc and glyph).</summary>
    internal AFrameLayout Icon { get; }

    /// <summary>The icon's disc glyph.</summary>
    internal ATextView IconDisc { get; }

    /// <summary>The icon's glyph.</summary>
    internal ATextView IconGlyph { get; }

    /// <summary>The title and message.</summary>
    internal ATextView Text { get; }

    /// <summary>The close button.</summary>
    internal AMaterialButton Close { get; }

    private static ATextView Glyph(AContext context)
    {
        var glyph = new ATextView(context) { Gravity = AGravityFlags.Center };
        glyph.SetIncludeFontPadding(false);
        return glyph;
    }
}

/// <summary>
/// AP10-B: the handler of InfoBar (tsv rows InfoBar / InfoBarPanel: "MaterialCardView composition (severity colour,
/// icon, title, message, action button, close)"). The InfoBar keeps its Fluent template and every behaviour of it:
/// IsOpen, the severity visual states, Closing / Closed, CloseButtonClick and the template's CloseButton answering
/// Core input. Its visible form is a native Material card (<see cref="InfoBarCardView"/>) drawn over the template,
/// showing what the template's parts show after Core's visual states ran: the ContentRoot's fill and border, the
/// IconBackground / StandardIcon glyphs and colours, the Title and Message texts, the CloseButton's visibility. The
/// native close button lies exactly over the template's CloseButton: a finger on it shows the Material press and the
/// same touch closes the bar through that part in Core (an accessibility click runs the part's click). An InfoBar
/// with an ActionButton or Content keeps its template visible (those are Core elements the card has no place for);
/// InfoBarPanel, the template's title/message panel, is laid out by Core and drawn by the card.
/// </summary>
internal sealed class InfoBarHandler : TemplateOverlayHandler<InfoBar, InfoBarCardView>
{
    /// <summary>InfoBar's mapper.</summary>
    public static readonly PropertyMapper<InfoBar, InfoBarHandler> Mapper = new(ViewMappers.ViewMapper)
    {
        [InfoBar.IsOpenProperty] = MapOverlay,
        [InfoBar.TitleProperty] = MapOverlay,
        [InfoBar.MessageProperty] = MapOverlay,
        [InfoBar.SeverityProperty] = MapOverlay,
        [InfoBar.IsClosableProperty] = MapOverlay,
        [InfoBar.IsIconVisibleProperty] = MapOverlay,
        [InfoBar.ActionButtonProperty] = MapOverlay,
        [ContentControl.ContentProperty] = MapOverlay,
        [Control.BackgroundProperty] = MapOverlay,
        [Control.ForegroundProperty] = MapOverlay,
        [Control.FontFamilyProperty] = MapOverlay,
    };

    private long _lastTouchUp;

    /// <summary>Creates the handler.</summary>
    public InfoBarHandler()
        : base(Mapper)
    {
    }

    /// <summary>The handler, or the templated fallback for a re-templated InfoBar.</summary>
    /// <param name="element">The InfoBar.</param>
    /// <returns>The handler.</returns>
    internal static IAndroidElementHandler Create(UIElement element) =>
        element is InfoBar bar && NativeControlPolicy.IsNative(bar, typeof(InfoBar), new[] { "DefaultInfoBarStyle" }, out _)
            ? new InfoBarHandler()
            : new TemplatedFallbackHandler();

    /// <inheritdoc />
    protected override InfoBarCardView CreateOverlay(AContext context)
    {
        var card = new InfoBarCardView(context);
        card.Close.Click += OnClose;
        card.Close.Touch += OnWidgetTouch;
        return card;
    }

    /// <inheritdoc />
    protected override bool IsOverlayShown(InfoBar element) =>
        element.IsOpen && element.ActionButton == null && element.Content == null && FindNamed(element, "ContentRoot") is { Visibility: Visibility.Visible };

    /// <inheritdoc />
    protected override void UpdateOverlay(InfoBar element, InfoBarCardView card)
    {
        var density = Density;
        var root = FindNamed(element, "ContentRoot") as Border;
        card.SetCardBackgroundColor(ThemeResources.ColorOf(root?.Background) ?? PagingWidgets.Role(card.Context, "colorSurfaceContainerHigh", unchecked((int)0xFFECE6F0)));
        card.StrokeColor = ThemeResources.ColorOf(root?.BorderBrush) ?? 0;
        card.StrokeWidth = root == null ? 0 : (int)Math.Round(root.BorderThickness.Left * density);

        var disc = FindNamed(element, "IconBackground") as TextBlock;
        var glyph = FindNamed(element, "StandardIcon") as TextBlock;
        var iconShown = element.IsIconVisible && FindNamed(element, "StandardIconArea") is { Visibility: Visibility.Visible } && glyph != null;
        card.Icon.Visibility = iconShown ? AViewStates.Visible : AViewStates.Gone;
        if (iconShown)
        {
            Glyph(card.IconDisc, disc, density);
            Glyph(card.IconGlyph, glyph, density);
        }

        var title = FindNamed(element, "Title") as TextBlock;
        var message = FindNamed(element, "Message") as TextBlock;
        var text = new ASpannableStringBuilder();
        if (title is { Visibility: Visibility.Visible } && !string.IsNullOrEmpty(title.Text))
        {
            text.Append(title.Text);
            text.SetSpan(new AStyleSpan(ATypefaceStyle.Bold), 0, text.Length(), ASpanTypes.ExclusiveExclusive);
        }

        if (message is { Visibility: Visibility.Visible } && !string.IsNullOrEmpty(message.Text))
        {
            if (text.Length() > 0)
            {
                text.Append("   ");
            }

            text.Append(message.Text);
        }

        card.Text.TextFormatted = text;
        card.Text.Typeface = AndroidPlatformBootstrap.Fonts?.Resolve(element.FontFamily, Microsoft.UI.Text.FontWeights.Normal, Windows.UI.Text.FontStyle.Normal, Windows.UI.Text.FontStretch.Normal);
        card.Text.SetTextSize(AComplexUnitType.Px, (float)((message?.FontSize ?? 14) * density));
        card.Text.SetTextColor(new AColor(ThemeResources.ColorOf(message?.Foreground) ?? PagingWidgets.Role(card.Context, "colorOnSurface", unchecked((int)0xFF1D1B20))));

        var close = FindNamed(element, "CloseButton");
        var closeShown = element.IsClosable && close is { Visibility: Visibility.Visible, ActualWidth: > 0 };
        card.Close.Visibility = closeShown ? AViewStates.Visible : AViewStates.Gone;
        if (closeShown)
        {
            var origin = close.TransformToVisual(element).TransformPoint(default);
            card.PlaceClose(
                MaterialWidgets.Px(origin.X, density),
                MaterialWidgets.Px(origin.Y, density),
                MaterialWidgets.Px(close.ActualWidth, density),
                MaterialWidgets.Px(close.ActualHeight, density),
                MaterialWidgets.Px(ArrangedRect.Width, density));
        }
        else
        {
            card.Row.SetPadding(card.Row.PaddingLeft, 0, MaterialWidgets.Px(16, density), 0);
        }
        card.ContentDescription = AutomationText.ContentDescriptionOr(AutomationProperties.GetName(element), element.Severity + " " + title?.Text);
    }

    /// <inheritdoc />
    protected override IReadOnlyList<FrameworkElement> CoveredParts(InfoBar element) => null;

    /// <inheritdoc />
    protected override void DisconnectHandler(TemplateOverlayHostView platformView)
    {
        if (platformView.NativeOverlay is InfoBarCardView card)
        {
            card.Close.Click -= OnClose;
            card.Close.Touch -= OnWidgetTouch;
        }

        base.DisconnectHandler(platformView);
    }

    private static void Glyph(ATextView view, TextBlock source, double density)
    {
        if (source == null)
        {
            view.Text = string.Empty;
            return;
        }

        view.Text = source.Text;
        view.Typeface = AndroidPlatformBootstrap.Fonts?.Resolve(source.FontFamily, source.FontWeight, source.FontStyle, source.FontStretch);
        view.SetTextSize(AComplexUnitType.Px, (float)(source.FontSize * density));
        view.SetTextColor(new AColor(ThemeResources.ColorOf(source.Foreground) ?? unchecked((int)0xFF000000)));
    }

    private void OnWidgetTouch(object sender, AView.TouchEventArgs e)
    {
        // The native button shows the press (ripple); the same touch reaches Core, where it lands on the template's
        // CloseButton under it, which closes the bar (ButtonHandler's input model).
        e.Handled = false;
        if (e.Event is { } motion && (motion.ActionMasked == global::Android.Views.MotionEventActions.Up || motion.ActionMasked == global::Android.Views.MotionEventActions.Cancel))
        {
            _lastTouchUp = global::Android.OS.SystemClock.UptimeMillis();
        }
    }

    private void OnClose(object sender, EventArgs e)
    {
        // A click right after a touch is Core's (it saw the same touch); any other native click comes from an
        // accessibility service and runs the template CloseButton's click here.
        if (global::Android.OS.SystemClock.UptimeMillis() - _lastTouchUp < 1000)
        {
            return;
        }

        if (Element is InfoBar element && FindNamed(element, "CloseButton") is ButtonBase close)
        {
            close.RaiseClickFromPlatform();
        }
    }
}
