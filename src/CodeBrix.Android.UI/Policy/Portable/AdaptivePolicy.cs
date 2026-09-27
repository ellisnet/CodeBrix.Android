using System;
using System.Collections.Generic;
using CodeBrix.Android.UI.Overlay;
using Microsoft.UI.Xaml.Controls;

namespace CodeBrix.Android.UI.Policy;

/// <summary>The native container a NavigationView shows its menu in.</summary>
internal enum NavigationContainer
{
    /// <summary>Core's Fluent template (an explicit PaneDisplayMode, or the adaptive mapping switched off).</summary>
    Template,

    /// <summary>A Material BottomNavigationView under the content (Compact, at most five items).</summary>
    BottomBar,

    /// <summary>A modal Material navigation drawer over the content, opened from a top bar (Compact, more than five items).</summary>
    ModalDrawer,

    /// <summary>A Material NavigationRailView beside the content (Medium).</summary>
    Rail,

    /// <summary>A persistent Material NavigationView drawer beside the content (Expanded).</summary>
    PersistentDrawer,
}

/// <summary>Where a WinUI CommandBar goes.</summary>
internal enum CommandBarPlacement
{
    /// <summary>A bottom app bar (Compact).</summary>
    BottomAppBar,

    /// <summary>A top toolbar, SecondaryCommands in the overflow menu (Medium, Expanded).</summary>
    TopToolbar,
}

/// <summary>What a ContentDialog looks like.</summary>
internal enum DialogForm
{
    /// <summary>A basic (centred) dialog.</summary>
    Basic,

    /// <summary>A full-screen dialog (Compact, content that is not plain text).</summary>
    FullScreen,
}

/// <summary>What a Flyout / MenuFlyout / ContextFlyout looks like.</summary>
internal enum FlyoutForm
{
    /// <summary>A modal bottom sheet (Compact).</summary>
    BottomSheet,

    /// <summary>A popup or menu anchored at its target (Medium, Expanded).</summary>
    Anchored,
}

/// <summary>What a DatePicker / TimePicker opens.</summary>
internal enum PickerForm
{
    /// <summary>The modal Material picker in its touch mode (calendar / clock).</summary>
    Modal,

    /// <summary>The modal Material picker opened in its text-input mode (Expanded windows, a fine pointer).</summary>
    ModalTextInputFirst,
}

/// <summary>What shows a ToolTip.</summary>
internal enum ToolTipTrigger
{
    /// <summary>A long press.</summary>
    LongPress,

    /// <summary>A long press, and hovering with a fine pointer.</summary>
    LongPressAndHover,
}

/// <summary>The scroll bars of a ScrollViewer.</summary>
internal enum ScrollBarForm
{
    /// <summary>Overlay bars that fade (touch).</summary>
    OverlayFading,

    /// <summary>Persistent thin bars (a fine pointer).</summary>
    PersistentThin,
}

/// <summary>How ListView / GridView selection is operated.</summary>
internal enum SelectionInput
{
    /// <summary>Touch (a check box per item in Multiple mode).</summary>
    Touch,

    /// <summary>Touch plus a hover state layer and Ctrl/Shift multi-select (a fine pointer).</summary>
    TouchAndPointer,
}

/// <summary>The window chrome a page gets.</summary>
internal enum PageChrome
{
    /// <summary>Edge to edge, the system bars' insets absorbed.</summary>
    EdgeToEdge,

    /// <summary>Edge to edge with a caption bar (desktop windowing: a fine pointer in an Expanded window).</summary>
    EdgeToEdgeWithCaptionBar,
}

/// <summary>A MenuBar's form.</summary>
internal enum MenuBarForm
{
    /// <summary>A toolbar overflow menu with submenus.</summary>
    ToolbarOverflow,

    /// <summary>A real menu bar row (a fine pointer).</summary>
    MenuBarRow,
}

/// <summary>The CommandBar add-in's ToolBar family form.</summary>
internal enum ToolBarForm
{
    /// <summary>A scrolling icon strip with an overflow menu (Compact).</summary>
    ScrollingStripWithOverflow,

    /// <summary>The full strip (Medium).</summary>
    FullStrip,

    /// <summary>The full strip, labels per LabelMode (Expanded).</summary>
    FullStripWithLabels,
}

/// <summary>The Toolkit TriPaneView's form.</summary>
internal enum TriPaneForm
{
    /// <summary>One pane visible (Compact); the restore grips switch panes (the drawer / bottom sheet / tab containers of the table are not done).</summary>
    OnePane,

    /// <summary>The side pane and one stacked pane, the lower one collapsible (Medium).</summary>
    SidePaneAndOneStacked,

    /// <summary>All three panes with draggable dividers (Expanded).</summary>
    ThreePanes,
}

/// <summary>A TabView's tab row.</summary>
internal enum TabRowForm
{
    /// <summary>A scrollable tab row, tabs closed from a long-press menu (Compact).</summary>
    ScrollableCloseByLongPress,

    /// <summary>A scrollable tab row with close buttons (Medium, Expanded).</summary>
    ScrollableWithCloseButtons,
}

/// <summary>One row of the adaptive mapping table, as documented and as implemented.</summary>
/// <param name="Element">The element (family).</param>
/// <param name="Compact">What a Compact window shows.</param>
/// <param name="Medium">What a Medium window shows.</param>
/// <param name="Expanded">What an Expanded window shows.</param>
/// <param name="FinePointer">What a fine pointer adds.</param>
/// <param name="AppliedBy">Where CodeBrix.Android applies the row (or which later package does).</param>
internal sealed record AdaptiveRow(string Element, string Compact, string Medium, string Expanded, string FinePointer, string AppliedBy);

/// <summary>
/// The adaptive mapping table (plan 2.10, D-P6): which native presentation each adaptive element gets for a
/// window's size classes. Replaceable and versioned by name (<see cref="Name"/>); every decision is a pure
/// function of <see cref="WindowSizeClass"/> (host-testable), consumed by the handlers and overlays that show
/// the element, which re-map live when a window changes class (a docked phone switching to desktop mode).
/// </summary>
internal static class AdaptivePolicy
{
    /// <summary>The table's version name.</summary>
    internal const string Name = "Material 3 Expressive as of 2026-09-23";

    /// <summary>The largest number of items a bottom navigation bar shows (Material's limit).</summary>
    internal const int BottomBarMaxItems = 5;

    /// <summary>The largest number of items a navigation rail shows (Material's limit).</summary>
    internal const int RailMaxItems = 7;

    /// <summary>True (default) to map an Auto NavigationView to the native adaptive containers.</summary>
    internal static bool AdaptiveNavigationView { get; set; } = true;

    /// <summary>True (default) to show a ContentDialog with XAML content full screen in a Compact window.</summary>
    internal static bool FullScreenDialogs { get; set; } = true;

    /// <summary>The AppContext switch that turns the adaptive TriPaneView form off (on by default).</summary>
    internal const string AdaptiveTriPaneViewSwitch = "CodeBrix.Android.UI.AdaptiveTriPaneView";

    /// <summary>
    /// True (default) to show a Toolkit TriPaneView in the form its window's width class asks for (one pane / side pane +
    /// one stacked pane / three panes, Handlers/Toolkit/TriPaneViewHandler); false keeps the three panes at every size.
    /// </summary>
    internal static bool AdaptiveTriPaneView { get; set; } = DefaultAdaptiveTriPaneView;

    private static bool DefaultAdaptiveTriPaneView => !AppContext.TryGetSwitch(AdaptiveTriPaneViewSwitch, out var on) || on;

    /// <summary>
    /// A size-class override for diagnostics and tests (null = the window's real classes): every consumer of the
    /// table sees these classes, and setting it re-maps live exactly as a window resize does.
    /// </summary>
    internal static WindowSizeClass? Override { get; set; }

    /// <summary>The documented table, row by row (plan 2.10), with where each row is applied.</summary>
    internal static IReadOnlyList<AdaptiveRow> Rows { get; } = new[]
    {
        new AdaptiveRow("NavigationView (Auto)", "BottomNavigationView (<=5 items) else modal drawer", "NavigationRailView (<=7 items) else modal drawer", "persistent NavigationView drawer", "hover states, tooltips on rail items", "Handlers/Navigation/NavigationViewHandler (AP5)"),
        new AdaptiveRow("CommandBar (WinUI)", "BottomAppBar", "top MaterialToolbar + overflow", "top toolbar, SecondaryCommands in overflow", "accelerators listed in menus", "decision only: the native CommandBar is AP10 (templated until then)"),
        new AdaptiveRow("MenuBar", "toolbar overflow with submenus", "overflow", "overflow", "real menu bar row", "decision only: native MenuBar is AP10 (templated until then)"),
        new AdaptiveRow("ToolBar family (CommandBar add-in)", "scrolling icon strip + overflow", "full strip", "full strip, labels per LabelMode", "hover tooltips", "decision only: the CommandBar add-in is AP7.3"),
        new AdaptiveRow("TriPaneView (Toolkit)", "one pane visible; side pane as drawer/bottom sheet; upper/lower as tabs", "side pane + one stacked pane", "three panes, draggable dividers", "resize cursor on dividers (Core)", "Handlers/Toolkit/TriPaneViewHandler (AP7-B): the form through Core's engine (a region the window has no room for gets weight 0 and its restore grip switches panes; its weight comes back when the window widens); a finger drags a divider through a 48-dp touch target; the drawer / bottom sheet / tab containers are not done"),
        new AdaptiveRow("SplitView", "declared mode; Inline/CompactInline degrade to Overlay", "declared mode", "declared mode", "-", "decision only: SplitView keeps Core's template (its DisplayMode is the app's); native SplitView is AP10"),
        new AdaptiveRow("ContentDialog", "full screen for non-text content", "basic", "basic", "Esc = Close, Enter = default button", "Policy/ContentDialogPolicy (AP5, Core dialogs) + Overlay/NativeContentDialog (AP4, text dialogs are always basic)"),
        new AdaptiveRow("Flyout / MenuFlyout / ContextFlyout", "bottom sheet", "anchored popup/menu", "anchored popup/menu", "right-click opens ContextFlyout at the pointer", "Overlay/NativeMenuFlyout (AP4: MenuFlyout); a Flyout with content stays Core's anchored popup at every size"),
        new AdaptiveRow("TabView", "scrollable TabLayout, close via long-press menu", "scrollable tabs with close buttons", "same", "close buttons on hover", "decision only: Handlers/Tabs (AP3b) shows scrollable tabs with close buttons at every size"),
        new AdaptiveRow("DatePicker / TimePicker", "modal picker", "modal", "modal, text-input mode first", "text-input mode first", "decision only: the Material pickers are AP6/AP10 (Core flyout pickers until then)"),
        new AdaptiveRow("ToolTip", "long-press", "long-press", "long-press", "hover", "Handlers/Overlays/ToolTipMapping (AP4: TooltipCompat shows on long-press and on mouse hover)"),
        new AdaptiveRow("ScrollViewer scroll bars", "overlay, fading", "overlay", "overlay", "persistent thin bars", "Core's ScrollViewer template (its scroll bars are Core's; they follow the pointer kind as on Skia)"),
        new AdaptiveRow("ListView/GridView selection", "touch (checkbox in Multiple mode)", "same", "same", "hover state layer, Ctrl/Shift multi-select", "Core's ListViewItem template (AP3b keeps the containers' Fluent template)"),
        new AdaptiveRow("Page insets", "edge-to-edge, insets absorbed", "same", "same", "caption bar in desktop windowing", "Platform/Insets + Handlers/Content/SafeAreaAbsorbers (AP2)"),
    };

    /// <summary>The container of a NavigationView.</summary>
    /// <param name="window">The window's classes.</param>
    /// <param name="mode">The NavigationView's PaneDisplayMode (an explicit mode keeps the Fluent template).</param>
    /// <param name="itemCount">How many top-level destinations it has (menu items plus Settings).</param>
    /// <returns>The container.</returns>
    internal static NavigationContainer NavigationView(WindowSizeClass window, NavigationViewPaneDisplayMode mode, int itemCount)
    {
        if (!AdaptiveNavigationView || mode != NavigationViewPaneDisplayMode.Auto)
        {
            return NavigationContainer.Template;
        }

        return window.Width switch
        {
            WindowWidthClass.Compact => itemCount <= BottomBarMaxItems ? NavigationContainer.BottomBar : NavigationContainer.ModalDrawer,
            WindowWidthClass.Medium => itemCount <= RailMaxItems ? NavigationContainer.Rail : NavigationContainer.ModalDrawer,
            _ => NavigationContainer.PersistentDrawer,
        };
    }

    /// <summary>The SplitView display mode a declared mode becomes.</summary>
    /// <param name="window">The window's classes.</param>
    /// <param name="declared">The app's DisplayMode.</param>
    /// <returns>The mode to show.</returns>
    internal static SplitViewDisplayMode SplitView(WindowSizeClass window, SplitViewDisplayMode declared) =>
        window.Width != WindowWidthClass.Compact ? declared : declared switch
        {
            SplitViewDisplayMode.Inline => SplitViewDisplayMode.Overlay,
            SplitViewDisplayMode.CompactInline => SplitViewDisplayMode.CompactOverlay,
            _ => declared,
        };

    /// <summary>What a ContentDialog looks like.</summary>
    /// <param name="window">The window's classes.</param>
    /// <param name="textContent">True when its content is plain text (or nothing).</param>
    /// <returns>The form.</returns>
    internal static DialogForm ContentDialog(WindowSizeClass window, bool textContent) =>
        FullScreenDialogs && window.Width == WindowWidthClass.Compact && !textContent ? DialogForm.FullScreen : DialogForm.Basic;

    /// <summary>What a Flyout, MenuFlyout or ContextFlyout looks like.</summary>
    /// <param name="window">The window's classes.</param>
    /// <returns>The form.</returns>
    internal static FlyoutForm Flyout(WindowSizeClass window) => window.Width == WindowWidthClass.Compact ? FlyoutForm.BottomSheet : FlyoutForm.Anchored;

    /// <summary>True when a right click opens a ContextFlyout at the pointer.</summary>
    /// <param name="window">The window's classes.</param>
    /// <returns>True with a fine pointer.</returns>
    internal static bool ContextFlyoutAtPointer(WindowSizeClass window) => window.FinePointer;

    /// <summary>Where a CommandBar goes.</summary>
    /// <param name="window">The window's classes.</param>
    /// <returns>The placement.</returns>
    internal static CommandBarPlacement CommandBar(WindowSizeClass window) => window.Width == WindowWidthClass.Compact ? CommandBarPlacement.BottomAppBar : CommandBarPlacement.TopToolbar;

    /// <summary>What a MenuBar becomes.</summary>
    /// <param name="window">The window's classes.</param>
    /// <returns>The form.</returns>
    internal static MenuBarForm MenuBar(WindowSizeClass window) => window.FinePointer ? MenuBarForm.MenuBarRow : MenuBarForm.ToolbarOverflow;

    /// <summary>What a ToolBar (CommandBar add-in) becomes.</summary>
    /// <param name="window">The window's classes.</param>
    /// <returns>The form.</returns>
    internal static ToolBarForm ToolBar(WindowSizeClass window) => window.Width switch
    {
        WindowWidthClass.Compact => ToolBarForm.ScrollingStripWithOverflow,
        WindowWidthClass.Medium => ToolBarForm.FullStrip,
        _ => ToolBarForm.FullStripWithLabels,
    };

    /// <summary>What a TriPaneView shows.</summary>
    /// <param name="window">The window's classes.</param>
    /// <returns>The form.</returns>
    internal static TriPaneForm TriPane(WindowSizeClass window) => window.Width switch
    {
        WindowWidthClass.Compact => TriPaneForm.OnePane,
        WindowWidthClass.Medium => TriPaneForm.SidePaneAndOneStacked,
        _ => TriPaneForm.ThreePanes,
    };

    /// <summary>What a TabView's tab row looks like.</summary>
    /// <param name="window">The window's classes.</param>
    /// <returns>The form.</returns>
    internal static TabRowForm TabView(WindowSizeClass window) =>
        window.Width == WindowWidthClass.Compact && !window.FinePointer ? TabRowForm.ScrollableCloseByLongPress : TabRowForm.ScrollableWithCloseButtons;

    /// <summary>What a DatePicker / TimePicker opens.</summary>
    /// <param name="window">The window's classes.</param>
    /// <returns>The form.</returns>
    internal static PickerForm Picker(WindowSizeClass window) =>
        window.FinePointer || window.Width == WindowWidthClass.Expanded ? PickerForm.ModalTextInputFirst : PickerForm.Modal;

    /// <summary>What shows a ToolTip.</summary>
    /// <param name="window">The window's classes.</param>
    /// <returns>The trigger.</returns>
    internal static ToolTipTrigger ToolTip(WindowSizeClass window) => window.FinePointer ? ToolTipTrigger.LongPressAndHover : ToolTipTrigger.LongPress;

    /// <summary>A ScrollViewer's scroll bars.</summary>
    /// <param name="window">The window's classes.</param>
    /// <returns>The form.</returns>
    internal static ScrollBarForm ScrollBars(WindowSizeClass window) => window.FinePointer ? ScrollBarForm.PersistentThin : ScrollBarForm.OverlayFading;

    /// <summary>How list selection is operated.</summary>
    /// <param name="window">The window's classes.</param>
    /// <returns>The input.</returns>
    internal static SelectionInput ListSelection(WindowSizeClass window) => window.FinePointer ? SelectionInput.TouchAndPointer : SelectionInput.Touch;

    /// <summary>The chrome a page gets.</summary>
    /// <param name="window">The window's classes.</param>
    /// <returns>The chrome.</returns>
    internal static PageChrome Page(WindowSizeClass window) =>
        window.FinePointer && window.Width == WindowWidthClass.Expanded ? PageChrome.EdgeToEdgeWithCaptionBar : PageChrome.EdgeToEdge;

    /// <summary>
    /// The Material motion of a Frame page change (plan 2.8 / 2.11): a Frame whose NavigationView is shown in a
    /// native navigation container (bar, rail, drawer) switches between top-level destinations, which Material
    /// animates with fade through; everywhere else the transition info's own motion applies (shared axis X for
    /// Slide, Z for Entrance, fade through for DrillIn, none for Suppress).
    /// </summary>
    /// <param name="kind">The motion the transition info asks for.</param>
    /// <param name="underNativeNavigation">True when the Frame is the content of a natively shown NavigationView.</param>
    /// <returns>The motion to play.</returns>
    internal static NavigationMotionKind FrameMotion(NavigationMotionKind kind, bool underNativeNavigation) =>
        underNativeNavigation && kind == NavigationMotionKind.SharedAxisZ ? NavigationMotionKind.FadeThrough : kind;

    /// <summary>Puts every switch and the override back to their defaults.</summary>
    internal static void Reset()
    {
        AdaptiveNavigationView = true;
        FullScreenDialogs = true;
        AdaptiveTriPaneView = DefaultAdaptiveTriPaneView;
        Override = null;
    }
}
