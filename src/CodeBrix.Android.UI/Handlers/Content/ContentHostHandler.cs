using System;
using CodeBrix.Android.UI.Hosting;
using CodeBrix.Android.UI.Platform;
using CodeBrix.Android.UI.Platform.Drawables;
using CodeBrix.Android.UI.Portable.Layout;
using CodeBrix.Platform.UI.Contracts;
using CodeBrix.Platform.UI.Hosting;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>
/// The base of the handlers that HOST a content control's content directly (plan 2.5, seam
/// capability HostsContent): ContentControl, UserControl, Page and Frame. Core does not expand the
/// control's template (OwnsVisuals); the content (UIElement content as is, other content through its
/// DataTemplate) becomes the control's only visual child, and this handler lays it out itself
/// (MeasuresNatively): measured inside the control's border, padding and absorbed insets, arranged in
/// that inner rectangle by the content alignments - what the template's ContentPresenter did. The
/// view draws the control's background and border (<see cref="BorderDrawable"/>).
/// </summary>
/// <typeparam name="TElement">The content control type.</typeparam>
internal abstract class ContentHostHandler<TElement> : ViewGroupHandler<TElement, CodeBrixContentViewGroup>
    where TElement : ContentControl
{
    private readonly BrushWatcher _backgroundWatcher;
    private readonly BrushWatcher _borderWatcher;
    private BorderDrawable _drawable;
    private double _drawnDensity;

    /// <summary>Creates the handler.</summary>
    /// <param name="mapper">The property mapper.</param>
    /// <param name="commandMapper">The command mapper, or null.</param>
    protected ContentHostHandler(IPropertyMapper mapper, CommandMapper commandMapper = null)
        : base(mapper, commandMapper)
    {
        _backgroundWatcher = new BrushWatcher(() => _drawable?.InvalidateBrushes());
        _borderWatcher = new BrushWatcher(() => _drawable?.InvalidateBrushes());
    }

    /// <inheritdoc />
    public override ElementHandlerCapabilities Capabilities =>
        ElementHandlerCapabilities.OwnsVisuals | ElementHandlerCapabilities.HostsContent
        | ElementHandlerCapabilities.MeasuresNatively | ElementHandlerCapabilities.OwnsChildren;

    /// <summary>
    /// True when the host applies what the control's template did around the content: Padding,
    /// BorderThickness and the content alignments (ContentControl, Frame). UserControl and Page have no
    /// template: their content fills them.
    /// </summary>
    protected virtual bool AppliesTemplateLayout => true;

    /// <summary>The insets the content is kept clear of (Page: the safe area it overlaps), in DIPs.</summary>
    protected virtual SafeAreaPadding ContentInsets => SafeAreaPadding.Empty;

    /// <summary>The hosted content (the control's only visual child), or null.</summary>
    protected UIElement Content
    {
        get
        {
            if (Element is not ContentControl control)
            {
                return null;
            }

            if (control.ContentTemplateRoot is { } root)
            {
                return root;
            }

            return VisualTreeHelper.GetChildrenCount(control) > 0 ? VisualTreeHelper.GetChild(control, 0) as UIElement : null;
        }
    }

    /// <summary>Maps Background / BorderBrush / BorderThickness / CornerRadius / BackgroundSizing.</summary>
    public static void MapBackground(ContentHostHandler<TElement> handler, TElement element) => handler.UpdateBackground();

    /// <summary>Maps a property that changes the content's layout inside the host.</summary>
    public static void MapContentLayout(ContentHostHandler<TElement> handler, TElement element) => element.InvalidateMeasure();

    /// <inheritdoc />
    public override Size Measure(Size availableSize)
    {
        var inner = InnerEdges();
        var content = Content;
        if (content == null)
        {
            return ContentHostMath.Inflate(new Size(0, 0), inner);
        }

        content.Measure(ContentHostMath.Deflate(availableSize, inner));
        return ContentHostMath.Inflate(content.DesiredSize, inner);
    }

    /// <inheritdoc />
    protected override void OnArranged(Rect finalRect, bool changed)
    {
        if (Content is { } content && Element is ContentControl control)
        {
            var rect = ContentHostMath.ArrangeRect(
                new Size(finalRect.Width, finalRect.Height),
                InnerEdges(),
                content.DesiredSize,
                AppliesTemplateLayout ? control.HorizontalContentAlignment : HorizontalAlignment.Stretch,
                AppliesTemplateLayout ? control.VerticalContentAlignment : VerticalAlignment.Stretch);
            content.Arrange(rect);
        }

        base.OnArranged(finalRect, changed);
    }

    /// <summary>The background and border the view draws (Page: background only).</summary>
    protected virtual (Brush Background, Brush BorderBrush, Thickness BorderThickness, CornerRadius CornerRadius, BackgroundSizing Sizing) BorderInfo(TElement element) =>
        (element.Background, element.BorderBrush, element.BorderThickness, element.CornerRadius, element.BackgroundSizing);

    /// <inheritdoc />
    protected override CodeBrixContentViewGroup CreatePlatformView() => new(Context);

    /// <inheritdoc />
    protected override void DisconnectHandler(CodeBrixContentViewGroup platformView)
    {
        _backgroundWatcher.Clear();
        _borderWatcher.Clear();
        base.DisconnectHandler(platformView);
    }

    /// <inheritdoc />
    protected override void OnArrangedSizeChanged(Size size)
    {
        base.OnArrangedSizeChanged(size);
        if (_drawnDensity != Density)
        {
            UpdateBackground();
        }
    }

    private Thickness InnerEdges()
    {
        if (Element is not TElement element)
        {
            return default;
        }

        return AppliesTemplateLayout
            ? ContentHostMath.Inner(element.BorderThickness, element.Padding, ContentInsets)
            : ContentHostMath.Inner(default, default, ContentInsets);
    }

    private void UpdateBackground()
    {
        if (NativeView is not CodeBrixContentViewGroup view || Element is not TElement element)
        {
            return;
        }

        var density = Density;
        _drawnDensity = density;
        var info = BorderInfo(element);
        _backgroundWatcher.Watch(info.Background);
        _borderWatcher.Watch(info.BorderBrush);
        if (info.Background == null && info.BorderBrush == null)
        {
            if (_drawable != null)
            {
                view.Background = null;
                _drawable = null;
            }

            return;
        }

        if (_drawable == null)
        {
            _drawable = new BorderDrawable();
            view.Background = _drawable;
        }

        _drawable.Update(info.Background, info.BorderBrush, info.BorderThickness, info.CornerRadius, info.Sizing, density);
    }
}

/// <summary>The content-host mapper keys shared by the content controls.</summary>
internal static class ContentHostMappers
{
    /// <summary>Adds the background, border and content-layout keys to a content control mapper.</summary>
    internal static PropertyMapper<TElement, THandler> WithContentHostKeys<TElement, THandler>(this PropertyMapper<TElement, THandler> mapper)
        where TElement : ContentControl
        where THandler : ContentHostHandler<TElement>
    {
        mapper[Control.BackgroundProperty] = ContentHostHandler<TElement>.MapBackground;
        mapper[Control.BorderBrushProperty] = ContentHostHandler<TElement>.MapBackground;
        mapper[Control.BorderThicknessProperty] = (h, e) =>
        {
            ContentHostHandler<TElement>.MapBackground(h, e);
            ContentHostHandler<TElement>.MapContentLayout(h, e);
        };
        mapper[Control.CornerRadiusProperty] = ContentHostHandler<TElement>.MapBackground;
        mapper[Control.BackgroundSizingProperty] = ContentHostHandler<TElement>.MapBackground;
        mapper[Control.PaddingProperty] = ContentHostHandler<TElement>.MapContentLayout;
        mapper[Control.HorizontalContentAlignmentProperty] = ContentHostHandler<TElement>.MapContentLayout;
        mapper[Control.VerticalContentAlignmentProperty] = ContentHostHandler<TElement>.MapContentLayout;
        return mapper;
    }
}

/// <summary>
/// The handler of a plain ContentControl (exactly that type, with its default template): its content is
/// hosted directly (HostsContent). A ContentControl subclass (Button, ListViewItem, ...) or one with an
/// app template/style keeps the templated fallback (see <see cref="CodeBrixHandlers"/>).
/// </summary>
internal sealed class ContentControlHandler : ContentHostHandler<ContentControl>
{
    /// <summary>ContentControl's mapper.</summary>
    public static readonly PropertyMapper<ContentControl, ContentControlHandler> Mapper =
        new PropertyMapper<ContentControl, ContentControlHandler>(ViewMappers.ViewMapper).WithContentHostKeys();

    /// <summary>Creates the handler.</summary>
    public ContentControlHandler()
        : base(Mapper)
    {
    }

    /// <summary>
    /// True when a ContentControl-family element keeps its default look: no template or style set by the
    /// app (a local Template or Style value), so hosting the content directly shows what the default
    /// template showed.
    /// </summary>
    internal static bool HasDefaultTemplate(Control control) =>
        control.ReadLocalValue(Control.TemplateProperty) == DependencyProperty.UnsetValue
        && control.ReadLocalValue(FrameworkElement.StyleProperty) == DependencyProperty.UnsetValue;
}

/// <summary>
/// The handler of UserControl and every app UserControl: the content (the XAML the control was written
/// with) fills the control; the control itself draws nothing (as in Core).
/// </summary>
internal sealed class UserControlHandler : ContentHostHandler<UserControl>
{
    /// <summary>UserControl's mapper.</summary>
    public static readonly PropertyMapper<UserControl, UserControlHandler> Mapper = new(ViewMappers.ViewMapper);

    /// <summary>Creates the handler.</summary>
    public UserControlHandler()
        : base(Mapper)
    {
    }

    /// <inheritdoc />
    protected override bool AppliesTemplateLayout => false;

    /// <inheritdoc />
    protected override (Brush Background, Brush BorderBrush, Thickness BorderThickness, CornerRadius CornerRadius, BackgroundSizing Sizing) BorderInfo(UserControl element) =>
        (null, null, default, default, BackgroundSizing.InnerBorderEdge);
}

/// <summary>
/// The handler of Page and every app page: draws the page Background (Core's Page is the one control
/// that paints its own background) and ABSORBS the window's safe-area insets (plan D-P10): the part of
/// the system bars and display cutout the page overlaps becomes padding around its content, so the
/// content is laid out clear of them while the background still runs edge to edge. A page nested in an
/// absorbing page (a Frame inside a page) is already clear and absorbs nothing.
/// </summary>
internal sealed class PageHandler : ContentHostHandler<Page>, ISafeAreaAbsorber
{
    /// <summary>Page's mapper.</summary>
    public static readonly PropertyMapper<Page, PageHandler> Mapper = new(ViewMappers.ViewMapper)
    {
        [Control.BackgroundProperty] = MapBackground,
        [FrameworkElement.MarginProperty] = (h, e) => h.RevalidateSafeArea(),
    };

    private SafeAreaPadding _insets;
    private bool _hasInsets;
    private AndroidNativeWindowWrapper _wrapper;

    /// <summary>Creates the handler.</summary>
    public PageHandler()
        : base(Mapper)
    {
    }

    /// <summary>The safe-area insets the page absorbs now, in DIPs.</summary>
    internal SafeAreaPadding AbsorbedInsets => ContentInsets;

    /// <inheritdoc />
    protected override bool AppliesTemplateLayout => false;

    /// <inheritdoc />
    protected override SafeAreaPadding ContentInsets
    {
        get
        {
            if (!_hasInsets)
            {
                // Before the first arrange the page's position is unknown: MAUI's rule - a page with no
                // absorbing page above it takes the whole safe area (the common root-page case), a nested
                // page none. Corrected after the layout pass (RevalidateSafeArea).
                _insets = HasAbsorbingAncestor() ? SafeAreaPadding.Empty : WindowSafeArea();
                _hasInsets = true;
            }

            return _insets;
        }
    }

    /// <inheritdoc />
    public void RevalidateSafeArea()
    {
        if (Element is not Page page || !HasArranged)
        {
            return;
        }

        var computed = ComputeInsets(page);
        if (!_hasInsets || SafeAreaMath.Differs(computed, _insets))
        {
            _insets = computed;
            _hasInsets = true;
            page.InvalidateMeasure();
        }
    }

    /// <inheritdoc />
    protected override (Brush Background, Brush BorderBrush, Thickness BorderThickness, CornerRadius CornerRadius, BackgroundSizing Sizing) BorderInfo(Page element) =>
        (element.Background, null, default, default, BackgroundSizing.InnerBorderEdge);

    /// <inheritdoc />
    protected override void OnConnected()
    {
        base.OnConnected();
        _hasInsets = false;
        SafeAreaAbsorbers.Add(this);
        _wrapper = (Element?.XamlRoot is { } root ? XamlRootMap.GetHostForRoot(root) as AndroidXamlRootHost : null)?.Wrapper;
        if (_wrapper != null)
        {
            _wrapper.SafeAreaChanged += OnSafeAreaChanged;
        }
    }

    /// <inheritdoc />
    protected override void DisconnectHandler(CodeBrixContentViewGroup platformView)
    {
        SafeAreaAbsorbers.Remove(this);
        if (_wrapper != null)
        {
            _wrapper.SafeAreaChanged -= OnSafeAreaChanged;
            _wrapper = null;
        }

        base.DisconnectHandler(platformView);
    }

    private void OnSafeAreaChanged(object sender, EventArgs e) => RevalidateSafeArea();

    private SafeAreaPadding WindowSafeArea() => _wrapper?.SafeAreaDips ?? SafeAreaPadding.Empty;

    private bool HasAbsorbingAncestor()
    {
        for (var parent = Element is { } element ? VisualTreeHelper.GetParent(element) : null; parent != null; parent = VisualTreeHelper.GetParent(parent))
        {
            if (parent is Page { Handler: PageHandler })
            {
                return true;
            }
        }

        return false;
    }

    private SafeAreaPadding ComputeInsets(Page page)
    {
        var safeArea = WindowSafeArea();
        if (safeArea.IsEmpty || page.XamlRoot is not { } root)
        {
            return SafeAreaPadding.Empty;
        }

        // The page's layout rectangle in window DIPs (Core's arranged rectangles, not render transforms:
        // a page sliding in with a transition keeps its insets).
        var x = ArrangedRect.X;
        var y = ArrangedRect.Y;
        for (var parent = VisualTreeHelper.GetParent(page); parent is UIElement ui; parent = VisualTreeHelper.GetParent(ui))
        {
            if (ui.Handler is IAndroidElementHandler { HasArranged: true } handler)
            {
                x += handler.ArrangedRect.X;
                y += handler.ArrangedRect.Y;
            }
        }

        var margin = page.Margin;
        return SafeAreaMath.Overlap(
            x - margin.Left, y - margin.Top,
            ArrangedRect.Width + margin.Left + margin.Right, ArrangedRect.Height + margin.Top + margin.Bottom,
            root.Size.Width, root.Size.Height,
            safeArea);
    }
}

/// <summary>
/// The handler of Frame (plan 2.8, D-P5: view-based, no fragments; Core owns BackStack, ForwardStack, page
/// caching and Navigating / Navigated). The frame keeps its template: Core's own navigation swaps the page
/// inside the template's ContentPresenter and the page views follow. What the handler adds:
/// <list type="bullet">
/// <item>Material motion on each page change (<see cref="FrameTransitions"/>: shared axis Z by default, X for
/// SlideNavigationTransitionInfo, fade through for DrillIn, none for Suppress and for the first page; reversed
/// going back);</item>
/// <item>the Android back button / gesture going back through the frame while it can go back, with the
/// predictive back preview of the page being left (<see cref="BackNavigation"/>);</item>
/// <item>the navigation state kept across process death (<see cref="NavigationStateKeeper"/>).</item>
/// </list>
/// </summary>
/// <remarks>
/// Hosting the page directly (HostsContent) was tried first: with the WinUI Frame behaviour the page
/// that Frame.GoBack creates never entered the live tree (its content presenter is a template part), so
/// the frame keeps its template.
/// </remarks>
internal sealed class FrameHandler : ViewGroupHandler<Frame, CodeBrixContentViewGroup>
{
    /// <summary>Frame's mapper.</summary>
    public static readonly PropertyMapper<Frame, FrameHandler> Mapper = new(ViewMappers.ViewMapper)
    {
        [Frame.CanGoBackProperty] = (h, e) => FrameBackNavigation.Update(h.Context),
    };

    private Frame _subscribed;

    /// <summary>Creates the handler.</summary>
    public FrameHandler()
        : base(Mapper)
    {
    }

    /// <inheritdoc />
    public override ElementHandlerCapabilities Capabilities => ElementHandlerCapabilities.OwnsChildren;

    /// <summary>The native view of the page the frame shows now (null when none).</summary>
    internal global::Android.Views.View CurrentPageView =>
        (Element as Frame)?.Content is UIElement { Handler: IViewHandler { NativeView: { } view } } ? view : null;

    /// <summary>Goes back because of the Android back button / gesture.</summary>
    internal void GoBackFromSystem()
    {
        if (Element is Frame { CanGoBack: true } frame)
        {
            frame.GoBack();
        }
    }

    /// <inheritdoc />
    protected override CodeBrixContentViewGroup CreatePlatformView() => new(Context);

    /// <inheritdoc />
    protected override void OnConnected()
    {
        base.OnConnected();
        if (Element is Frame frame)
        {
            _subscribed = frame;
            frame.Navigating += OnNavigating;
            frame.Navigated += OnNavigated;
        }

        FrameBackNavigation.Add(this);
        NavigationStateKeeper.OnFrameConnected(this);
    }

    /// <inheritdoc />
    protected override void DisconnectHandler(CodeBrixContentViewGroup platformView)
    {
        if (_subscribed != null)
        {
            _subscribed.Navigating -= OnNavigating;
            _subscribed.Navigated -= OnNavigated;
            _subscribed = null;
        }

        FrameBackNavigation.Remove(this);
        base.DisconnectHandler(platformView);
    }

    private void OnNavigating(object sender, Microsoft.UI.Xaml.Navigation.NavigatingCancelEventArgs e)
    {
        if (NativeView is global::Android.Views.ViewGroup view && Element is Frame frame)
        {
            FrameTransitions.Begin(view, Overlay.NavigationMotion.For(e.NavigationTransitionInfo, e.NavigationMode, frame.Content != null));
        }
    }

    private void OnNavigated(object sender, Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        FrameBackNavigation.Update(Context);
        NavigationStateKeeper.OnFrameNavigated(this);
    }
}
