using System;
using System.Numerics;
using CodeBrix.Android.UI.Platform;
using CodeBrix.Android.UI.Portable.Layout;
using CodeBrix.Platform.UI.Contracts;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>
/// The handler of ScrollViewer (seam capability OwnsScrolling). The ScrollViewer keeps its template
/// (scroll bars and the ScrollContentPresenter are Core's); the scrolling itself is native: the
/// presenter's view is a <see cref="CodeBrixScrollView"/> (see <see cref="ScrollContentPresenterHandler"/>).
/// ChangeView arrives here as the seam's ChangeView command (H10) and is forwarded to that view;
/// offset changes Core makes itself (its own manipulation of an injected pointer, the mouse wheel, a
/// clamp) arrive as VerticalOffset/HorizontalOffset changes and move the native view to match.
/// </summary>
internal sealed class ScrollViewerHandler : ViewGroupHandler<ScrollViewer, CodeBrixContentViewGroup>
{
    /// <summary>ScrollViewer's mapper.</summary>
    public static readonly PropertyMapper<ScrollViewer, ScrollViewerHandler> Mapper = new(ViewMappers.ViewMapper)
    {
        [ScrollViewer.HorizontalOffsetProperty] = (h, e) => h.Presenter?.FollowCore(),
        [ScrollViewer.VerticalOffsetProperty] = (h, e) => h.Presenter?.FollowCore(),
        [ScrollViewer.HorizontalScrollModeProperty] = (h, e) => h.Presenter?.UpdateAxes(),
        [ScrollViewer.VerticalScrollModeProperty] = (h, e) => h.Presenter?.UpdateAxes(),
        [ScrollViewer.HorizontalScrollBarVisibilityProperty] = (h, e) => h.Presenter?.UpdateAxes(),
        [ScrollViewer.VerticalScrollBarVisibilityProperty] = (h, e) => h.Presenter?.UpdateAxes(),
    };

    /// <summary>ScrollViewer's commands: ChangeView (H10).</summary>
    public static readonly CommandMapper<ScrollViewer, ScrollViewerHandler> Commands = new(ElementHandler.ElementCommandMapper)
    {
        [ElementHandlerCommands.ChangeView] = (h, e, args) => h.Presenter?.ChangeView(args as ChangeViewRequest) == true,
    };

    private WeakReference<ScrollContentPresenterHandler> _presenter;

    /// <summary>Creates the handler.</summary>
    public ScrollViewerHandler()
        : base(Mapper, Commands)
    {
    }

    /// <inheritdoc />
    public override ElementHandlerCapabilities Capabilities => ElementHandlerCapabilities.OwnsChildren | ElementHandlerCapabilities.OwnsScrolling;

    /// <summary>The presenter handler whose view scrolls natively (null until the template's presenter connected).</summary>
    internal ScrollContentPresenterHandler Presenter
    {
        get
        {
            if (_presenter != null && _presenter.TryGetTarget(out var live) && live.Element != null)
            {
                return live;
            }

            // Not attached yet (or re-created): find it in the template.
            if (Element is ScrollViewer viewer && FindPresenter(viewer) is { Handler: ScrollContentPresenterHandler found })
            {
                _presenter = new WeakReference<ScrollContentPresenterHandler>(found);
                return found;
            }

            return null;
        }
    }

    /// <summary>Links the presenter of this ScrollViewer's template.</summary>
    internal void Attach(ScrollContentPresenterHandler presenter) => _presenter = new WeakReference<ScrollContentPresenterHandler>(presenter);

    /// <inheritdoc />
    protected override CodeBrixContentViewGroup CreatePlatformView() => new(Context);

    private static ScrollContentPresenter FindPresenter(DependencyObject root)
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is ScrollContentPresenter presenter)
            {
                return presenter;
            }

            if (child is not ScrollViewer && FindPresenter(child) is { } nested)
            {
                return nested;
            }
        }

        return null;
    }
}

/// <summary>
/// The handler of ScrollContentPresenter: its view is the native <see cref="CodeBrixScrollView"/>. Core
/// keeps the presenter's layout (the content at its full extent, the extent and viewport sizes) and its
/// offsets; the view scrolls, and every native scroll position is reported to Core through the
/// presenter's own offset path (ScrollContentPresenter.Set -> Updated -> ScrollViewer.OnPresenterScrolled),
/// which also keeps the presenter's offsets right for Core's ChangeView, wheel and manipulation logic.
/// The content's composition visual follows the offset (AnchorPoint = -offset, what Core's managed
/// presenter does when it scrolls itself), so Core's hit testing and TransformToVisual see the content
/// where the native view shows it.
/// </summary>
internal sealed class ScrollContentPresenterHandler : ViewGroupHandler<ScrollContentPresenter, CodeBrixScrollView>
{
    /// <summary>ScrollContentPresenter's mapper.</summary>
    public static readonly PropertyMapper<ScrollContentPresenter, ScrollContentPresenterHandler> Mapper = new(ViewMappers.ViewMapper);

    private bool _reporting;

    /// <summary>Creates the handler.</summary>
    public ScrollContentPresenterHandler()
        : base(Mapper)
    {
    }

    /// <inheritdoc />
    public override ElementHandlerCapabilities Capabilities => ElementHandlerCapabilities.OwnsChildren;

    /// <summary>
    /// The ScrollViewer whose template holds the presenter (its TemplatedParent, else the nearest
    /// ScrollViewer above it). Not ScrollContentPresenter.ScrollOwner: that getter throws until the
    /// presenter has loaded, and the handler connects before that.
    /// </summary>
    private ScrollViewer Owner
    {
        get
        {
            if (Element is not ScrollContentPresenter presenter)
            {
                return null;
            }

            if (presenter.TemplatedParent is ScrollViewer templated)
            {
                return templated;
            }

            for (var parent = VisualTreeHelper.GetParent(presenter); parent != null; parent = VisualTreeHelper.GetParent(parent))
            {
                if (parent is ScrollViewer viewer)
                {
                    return viewer;
                }
            }

            return null;
        }
    }

    /// <summary>Applies the ScrollViewer's scroll modes and bar visibilities to the native view.</summary>
    internal void UpdateAxes()
    {
        if (NativeView is not CodeBrixScrollView view || Element is not ScrollContentPresenter presenter)
        {
            return;
        }

        var owner = Owner;
        view.AllowsVerticalScroll = presenter.CanVerticallyScroll
            && owner is not { VerticalScrollMode: ScrollMode.Disabled }
            && owner is not { VerticalScrollBarVisibility: ScrollBarVisibility.Disabled };
        view.AllowsHorizontalScroll = presenter.CanHorizontallyScroll
            && owner is not { HorizontalScrollMode: ScrollMode.Disabled }
            && owner is not { HorizontalScrollBarVisibility: ScrollBarVisibility.Disabled };
    }

    /// <summary>Moves the native view to the offsets Core holds (no report back).</summary>
    internal void FollowCore()
    {
        if (_reporting || NativeView is not CodeBrixScrollView view || Element is not ScrollContentPresenter presenter)
        {
            return;
        }

        var density = Density;
        UpdateExtent(view, presenter, density);
        var (x, y) = (OffsetToPixels(presenter.HorizontalOffset, density), OffsetToPixels(presenter.VerticalOffset, density));
        view.FollowCore(x, y);
        MoveContentVisual(presenter.HorizontalOffset, presenter.VerticalOffset);
    }

    /// <summary>The seam's ChangeView: scroll the native view there (animated unless asked not to).</summary>
    internal bool ChangeView(ChangeViewRequest request)
    {
        if (request == null || NativeView is not CodeBrixScrollView view || Element is not ScrollContentPresenter presenter)
        {
            return false;
        }

        var density = Density;
        UpdateExtent(view, presenter, density);
        var x = request.HorizontalOffset is { } h ? OffsetToPixels(h, density) : view.ScrollX;
        var y = request.VerticalOffset is { } v ? OffsetToPixels(v, density) : view.ScrollY;
        view.ScrollToPosition(x, y, animate: !request.DisableAnimation);
        return true;
    }

    /// <inheritdoc />
    protected override CodeBrixScrollView CreatePlatformView() => new(Context);

    /// <inheritdoc />
    protected override void ConnectHandler(CodeBrixScrollView platformView)
    {
        base.ConnectHandler(platformView);
        platformView.Scrolled += OnNativeScrolled;
    }

    /// <inheritdoc />
    protected override void DisconnectHandler(CodeBrixScrollView platformView)
    {
        platformView.Scrolled -= OnNativeScrolled;
        base.DisconnectHandler(platformView);
    }

    /// <inheritdoc />
    protected override void OnConnected()
    {
        base.OnConnected();
        if (Owner?.Handler is ScrollViewerHandler owner)
        {
            owner.Attach(this);
        }

        UpdateAxes();
    }

    /// <inheritdoc />
    protected override void OnArranged(Rect finalRect, bool changed)
    {
        base.OnArranged(finalRect, changed);
        if (NativeView is CodeBrixScrollView view && Element is ScrollContentPresenter presenter)
        {
            if (Owner?.Handler is ScrollViewerHandler owner)
            {
                owner.Attach(this);
            }

            UpdateAxes();
            UpdateExtent(view, presenter, Density);
            FollowCore();
        }
    }

    private static int OffsetToPixels(double offset, double density) =>
        double.IsNaN(offset) ? 0 : (int)Math.Round(offset * density);

    private static void UpdateExtent(CodeBrixScrollView view, ScrollContentPresenter presenter, double density) =>
        view.ExtentPx = (LayoutReplayMath.ToPixels(presenter.ExtentWidth, density), LayoutReplayMath.ToPixels(presenter.ExtentHeight, density));

    private void OnNativeScrolled(int x, int y, bool isIntermediate)
    {
        if (Element is not ScrollContentPresenter presenter)
        {
            return;
        }

        var density = Density;
        var horizontal = LayoutReplayMath.FromPixels(x, density);
        var vertical = LayoutReplayMath.FromPixels(y, density);
        MoveContentVisual(horizontal, vertical);
        _reporting = true;
        try
        {
            // Core's own offset path: the presenter's offsets, then ScrollViewer.OnPresenterScrolled
            // (ViewChanging/ViewChanged, HorizontalOffset/VerticalOffset). No content translation: OwnsScrolling.
            presenter.Set(horizontal, vertical, null, true, isIntermediate, nameof(OnNativeScrolled), 0);
        }
        finally
        {
            _reporting = false;
        }
    }

    private void MoveContentVisual(double horizontal, double vertical)
    {
        if (Element is not ScrollContentPresenter presenter)
        {
            return;
        }

        var content = presenter.Content as UIElement
            ?? (VisualTreeHelper.GetChildrenCount(presenter) > 0 ? VisualTreeHelper.GetChild(presenter, 0) as UIElement : null);
        if (content?.Visual is { } visual)
        {
            var anchor = new Vector2(-(float)horizontal, -(float)vertical);
            if (visual.AnchorPoint != anchor)
            {
                visual.AnchorPoint = anchor;
            }
        }
    }
}
