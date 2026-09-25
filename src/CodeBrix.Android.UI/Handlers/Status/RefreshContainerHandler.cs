// Technique from .NET MAUI, src/Core/src/Platform/Android/MauiSwipeRefreshLayout.cs and
// src/Core/src/Handlers/RefreshView/RefreshViewHandler.Android.cs @ 828569a864 (a SwipeRefreshLayout around the
// content; CanChildScrollUp asks the scrollable views inside the content; Refreshing follows the cross-platform
// state both ways). Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License. See
// THIRD-PARTY-NOTICES.txt.

using System;
using CodeBrix.Android.UI.Input;
using CodeBrix.Android.UI.Platform;
using CodeBrix.Android.UI.Portable.Layout;
using CodeBrix.Platform.UI.Contracts;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using AContext = global::Android.Content.Context;
using AMotionEvent = global::Android.Views.MotionEvent;
using ASwipeRefreshLayout = AndroidX.SwipeRefreshLayout.Widget.SwipeRefreshLayout;
using AView = global::Android.Views.View;
using AViewGroup = global::Android.Views.ViewGroup;
using AViewStates = global::Android.Views.ViewStates;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>
/// AP10-B: the native view of a RefreshContainer: a SwipeRefreshLayout whose one child (its pull target) is the view
/// group mirroring the control's Core template.
/// </summary>
internal sealed class RefreshContainerLayout : ASwipeRefreshLayout
{
    /// <summary>Creates the view.</summary>
    /// <param name="context">A Material 3 context.</param>
    internal RefreshContainerLayout(AContext context)
        : base(context)
    {
        Content = new CodeBrixContentViewGroup(context);
        AddView(Content, new AViewGroup.LayoutParams(AViewGroup.LayoutParams.MatchParent, AViewGroup.LayoutParams.MatchParent));
    }

    /// <summary>The view group mirroring the template (the pull target).</summary>
    internal CodeBrixContentViewGroup Content { get; }

    /// <summary>The handler (for the layout origin).</summary>
    internal IAndroidElementHandler ElementHandler { get; set; }

    /// <summary>
    /// True when a view inside the content can scroll up: the pull then scrolls it instead of refreshing (MAUI's
    /// MauiSwipeRefreshLayout rule, over the native scroll views of the Core content).
    /// </summary>
    public override bool CanChildScrollUp() => CanScrollUp(Content);

    /// <inheritdoc />
    public override bool OnInterceptTouchEvent(AMotionEvent ev)
    {
        var intercepted = base.OnInterceptTouchEvent(ev);
        if (intercepted && ev != null)
        {
            // The pull is the layout's from now on: Core gets a cancel and no more of this pointer.
            NativeInput.CancelPointer(ev, ev.ActionIndex);
        }

        return intercepted;
    }

    /// <inheritdoc />
    protected override void OnLayout(bool changed, int left, int top, int right, int bottom)
    {
        if (Parent is CodeBrixViewGroup parent && ElementHandler is { HasArranged: true } handler)
        {
            // What a CodeBrixViewGroup parent does for a view group child: its element origin in window DIPs.
            Content.AbsoluteDipOrigin = LayoutReplayMath.ChildOrigin(parent.AbsoluteDipOrigin, handler.ArrangedRect);
        }

        base.OnLayout(changed, left, top, right, bottom);
    }

    private static bool CanScrollUp(AView view)
    {
        if (view == null || view.Visibility != AViewStates.Visible)
        {
            return false;
        }

        if (view.CanScrollVertically(-1))
        {
            return true;
        }

        if (view is AViewGroup group)
        {
            for (var i = 0; i < group.ChildCount; i++)
            {
                if (CanScrollUp(group.GetChildAt(i)))
                {
                    return true;
                }
            }
        }

        return false;
    }
}

/// <summary>
/// AP10-B: the handler of RefreshContainer (tsv rows RefreshContainer / RefreshVisualizer: "SwipeRefreshLayout"). The
/// container keeps its Fluent template - the content, the RefreshVisualizer and its state machine, RefreshRequested and
/// its deferral stay Core's - inside a native SwipeRefreshLayout (<see cref="RefreshContainerLayout"/>), which takes
/// the pull gesture (only when nothing in the content can scroll up) and shows the Material refresh indicator. A pull
/// runs RequestRefresh (Core raises RefreshRequested; the visualizer goes to Refreshing until the app completes the
/// deferral); the visualizer's state drives the native indicator both ways, so RequestRefresh from code shows it too.
/// The template's own RefreshVisualizer (whose pull animation is a composition animation) is laid out by Core and not
/// drawn: the native indicator replaces it. PullDirection other than TopToBottom has no Material form: the layout is
/// disabled and nothing pulls.
/// </summary>
internal sealed class RefreshContainerHandler : ViewHandler<RefreshContainer, RefreshContainerLayout>, IViewGroupHandler
{
    /// <summary>RefreshContainer's mapper.</summary>
    public static readonly PropertyMapper<RefreshContainer, RefreshContainerHandler> Mapper = new(ViewMappers.ViewMapper)
    {
        [RefreshContainer.PullDirectionProperty] = MapPullDirection,
        [RefreshContainer.VisualizerProperty] = (h, e) => h.FollowVisualizer(),
        [Control.IsEnabledProperty] = MapPullDirection,
    };

    private RefreshVisualizer _visualizer;
    private AView _hiddenVisualizerView;
    private bool _requesting;

    /// <summary>Creates the handler.</summary>
    public RefreshContainerHandler()
        : base(Mapper)
    {
    }

    /// <inheritdoc />
    public override ElementHandlerCapabilities Capabilities => ElementHandlerCapabilities.OwnsChildren;

    /// <inheritdoc />
    public CodeBrixViewGroup ViewGroup => PlatformView?.Content;

    /// <summary>The handler, or the templated fallback for a re-templated RefreshContainer.</summary>
    /// <param name="element">The RefreshContainer.</param>
    /// <returns>The handler.</returns>
    internal static IAndroidElementHandler Create(UIElement element) =>
        element is RefreshContainer refresh && NativeControlPolicy.IsNative(refresh, typeof(RefreshContainer), new[] { "DefaultRefreshContainerStyle" }, out _)
            ? new RefreshContainerHandler()
            : new TemplatedFallbackHandler();

    /// <summary>Maps PullDirection and IsEnabled: only a top-to-bottom pull has a Material form.</summary>
    public static void MapPullDirection(RefreshContainerHandler handler, RefreshContainer element) =>
        handler.PlatformView.Enabled = element.IsEnabled && element.PullDirection == RefreshPullDirection.TopToBottom;

    /// <summary>True while the native indicator shows a refresh.</summary>
    internal bool IsNativeRefreshing => PlatformView?.Refreshing == true;

    /// <summary>Starts a refresh as a finished pull does (tests and accessibility).</summary>
    internal void PullFromNative() => OnNativeRefresh(this, EventArgs.Empty);

    /// <inheritdoc />
    public override void OnChildAdded(UIElement child, int index)
    {
        if (child?.Handler is IViewHandler { NativeView: { } view } && ViewGroup is { } group)
        {
            group.AddElementChild(child, view, index);
        }
    }

    /// <inheritdoc />
    public override void OnChildRemoved(UIElement child)
    {
        if (child != null)
        {
            ViewGroup?.RemoveElementChild(child);
        }
    }

    /// <inheritdoc />
    public override void OnChildMoved(int oldIndex, int newIndex) => ViewGroup?.MoveElementChild(oldIndex, newIndex);

    /// <inheritdoc />
    public void AttachChild(UIElement child, AView view)
    {
        if (ViewGroup is not { } group || Element is not { } element)
        {
            return;
        }

        var index = 0;
        var count = VisualTreeHelper.GetChildrenCount(element);
        for (var i = 0; i < count; i++)
        {
            var sibling = VisualTreeHelper.GetChild(element, i);
            if (ReferenceEquals(sibling, child))
            {
                break;
            }

            if (sibling is UIElement shown && group.ViewOf(shown) != null)
            {
                index++;
            }
        }

        group.AddElementChild(child, view, index);
    }

    /// <inheritdoc />
    protected override RefreshContainerLayout CreatePlatformView() => new(MaterialWidgets.Material3(Context));

    /// <inheritdoc />
    protected override void ConnectHandler(RefreshContainerLayout platformView)
    {
        base.ConnectHandler(platformView);
        platformView.ElementHandler = this;
        platformView.Content.ElementHandler = this;
        platformView.Refresh += OnNativeRefresh;
    }

    /// <inheritdoc />
    protected override void DisconnectHandler(RefreshContainerLayout platformView)
    {
        platformView.Refresh -= OnNativeRefresh;
        platformView.ElementHandler = null;
        platformView.Content.ElementHandler = null;
        FollowVisualizer(null);
        ShowVisualizerView(null);
        base.DisconnectHandler(platformView);
    }

    /// <inheritdoc />
    protected override void OnArranged(Rect finalRect, bool changed)
    {
        base.OnArranged(finalRect, changed);
        PlatformView?.Content.RequestLayout();
        FollowVisualizer();
    }

    private void FollowVisualizer() => FollowVisualizer((Element as RefreshContainer)?.Visualizer);

    private void FollowVisualizer(RefreshVisualizer visualizer)
    {
        if (!ReferenceEquals(visualizer, _visualizer))
        {
            if (_visualizer != null)
            {
                _visualizer.RefreshStateChanged -= OnRefreshStateChanged;
            }

            _visualizer = visualizer;
            if (visualizer != null)
            {
                visualizer.RefreshStateChanged += OnRefreshStateChanged;
                ShowState(visualizer.State);
            }
        }

        ShowVisualizerView(visualizer);
    }

    private void ShowVisualizerView(RefreshVisualizer visualizer)
    {
        var view = (visualizer?.Handler as IViewHandler)?.NativeView;
        if (!ReferenceEquals(view, _hiddenVisualizerView) && _hiddenVisualizerView is { Handle: var handle } previous && handle != IntPtr.Zero
            && previous.Visibility == AViewStates.Invisible)
        {
            previous.Visibility = AViewStates.Visible;
        }

        _hiddenVisualizerView = view;
        if (view != null && view.Visibility == AViewStates.Visible)
        {
            // Core still runs the visualizer's state machine; the SwipeRefreshLayout's indicator is what is drawn.
            view.Visibility = AViewStates.Invisible;
        }
    }

    private void OnRefreshStateChanged(RefreshVisualizer sender, RefreshStateChangedEventArgs args) => ShowState(args.NewState);

    private void ShowState(RefreshVisualizerState state)
    {
        if (PlatformView is not { } layout)
        {
            return;
        }

        var refreshing = state == RefreshVisualizerState.Refreshing;
        if (layout.Refreshing != refreshing)
        {
            layout.Refreshing = refreshing;
        }
    }

    private void OnNativeRefresh(object sender, EventArgs e)
    {
        if (_requesting || Element is not RefreshContainer element)
        {
            return;
        }

        _requesting = true;
        try
        {
            FollowVisualizer();
            element.RequestRefresh();
            if (_visualizer == null || _visualizer.State != RefreshVisualizerState.Refreshing)
            {
                // Core has no visualizer to run the refresh: nothing will stop the native indicator.
                ShowState(_visualizer?.State ?? RefreshVisualizerState.Idle);
            }
        }
        finally
        {
            _requesting = false;
        }
    }
}
