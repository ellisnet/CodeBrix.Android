// Technique from .NET MAUI, src/Core/src/Platform/Android/MauiScrollView.cs @ 828569a864 (a native scroll view around the
// cross-platform content; the scroll position reported to the cross-platform element, the element's scroll requests
// forwarded to the view). Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License. See
// THIRD-PARTY-NOTICES.txt.

using System;
using CodeBrix.Android.UI.Platform;
using CodeBrix.Android.UI.Portable.Layout;
using CodeBrix.Platform.UI.Contracts;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Windows.Foundation;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>
/// AP10-B: the handler of ScrollPresenter, the scroller inside the new WinUI ScrollView (tsv row ScrollView: "same handler
/// as ScrollViewer (NestedScrollView)"). ScrollView keeps its Fluent template (its scroll bars and its ScrollPresenter,
/// whose own scrolling is a composition interaction tracker that draws nothing on Android); the ScrollPresenter's view is
/// the same native scroll view the ScrollViewer's presenter uses (<see cref="CodeBrixScrollView"/>), holding the content
/// at its full extent. A finger scrolls it natively and every position is reported to Core through the presenter's own
/// ScrollTo (no animation, snap points ignored), so HorizontalOffset / VerticalOffset, ViewChanged and the scroll bars
/// follow; a scroll Core makes (ScrollTo / ScrollBy from code, a scroll bar) arrives as ViewChanged and moves the view.
/// </summary>
internal sealed class ScrollPresenterHandler : ViewGroupHandler<ScrollPresenter, CodeBrixScrollView>
{
    /// <summary>ScrollPresenter's mapper.</summary>
    public static readonly PropertyMapper<ScrollPresenter, ScrollPresenterHandler> Mapper = new(ViewMappers.ViewMapper)
    {
        [ScrollPresenter.HorizontalScrollModeProperty] = (h, e) => h.UpdateAxes(),
        [ScrollPresenter.VerticalScrollModeProperty] = (h, e) => h.UpdateAxes(),
        [ScrollPresenter.ContentOrientationProperty] = (h, e) => h.UpdateAxes(),
    };

    private bool _reporting;
    private bool _following;

    /// <summary>Creates the handler.</summary>
    public ScrollPresenterHandler()
        : base(Mapper)
    {
    }

    /// <inheritdoc />
    public override ElementHandlerCapabilities Capabilities => ElementHandlerCapabilities.OwnsChildren;

    /// <summary>The native scroll position in pixels (tests).</summary>
    internal (int X, int Y) NativeOffset => PlatformView == null ? (0, 0) : (PlatformView.ScrollX, PlatformView.ScrollY);

    /// <inheritdoc />
    protected override CodeBrixScrollView CreatePlatformView() => new(Context);

    /// <inheritdoc />
    protected override void ConnectHandler(CodeBrixScrollView platformView)
    {
        base.ConnectHandler(platformView);
        platformView.Scrolled += OnNativeScrolled;
        if (Element is ScrollPresenter presenter)
        {
            presenter.ViewChanged += OnCoreViewChanged;
        }
    }

    /// <inheritdoc />
    protected override void DisconnectHandler(CodeBrixScrollView platformView)
    {
        platformView.Scrolled -= OnNativeScrolled;
        if (Element is ScrollPresenter presenter)
        {
            presenter.ViewChanged -= OnCoreViewChanged;
        }

        base.DisconnectHandler(platformView);
    }

    /// <inheritdoc />
    protected override void OnArranged(Rect finalRect, bool changed)
    {
        base.OnArranged(finalRect, changed);
        UpdateAxes();
        FollowCore();
    }

    private void UpdateAxes()
    {
        if (PlatformView is not { } view || Element is not ScrollPresenter presenter)
        {
            return;
        }

        view.AllowsHorizontalScroll = presenter.HorizontalScrollMode != ScrollingScrollMode.Disabled
            && presenter.ContentOrientation is ScrollingContentOrientation.Horizontal or ScrollingContentOrientation.Both or ScrollingContentOrientation.None;
        view.AllowsVerticalScroll = presenter.VerticalScrollMode != ScrollingScrollMode.Disabled
            && presenter.ContentOrientation is ScrollingContentOrientation.Vertical or ScrollingContentOrientation.Both or ScrollingContentOrientation.None;
    }

    private void FollowCore()
    {
        if (_reporting || PlatformView is not { } view || Element is not ScrollPresenter presenter)
        {
            return;
        }

        var density = Density;
        view.ExtentPx = (LayoutReplayMath.ToPixels(presenter.ExtentWidth, density), LayoutReplayMath.ToPixels(presenter.ExtentHeight, density));
        _following = true;
        try
        {
            view.FollowCore(Pixels(presenter.HorizontalOffset, density), Pixels(presenter.VerticalOffset, density));
        }
        finally
        {
            _following = false;
        }
    }

    private void OnCoreViewChanged(ScrollPresenter sender, object args) => FollowCore();

    private void OnNativeScrolled(int x, int y, bool isIntermediate)
    {
        if (_following || Element is not ScrollPresenter presenter)
        {
            return;
        }

        var density = Density;
        _reporting = true;
        try
        {
            // The presenter's own scroll request, immediate: its offsets, ViewChanged and the scroll bars follow.
            presenter.ScrollTo(
                LayoutReplayMath.FromPixels(x, density),
                LayoutReplayMath.FromPixels(y, density),
                new ScrollingScrollOptions(ScrollingAnimationMode.Disabled, ScrollingSnapPointsMode.Ignore));
        }
        finally
        {
            _reporting = false;
        }
    }

    private static int Pixels(double offset, double density) => double.IsNaN(offset) ? 0 : (int)Math.Round(offset * density);
}
