using System;
using System.Collections.Generic;
using CodeBrix.Android.UI.Platform;
using CodeBrix.Android.UI.Portable.Layout;
using CodeBrix.Platform.UI.Contracts;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using AContext = global::Android.Content.Context;
using AView = global::Android.Views.View;
using AViewStates = global::Android.Views.ViewStates;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>
/// The view of a control whose tab row is native: it mirrors the control's Core template (like the templated
/// fallback) and shows a <see cref="NativeTabStrip"/> on top, laid out where the template's own tab row is.
/// </summary>
internal sealed class TabStripHostView : CodeBrixContentViewGroup
{
    private Rect _stripDips = Rect.Empty;

    /// <summary>Creates the view.</summary>
    /// <param name="context">The context.</param>
    internal TabStripHostView(AContext context)
        : base(context)
    {
        Strip = new NativeTabStrip(context);
        AddView(Strip);
    }

    /// <summary>The native tab row.</summary>
    internal NativeTabStrip Strip { get; }

    /// <summary>Where the tab row goes, relative to this view, in DIPs (empty = hidden).</summary>
    internal Rect StripRect
    {
        get => _stripDips;
        set
        {
            if (_stripDips == value)
            {
                return;
            }

            _stripDips = value;
            Strip.Visibility = value.IsEmpty || value.Width <= 0 || value.Height <= 0 ? AViewStates.Gone : AViewStates.Visible;
            RequestLayout();
        }
    }

    /// <inheritdoc />
    protected override void OnMeasure(int widthMeasureSpec, int heightMeasureSpec)
    {
        base.OnMeasure(widthMeasureSpec, heightMeasureSpec);
        var (left, top, right, bottom) = StripPixels();
        Strip.Measure(MeasureSpecExtensions.Exactly(right - left), MeasureSpecExtensions.Exactly(bottom - top));
    }

    /// <inheritdoc />
    protected override void OnLaidOut(int width, int height)
    {
        base.OnLaidOut(width, height);
        var (left, top, right, bottom) = StripPixels();
        if (Strip.MeasuredWidth != right - left || Strip.MeasuredHeight != bottom - top)
        {
            Strip.Measure(MeasureSpecExtensions.Exactly(right - left), MeasureSpecExtensions.Exactly(bottom - top));
        }

        // The strip was added first and element views are inserted before it, so it stays the last child:
        // drawn on top of the template it covers.
        Strip.Layout(left, top, right, bottom);
    }

    private (int Left, int Top, int Right, int Bottom) StripPixels()
    {
        if (_stripDips.IsEmpty)
        {
            return (0, 0, 0, 0);
        }

        var density = HandlerContext.Density(ElementHandler?.Element);
        var pixels = LayoutReplayMath.ChildPixels(AbsoluteDipOrigin, _stripDips, density);
        return (pixels.Left, pixels.Top, pixels.Right, pixels.Bottom);
    }
}

/// <summary>
/// The base of the TabView / Pivot / SelectorBar handlers (plan 3: a Material TabLayout row). The control
/// keeps its Core template - selection, SelectionChanged, the content area and every event stay Core's - and
/// its tab row is shown natively: the template's own row (<see cref="FindStripPart"/>) is laid out by Core
/// but not drawn, and a <see cref="NativeTabStrip"/> takes its place. A finger on a native tab selects
/// through the control's public selection property (Core then raises what WinUI raises); a selection made
/// in Core (a tap Core saw, code) moves the native indicator.
/// </summary>
/// <typeparam name="TElement">The control type.</typeparam>
internal abstract class TabStripOverlayHandler<TElement> : ViewGroupHandler<TElement, TabStripHostView>
    where TElement : FrameworkElement
{
    private FrameworkElement _hiddenPart;
    private bool _refreshPosted;

    /// <summary>Creates the handler.</summary>
    /// <param name="mapper">The property mapper.</param>
    protected TabStripOverlayHandler(IPropertyMapper mapper)
        : base(mapper)
    {
    }

    /// <inheritdoc />
    public override ElementHandlerCapabilities Capabilities => ElementHandlerCapabilities.OwnsChildren;

    /// <summary>The native tab row (null while disconnected).</summary>
    internal NativeTabStrip Strip => (NativeView as TabStripHostView)?.Strip;

    /// <summary>The template element that is the control's tab row (laid out by Core, not drawn).</summary>
    protected abstract FrameworkElement FindStripPart(TElement element);

    /// <summary>The tabs to show.</summary>
    protected abstract IReadOnlyList<TabSpec> ReadTabs(TElement element);

    /// <summary>The selected tab index (-1 = none).</summary>
    protected abstract int ReadSelectedIndex(TElement element);

    /// <summary>A finger selected tab <paramref name="index"/>: select it in Core.</summary>
    protected abstract void SelectFromNative(TElement element, int index);

    /// <summary>A finger pressed the close button of tab <paramref name="index"/>.</summary>
    protected virtual void CloseFromNative(TElement element, int index)
    {
    }

    /// <summary>A finger pressed the add button.</summary>
    protected virtual void AddFromNative(TElement element)
    {
    }

    /// <summary>True when the native row shows an add button.</summary>
    protected virtual bool ShowsAddButton(TElement element) => false;

    /// <summary>Maps the selection property.</summary>
    public static void MapSelection(TabStripOverlayHandler<TElement> handler, TElement element) =>
        handler.Strip?.SelectIndex(handler.ReadSelectedIndex(element));

    /// <summary>Maps a property that changes the tabs.</summary>
    public static void MapTabs(TabStripOverlayHandler<TElement> handler, TElement element) => handler.PostRefresh();

    /// <summary>Re-reads the tabs and the row position after the current layout pass.</summary>
    protected void PostRefresh()
    {
        if (_refreshPosted || NativeView is not { } view)
        {
            return;
        }

        _refreshPosted = true;
        view.Post(() =>
        {
            _refreshPosted = false;
            Refresh();
        });
    }

    /// <inheritdoc />
    protected override TabStripHostView CreatePlatformView() => new(Context);

    /// <inheritdoc />
    protected override void ConnectHandler(TabStripHostView platformView)
    {
        base.ConnectHandler(platformView);
        platformView.Strip.TabActivated += OnTabActivated;
        platformView.Strip.CloseRequested += OnCloseRequested;
        platformView.Strip.AddRequested += OnAddRequested;
    }

    /// <inheritdoc />
    protected override void DisconnectHandler(TabStripHostView platformView)
    {
        platformView.Strip.TabActivated -= OnTabActivated;
        platformView.Strip.CloseRequested -= OnCloseRequested;
        platformView.Strip.AddRequested -= OnAddRequested;
        ShowPart(null);
        base.DisconnectHandler(platformView);
    }

    /// <inheritdoc />
    protected override void OnArranged(Rect finalRect, bool changed)
    {
        base.OnArranged(finalRect, changed);
        Refresh();
    }

    private void Refresh()
    {
        if (Element is not TElement element || NativeView is not TabStripHostView view)
        {
            return;
        }

        var part = FindStripPart(element);
        ShowPart(part);
        if (part != null && part.ActualWidth > 0 && part.ActualHeight > 0)
        {
            var origin = part.TransformToVisual(element).TransformPoint(default);
            view.StripRect = new Rect(origin.X, origin.Y, part.ActualWidth, part.ActualHeight);
        }
        else
        {
            view.StripRect = Rect.Empty;
        }

        view.Strip.ShowsAddButton = ShowsAddButton(element);
        view.Strip.SetTabs(ReadTabs(element), ReadSelectedIndex(element));
    }

    private void ShowPart(FrameworkElement part)
    {
        if (!ReferenceEquals(_hiddenPart, part) && _hiddenPart?.Handler is IViewHandler { NativeView: { } shown })
        {
            shown.Visibility = _hiddenPart.Visibility == Visibility.Visible ? AViewStates.Visible : AViewStates.Gone;
        }

        _hiddenPart = part;
        if (part?.Handler is IViewHandler { NativeView: AView hidden } && hidden.Visibility == AViewStates.Visible)
        {
            // Core still lays the template's row out (its elements answer hit tests, automation, focus);
            // the native row draws it and takes the fingers.
            hidden.Visibility = AViewStates.Invisible;
        }
    }

    private void OnTabActivated(int index)
    {
        if (Element is TElement element && index != ReadSelectedIndex(element))
        {
            SelectFromNative(element, index);
        }
    }

    private void OnCloseRequested(int index)
    {
        if (Element is TElement element)
        {
            CloseFromNative(element, index);
        }
    }

    private void OnAddRequested()
    {
        if (Element is TElement element)
        {
            AddFromNative(element);
        }
    }

    /// <summary>The first descendant of <paramref name="root"/> of type <typeparamref name="T"/> (template order).</summary>
    protected static T FindDescendant<T>(DependencyObject root)
        where T : class
    {
        if (root == null)
        {
            return null;
        }

        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match)
            {
                return match;
            }

            if (FindDescendant<T>(child) is { } nested)
            {
                return nested;
            }
        }

        return null;
    }
}
