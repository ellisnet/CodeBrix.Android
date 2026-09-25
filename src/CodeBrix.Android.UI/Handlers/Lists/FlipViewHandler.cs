using System;
using CodeBrix.Android.UI.Platform.Recycler.Portable;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using ALinearLayoutManager = global::AndroidX.RecyclerView.Widget.LinearLayoutManager;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>
/// The handler of FlipView (plan 3: a ViewPager2-like RecyclerView + PagerSnapHelper, the MAUI
/// CarouselView technique): one item per page, filling the viewport; a swipe turns the page and comes to
/// rest on it natively, and the page the pager rests on becomes the FlipView's SelectedIndex; setting
/// SelectedIndex shows that page. Pages are Core's FlipViewItem containers (ItemContainerStyle and the item
/// template apply). Core-only pointer drags (injected input) move the pager and turn one page in the drag
/// direction once they travel past a tenth of the page or end fast.
/// </summary>
internal sealed class FlipViewHandler : RecyclerItemsControlHandler<FlipView>
{
    /// <summary>The share of a page a drag has to travel to turn it.</summary>
    internal const double TurnThreshold = 0.1;

    /// <summary>The release velocity (DIPs per second) that turns a page whatever the travel.</summary>
    internal const double TurnVelocity = 600;

    /// <summary>FlipView's mapper.</summary>
    public static readonly PropertyMapper<FlipView, FlipViewHandler> Mapper = new PropertyMapper<FlipView, FlipViewHandler>(ViewMappers.ViewMapper)
    {
        [Selector.SelectedIndexProperty] = (h, e) => h.ShowSelected(),
    }.WithRecyclerItemsKeys();

    private bool _settling;

    /// <summary>Creates the handler.</summary>
    public FlipViewHandler()
        : base(Mapper)
    {
    }

    /// <inheritdoc />
    protected override ItemsLayoutSpec DefaultSpec => ItemsLayoutSpec.HorizontalPager;

    /// <summary>True when a FlipView can be shown by this handler (not re-templated by the application).</summary>
    internal static bool CanHandle(FlipView flip) =>
        flip != null && flip.ReadLocalValue(Control.TemplateProperty) == DependencyProperty.UnsetValue && !flip.IsGrouping;

    /// <inheritdoc />
    protected override ItemsLayoutSpec ResolveSpec()
    {
        var panel = SpecOf((Element as ItemsControl)?.ItemsPanel, ItemsLayoutSpec.HorizontalPager);
        var horizontal = panel?.ScrollsHorizontally ?? true;
        return horizontal ? ItemsLayoutSpec.HorizontalPager : ItemsLayoutSpec.HorizontalPager with { ScrollsHorizontally = false };
    }

    /// <inheritdoc />
    protected override void OnConnected()
    {
        base.OnConnected();
        if (Engine is { } engine)
        {
            engine.Recycler.Settled += OnSettled;
            engine.Adapter.DataChanged += OnDataChanged;
        }

        ShowSelected();
    }

    /// <inheritdoc />
    protected override void DisconnectHandler(Platform.CodeBrixContentViewGroup platformView)
    {
        if (Engine is { } engine)
        {
            engine.Recycler.Settled -= OnSettled;
            engine.Adapter.DataChanged -= OnDataChanged;
        }

        base.DisconnectHandler(platformView);
    }

    /// <inheritdoc />
    protected override void OnBridgedDragEnded(double travel, double velocity)
    {
        if (Engine is not { } engine || Element is not FlipView flip)
        {
            return;
        }

        var page = CurrentPage();
        var extent = engine.Spec.ScrollsHorizontally ? flip.ActualWidth : flip.ActualHeight;
        var target = page;
        if (extent > 0 && (Math.Abs(travel) >= extent * TurnThreshold || Math.Abs(velocity) >= TurnVelocity))
        {
            // A finger moving left (negative travel) shows the next page.
            target = SelectedPage(flip) + (travel < 0 ? 1 : -1);
        }

        target = Math.Clamp(target, 0, Math.Max(0, ItemCount - 1));
        _settling = true;
        engine.Recycler.SmoothScrollToPosition(target);
    }

    private void ShowSelected()
    {
        if (Engine is not { } engine || Element is not FlipView flip)
        {
            return;
        }

        var index = flip.SelectedIndex;
        if (index < 0 || index >= ItemCount || (_settling && engine.Recycler.ScrollState != 0))
        {
            return;
        }

        if (CurrentPage() != index || engine.Recycler.ChildCount == 0)
        {
            engine.Recycler.StopScroll();
            if (engine.Recycler.GetLayoutManager() is ALinearLayoutManager linear)
            {
                linear.ScrollToPositionWithOffset(index, 0);
            }
            else
            {
                engine.Recycler.ScrollToPosition(index);
            }
        }
    }

    private void OnSettled()
    {
        _settling = false;
        if (Element is not FlipView flip || ItemCount == 0)
        {
            return;
        }

        var page = CurrentPage();
        if (page >= 0 && page != flip.SelectedIndex)
        {
            flip.SelectedIndex = page;
        }
    }

    private void OnDataChanged()
    {
        if (Engine?.Recycler is { } recycler)
        {
            recycler.Post(ShowSelected);
        }
    }

    private static int SelectedPage(FlipView flip) => Math.Max(0, flip.SelectedIndex);

    private int CurrentPage()
    {
        if (Engine is not { } engine || engine.Recycler.GetLayoutManager() is not ALinearLayoutManager linear)
        {
            return -1;
        }

        var complete = linear.FindFirstCompletelyVisibleItemPosition();
        if (complete >= 0)
        {
            return complete;
        }

        var first = linear.FindFirstVisibleItemPosition();
        var last = linear.FindLastVisibleItemPosition();
        if (first < 0)
        {
            return -1;
        }

        if (first == last)
        {
            return first;
        }

        // Between two pages: the one showing more of itself.
        var firstView = linear.FindViewByPosition(first);
        var extent = engine.Spec.ScrollsHorizontally ? engine.Recycler.Width : engine.Recycler.Height;
        if (firstView != null && extent > 0)
        {
            var visible = engine.Spec.ScrollsHorizontally ? firstView.Right : firstView.Bottom;
            return visible * 2 >= extent ? first : first + 1;
        }

        return first;
    }
}
