// Technique from .NET MAUI, src/Core/src/Handlers/Toolbar/ToolbarHandler.Android.cs and
// src/Core/src/Platform/Android/ToolbarExtensions.cs @ 828569a864 (a MaterialToolbar filled from the
// cross-platform model: title, menu items with icons and ShowAsAction, the overflow icon tinted with the bar's
// foreground, menu clicks routed back to the cross-platform command). Copyright (c) .NET Foundation and
// Contributors. Licensed under the MIT License. See THIRD-PARTY-NOTICES.txt.

using System;
using System.Collections.Generic;
using CodeBrix.Android.UI.Hosting;
using CodeBrix.Android.UI.Policy;
using CodeBrix.Platform.UI.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Windows.Foundation.Collections;
using AColor = global::Android.Graphics.Color;
using AContext = global::Android.Content.Context;
using AFrameLayout = global::Android.Widget.FrameLayout;
using AMaterialBottomAppBar = Google.Android.Material.BottomAppBar.BottomAppBar;
using AMaterialColors = Google.Android.Material.Color.MaterialColors;
using AMaterialToolbar = Google.Android.Material.AppBar.MaterialToolbar;
using AMenu = global::Android.Views.IMenu;
using AMenuCompat = AndroidX.Core.View.MenuCompat;
using AMenuItem = global::Android.Views.IMenuItem;
using AShowAsAction = global::Android.Views.ShowAsAction;
using AToolbar = AndroidX.AppCompat.Widget.Toolbar;
using AView = global::Android.Views.View;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>
/// The native view of a WinUI CommandBar: a frame holding the Material app bar of the window's size class
/// (a <see cref="AMaterialBottomAppBar"/> in a Compact window, a <see cref="AMaterialToolbar"/> otherwise). It
/// reports window-focus changes, which is how the handler learns that the overflow menu (a popup window) opened
/// or closed.
/// </summary>
internal sealed class AppBarHostView : AFrameLayout
{
    /// <summary>Creates the view.</summary>
    /// <param name="context">The context.</param>
    internal AppBarHostView(AContext context)
        : base(context)
    {
    }

    /// <summary>Raised when the window this view is in gains or loses the focus.</summary>
    internal event Action<bool> WindowFocusChanged;

    /// <summary>The Material app bar shown (null before the first mapping).</summary>
    internal AToolbar Bar { get; private set; }

    /// <summary>Replaces the app bar shown.</summary>
    /// <param name="bar">The new bar.</param>
    internal void SetBar(AToolbar bar)
    {
        if (ReferenceEquals(Bar, bar))
        {
            return;
        }

        if (Bar != null)
        {
            RemoveView(Bar);
        }

        Bar = bar;
        if (bar != null)
        {
            AddView(bar, new LayoutParams(LayoutParams.MatchParent, LayoutParams.MatchParent));
        }
    }

    /// <inheritdoc />
    public override void OnWindowFocusChanged(bool hasWindowFocus)
    {
        base.OnWindowFocusChanged(hasWindowFocus);
        WindowFocusChanged?.Invoke(hasWindowFocus);
    }
}

/// <summary>
/// AP10-A: the handler of the WinUI CommandBar (plan 2.10 adaptive row "CommandBar (WinUI)"; tsv rows CommandBar,
/// AppBar, AppBarButton, AppBarToggleButton, AppBarSeparator, CommandBarOverflowPresenter). The bar is a Material
/// app bar laid out where Core puts the CommandBar: a bottom app bar (BottomAppBar) in a Compact window, a top app
/// bar (MaterialToolbar, the Content text as its title) in a Medium or Expanded one, re-mapped live when the window
/// changes class. PrimaryCommands are the bar's actions, SecondaryCommands its overflow menu (the native overflow
/// popup replaces CommandBarOverflowPresenter), see <see cref="AppBarModel"/>. An AppBarButton whose Flyout is a
/// MenuFlyout of plain items is a sub-menu. Every action runs Core's path (ButtonBase.RaiseClickFromPlatform: Click,
/// Command, the toggle of an AppBarToggleButton); IsOpen follows the overflow menu both ways (Core raises
/// Opening/Opened/Closing/Closed). ON by default; the AppContext switch <see cref="NativeCommandBarSwitch"/> (false)
/// keeps every CommandBar on its Fluent template, as does a bar <see cref="AppBarModel.CanMapNatively"/> refuses
/// or an application template / style.
/// </summary>
internal sealed class CommandBarHandler : ViewHandler<CommandBar, AppBarHostView>
{
    /// <summary>The AppContext switch that turns the native app bars off (set it to false).</summary>
    internal const string NativeCommandBarSwitch = "CodeBrix.Android.UI.NativeCommandBar";

    /// <summary>CommandBar's mapper.</summary>
    public static readonly PropertyMapper<CommandBar, CommandBarHandler> Mapper = new(ViewMappers.ViewMapper)
    {
        [ContentControl.ContentProperty] = MapTitle,
        [CommandBar.DefaultLabelPositionProperty] = MapMenu,
        [AppBar.ClosedDisplayModeProperty] = MapMenu,
        [CommandBar.OverflowButtonVisibilityProperty] = MapMenu,
        [CommandBar.IsDynamicOverflowEnabledProperty] = MapMenu,
        [Control.IsEnabledProperty] = MapMenu,
        [AppBar.IsOpenProperty] = MapIsOpen,
        [Control.BackgroundProperty] = MapColors,
        [Control.ForegroundProperty] = MapColors,
    };

    private static readonly ILogger _log = HostLog.For("CodeBrix.Android.UI.Handlers.CommandBar");
    private readonly Dictionary<int, ICommandBarElement> _commands = new();
    private readonly Dictionary<int, MenuFlyoutItem> _flyoutItems = new();
    private readonly List<(DependencyObject Owner, DependencyProperty Property, long Token)> _watches = new();
    private readonly BrushWatcher _backgroundWatcher;
    private readonly BrushWatcher _foregroundWatcher;
    private IObservableVector<ICommandBarElement> _primary;
    private IObservableVector<ICommandBarElement> _secondary;
    private CommandBarPlacement? _placement;
    private bool _rebuildPosted;
    private bool _syncingOpen;

    /// <summary>Creates the handler.</summary>
    public CommandBarHandler()
        : base(Mapper)
    {
        _backgroundWatcher = new BrushWatcher(Recolor);
        _foregroundWatcher = new BrushWatcher(PostRebuild);
    }

    /// <summary>
    /// True (default) to show CommandBars as Material app bars; false (AppContext switch
    /// <see cref="NativeCommandBarSwitch"/> = false) keeps their Fluent template. Settable in-repo (tests).
    /// </summary>
    internal static bool Enabled { get; set; } = !AppContext.TryGetSwitch(NativeCommandBarSwitch, out var on) || on;

    /// <inheritdoc />
    public override ElementHandlerCapabilities Capabilities => ElementHandlerCapabilities.OwnsVisuals | ElementHandlerCapabilities.MeasuresNatively;

    /// <summary>The Material app bar shown now (null while disconnected).</summary>
    internal AToolbar Bar => PlatformView?.Bar;

    /// <summary>Where the bar is shown now.</summary>
    internal CommandBarPlacement? Placement => _placement;

    /// <summary>The command a native menu item stands for.</summary>
    /// <param name="itemId">The menu item id.</param>
    /// <returns>The command, or null.</returns>
    internal ICommandBarElement CommandOf(int itemId) => _commands.TryGetValue(itemId, out var command) ? command : null;

    /// <summary>
    /// The handler of a CommandBar: the Material app bar when enabled and the bar can be shown natively, else the
    /// templated fallback (logged once with the reason).
    /// </summary>
    /// <param name="element">The CommandBar.</param>
    /// <returns>The handler.</returns>
    internal static IAndroidElementHandler Create(UIElement element)
    {
        if (!Enabled || element is not CommandBar bar)
        {
            return new TemplatedFallbackHandler();
        }

        if (!NativeControlPolicy.IsNative(bar, bar is CommandBarFlyoutCommandBar ? typeof(CommandBarFlyoutCommandBar) : typeof(CommandBar), new[] { "DefaultCommandBarStyle" }, out _))
        {
            _log.LogInformation("CommandBar {Name} keeps its Fluent template: it has an application template or style.", bar.Name);
            return new TemplatedFallbackHandler();
        }

        if (!AppBarModel.CanMapNatively(bar, out var reason))
        {
            _log.LogInformation("CommandBar {Name} keeps its Fluent template: {Reason}.", bar.Name, reason);
            return new TemplatedFallbackHandler();
        }

        return new CommandBarHandler();
    }

    /// <summary>Maps Content: the top app bar's title (a bottom app bar has none).</summary>
    public static void MapTitle(CommandBarHandler handler, CommandBar element)
    {
        if (handler.Bar is AMaterialToolbar toolbar && handler._placement == CommandBarPlacement.TopToolbar)
        {
            toolbar.Title = element.Content as string ?? string.Empty;
        }

        element.InvalidateMeasure();
    }

    /// <summary>Maps a property that changes the menu.</summary>
    public static void MapMenu(CommandBarHandler handler, CommandBar element) => handler.PostRebuild();

    /// <summary>Maps IsOpen: the overflow menu opens and shuts with it.</summary>
    public static void MapIsOpen(CommandBarHandler handler, CommandBar element)
    {
        if (handler._syncingOpen || handler.Bar is not { } bar)
        {
            return;
        }

        if (element.IsOpen && !bar.IsOverflowMenuShowing)
        {
            bar.Post(() => handler.Bar?.ShowOverflowMenu());
        }
        else if (!element.IsOpen && bar.IsOverflowMenuShowing)
        {
            bar.HideOverflowMenu();
        }

        if (element.ClosedDisplayMode == AppBarClosedDisplayMode.Hidden)
        {
            element.InvalidateMeasure();
        }
    }

    /// <summary>Maps Background / Foreground (the action icons are drawn in the foreground: the menu is rebuilt).</summary>
    public static void MapColors(CommandBarHandler handler, CommandBar element)
    {
        handler.Recolor();
        handler.PostRebuild();
    }

    /// <inheritdoc />
    public override Size Measure(Size availableSize)
    {
        if (Element is not CommandBar element || Bar is not { } bar)
        {
            return new Size(0, 0);
        }

        if (element.ClosedDisplayMode == AppBarClosedDisplayMode.Hidden && !element.IsOpen)
        {
            return new Size(0, 0);
        }

        return ViewHandlerExtensions.GetDesiredSizeFromView(bar, new Size(availableSize.Width, double.PositiveInfinity), Density);
    }

    /// <inheritdoc />
    protected override AppBarHostView CreatePlatformView() => new(Context);

    /// <inheritdoc />
    protected override void ConnectHandler(AppBarHostView platformView)
    {
        base.ConnectHandler(platformView);
        platformView.WindowFocusChanged += OnWindowFocusChanged;
        ApplyPlacement();
    }

    /// <inheritdoc />
    protected override void OnConnected()
    {
        base.OnConnected();
        WindowSizeClassMonitor.Changed += OnSizeClassChanged;
        if (Element is CommandBar bar)
        {
            _primary = bar.PrimaryCommands;
            _secondary = bar.SecondaryCommands;
            _primary.VectorChanged += OnCommandsChanged;
            _secondary.VectorChanged += OnCommandsChanged;
        }

        ApplyPlacement();
        Rebuild();
    }

    /// <inheritdoc />
    protected override void DisconnectHandler(AppBarHostView platformView)
    {
        WindowSizeClassMonitor.Changed -= OnSizeClassChanged;
        platformView.WindowFocusChanged -= OnWindowFocusChanged;
        if (_primary != null)
        {
            _primary.VectorChanged -= OnCommandsChanged;
            _primary = null;
        }

        if (_secondary != null)
        {
            _secondary.VectorChanged -= OnCommandsChanged;
            _secondary = null;
        }

        Unwatch();
        _backgroundWatcher.Clear();
        _foregroundWatcher.Clear();
        if (platformView.Bar is { } bar)
        {
            bar.MenuItemClick -= OnMenuItemClick;
        }

        _commands.Clear();
        _placement = null;
        base.DisconnectHandler(platformView);
    }

    private void OnSizeClassChanged(object sender, WindowSizeClassChangedEventArgs e)
    {
        if (Element is CommandBar element && e.Concerns(WindowSizeClassMonitor.ActivityOf(element)))
        {
            if (ApplyPlacement())
            {
                Rebuild();
                element.InvalidateMeasure();
            }
        }
    }

    // Creates the bar of the window's size class; true when it changed.
    private bool ApplyPlacement()
    {
        if (Element is not CommandBar element || PlatformView is not { } host)
        {
            return false;
        }

        var placement = AdaptivePolicy.CommandBar(WindowSizeClassMonitor.For(element));
        if (_placement == placement && host.Bar != null)
        {
            return false;
        }

        if (host.Bar is { } old)
        {
            old.MenuItemClick -= OnMenuItemClick;
            if (old.IsOverflowMenuShowing)
            {
                old.HideOverflowMenu();
            }
        }

        _placement = placement;
        var context = MaterialWidgets.Material3(Context);
        AToolbar bar;
        if (placement == CommandBarPlacement.BottomAppBar)
        {
            bar = new AMaterialBottomAppBar(context);
        }
        else
        {
            var surfaceStyle = MaterialWidgets.AttrId(context, "toolbarSurfaceStyle");
            bar = surfaceStyle != 0 ? new AMaterialToolbar(context, null, surfaceStyle) : new AMaterialToolbar(context);
            ((AMaterialToolbar)bar).Title = element.Content as string ?? string.Empty;
        }

        bar.MenuItemClick += OnMenuItemClick;
        host.SetBar(bar);
        _log.LogDebug("CommandBar {Name} is a {Placement}.", element.Name, placement);
        return true;
    }

    private void OnCommandsChanged(IObservableVector<ICommandBarElement> sender, IVectorChangedEventArgs e) => PostRebuild();

    private void PostRebuild()
    {
        if (_rebuildPosted || PlatformView is not { } view)
        {
            return;
        }

        _rebuildPosted = true;
        view.Post(() =>
        {
            _rebuildPosted = false;
            Rebuild();
        });
    }

    // Fills the bar's menu from the model and watches every command for changes.
    private void Rebuild()
    {
        if (Element is not CommandBar element || Bar is not { } bar)
        {
            return;
        }

        Unwatch();
        Watch(element.PrimaryCommands);
        Watch(element.SecondaryCommands);
        _commands.Clear();
        var menu = bar.Menu;
        menu.Clear();
        var entries = AppBarModel.Entries(element);
        var colors = Colors(element);
        var density = Density;
        var id = 1;
        var order = 0;
        foreach (var entry in entries)
        {
            var itemId = id++;
            _commands[itemId] = entry.Element;
            AMenuItem item;
            if (entry.Kind == AppBarEntryKind.Button && entry.Element is AppBarButton { Flyout: MenuFlyout flyout } && SimpleFlyout(flyout))
            {
                var sub = menu.AddSubMenu(entry.Group, itemId, order++, new Java.Lang.String(entry.Label));
                FillSubMenu(sub, flyout, ref id);
                item = sub.Item;
            }
            else
            {
                item = menu.Add(entry.Group, itemId, order++, new Java.Lang.String(entry.Label));
            }

            var icon = entry.Icon == null ? null : IconDrawables.Create(entry.Icon, Context, density, entry.IsChecked ? colors.Checked : colors.Foreground, 24);
            if (icon != null)
            {
                item.SetIcon(icon);
            }

            item.SetEnabled(entry.IsEnabled);
            if (entry.Kind == AppBarEntryKind.ToggleButton)
            {
                item.SetCheckable(true);
                item.SetChecked(entry.IsChecked);
            }

            item.SetShowAsAction(entry.Placement switch
            {
                AppBarEntryPlacement.Action => AShowAsAction.Always | (entry.ShowLabel || icon == null ? AShowAsAction.WithText : 0),
                AppBarEntryPlacement.ActionIfRoom => AShowAsAction.IfRoom | (entry.ShowLabel || icon == null ? AShowAsAction.WithText : 0),
                _ => AShowAsAction.Never,
            });
            if (!string.IsNullOrEmpty(entry.Label))
            {
                item.SetContentDescription(entry.Label);
                item.SetTooltipText(entry.Label);
            }
        }

        AMenuCompat.SetGroupDividerEnabled(menu, true);
        Recolor();
        element.InvalidateMeasure();
    }

    private static bool SimpleFlyout(MenuFlyout flyout)
    {
        foreach (var item in flyout.Items)
        {
            if (item is not (MenuFlyoutItem or MenuFlyoutSeparator))
            {
                return false;
            }
        }

        return flyout.Items.Count > 0;
    }

    private void FillSubMenu(AMenu sub, MenuFlyout flyout, ref int id)
    {
        var group = 0;
        var order = 0;
        foreach (var item in flyout.Items)
        {
            switch (item)
            {
                case MenuFlyoutSeparator:
                    group++;
                    break;
                case ToggleMenuFlyoutItem toggle when toggle.Visibility == Visibility.Visible:
                    var toggleId = id++;
                    _flyoutItems[toggleId] = toggle;
                    var toggleItem = sub.Add(group, toggleId, order++, new Java.Lang.String(toggle.Text ?? string.Empty));
                    toggleItem.SetCheckable(true);
                    toggleItem.SetChecked(toggle.IsChecked);
                    toggleItem.SetEnabled(toggle.IsEnabled);
                    break;
                case MenuFlyoutItem plain when plain.Visibility == Visibility.Visible:
                    var plainId = id++;
                    _flyoutItems[plainId] = plain;
                    sub.Add(group, plainId, order++, new Java.Lang.String(plain.Text ?? string.Empty)).SetEnabled(plain.IsEnabled);
                    break;
            }
        }

        AMenuCompat.SetGroupDividerEnabled(sub, true);
    }

    private void OnMenuItemClick(object sender, AToolbar.MenuItemClickEventArgs e)
    {
        e.Handled = true;
        var itemId = e.Item?.ItemId ?? 0;
        if (e.Item?.HasSubMenu == true)
        {
            return;
        }

        if (_flyoutItems.TryGetValue(itemId, out var flyoutItem))
        {
            flyoutItem.Invoke();
            return;
        }

        if (CommandOf(itemId) is ButtonBase button)
        {
            button.RaiseClickFromPlatform();
        }
    }

    // The overflow menu is a popup window: the bar's window loses the focus while it shows.
    private void OnWindowFocusChanged(bool hasFocus)
    {
        if (Element is not CommandBar element || Bar is not { } bar)
        {
            return;
        }

        var showing = bar.IsOverflowMenuShowing;
        if (element.IsOpen == showing)
        {
            return;
        }

        _syncingOpen = true;
        try
        {
            element.IsOpen = showing;
        }
        finally
        {
            _syncingOpen = false;
        }
    }

    private void Watch(IEnumerable<ICommandBarElement> commands)
    {
        foreach (var command in commands)
        {
            if (command is not DependencyObject owner)
            {
                continue;
            }

            Watch(owner, UIElement.VisibilityProperty);
            Watch(owner, Control.IsEnabledProperty);
            switch (owner)
            {
                case AppBarButton:
                    Watch(owner, AppBarButton.LabelProperty);
                    Watch(owner, AppBarButton.IconProperty);
                    Watch(owner, AppBarButton.LabelPositionProperty);
                    Watch(owner, Button.FlyoutProperty);
                    break;
                case AppBarToggleButton:
                    Watch(owner, AppBarToggleButton.LabelProperty);
                    Watch(owner, AppBarToggleButton.IconProperty);
                    Watch(owner, AppBarToggleButton.LabelPositionProperty);
                    Watch(owner, ToggleButton.IsCheckedProperty);
                    break;
            }
        }
    }

    private void Watch(DependencyObject owner, DependencyProperty property) =>
        _watches.Add((owner, property, owner.RegisterPropertyChangedCallback(property, (_, _) => PostRebuild())));

    private void Unwatch()
    {
        foreach (var (owner, property, token) in _watches)
        {
            owner.UnregisterPropertyChangedCallback(property, token);
        }

        _watches.Clear();
        _flyoutItems.Clear();
    }

    private (int Foreground, int Checked, int? Background) Colors(CommandBar element)
    {
        var context = Bar?.Context ?? Context;
        var onSurfaceVariant = AMaterialColors.GetColor(context, MaterialWidgets.AttrId(context, "colorOnSurfaceVariant"), unchecked((int)0xFF49454F));
        var primary = AMaterialColors.GetColor(context, MaterialWidgets.AttrId(context, "colorPrimary"), unchecked((int)0xFF6750A4));
        var foreground = AppColor(element, element.Foreground, "CommandBarForeground") ?? onSurfaceVariant;
        var background = AppColor(element, element.Background, "CommandBarBackground");
        return (foreground, primary, background);
    }

    // An application brush (one that differs from the Fluent default key) wins; the Material colours otherwise.
    private static int? AppColor(CommandBar element, Brush brush, string defaultKey)
    {
        var actual = ThemeResources.ColorOf(brush);
        if (actual == null)
        {
            return null;
        }

        var themed = ThemeResources.TryFind(element, defaultKey, out var value) ? ThemeResources.ColorOf(value as Brush) : null;
        return themed == actual ? null : actual;
    }

    private void Recolor()
    {
        if (Element is not CommandBar element || Bar is not { } bar)
        {
            return;
        }

        _backgroundWatcher.Watch(element.Background);
        _foregroundWatcher.Watch(element.Foreground);
        var colors = Colors(element);
        if (colors.Background is int background)
        {
            if (bar is AMaterialBottomAppBar bottom)
            {
                bottom.BackgroundTint = global::Android.Content.Res.ColorStateList.ValueOf(new AColor(background));
            }
            else
            {
                bar.SetBackgroundColor(new AColor(background));
            }
        }

        bar.SetTitleTextColor(colors.Foreground);
        bar.OverflowIcon?.SetTint(colors.Foreground);
    }
}
