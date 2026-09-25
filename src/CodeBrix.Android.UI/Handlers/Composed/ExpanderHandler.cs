// Technique from .NET MAUI, src/Controls/src/Core/Compatibility/Handlers/Android/FrameRenderer.cs @ 828569a864 (a
// card surface drawn as the view's background, the content laid out inside it). Copyright (c) .NET Foundation and
// Contributors. Licensed under the MIT License. See THIRD-PARTY-NOTICES.txt.

using System;
using CodeBrix.Android.UI.Android;
using CodeBrix.Android.UI.Platform;
using CodeBrix.Android.UI.Platform.Drawables;
using CodeBrix.Android.UI.Portable.Drawing;
using CodeBrix.Platform.UI.Contracts;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using AColor = global::Android.Graphics.Color;
using AColorStateList = global::Android.Content.Res.ColorStateList;
using AComplexUnitType = global::Android.Util.ComplexUnitType;
using AContext = global::Android.Content.Context;
using AGravityFlags = global::Android.Views.GravityFlags;
using ALinearLayout = global::Android.Widget.LinearLayout;
using AMotionEventActions = global::Android.Views.MotionEventActions;
using ARippleDrawable = global::Android.Graphics.Drawables.RippleDrawable;
using ASystemClock = global::Android.OS.SystemClock;
using ATextView = global::Android.Widget.TextView;
using AView = global::Android.Views.View;
using AViewGroup = global::Android.Views.ViewGroup;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>
/// The native Expander (plan 3 row Expander; Pinta's error dialog): a Material card surface whose header row is a
/// native row - the header text and a chevron that turns over, with a ripple - and whose content box under it hosts
/// the Expander's Core content (HostsContent: the content's own native views, laid out here), shown while
/// <see cref="Expander.IsExpanded"/> and faded/slid in by the system animator. Tapping the header (a real finger,
/// Core-injected input, or an accessibility click) toggles IsExpanded; Expanding/Collapsed stay Core's (raised by
/// the IsExpanded change). Colours: the Fluent Expander keys (ExpanderHeaderBackground/Foreground/BorderBrush,
/// ExpanderChevronForeground) and the control's Background/BorderBrush/BorderThickness/CornerRadius/Padding for the
/// content box, so an app's re-keys and the theme bridge apply. An Expander whose header is an element, a
/// template, or that opens upwards keeps its Fluent template (<see cref="ExpanderLayout.CanMapNatively"/>).
/// </summary>
/// <remarks>
/// Hit testing: Core treats a Control with native visuals and no hit-testable child as invisible to input (the
/// WPH1 CoerceHitTestVisibility defect of the pending list), so the handler keeps one transparent Core Border over
/// the header row: Core-injected and real taps reach it, and its Tapped bubbles to the Expander.
/// </remarks>
internal sealed class ExpanderHandler : ViewGroupHandler<Expander, ExpanderView>
{
    /// <summary>The Expander's mapper.</summary>
    public static readonly PropertyMapper<Expander, ExpanderHandler> Mapper = new(ViewMappers.ViewMapper)
    {
        [Expander.HeaderProperty] = MapHeader,
        [Expander.IsExpandedProperty] = MapIsExpanded,
        [Control.BackgroundProperty] = MapColors,
        [Control.BorderBrushProperty] = MapColors,
        [Control.ForegroundProperty] = MapColors,
        [Control.BorderThicknessProperty] = MapLayout,
        [Control.CornerRadiusProperty] = MapColors,
        [Control.PaddingProperty] = MapLayout,
        [Control.HorizontalContentAlignmentProperty] = MapLayout,
        [Control.VerticalContentAlignmentProperty] = MapLayout,
        [Control.FontFamilyProperty] = MapFont,
        [Control.FontSizeProperty] = MapFont,
        [Control.FontWeightProperty] = MapFont,
        [Control.FontStyleProperty] = MapFont,
        [Control.IsEnabledProperty] = MapIsEnabled,
    };

    private readonly BrushWatcher _backgroundWatcher;
    private readonly BrushWatcher _borderWatcher;
    private Border _hitArea;
    private NativeTemplateParts _parts;
    private double _headerHeight = ExpanderLayout.MinHeaderHeight;
    private bool _shownExpanded;
    private long _lastTouchUp;

    /// <summary>Creates the handler.</summary>
    public ExpanderHandler()
        : base(Mapper)
    {
        _backgroundWatcher = new BrushWatcher(() => MapColors(this, Element as Expander));
        _borderWatcher = new BrushWatcher(() => MapColors(this, Element as Expander));
    }

    /// <summary>The native view (null while disconnected).</summary>
    internal ExpanderView Native => ((ElementHandler)this).PlatformView as ExpanderView;

    /// <inheritdoc />
    public override ElementHandlerCapabilities Capabilities =>
        ElementHandlerCapabilities.OwnsVisuals | ElementHandlerCapabilities.HostsContent
        | ElementHandlerCapabilities.MeasuresNatively | ElementHandlerCapabilities.OwnsChildren;

    /// <summary>The header row's height in DIPs (after the last measure).</summary>
    internal double HeaderHeight => _headerHeight;

    /// <summary>The AppContext switch that turns the native Expander on in an app.</summary>
    internal const string NativeExpanderSwitch = "CodeBrix.Android.UI.NativeExpander";

    /// <summary>
    /// True when Expanders are native: the default since pin 1.0.268.12, whose Core keeps a HostsContent control's
    /// hosted content across a Template change (WPE1-1 C0d; before it Control.OnTemplateChanged dropped the content
    /// of an Expander created with its Content already set, and the native Expander was off). The AppContext switch
    /// <see cref="NativeExpanderSwitch"/> = false keeps the Fluent template.
    /// </summary>
    internal static bool Enabled { get; set; } = !AppContext.TryGetSwitch(NativeExpanderSwitch, out var on) || on;

    /// <summary>
    /// Creates the native Expander for <paramref name="element"/> (when <see cref="Enabled"/>), or the templated
    /// fallback when its header or direction needs the Fluent template, or its style replaces the template.
    /// </summary>
    /// <param name="element">The Expander.</param>
    /// <returns>The handler.</returns>
    internal static IAndroidElementHandler Create(UIElement element) =>
        Enabled && element is Expander expander
        && ExpanderLayout.CanMapNatively(expander.Header, expander.HeaderTemplate, expander.HeaderTemplateSelector, expander.ExpandDirection)
        && NativeControlPolicy.IsNative(expander, typeof(Expander), new[] { "DefaultExpanderStyle" }, out _)
            ? new ExpanderHandler()
            : new TemplatedFallbackHandler();

    /// <summary>Maps Header (the header row's text).</summary>
    public static void MapHeader(ExpanderHandler handler, Expander element)
    {
        if (handler.Native?.HeaderText is { } text)
        {
            var value = ExpanderLayout.HeaderText(element.Header);
            if (!string.Equals(text.Text, value, StringComparison.Ordinal))
            {
                text.Text = value;
                element.InvalidateMeasure();
            }
        }
    }

    /// <summary>Maps IsExpanded (content shown or not, the chevron turned; animated by the system animator).</summary>
    public static void MapIsExpanded(ExpanderHandler handler, Expander element)
    {
        if (handler.Native is not { } view)
        {
            return;
        }

        var expanded = element.IsExpanded;
        var animate = handler.State == ElementHandlerState.Connected && expanded != handler._shownExpanded;
        handler._shownExpanded = expanded;
        view.SetExpanded(expanded, animate, element.ContentTemplateRoot is { } content ? content.Handler as IViewHandler : null, handler.Density);
        element.InvalidateMeasure();
    }

    /// <summary>Maps the colours and the corner radius.</summary>
    public static void MapColors(ExpanderHandler handler, Expander element)
    {
        if (handler.Native is not { } view || element == null)
        {
            return;
        }

        var background = element.Background ?? ThemeResources.FindBrush(element, "ExpanderContentBackground");
        var border = element.BorderBrush ?? ThemeResources.FindBrush(element, "ExpanderContentBorderBrush");
        handler._backgroundWatcher.Watch(element.Background);
        handler._borderWatcher.Watch(element.BorderBrush);
        var headerBackground = ThemeResources.FindBrush(element, "ExpanderHeaderBackground") ?? background;
        var headerBorder = ThemeResources.FindBrush(element, "ExpanderHeaderBorderBrush") ?? border;
        var headerThickness = ThemeResources.TryFind(element, "ExpanderHeaderBorderThickness", out var value) && value is Thickness t ? t : new Thickness(1);
        var foreground = ThemeResources.FindColor(element, "ExpanderHeaderForeground") ?? BrushPaint.SingleColor(element.Foreground, unchecked((int)0xFF000000));
        var chevron = ThemeResources.FindColor(element, "ExpanderChevronForeground") ?? foreground;
        view.Surface.Update(headerBackground, headerBorder, headerThickness, background, border, element.BorderThickness, element.CornerRadius, handler.Density);
        view.HeaderText.SetTextColor(new AColor(foreground));
        view.Chevron.Color = chevron;
        view.HeaderRow.Foreground = Ripple(foreground);
    }

    /// <summary>Maps a property that moves the content (border, padding, alignments).</summary>
    public static void MapLayout(ExpanderHandler handler, Expander element)
    {
        MapColors(handler, element);
        element.InvalidateMeasure();
    }

    /// <summary>Maps the header font.</summary>
    public static void MapFont(ExpanderHandler handler, Expander element)
    {
        if (handler.Native?.HeaderText is not { } text)
        {
            return;
        }

        var typeface = AndroidPlatformBootstrap.Fonts?.Resolve(element.FontFamily, element.FontWeight, element.FontStyle, element.FontStretch);
        if (typeface != null)
        {
            text.Typeface = typeface;
        }

        text.SetTextSize(AComplexUnitType.Px, (float)(element.FontSize * handler.Density));
        element.InvalidateMeasure();
    }

    /// <summary>Maps IsEnabled (the header row stops reacting).</summary>
    public static void MapIsEnabled(ExpanderHandler handler, Expander element)
    {
        if (handler.Native is { } view)
        {
            view.HeaderRow.Enabled = element.IsEnabled;
            view.HeaderRow.Alpha = element.IsEnabled ? 1f : 0.38f;
        }
    }

    /// <inheritdoc />
    public override Size Measure(Size availableSize)
    {
        if (Element is not Expander element || Native is not { } view)
        {
            return new Size(0, 0);
        }

        EnsureHitArea(element);
        var density = Density;
        var widthPx = double.IsInfinity(availableSize.Width) ? 0 : MaterialWidgets.Px(availableSize.Width, density);
        view.HeaderRow.Measure(
            widthPx > 0 ? MeasureSpecExtensions.Exactly(widthPx) : global::Android.Views.View.MeasureSpec.MakeMeasureSpec(0, global::Android.Views.MeasureSpecMode.Unspecified),
            global::Android.Views.View.MeasureSpec.MakeMeasureSpec(0, global::Android.Views.MeasureSpecMode.Unspecified));
        _headerHeight = ExpanderLayout.HeaderHeight(view.HeaderRow.MeasuredHeight / density);
        var headerWidth = widthPx > 0 ? availableSize.Width : view.HeaderRow.MeasuredWidth / density;

        var inner = Inner(element);
        var contentDesired = new Size(0, 0);
        if (element.ContentTemplateRoot is { } content)
        {
            content.Measure(ExpanderLayout.ContentAvailable(availableSize, inner));
            contentDesired = content.DesiredSize;
        }

        _hitArea?.Measure(new Size(headerWidth, _headerHeight));
        return ExpanderLayout.Desired(availableSize, headerWidth, _headerHeight, element.IsExpanded, contentDesired, inner);
    }

    /// <inheritdoc />
    protected override ExpanderView CreatePlatformView() => new(Context);

    /// <inheritdoc />
    protected override void ConnectHandler(ExpanderView platformView)
    {
        base.ConnectHandler(platformView);
        platformView.HeaderRow.Touch += OnHeaderTouch;
        platformView.HeaderRow.Click += OnHeaderClick;
        if (Element is Expander expander)
        {
            expander.Tapped += OnTapped;
            _shownExpanded = expander.IsExpanded;
            _parts = NativeTemplateParts.For(expander);
            _parts.Declare("ExpanderHeader", "ExpanderContentClip", "ExpanderContent");
            EnsureHitArea(expander);
        }
    }

    /// <inheritdoc />
    protected override void DisconnectHandler(ExpanderView platformView)
    {
        platformView.HeaderRow.Touch -= OnHeaderTouch;
        platformView.HeaderRow.Click -= OnHeaderClick;
        if (Element is Expander expander)
        {
            expander.Tapped -= OnTapped;
        }

        _backgroundWatcher.Clear();
        _borderWatcher.Clear();
        base.DisconnectHandler(platformView);
    }

    /// <inheritdoc />
    protected override void OnArranged(Rect finalRect, bool changed)
    {
        if (Element is Expander element)
        {
            EnsureHitArea(element);
            var size = new Size(finalRect.Width, finalRect.Height);
            var contentDesired = element.ContentTemplateRoot?.DesiredSize ?? new Size(0, 0);
            var contentRect = ExpanderLayout.ContentRect(size, _headerHeight, element.IsExpanded, contentDesired, Inner(element),
                element.HorizontalContentAlignment, element.VerticalContentAlignment);
            element.ContentTemplateRoot?.Arrange(contentRect);
            _hitArea?.Arrange(new Rect(0, 0, finalRect.Width, _headerHeight));
            Native?.SetHeaderHeight(MaterialWidgets.Px(_headerHeight, Density), element.IsExpanded);

            if (NativeTemplateParts.Enabled && _parts != null)
            {
                var box = new Rect(0, _headerHeight, finalRect.Width, Math.Max(0, finalRect.Height - _headerHeight));
                _parts.Set("ExpanderHeader", new Rect(0, 0, finalRect.Width, _headerHeight));
                _parts.Set("ExpanderContentClip", element.IsExpanded ? box : null);
                _parts.Set("ExpanderContent", element.IsExpanded ? contentRect : null);
                _parts.Arrange();
            }
        }

        base.OnArranged(finalRect, changed);
    }

    private static Thickness Inner(Expander element)
    {
        var border = element.BorderThickness;
        var padding = element.Padding;
        return new Thickness(border.Left + padding.Left, border.Top + padding.Top, border.Right + padding.Right, border.Bottom + padding.Bottom);
    }

    private static ARippleDrawable Ripple(int foreground)
    {
        var highlight = (foreground & 0x00FFFFFF) | 0x1F000000;
        return new ARippleDrawable(AColorStateList.ValueOf(new AColor(highlight)), null, new global::Android.Graphics.Drawables.ColorDrawable(AColor.White));
    }

    private void EnsureHitArea(Expander element)
    {
        if (_hitArea == null)
        {
            _hitArea = new Border { Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent), IsTabStop = false };
        }

        if (!ReferenceEquals(VisualTreeHelper.GetParent(_hitArea), element))
        {
            (VisualTreeHelper.GetParent(_hitArea) as UIElement)?.RemoveChild(_hitArea);
            element.AddChild(_hitArea, null);
        }
    }

    private void OnTapped(object sender, TappedRoutedEventArgs e)
    {
        if (e.Handled || Element is not Expander element || !element.IsEnabled)
        {
            return;
        }

        var point = e.GetPosition(element);
        if (ExpanderLayout.IsOnHeader(point, element.ActualWidth, _headerHeight))
        {
            e.Handled = true;
            element.IsExpanded = !element.IsExpanded;
        }
    }

    private void OnHeaderTouch(object sender, AView.TouchEventArgs e)
    {
        // The row shows its ripple; Core sees the same touch and raises Tapped (OnTapped toggles).
        e.Handled = false;
        if (e.Event is { } motion && (motion.ActionMasked == AMotionEventActions.Up || motion.ActionMasked == AMotionEventActions.Cancel))
        {
            _lastTouchUp = ASystemClock.UptimeMillis();
        }
    }

    private void OnHeaderClick(object sender, EventArgs e)
    {
        // A click right after a touch is Core's; any other click (an accessibility service) toggles here.
        if (ASystemClock.UptimeMillis() - _lastTouchUp < 1000 || Element is not Expander element || !element.IsEnabled)
        {
            return;
        }

        element.IsExpanded = !element.IsExpanded;
    }
}

/// <summary>
/// The native view of an Expander: the card surface (<see cref="ExpanderSurface"/>) as its background, the native
/// header row on top, and the Core content's views laid out where the handler arranged them.
/// </summary>
internal sealed class ExpanderView : CodeBrixContentViewGroup
{
    private int _headerPx;

    /// <summary>Creates the view.</summary>
    /// <param name="context">The context.</param>
    internal ExpanderView(AContext context)
        : base(context)
    {
        var density = context.Resources?.DisplayMetrics?.Density ?? 1f;
        Surface = new ExpanderSurface();
        Background = Surface;
        HeaderRow = new ALinearLayout(context)
        {
            Orientation = global::Android.Widget.Orientation.Horizontal,
            Clickable = true,
            Focusable = true,
        };
        HeaderRow.SetGravity(AGravityFlags.CenterVertical);
        HeaderRow.SetPadding((int)(16 * density), (int)(12 * density), (int)(12 * density), (int)(12 * density));
        HeaderText = new ATextView(context);
        HeaderText.SetSingleLine(false);
        HeaderRow.AddView(HeaderText, new ALinearLayout.LayoutParams(0, AViewGroup.LayoutParams.WrapContent, 1f));
        Chevron = new ChevronView(context);
        var chevronSize = (int)(32 * density);
        HeaderRow.AddView(Chevron, new ALinearLayout.LayoutParams(chevronSize, chevronSize));
        AddView(HeaderRow);
    }

    /// <summary>The card surface (header and content boxes).</summary>
    internal ExpanderSurface Surface { get; }

    /// <summary>The header row (text + chevron, clickable, ripple).</summary>
    internal ALinearLayout HeaderRow { get; }

    /// <summary>The header text.</summary>
    internal ATextView HeaderText { get; }

    /// <summary>The chevron.</summary>
    internal ChevronView Chevron { get; }

    /// <summary>True while the content is shown.</summary>
    internal bool IsExpanded { get; private set; }

    /// <summary>Sets the header row's height (pixels) and whether the content box is drawn.</summary>
    /// <param name="headerPx">The header height in pixels.</param>
    /// <param name="expanded">True while expanded.</param>
    internal void SetHeaderHeight(int headerPx, bool expanded)
    {
        if (_headerPx != headerPx || Surface.Expanded != expanded)
        {
            _headerPx = headerPx;
            Surface.SetLayout(headerPx, expanded);
            RequestLayout();
        }
    }

    /// <summary>Shows or hides the content (animated with the system animator when asked).</summary>
    /// <param name="expanded">True to show it.</param>
    /// <param name="animate">True to animate the change.</param>
    /// <param name="content">The content's handler (its view fades and slides in).</param>
    /// <param name="density">Pixels per DIP.</param>
    internal void SetExpanded(bool expanded, bool animate, IViewHandler content, double density)
    {
        IsExpanded = expanded;
        Chevron.Animate()?.Cancel();

        // Motion follows the system animator scale (MotionMode.FollowSystem): no animation when it is off.
        animate = animate && global::Android.Animation.ValueAnimator.AreAnimatorsEnabled();
        if (animate)
        {
            Chevron.Animate().Rotation(expanded ? 180f : 0f).SetDuration(200).Start();
        }
        else
        {
            Chevron.Rotation = expanded ? 180f : 0f;
        }

        if (content?.NativeView is { } contentView && expanded && animate)
        {
            contentView.Alpha = 0f;
            contentView.TranslationY = (float)(-12 * density);
            contentView.Animate().Alpha(1f).TranslationY(0f).SetDuration(200).WithEndAction(new global::Java.Lang.Runnable(() =>
            {
                contentView.Alpha = 1f;
                contentView.TranslationY = 0f;
            })).Start();
        }

        Surface.SetLayout(_headerPx, expanded);
        HeaderRow.ContentDescription = HeaderText.Text;
        HeaderRow.StateDescription = expanded ? "Expanded" : "Collapsed";
    }

    /// <inheritdoc />
    protected override void OnMeasure(int widthMeasureSpec, int heightMeasureSpec)
    {
        base.OnMeasure(widthMeasureSpec, heightMeasureSpec);
        HeaderRow.Measure(MeasureSpecExtensions.Exactly(MeasuredWidth), MeasureSpecExtensions.Exactly(_headerPx));
    }

    /// <inheritdoc />
    protected override void OnLaidOut(int width, int height)
    {
        base.OnLaidOut(width, height);
        HeaderRow.Layout(0, 0, width, _headerPx);
    }
}

/// <summary>The chevron of the native Expander's header: a "v" drawn in the chevron colour (turned over while expanded).</summary>
internal sealed class ChevronView : AView
{
    private readonly global::Android.Graphics.Paint _paint = new(global::Android.Graphics.PaintFlags.AntiAlias);
    private readonly global::Android.Graphics.Path _path = new();

    /// <summary>Creates the view.</summary>
    /// <param name="context">The context.</param>
    internal ChevronView(AContext context)
        : base(context)
    {
        _paint.SetStyle(global::Android.Graphics.Paint.Style.Stroke);
        _paint.StrokeCap = global::Android.Graphics.Paint.Cap.Round;
        _paint.StrokeJoin = global::Android.Graphics.Paint.Join.Round;
        ImportantForAccessibility = global::Android.Views.ImportantForAccessibility.No;
    }

    /// <summary>The chevron's ARGB colour.</summary>
    internal int Color
    {
        get => _paint.Color;
        set
        {
            _paint.Color = new AColor(value);
            Invalidate();
        }
    }

    /// <inheritdoc />
    protected override void OnDraw(global::Android.Graphics.Canvas canvas)
    {
        base.OnDraw(canvas);
        var density = Resources?.DisplayMetrics?.Density ?? 1f;
        var cx = Width / 2f;
        var cy = Height / 2f;
        var half = 5f * density;
        _paint.StrokeWidth = 1.5f * density;
        _path.Reset();
        _path.MoveTo(cx - half, cy - (half / 2));
        _path.LineTo(cx, cy + (half / 2));
        _path.LineTo(cx + half, cy - (half / 2));
        canvas.DrawPath(_path, _paint);
    }
}

/// <summary>
/// The native Expander's card surface: the header box (the Fluent header keys, rounded at the top - and at the
/// bottom while collapsed) and, while expanded, the content box under it (the control's Background, BorderBrush,
/// BorderThickness, rounded at the bottom).
/// </summary>
internal sealed class ExpanderSurface : global::Android.Graphics.Drawables.Drawable
{
    private readonly BorderDrawable _header = new();
    private readonly BorderDrawable _content = new();
    private Brush _headerBackground;
    private Brush _headerBorder;
    private Thickness _headerThickness;
    private Brush _contentBackground;
    private Brush _contentBorder;
    private Thickness _contentThickness;
    private CornerRadius _radius;
    private double _density = 1;
    private int _headerPx;

    /// <summary>True while the content box is drawn.</summary>
    internal bool Expanded { get; private set; }

    /// <summary>Updates the brushes, thicknesses and corner radius.</summary>
    internal void Update(Brush headerBackground, Brush headerBorder, Thickness headerThickness, Brush contentBackground, Brush contentBorder,
        Thickness contentThickness, CornerRadius radius, double density)
    {
        _headerBackground = headerBackground;
        _headerBorder = headerBorder;
        _headerThickness = headerThickness;
        _contentBackground = contentBackground;
        _contentBorder = contentBorder;
        _contentThickness = contentThickness;
        _radius = radius;
        _density = density;
        Apply();
    }

    /// <summary>Sets the header height (pixels) and whether the content box is drawn.</summary>
    internal void SetLayout(int headerPx, bool expanded)
    {
        _headerPx = headerPx;
        Expanded = expanded;
        Apply();
    }

    /// <inheritdoc />
    public override int Opacity => (int)global::Android.Graphics.Format.Translucent;

    /// <inheritdoc />
    public override void SetAlpha(int alpha)
    {
        _header.SetAlpha(alpha);
        _content.SetAlpha(alpha);
    }

    /// <inheritdoc />
    public override void SetColorFilter(global::Android.Graphics.ColorFilter colorFilter)
    {
        _header.SetColorFilter(colorFilter);
        _content.SetColorFilter(colorFilter);
    }

    /// <inheritdoc />
    public override void Draw(global::Android.Graphics.Canvas canvas)
    {
        var bounds = Bounds;
        var headerBottom = Math.Min(bounds.Bottom, bounds.Top + _headerPx);
        _header.SetBounds(bounds.Left, bounds.Top, bounds.Right, headerBottom);
        _header.Draw(canvas);
        if (Expanded && bounds.Bottom > headerBottom)
        {
            _content.SetBounds(bounds.Left, headerBottom, bounds.Right, bounds.Bottom);
            _content.Draw(canvas);
        }
    }

    /// <inheritdoc />
    protected override void OnBoundsChange(global::Android.Graphics.Rect bounds)
    {
        base.OnBoundsChange(bounds);
        InvalidateSelf();
    }

    private void Apply()
    {
        var headerRadius = Expanded
            ? new CornerRadius(_radius.TopLeft, _radius.TopRight, 0, 0)
            : _radius;
        _header.Update(_headerBackground, _headerBorder, _headerThickness, headerRadius, BackgroundSizing.InnerBorderEdge, _density);
        _content.Update(_contentBackground, _contentBorder, _contentThickness, new CornerRadius(0, 0, _radius.BottomRight, _radius.BottomLeft),
            BackgroundSizing.InnerBorderEdge, _density);
        InvalidateSelf();
    }
}
