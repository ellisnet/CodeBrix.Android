using System;
using CodeBrix.Android.UI.Android;
using CodeBrix.Platform.UI.Contracts;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;
using AColor = global::Android.Graphics.Color;
using ACanvas = global::Android.Graphics.Canvas;
using AContext = global::Android.Content.Context;
using ADrawable = global::Android.Graphics.Drawables.Drawable;
using APaint = global::Android.Graphics.Paint;
using APaintFlags = global::Android.Graphics.PaintFlags;
using ARectF = global::Android.Graphics.RectF;
using ATypeface = global::Android.Graphics.Typeface;
using AView = global::Android.Views.View;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>
/// AP10-B: the native view of an InfoBadge: a Material 3 badge - a 6 dp dot, a 16 dp pill holding a number, or a
/// 16 dp disc holding an icon - drawn in the badge colours (Material colorError / colorOnError unless the app set
/// Background / Foreground itself).
/// </summary>
internal sealed class InfoBadgeView : AView
{
    private readonly APaint _fill = new(APaintFlags.AntiAlias);
    private readonly APaint _text = new(APaintFlags.AntiAlias | APaintFlags.SubpixelText);
    private readonly ARectF _rect = new();

    /// <summary>Creates the view.</summary>
    /// <param name="context">A Material 3 context.</param>
    internal InfoBadgeView(AContext context)
        : base(context)
    {
        _text.TextAlign = APaint.Align.Center;
    }

    /// <summary>What the badge shows.</summary>
    internal InfoBadgeKind Kind { get; private set; }

    /// <summary>The number text (a number badge).</summary>
    internal string Text { get; private set; } = string.Empty;

    /// <summary>The badge colour (ARGB).</summary>
    internal int FillColor => _fill.Color.ToArgb();

    /// <summary>The icon (an icon badge).</summary>
    internal ADrawable Icon { get; private set; }

    /// <summary>Shows a badge.</summary>
    internal void Set(InfoBadgeKind kind, string text, ADrawable icon, int fill, int foreground, ATypeface typeface, float textSizePx)
    {
        Kind = kind;
        Text = text ?? string.Empty;
        Icon = icon;
        _fill.Color = new AColor(fill);
        _text.Color = new AColor(foreground);
        _text.SetTypeface(typeface);
        _text.TextSize = textSizePx;
        ContentDescription = kind switch
        {
            InfoBadgeKind.Value => "Badge " + Text,
            InfoBadgeKind.Icon => "Icon badge",
            _ => "Badge",
        };
        Invalidate();
    }

    /// <summary>The width of the number text in pixels.</summary>
    internal float MeasureText() => string.IsNullOrEmpty(Text) ? 0 : _text.MeasureText(Text);

    /// <inheritdoc />
    protected override void OnDraw(ACanvas canvas)
    {
        base.OnDraw(canvas);
        if (Width <= 0 || Height <= 0)
        {
            return;
        }

        _rect.Set(0, 0, Width, Height);
        var radius = Math.Min(Width, Height) / 2f;
        canvas.DrawRoundRect(_rect, radius, radius, _fill);
        switch (Kind)
        {
            case InfoBadgeKind.Value:
                var baseline = (Height / 2f) - ((_text.Descent() + _text.Ascent()) / 2f);
                canvas.DrawText(Text, Width / 2f, baseline, _text);
                break;
            case InfoBadgeKind.Icon when Icon != null:
                var size = (int)Math.Round(Math.Min(Width, Height) * 0.75);
                var left = (Width - size) / 2;
                var top = (Height - size) / 2;
                Icon.SetBounds(left, top, left + size, top + size);
                Icon.Draw(canvas);
                break;
        }
    }
}

/// <summary>
/// AP10-B: the handler of InfoBadge (tsv row InfoBadge: "BadgeDrawable attached to the host view"). A stand-alone
/// InfoBadge is an element of its own in the layout, so it is drawn as a Material 3 badge view of its own
/// (<see cref="InfoBadgeView"/>; a BadgeDrawable needs an anchor view to position itself, which an element laid out by
/// Core does not have). Value &gt;= 0 shows the number, else IconSource an icon, else a dot; the size is Material's.
/// </summary>
internal sealed class InfoBadgeHandler : ViewHandler<InfoBadge, InfoBadgeView>
{
    /// <summary>InfoBadge's mapper.</summary>
    public static readonly PropertyMapper<InfoBadge, InfoBadgeHandler> Mapper = new(ViewMappers.ViewMapper)
    {
        [InfoBadge.ValueProperty] = MapBadge,
        [InfoBadge.IconSourceProperty] = MapBadge,
        [Control.BackgroundProperty] = MapBadge,
        [Control.ForegroundProperty] = MapBadge,
        [Control.FontFamilyProperty] = MapBadge,
    };

    /// <summary>Creates the handler.</summary>
    public InfoBadgeHandler()
        : base(Mapper)
    {
    }

    /// <inheritdoc />
    public override ElementHandlerCapabilities Capabilities => ElementHandlerCapabilities.OwnsVisuals | ElementHandlerCapabilities.MeasuresNatively;

    /// <summary>The handler, or the templated fallback for a re-templated badge.</summary>
    /// <param name="element">The InfoBadge.</param>
    /// <returns>The handler.</returns>
    internal static IAndroidElementHandler Create(UIElement element) =>
        element is InfoBadge badge && NativeControlPolicy.IsNative(badge, typeof(InfoBadge), new[] { "DefaultInfoBadgeStyle" }, out _)
            ? new InfoBadgeHandler()
            : new TemplatedFallbackHandler();

    /// <summary>Maps every property: the badge is redrawn and re-measured.</summary>
    public static void MapBadge(InfoBadgeHandler handler, InfoBadge element)
    {
        var view = handler.PlatformView;
        var context = view.Context;
        var kind = StatusMath.BadgeKind(element.Value, element.IconSource != null);
        var fill = ThemeResources.IsLocal(element, Control.BackgroundProperty) && ThemeResources.ColorOf(element.Background) is int b
            ? b
            : PagingWidgets.Role(context, "colorError", unchecked((int)0xFFB3261E));
        var foreground = ThemeResources.IsLocal(element, Control.ForegroundProperty) && ThemeResources.ColorOf(element.Foreground) is int f
            ? f
            : PagingWidgets.Role(context, "colorOnError", unchecked((int)0xFFFFFFFF));
        var density = handler.Density;
        var icon = kind == InfoBadgeKind.Icon ? IconDrawables.Create(element.IconSource, context, density, foreground, 12) : null;
        icon?.SetTint(foreground);
        var typeface = AndroidPlatformBootstrap.Fonts?.Resolve(element.FontFamily, Microsoft.UI.Text.FontWeights.Medium, Windows.UI.Text.FontStyle.Normal, Windows.UI.Text.FontStretch.Normal);
        view.Set(kind, kind == InfoBadgeKind.Value ? StatusMath.BadgeText(element.Value) : null, icon, fill, foreground, typeface, (float)(11 * density));
        element.InvalidateMeasure();
    }

    /// <inheritdoc />
    public override Size Measure(Size availableSize)
    {
        var density = Density;
        var textWidth = PlatformView == null ? 0 : PlatformView.MeasureText() / density;
        return StatusMath.BadgeSize(PlatformView?.Kind ?? InfoBadgeKind.Dot, textWidth);
    }

    /// <inheritdoc />
    protected override InfoBadgeView CreatePlatformView() => new(MaterialWidgets.Material3(Context));
}
