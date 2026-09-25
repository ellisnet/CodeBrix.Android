using System;
using System.Collections.Generic;
using CodeBrix.Android.UI.Platform;
using CodeBrix.Android.UI.Policy;
using CodeBrix.Android.UI.Portable.Layout;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using AColor = global::Android.Graphics.Color;
using AContext = global::Android.Content.Context;
using ABottomNavigationView = Google.Android.Material.BottomNavigation.BottomNavigationView;
using AMaterialNavigationView = Google.Android.Material.Navigation.NavigationView;
using AMaterialToolbar = Google.Android.Material.AppBar.MaterialToolbar;
using AMenu = global::Android.Views.IMenu;
using AMenuItem = global::Android.Views.IMenuItem;
using ANavigationBarView = Google.Android.Material.Navigation.NavigationBarView;
using ANavigationRailView = Google.Android.Material.NavigationRail.NavigationRailView;
using AView = global::Android.Views.View;
using AViewGroup = global::Android.Views.ViewGroup;
using AViewCompat = global::AndroidX.Core.View.ViewCompat;
using AViewStates = global::Android.Views.ViewStates;
using AWindowInsetsCompat = global::AndroidX.Core.View.WindowInsetsCompat;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>
/// The native view of a NavigationView shown in a Material container (plan 2.10 row NavigationView, D-P6): the
/// NavigationView's content (Core's, hosted directly: the handler has HostsContent) plus the container's chrome -
/// a bottom navigation bar under the content (Compact), a navigation rail beside it (Medium), a persistent
/// navigation drawer beside it (Expanded), or a top bar with a modal drawer over the content (Compact or Medium
/// with more destinations than a bar or rail holds). Changing container swaps only the chrome: the content's
/// view stays where it is, so the page and its state survive a size-class change. The chrome runs edge to edge
/// and pads its destinations clear of the system bars it overlaps (<see cref="SafeArea"/>, set by the handler,
/// which absorbs the window's safe area; <see cref="NavigationChromeInsets"/>); the Material views' own inset
/// handling is switched off so the padding is applied once.
/// </summary>
internal sealed class AdaptiveNavigationLayout : CodeBrixContentViewGroup
{
    /// <summary>The height of the bottom navigation bar, in dp (Material 3).</summary>
    internal const double BottomBarHeight = NavigationChromeInsets.BottomBarHeight;

    /// <summary>The width of the navigation rail, in dp (Material 3).</summary>
    internal const double RailWidth = NavigationChromeInsets.RailWidth;

    /// <summary>The height of the top bar of the modal-drawer container, in dp (Material 3 small top app bar).</summary>
    internal const double TopBarHeight = NavigationChromeInsets.TopBarHeight;

    /// <summary>The room a modal drawer leaves at the right of the window, in dp (Material 3: 56).</summary>
    internal const double ModalDrawerRightMargin = 56;

    private readonly List<AView> _chrome = new();
    private AView _bar;
    private AMaterialToolbar _topBar;
    private AView _scrim;
    private AMaterialNavigationView _drawer;
    private AMenu _menu;
    private bool _syncing;
    private double _paneWidth = 320;
    private SafeAreaPadding _safeArea;
    private double _iconDensity;

    /// <summary>Creates the view.</summary>
    /// <param name="context">The context.</param>
    internal AdaptiveNavigationLayout(AContext context)
        : base(context)
    {
    }

    /// <summary>The container shown (<see cref="NavigationContainer.Template"/> = no chrome).</summary>
    internal NavigationContainer Container { get; private set; } = NavigationContainer.Template;

    /// <summary>True while the modal drawer is open.</summary>
    internal bool IsDrawerOpen { get; private set; }

    /// <summary>The Material view showing the destinations (the bar, the rail or the drawer), or null.</summary>
    internal AView MenuView => Container == NavigationContainer.ModalDrawer ? _drawer : _bar;

    /// <summary>The native menu of the container, or null.</summary>
    internal AMenu Menu => _menu;

    /// <summary>The top bar of the modal-drawer container, or null.</summary>
    internal AMaterialToolbar TopBar => _topBar;

    /// <summary>The density the menu and top-bar icons were drawn at (0 before the first menu).</summary>
    internal double IconDensity => _iconDensity;

    /// <summary>
    /// The safe-area insets (DIPs) the NavigationView overlaps: the chrome grows by them on the edges it touches
    /// and pads its destinations clear of the bars.
    /// </summary>
    internal SafeAreaPadding SafeArea
    {
        get => _safeArea;
        set
        {
            if (value == _safeArea)
            {
                return;
            }

            _safeArea = value;
            ApplyChromePadding();
            RequestLayout();
        }
    }

    /// <summary>Raised when the user picks a destination: the menu item id (the entry index + 1).</summary>
    internal event Action<int> ItemSelected;

    /// <summary>Raised when the top bar's navigation button is pressed.</summary>
    internal event Action DrawerToggleRequested;

    /// <summary>Raised when the scrim of an open modal drawer is tapped.</summary>
    internal event Action DrawerDismissRequested;

    /// <summary>The room the chrome takes from each edge, in dp (where the content goes).</summary>
    /// <param name="container">The container.</param>
    /// <param name="paneWidth">The persistent drawer's width in dp.</param>
    /// <returns>Left, top, right, bottom.</returns>
    internal static Thickness ChromeInsets(NavigationContainer container, double paneWidth)
    {
        var chrome = NavigationChromeInsets.Chrome(container, paneWidth);
        return new Thickness(chrome.Left, chrome.Top, chrome.Right, chrome.Bottom);
    }

    /// <summary>Shows <paramref name="container"/> with the menu <paramref name="entries"/>.</summary>
    /// <param name="container">The container.</param>
    /// <param name="entries">The menu entries.</param>
    /// <param name="selectedIndex">The selected entry, or -1.</param>
    /// <param name="paneWidth">The persistent (and modal) drawer width in dp.</param>
    /// <param name="title">The top bar title (modal drawer).</param>
    internal void Show(NavigationContainer container, IReadOnlyList<NavigationMenuEntry> entries, int selectedIndex, double paneWidth, string title)
    {
        _paneWidth = paneWidth > 0 && !double.IsNaN(paneWidth) ? paneWidth : 320;
        if (container != Container)
        {
            ClearChrome();
            Container = container;
            BuildChrome(container);
        }

        if (_topBar != null)
        {
            _topBar.Title = title ?? string.Empty;
            if (HandlerContext.Density(ElementHandler?.Element) != _iconDensity)
            {
                _topBar.NavigationIcon = NavigationIcon();
            }
        }

        FillMenu(entries);
        Select(selectedIndex);
        ApplyChromePadding();
        RequestLayout();
    }

    /// <summary>Removes the chrome (the NavigationView shows Core's template again).</summary>
    internal void Clear()
    {
        ClearChrome();
        Container = NavigationContainer.Template;
        RequestLayout();
    }

    /// <summary>Checks the menu item of entry <paramref name="index"/> (no selection event).</summary>
    /// <param name="index">The entry index, or -1 for none.</param>
    internal void Select(int index)
    {
        if (_menu == null)
        {
            return;
        }

        _syncing = true;
        try
        {
            var id = index + 1;
            for (var i = 0; i < _menu.Size(); i++)
            {
                CheckItems(_menu.GetItem(i), id);
            }
        }
        finally
        {
            _syncing = false;
        }
    }

    /// <summary>Opens or closes the modal drawer (animated with the system's animator scale).</summary>
    /// <param name="open">True to open.</param>
    internal void SetDrawerOpen(bool open)
    {
        IsDrawerOpen = open;
        if (_drawer == null || _scrim == null)
        {
            return;
        }

        var width = DrawerWidthPx() + SafePx(_safeArea.Left);
        _scrim.Visibility = open ? AViewStates.Visible : AViewStates.Gone;
        _drawer.Visibility = AViewStates.Visible;
        _drawer.Animate()?.Cancel();
        _drawer.Animate()?.TranslationX(open ? 0 : -width).SetDuration(250).WithEndAction(new global::Java.Lang.Runnable(() =>
        {
            if (!IsDrawerOpen && _drawer != null)
            {
                _drawer.Visibility = AViewStates.Invisible;
            }
        })).Start();
    }

    /// <inheritdoc />
    protected override void OnMeasure(int widthMeasureSpec, int heightMeasureSpec)
    {
        base.OnMeasure(widthMeasureSpec, heightMeasureSpec);
        foreach (var (view, rect) in ChromeRects(MeasuredWidth, MeasuredHeight))
        {
            view.Measure(MeasureSpecExtensions.Exactly(Math.Max(0, rect.Width())), MeasureSpecExtensions.Exactly(Math.Max(0, rect.Height())));
        }
    }

    /// <inheritdoc />
    protected override void OnLaidOut(int width, int height)
    {
        base.OnLaidOut(width, height);
        foreach (var (view, rect) in ChromeRects(width, height))
        {
            view.Layout(rect.Left, rect.Top, rect.Right, rect.Bottom);
        }

        if (_drawer != null && !IsDrawerOpen)
        {
            _drawer.TranslationX = -(DrawerWidthPx() + SafePx(_safeArea.Left));
        }
    }

    private static void CheckItems(AMenuItem item, int id)
    {
        if (item == null)
        {
            return;
        }

        if (item.HasSubMenu && item.SubMenu is { } sub)
        {
            for (var i = 0; i < sub.Size(); i++)
            {
                CheckItems(sub.GetItem(i), id);
            }

            return;
        }

        if (item.IsCheckable)
        {
            item.SetChecked(item.ItemId == id);
        }
    }

    private IEnumerable<(AView View, global::Android.Graphics.Rect Rect)> ChromeRects(int width, int height)
    {
        var density = HandlerContext.Density(ElementHandler?.Element);
        int Px(double dips) => LayoutReplayMath.ToPixels(dips, density);
        var safe = _safeArea;
        switch (Container)
        {
            case NavigationContainer.BottomBar when _bar != null:
                yield return (_bar, new global::Android.Graphics.Rect(0, height - Px(BottomBarHeight + safe.Bottom), width, height));
                break;
            case NavigationContainer.Rail when _bar != null:
                yield return (_bar, new global::Android.Graphics.Rect(0, 0, Px(RailWidth + safe.Left), height));
                break;
            case NavigationContainer.PersistentDrawer when _bar != null:
                yield return (_bar, new global::Android.Graphics.Rect(0, 0, Math.Min(width, Px(_paneWidth + safe.Left)), height));
                break;
            case NavigationContainer.ModalDrawer:
                if (_topBar != null)
                {
                    yield return (_topBar, new global::Android.Graphics.Rect(0, 0, width, Px(TopBarHeight + safe.Top)));
                }

                if (_scrim != null)
                {
                    yield return (_scrim, new global::Android.Graphics.Rect(0, 0, width, height));
                }

                if (_drawer != null)
                {
                    yield return (_drawer, new global::Android.Graphics.Rect(0, 0, DrawerWidthPx(width) + Px(safe.Left), height));
                }

                break;
        }
    }

    private int SafePx(double dips) => LayoutReplayMath.ToPixels(dips, HandlerContext.Density(ElementHandler?.Element));

    /// <summary>Pads the chrome views clear of the system bars they overlap (NavigationChromeInsets.ChromePadding).</summary>
    private void ApplyChromePadding()
    {
        var padding = NavigationChromeInsets.ChromePadding(Container, _safeArea);
        void Pad(AView view, SafeAreaPadding p)
        {
            view?.SetPadding(SafePx(p.Left), SafePx(p.Top), SafePx(p.Right), SafePx(p.Bottom));
        }

        switch (Container)
        {
            case NavigationContainer.BottomBar:
            case NavigationContainer.Rail:
            case NavigationContainer.PersistentDrawer:
                Pad(_bar, padding);
                break;
            case NavigationContainer.ModalDrawer:
                Pad(_topBar, padding);
                Pad(_drawer, NavigationChromeInsets.ChromePadding(NavigationContainer.PersistentDrawer, _safeArea));
                break;
        }
    }

    private int DrawerWidthPx(int? width = null)
    {
        var density = HandlerContext.Density(ElementHandler?.Element);
        var available = (width ?? Width) - LayoutReplayMath.ToPixels(ModalDrawerRightMargin, density);
        return Math.Max(0, Math.Min(LayoutReplayMath.ToPixels(_paneWidth, density), available));
    }

    private void BuildChrome(NavigationContainer container)
    {
        var context = MaterialWidgets.Material3(Context);
        switch (container)
        {
            case NavigationContainer.BottomBar:
                var bottom = new ABottomNavigationView(context) { LabelVisibilityMode = ANavigationBarView.LabelVisibilityLabeled };
                bottom.SetOnItemSelectedListener(new BarListener(this));
                Attach(_bar = bottom);
                _menu = bottom.Menu;
                break;
            case NavigationContainer.Rail:
                var rail = new ANavigationRailView(context) { LabelVisibilityMode = ANavigationBarView.LabelVisibilityLabeled };
                rail.SetOnItemSelectedListener(new BarListener(this));
                Attach(_bar = rail);
                _menu = rail.Menu;
                break;
            case NavigationContainer.PersistentDrawer:
                var persistent = new AMaterialNavigationView(context);
                persistent.SetNavigationItemSelectedListener(new DrawerListener(this));
                Attach(_bar = persistent);
                _menu = persistent.Menu;
                break;
            case NavigationContainer.ModalDrawer:
                _topBar = new AMaterialToolbar(context);
                _topBar.NavigationIcon = NavigationIcon();
                _topBar.NavigationContentDescription = "Open navigation";
                _topBar.NavigationClick += (_, _) => DrawerToggleRequested?.Invoke();
                Attach(_topBar);
                _scrim = new AView(Context) { Clickable = true, Visibility = AViewStates.Gone };
                _scrim.SetBackgroundColor(new AColor(0x52000000));
                _scrim.Click += (_, _) => DrawerDismissRequested?.Invoke();
                Attach(_scrim);
                _drawer = new AMaterialNavigationView(context) { Visibility = AViewStates.Invisible };
                _drawer.SetNavigationItemSelectedListener(new DrawerListener(this));
                Attach(_drawer);
                _menu = _drawer.Menu;
                IsDrawerOpen = false;
                break;
        }
    }

    private global::Android.Graphics.Drawables.Drawable NavigationIcon() =>
        IconDrawables.Create(new SymbolIcon(Symbol.GlobalNavigationButton), Context, HandlerContext.Density(ElementHandler?.Element), unchecked((int)0xFF1D1B20), 24);

    private void Attach(AView view)
    {
        _chrome.Add(view);

        // The chrome's padding comes from SafeArea (the handler's absorbed insets): the Material views' own window-inset
        // listeners (which pad for every system bar, wherever the view sits) are replaced by one that ignores them.
        AViewCompat.SetOnApplyWindowInsetsListener(view, IgnoreInsets.Instance);

        // Chrome goes after the element children, so element child indices stay valid and the chrome draws on top.
        AddView(view);
    }

    private void ClearChrome()
    {
        foreach (var view in _chrome)
        {
            view.Animate()?.Cancel();
            if (view.Parent == this)
            {
                RemoveView(view);
            }
        }

        _chrome.Clear();
        _bar = null;
        _topBar = null;
        _scrim = null;
        _drawer = null;
        _menu = null;
        IsDrawerOpen = false;
    }

    private void FillMenu(IReadOnlyList<NavigationMenuEntry> entries)
    {
        if (_menu == null)
        {
            return;
        }

        _syncing = true;
        try
        {
            _menu.Clear();
            var drawer = Container is NavigationContainer.PersistentDrawer or NavigationContainer.ModalDrawer;
            var density = HandlerContext.Density(ElementHandler?.Element);
            _iconDensity = density;
            var group = 1;
            AMenu target = _menu;
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                switch (entry.Kind)
                {
                    case NavigationMenuEntryKind.Separator when drawer:
                        group++;
                        target = _menu;
                        break;
                    case NavigationMenuEntryKind.Header when drawer:
                        group++;
                        target = _menu.AddSubMenu(group, 0, i, new global::Java.Lang.String(entry.Text));
                        break;
                    case NavigationMenuEntryKind.Item:
                    case NavigationMenuEntryKind.Settings:
                        var item = target.Add(drawer ? group : 0, i + 1, i, new global::Java.Lang.String(entry.Text));
                        item.SetCheckable(true);
                        item.SetEnabled(entry.IsEnabled);
                        var icon = entry.Icon != null
                            ? IconDrawables.Create(entry.Icon, Context, density, unchecked((int)0xFF1D1B20), 24)
                            : entry.Kind == NavigationMenuEntryKind.Settings ? IconDrawables.Create(new SymbolIcon(Symbol.Setting), Context, density, unchecked((int)0xFF1D1B20), 24) : null;
                        if (icon != null)
                        {
                            item.SetIcon(icon);
                        }

                        break;
                }
            }

            if (drawer)
            {
                _menu.SetGroupDividerEnabled(true);
                // Not exclusive: Select() checks exactly the selected destination, and none while SelectedItem is null
                // (an exclusive group shows its first item checked). A bar or rail always shows one destination selected
                // (Material's own rule).
                for (var g = 1; g <= group; g++)
                {
                    _menu.SetGroupCheckable(g, true, false);
                }
            }
        }
        finally
        {
            _syncing = false;
        }
    }

    private bool OnItemSelected(AMenuItem item)
    {
        if (!_syncing && item != null && item.ItemId > 0)
        {
            ItemSelected?.Invoke(item.ItemId);
        }

        return true;
    }

    private sealed class IgnoreInsets : global::Java.Lang.Object, global::AndroidX.Core.View.IOnApplyWindowInsetsListener
    {
        internal static readonly IgnoreInsets Instance = new();

        public AWindowInsetsCompat OnApplyWindowInsets(AView v, AWindowInsetsCompat insets) => AWindowInsetsCompat.Consumed;
    }

    private sealed class BarListener : global::Java.Lang.Object, ANavigationBarView.IOnItemSelectedListener
    {
        private readonly WeakReference<AdaptiveNavigationLayout> _owner;

        internal BarListener(AdaptiveNavigationLayout owner) => _owner = new WeakReference<AdaptiveNavigationLayout>(owner);

        public bool OnNavigationItemSelected(AMenuItem item) => _owner.TryGetTarget(out var owner) && owner.OnItemSelected(item);
    }

    private sealed class DrawerListener : global::Java.Lang.Object, AMaterialNavigationView.IOnNavigationItemSelectedListener
    {
        private readonly WeakReference<AdaptiveNavigationLayout> _owner;

        internal DrawerListener(AdaptiveNavigationLayout owner) => _owner = new WeakReference<AdaptiveNavigationLayout>(owner);

        public bool OnNavigationItemSelected(AMenuItem item) => _owner.TryGetTarget(out var owner) && owner.OnItemSelected(item);
    }
}
