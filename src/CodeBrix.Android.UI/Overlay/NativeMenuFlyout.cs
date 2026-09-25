#if __ANDROID__
using System.Collections.Generic;
using System.Linq;
using CodeBrix.Android.UI.Hosting;
using CodeBrix.Android.UI.Platform;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using AGravityFlags = global::Android.Views.GravityFlags;
using AIMenu = global::Android.Views.IMenu;
using AIMenuItem = global::Android.Views.IMenuItem;
using AJavaString = global::Java.Lang.String;
using AMenuCompat = global::AndroidX.Core.View.MenuCompat;
using APopupMenu = global::AndroidX.AppCompat.Widget.PopupMenu;
using AView = global::Android.Views.View;

namespace CodeBrix.Android.UI.Overlay;

/// <summary>
/// A MenuFlyout shown as a platform menu (plan 2.9 tier 2): in a Medium or Expanded window a Material popup menu
/// anchored at the ShowAt target (or at the point it was shown at - a context menu), in a Compact window a modal
/// bottom sheet (<see cref="MenuSheet"/>). MenuFlyoutItem, ToggleMenuFlyoutItem (checkable item),
/// RadioMenuFlyoutItem (exclusive group), MenuFlyoutSubItem (sub-menu) and MenuFlyoutSeparator (group divider)
/// are shown; a letter/digit KeyboardAccelerator becomes the item's shortcut. Choosing an item runs Core's own
/// path (MenuFlyoutItem.Invoke: Click, Command, the toggle) and then hides the flyout; dismissing the menu (a tap
/// outside, back) hides the flyout, so Opening / Opened / Closing / Closed stay Core's.
/// </summary>
internal sealed class NativeMenuFlyout : NativeOverlay
{
    private readonly MenuFlyout _flyout;
    private readonly Dictionary<int, MenuEntry> _byId = new();
    private IReadOnlyList<MenuEntry> _entries;
    private APopupMenu _popup;
    private MenuSheet _sheet;
    private AView _anchor;
    private bool _closingFromCore;
    private bool _showing;

    private NativeMenuFlyout(CodeBrixActivity activity, MenuFlyout flyout)
        : base(activity, flyout)
    {
        _flyout = flyout;
    }

    /// <summary>The menu's entries (top level, in menu order, separators included).</summary>
    internal IReadOnlyList<MenuEntry> Entries => _entries;

    /// <summary>True when shown as a bottom sheet (Compact window), false for a popup menu.</summary>
    internal bool IsSheet => _sheet != null;

    /// <summary>The rows the bottom sheet shows now (empty for a popup menu).</summary>
    internal System.Collections.Generic.IEnumerable<AView> SheetRows => _sheet?.Rows ?? Enumerable.Empty<AView>();

    /// <summary>The bottom sheet dialog (null for a popup menu).</summary>
    internal global::Google.Android.Material.BottomSheet.BottomSheetDialog SheetDialog => _sheet?.Dialog;

    /// <summary>The popup menu (null for a bottom sheet or once dismissed).</summary>
    internal APopupMenu Popup => _popup;

    /// <inheritdoc />
    internal override bool IsShowing => _showing;

    /// <summary>
    /// Shows <paramref name="flyout"/> as a platform menu when it can (native menus switched on, standard items
    /// only, at least one item, a live activity); returns null otherwise (Core presents it).
    /// </summary>
    internal static NativeMenuFlyout TryShow(MenuFlyout flyout, FrameworkElement target, FlyoutShowOptions options, ILogger log)
    {
        if (!OverlayPresentation.NativeMenuFlyouts || flyout == null || target == null)
        {
            return null;
        }

        if (!MenuModelBuilder.IsNativelyPresentable(flyout.Items))
        {
            if (log.IsEnabled(LogLevel.Debug))
            {
                log.LogDebug("MenuFlyout with custom items: presented by Core (tier 1).");
            }

            return null;
        }

        var entries = MenuModelBuilder.Build(flyout.Items);
        if (!entries.Any(e => e.Kind != MenuEntryKind.Separator))
        {
            return null;
        }

        if (HandlerContext.For(target) is not CodeBrixActivity { IsFinishing: false, IsDestroyed: false, RootLayout: not null } activity)
        {
            return null;
        }

        var overlay = new NativeMenuFlyout(activity, flyout) { _entries = entries };
        var widthClass = OverlayPresentation.WidthClassFor(target.XamlRoot?.Size.Width ?? 0);
        if (widthClass == WindowWidthClass.Compact)
        {
            overlay.ShowSheet();
        }
        else
        {
            overlay.ShowPopup(target, options?.Position, HandlerContext.Density(target));
        }

        return overlay;
    }

    /// <summary>Chooses the entry with <paramref name="text"/> (at any depth) through the native menu.</summary>
    /// <returns>False when no enabled entry has that text.</returns>
    internal bool PerformItem(string text)
    {
        if (_sheet != null)
        {
            return _sheet.PerformItem(text);
        }

        var match = _byId.FirstOrDefault(p => p.Value.Text == text && p.Value.Kind != MenuEntryKind.SubMenu);
        if (match.Value == null || !match.Value.IsEnabled || _popup == null)
        {
            return false;
        }

        return _popup.Menu.PerformIdentifierAction(match.Key, 0);
    }

    /// <summary>Takes the menu down the way a tap outside it does.</summary>
    internal void DismissByUser()
    {
        _popup?.Dismiss();
        _sheet?.Cancel();
    }

    /// <inheritdoc />
    internal override void CloseFromCore()
    {
        _closingFromCore = true;
        if (_popup != null)
        {
            _popup.Dismiss();
        }
        else if (_sheet != null)
        {
            _sheet.Dismiss();
        }
        else
        {
            OnDismissed();
        }
    }

    private void ShowPopup(FrameworkElement target, global::Windows.Foundation.Point? position, double density)
    {
        _anchor = OverlayAnchors.Add(Activity, OverlayAnchors.PlacementRect(target, position), density);
        var popup = new APopupMenu(Activity, _anchor, (int)AGravityFlags.Start);
        _popup = popup;
        var nextId = 1;
        AddEntries(popup.Menu, _entries, ref nextId);
        AMenuCompat.SetGroupDividerEnabled(popup.Menu, true);
        popup.MenuItemClick += (_, e) => e.Handled = OnMenuItemClick(e.Item);
        popup.DismissEvent += (_, _) => OnDismissed();
        _showing = true;
        NativeOverlays.Add(this);
        popup.Show();
    }

    private void ShowSheet()
    {
        _sheet = new MenuSheet(Activity, _entries, Choose);
        _sheet.Dismissed += OnDismissed;
        _showing = true;
        NativeOverlays.Add(this);
        _sheet.Show();
    }

    private void AddEntries(AIMenu menu, IReadOnlyList<MenuEntry> entries, ref int nextId)
    {
        var placed = MenuLayout.Place(entries);
        var checkedRadios = MenuLayout.CheckedRadioEntries(placed);
        var order = 0;
        var exclusiveGroups = new HashSet<int>();
        foreach (var p in placed)
        {
            var entry = p.Entry;
            var id = nextId++;
            _byId[id] = entry;
            AIMenuItem item;
            if (entry.Kind == MenuEntryKind.SubMenu)
            {
                var sub = menu.AddSubMenu(p.GroupId, id, order++, new AJavaString(entry.Text));
                AMenuCompat.SetGroupDividerEnabled(sub, true);
                AddEntries(sub, entry.Children, ref nextId);
                item = sub.Item;
            }
            else
            {
                item = menu.Add(p.GroupId, id, order++, new AJavaString(entry.Text));
            }

            item.SetEnabled(entry.IsEnabled);
            if (entry.Kind == MenuEntryKind.Toggle)
            {
                item.SetCheckable(true);
                item.SetChecked(entry.IsChecked);
            }
            else if (entry.Kind == MenuEntryKind.Radio)
            {
                exclusiveGroups.Add(p.GroupId);
            }

            if (entry.Shortcut is { } shortcut)
            {
                item.SetAlphabeticShortcut(shortcut.Character, shortcut.MetaState);
            }
        }

        foreach (var group in exclusiveGroups)
        {
            menu.SetGroupCheckable(group, true, true);
        }

        foreach (var p in placed)
        {
            if (p.ExclusiveGroup && checkedRadios.Contains(p.Entry))
            {
                var id = _byId.First(pair => ReferenceEquals(pair.Value, p.Entry)).Key;
                menu.FindItem(id)?.SetChecked(true);
            }
        }
    }

    private bool OnMenuItemClick(AIMenuItem item)
    {
        if (item == null || !_byId.TryGetValue(item.ItemId, out var entry) || entry.Kind == MenuEntryKind.SubMenu)
        {
            // A sub-menu opens itself.
            return false;
        }

        Choose(entry);
        return true;
    }

    private void Choose(MenuEntry entry)
    {
        if (entry.Source is MenuFlyoutItem item && entry.IsEnabled)
        {
            item.Invoke();
        }

        _flyout.Hide();
    }

    private void OnDismissed()
    {
        if (!_showing)
        {
            return;
        }

        _showing = false;
        OverlayAnchors.Remove(_anchor);
        _anchor = null;
        _popup = null;
        NativeOverlays.Remove(this);
        if (!_closingFromCore && _flyout.IsOpen)
        {
            // Dismissed by the user (a tap outside, back): Core closes the flyout (Closing / Closed).
            _flyout.Hide();
        }
    }
}
#endif
