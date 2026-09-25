using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using CodeBrix.Android.UI.Hosting;
using CodeBrix.Android.UI.Platform;
using CodeBrix.Android.UI.Policy;
using CodeBrix.Android.UI.Portable.Layout;
using CodeBrix.Platform.UI.Contracts;
using CodeBrix.Platform.UI.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;
using Windows.Foundation.Collections;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>
/// The handler of NavigationView (plan 2.10 row "NavigationView (Auto)", D-P6; AP4 back navigation).
/// A NavigationView whose PaneDisplayMode is Auto is shown in the Material container the adaptive table picks for
/// its window's size classes (<see cref="AdaptivePolicy.NavigationView"/>): a bottom navigation bar (Compact, at
/// most five destinations), a navigation rail (Medium), a persistent navigation drawer (Expanded; collapsed to the
/// rail while IsPaneOpen is false), or a top bar with a modal drawer (Compact or Medium with more destinations).
/// The containers re-map LIVE when the window changes class (<see cref="WindowSizeClassMonitor"/>: rotation,
/// resize, a docked phone entering desktop mode) - only the chrome changes, the content (the page) stays.
/// An explicit PaneDisplayMode (Left, LeftCompact, LeftMinimal, Top) is the app asking for that WinUI
/// presentation, and a NavigationView whose pane carries what a Material container has no place for
/// (<see cref="NavigationMenu.CanMapNatively"/>) keeps Core's Fluent template (mirrored like the templated
/// fallback). Either way the Android back button / gesture reaches it (<see cref="BackNavigation"/>).
/// </summary>
/// <remarks>
/// Native containers: the content is hosted directly (HostsContent; the template is not materialized) and laid out
/// in the room the chrome leaves; destinations are the menu items of the container (text and icon), selection
/// syncs both ways (a picked destination raises ItemInvoked - InvokedItemContainer is the NavigationViewItem - and
/// sets SelectedItem, which raises SelectionChanged); Settings (IsSettingsVisible) is the last destination and
/// raises ItemInvoked with IsSettingsInvoked (there is no SettingsItem without the template, so SelectedItem is
/// left as it was); the modal drawer follows IsPaneOpen and raises PaneOpening/Opened/Closing/Closed.
/// A native container ABSORBS the window's safe-area insets it overlaps (as a Page does, plan D-P10): the chrome
/// pads its destinations clear of the system bars and the content is laid out clear of them (a Page content
/// absorbs its own overlap; <see cref="NavigationChromeInsets"/>).
/// </remarks>
internal sealed class NavigationViewHandler : ViewGroupHandler<NavigationView, AdaptiveNavigationLayout>, ISafeAreaAbsorber
{
    /// <summary>NavigationView's mapper.</summary>
    public static readonly PropertyMapper<NavigationView, NavigationViewHandler> Mapper = new(ViewMappers.ViewMapper)
    {
        [NavigationView.IsPaneOpenProperty] = MapIsPaneOpen,
        [NavigationView.IsBackEnabledProperty] = MapBackState,
        [NavigationView.IsBackButtonVisibleProperty] = MapBackState,
        [NavigationView.DisplayModeProperty] = MapBackState,
        [NavigationView.PaneDisplayModeProperty] = MapStructure,
        [NavigationView.MenuItemsSourceProperty] = MapStructure,
        [NavigationView.IsSettingsVisibleProperty] = MapStructure,
        [NavigationView.PaneHeaderProperty] = MapStructure,
        [NavigationView.PaneFooterProperty] = MapStructure,
        [NavigationView.PaneCustomContentProperty] = MapStructure,
        [NavigationView.AutoSuggestBoxProperty] = MapStructure,
        [NavigationView.MenuItemTemplateProperty] = MapStructure,
        [NavigationView.MenuItemTemplateSelectorProperty] = MapStructure,
        [NavigationView.OpenPaneLengthProperty] = MapStructure,
        [NavigationView.PaneTitleProperty] = MapStructure,
        [NavigationView.SelectedItemProperty] = MapSelectedItem,
    };

    private readonly NavigationViewItem _settingsItem = new() { Content = NavigationMenu.SettingsLabel, Icon = new SymbolIcon(Symbol.Setting) };
    private NavigationContainer _container;
    private IReadOnlyList<NavigationMenuEntry> _entries = Array.Empty<NavigationMenuEntry>();
    private IObservableVector<object> _watchedItems;
    private INotifyCollectionChanged _watchedSource;
    private bool _settingPane;
    private bool _remapping;
    private SafeAreaPadding _safeArea;
    private AndroidNativeWindowWrapper _wrapper;

    /// <summary>Creates the handler (the container is decided when it connects).</summary>
    public NavigationViewHandler()
        : base(Mapper)
    {
    }

    /// <summary>Creates the handler of <paramref name="view"/>: its container is decided now, because Core reads the capabilities before it connects the handler.</summary>
    /// <param name="view">The NavigationView.</param>
    internal NavigationViewHandler(NavigationView view)
        : base(Mapper)
    {
        _container = Decide(view, NavigationMenu.Entries(view, _settingsItem));
    }

    /// <summary>The container shown (<see cref="NavigationContainer.Template"/> = Core's Fluent template).</summary>
    internal NavigationContainer Container => _container;

    /// <summary>True while a native container shows the NavigationView.</summary>
    internal bool IsNative => _container != NavigationContainer.Template;

    /// <summary>True when the Android back button closes this NavigationView's pane (an open modal drawer).</summary>
    internal bool ClosesPaneOnBack => _container == NavigationContainer.ModalDrawer && Element is NavigationView { IsPaneOpen: true };

    /// <summary>The native chrome view (tests, diagnostics).</summary>
    internal AdaptiveNavigationLayout Layout => PlatformView;

    /// <inheritdoc />
    public override ElementHandlerCapabilities Capabilities => IsNative
        ? ElementHandlerCapabilities.OwnsVisuals | ElementHandlerCapabilities.HostsContent | ElementHandlerCapabilities.MeasuresNatively | ElementHandlerCapabilities.OwnsChildren
        : ElementHandlerCapabilities.OwnsChildren;

    /// <summary>Re-evaluates the activity's back callback when a back-relevant property changes.</summary>
    public static void MapBackState(NavigationViewHandler handler, NavigationView view)
    {
        if (handler.Context is CodeBrixActivity activity)
        {
            BackNavigation.Update(activity);
        }
    }

    /// <summary>Re-decides the container (the menu, the pane contents, the display mode or the pane width changed).</summary>
    public static void MapStructure(NavigationViewHandler handler, NavigationView view)
    {
        handler.WatchItems(view);
        handler.Remap();
    }

    /// <summary>Opens or closes the modal drawer, or switches an Expanded window between the drawer and the rail.</summary>
    public static void MapIsPaneOpen(NavigationViewHandler handler, NavigationView view)
    {
        MapBackState(handler, view);
        if (handler._settingPane || !handler.IsNative)
        {
            return;
        }

        if (handler._container == NavigationContainer.ModalDrawer)
        {
            handler.ShowDrawer(view.IsPaneOpen, fromPlatform: false);
        }
        else
        {
            handler.Remap();
        }
    }

    /// <summary>Checks the destination of SelectedItem in the native container.</summary>
    public static void MapSelectedItem(NavigationViewHandler handler, NavigationView view)
    {
        if (handler.IsNative && handler.PlatformView is { } layout)
        {
            layout.Select(NavigationMenu.IndexOf(handler._entries, view.SelectedItem));
        }
    }

    /// <inheritdoc />
    public override Size Measure(Size availableSize)
    {
        if (!IsNative || Element is not NavigationView view)
        {
            return new Size(0, 0);
        }

        var insets = ContentInsets(view);
        var inner = new Size(
            Math.Max(0, availableSize.Width - insets.Left - insets.Right),
            Math.Max(0, availableSize.Height - insets.Top - insets.Bottom));
        var desired = new Size(0, 0);
        if (Content(view) is { } content)
        {
            content.Measure(inner);
            desired = content.DesiredSize;
        }

        return new Size(desired.Width + insets.Left + insets.Right, desired.Height + insets.Top + insets.Bottom);
    }

    /// <inheritdoc />
    protected override AdaptiveNavigationLayout CreatePlatformView() => new(Context);

    /// <inheritdoc />
    protected override void ConnectHandler(AdaptiveNavigationLayout platformView)
    {
        base.ConnectHandler(platformView);
        platformView.ItemSelected += OnItemSelected;
        platformView.DrawerToggleRequested += OnDrawerToggleRequested;
        platformView.DrawerDismissRequested += OnDrawerDismissRequested;
        WindowSizeClassMonitor.Changed += OnSizeClassChanged;
    }

    /// <inheritdoc />
    protected override void OnConnected()
    {
        base.OnConnected();
        BackNavigation.Add(this);
        SafeAreaAbsorbers.Add(this);
        _wrapper = (Element?.XamlRoot is { } root ? XamlRootMap.GetHostForRoot(root) as AndroidXamlRootHost : null)?.Wrapper;
        if (_wrapper != null)
        {
            _wrapper.SafeAreaChanged += OnSafeAreaChanged;
        }
        if (Element is NavigationView view)
        {
            WatchItems(view);
        }

        Remap();
    }

    /// <inheritdoc />
    protected override void DisconnectHandler(AdaptiveNavigationLayout platformView)
    {
        WindowSizeClassMonitor.Changed -= OnSizeClassChanged;
        platformView.ItemSelected -= OnItemSelected;
        platformView.DrawerToggleRequested -= OnDrawerToggleRequested;
        platformView.DrawerDismissRequested -= OnDrawerDismissRequested;
        UnwatchItems();
        BackNavigation.Remove(this);
        SafeAreaAbsorbers.Remove(this);
        if (_wrapper != null)
        {
            _wrapper.SafeAreaChanged -= OnSafeAreaChanged;
            _wrapper = null;
        }

        base.DisconnectHandler(platformView);
    }

    /// <inheritdoc />
    public void RevalidateSafeArea()
    {
        if (Element is not NavigationView view || !HasArranged)
        {
            return;
        }

        var computed = ComputeSafeArea(view);
        if (SafeAreaMath.Differs(computed, _safeArea))
        {
            _safeArea = computed;
            if (PlatformView is { } layout)
            {
                layout.SafeArea = computed;
            }

            view.InvalidateMeasure();
            view.InvalidateArrange();
        }
    }

    /// <inheritdoc />
    protected override void OnArranged(Rect finalRect, bool changed)
    {
        if (IsNative && Element is NavigationView view && Content(view) is { } content)
        {
            var insets = ContentInsets(view);
            content.Arrange(new Rect(
                insets.Left,
                insets.Top,
                Math.Max(0, finalRect.Width - insets.Left - insets.Right),
                Math.Max(0, finalRect.Height - insets.Top - insets.Bottom)));
        }

        base.OnArranged(finalRect, changed);
    }

    private static UIElement Content(NavigationView view) => view.ContentTemplateRoot ?? view.Content as UIElement;

    /// <summary>The room the content leaves free: the chrome's and the absorbed safe area's.</summary>
    private Thickness ContentInsets(NavigationView view)
    {
        var contentAbsorbs = Content(view) is Page { Handler: PageHandler };
        var insets = NavigationChromeInsets.Content(ShownContainer(view), view.OpenPaneLength, _safeArea, contentAbsorbs);
        return new Thickness(insets.Left, insets.Top, insets.Right, insets.Bottom);
    }

    private void OnSafeAreaChanged(object sender, EventArgs e)
    {
        RevalidateSafeArea();

        // The window's density changed (a display-density change, a docked phone): the destinations' icons are
        // drawn for the density the menu was filled at - fill it again at the new one.
        if (IsNative && Element is NavigationView view && PlatformView is { IconDensity: > 0 } layout && layout.IconDensity != HandlerContext.Density(view))
        {
            Remap();
        }
    }

    /// <summary>The safe-area insets (DIPs) the NavigationView's layout rectangle overlaps (as PageHandler computes them).</summary>
    private SafeAreaPadding ComputeSafeArea(NavigationView view)
    {
        var safeArea = _wrapper?.SafeAreaDips ?? SafeAreaPadding.Empty;
        if (safeArea.IsEmpty || view.XamlRoot is not { } root)
        {
            return SafeAreaPadding.Empty;
        }

        var x = ArrangedRect.X;
        var y = ArrangedRect.Y;
        for (var parent = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(view); parent is UIElement ui; parent = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(ui))
        {
            if (ui.Handler is IAndroidElementHandler { HasArranged: true } handler)
            {
                x += handler.ArrangedRect.X;
                y += handler.ArrangedRect.Y;
            }
        }

        return SafeAreaMath.Overlap(x, y, ArrangedRect.Width, ArrangedRect.Height, root.Size.Width, root.Size.Height, safeArea);
    }

    private static NavigationContainer Decide(NavigationView view, IReadOnlyList<NavigationMenuEntry> entries)
    {
        if (view == null || view.PaneDisplayMode != NavigationViewPaneDisplayMode.Auto || !AdaptivePolicy.AdaptiveNavigationView)
        {
            return NavigationContainer.Template;
        }

        if (!NavigationMenu.CanMapNatively(view, out _))
        {
            return NavigationContainer.Template;
        }

        return AdaptivePolicy.NavigationView(WindowSizeClassMonitor.For(view), view.PaneDisplayMode, NavigationMenu.DestinationCount(entries));
    }

    /// <summary>The container actually shown: an Expanded window whose pane is closed shows the rail.</summary>
    private NavigationContainer ShownContainer(NavigationView view) =>
        _container == NavigationContainer.PersistentDrawer && !view.IsPaneOpen ? NavigationContainer.Rail : _container;

    private void Remap()
    {
        if (_remapping || Element is not NavigationView view || PlatformView is not { } layout)
        {
            return;
        }

        _remapping = true;
        try
        {
            _entries = NavigationMenu.Entries(view, _settingsItem);
            var container = Decide(view, _entries);
            var previous = _container;
            if (container != previous)
            {
                _container = container;
                HostLog.For("CodeBrix.Android.UI.Navigation").LogInformation(
                    "NavigationView re-mapped: {Previous} -> {Current} ({Classes}).", previous, container, WindowSizeClassMonitor.For(view));
                if ((previous == NavigationContainer.Template) != (container == NavigationContainer.Template))
                {
                    view.NotifyHandlerCapabilitiesChanged();
                }

                OpenPaneFor(view, container);
            }

            if (IsNative)
            {
                layout.Show(ShownContainer(view), _entries, NavigationMenu.IndexOf(_entries, view.SelectedItem), view.OpenPaneLength, PaneTitle(view));
                if (_container == NavigationContainer.ModalDrawer)
                {
                    layout.SetDrawerOpen(view.IsPaneOpen);
                }
            }
            else if (layout.Container != NavigationContainer.Template)
            {
                layout.Clear();
            }

            view.InvalidateMeasure();
            MapBackState(this, view);
        }
        finally
        {
            _remapping = false;
        }
    }

    /// <summary>Sets IsPaneOpen for a new container, as WinUI's own adaptive logic does for its display modes.</summary>
    private void OpenPaneFor(NavigationView view, NavigationContainer container)
    {
        var open = container == NavigationContainer.PersistentDrawer;
        if (container == NavigationContainer.Template || view.IsPaneOpen == open)
        {
            return;
        }

        _settingPane = true;
        try
        {
            view.IsPaneOpen = open;
        }
        finally
        {
            _settingPane = false;
        }
    }

    private static string PaneTitle(NavigationView view) =>
        !string.IsNullOrEmpty(view.PaneTitle) ? view.PaneTitle : view.Header as string ?? string.Empty;

    private void ShowDrawer(bool open, bool fromPlatform)
    {
        if (Element is not NavigationView view || PlatformView is not { } layout || layout.IsDrawerOpen == open)
        {
            if (fromPlatform && Element is NavigationView same && same.IsPaneOpen != open)
            {
                SetPaneOpen(same, open);
            }

            return;
        }

        if (open)
        {
            view.RaisePaneOpeningFromPlatform();
            if (fromPlatform)
            {
                SetPaneOpen(view, true);
            }

            layout.SetDrawerOpen(true);
            view.RaisePaneOpenedFromPlatform();
        }
        else
        {
            if (view.RaisePaneClosingFromPlatform() && fromPlatform)
            {
                return;
            }

            if (fromPlatform)
            {
                SetPaneOpen(view, false);
            }

            layout.SetDrawerOpen(false);
            view.RaisePaneClosedFromPlatform();
        }

        MapBackState(this, view);
    }

    private void SetPaneOpen(NavigationView view, bool open)
    {
        _settingPane = true;
        try
        {
            view.IsPaneOpen = open;
        }
        finally
        {
            _settingPane = false;
        }
    }

    private void OnItemSelected(int id)
    {
        if (Element is not NavigationView view || id < 1 || id > _entries.Count)
        {
            return;
        }

        var entry = _entries[id - 1];
        if (entry.Kind == NavigationMenuEntryKind.Settings)
        {
            view.RaiseItemInvokedFromPlatform(entry.Item, true);
        }
        else if (entry.Kind == NavigationMenuEntryKind.Item)
        {
            view.RaiseItemInvokedFromPlatform(entry.Item, false);
            if (!ReferenceEquals(view.SelectedItem, entry.Item))
            {
                view.SelectedItem = entry.Item;
            }
        }

        if (_container == NavigationContainer.ModalDrawer)
        {
            ShowDrawer(false, fromPlatform: true);
        }
    }

    private void OnDrawerToggleRequested()
    {
        if (PlatformView is { } layout)
        {
            ShowDrawer(!layout.IsDrawerOpen, fromPlatform: true);
        }
    }

    private void OnDrawerDismissRequested() => ShowDrawer(false, fromPlatform: true);

    private void OnSizeClassChanged(object sender, WindowSizeClassChangedEventArgs e)
    {
        if (e.Concerns(Context as CodeBrixActivity))
        {
            Remap();
        }
    }

    private void WatchItems(NavigationView view)
    {
        var items = view.MenuItems as IObservableVector<object>;
        if (!ReferenceEquals(items, _watchedItems))
        {
            if (_watchedItems != null)
            {
                _watchedItems.VectorChanged -= OnItemsChanged;
            }

            _watchedItems = items;
            if (items != null)
            {
                items.VectorChanged += OnItemsChanged;
            }
        }

        var source = view.MenuItemsSource as INotifyCollectionChanged;
        if (!ReferenceEquals(source, _watchedSource))
        {
            if (_watchedSource != null)
            {
                _watchedSource.CollectionChanged -= OnSourceChanged;
            }

            _watchedSource = source;
            if (source != null)
            {
                source.CollectionChanged += OnSourceChanged;
            }
        }
    }

    private void UnwatchItems()
    {
        if (_watchedItems != null)
        {
            _watchedItems.VectorChanged -= OnItemsChanged;
            _watchedItems = null;
        }

        if (_watchedSource != null)
        {
            _watchedSource.CollectionChanged -= OnSourceChanged;
            _watchedSource = null;
        }
    }

    private void OnItemsChanged(IObservableVector<object> sender, IVectorChangedEventArgs e) => Remap();

    private void OnSourceChanged(object sender, NotifyCollectionChangedEventArgs e) => Remap();
}
