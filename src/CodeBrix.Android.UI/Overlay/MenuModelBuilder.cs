using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CodeBrix.Android.UI.Overlay;

/// <summary>
/// Reads a MenuFlyout's items (MenuFlyoutItem, ToggleMenuFlyoutItem, RadioMenuFlyoutItem, MenuFlyoutSubItem,
/// MenuFlyoutSeparator) into <see cref="MenuEntry"/> values a native menu is built from. Collapsed items are
/// left out. <see cref="IsNativelyPresentable"/> says whether every item is one of those kinds.
/// </summary>
internal static class MenuModelBuilder
{
    /// <summary>
    /// True when a native menu can show every item (the five menu item kinds, at any depth); an app-defined
    /// MenuFlyoutItemBase subclass keeps the menu in Core (tier 1).
    /// </summary>
    /// <param name="items">The menu's items.</param>
    /// <returns>True when natively presentable.</returns>
    internal static bool IsNativelyPresentable(IEnumerable<MenuFlyoutItemBase> items)
    {
        foreach (var item in items)
        {
            var type = item.GetType();
            if (item is MenuFlyoutSubItem sub)
            {
                if (type != typeof(MenuFlyoutSubItem) || !IsNativelyPresentable(sub.Items))
                {
                    return false;
                }
            }
            else if (type != typeof(MenuFlyoutItem) && type != typeof(ToggleMenuFlyoutItem)
                && type != typeof(RadioMenuFlyoutItem) && type != typeof(MenuFlyoutSeparator))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Reads the items.</summary>
    /// <param name="items">The menu's items.</param>
    /// <returns>The entries, in menu order.</returns>
    internal static IReadOnlyList<MenuEntry> Build(IEnumerable<MenuFlyoutItemBase> items)
    {
        var entries = new List<MenuEntry>();
        foreach (var item in items)
        {
            if (item.Visibility != Visibility.Visible)
            {
                continue;
            }

            switch (item)
            {
                case MenuFlyoutSeparator:
                    entries.Add(new MenuEntry { Kind = MenuEntryKind.Separator, Source = item });
                    break;
                case MenuFlyoutSubItem sub:
                    entries.Add(new MenuEntry
                    {
                        Kind = MenuEntryKind.SubMenu,
                        Text = sub.Text ?? string.Empty,
                        IsEnabled = sub.IsEnabled,
                        IconGlyph = GlyphOf(sub.Icon),
                        Children = Build(sub.Items),
                        Source = item,
                    });
                    break;
                case RadioMenuFlyoutItem radio:
                    entries.Add(new MenuEntry
                    {
                        Kind = MenuEntryKind.Radio,
                        Text = radio.Text ?? string.Empty,
                        IsEnabled = radio.IsEnabled,
                        IsChecked = radio.IsChecked,
                        RadioGroup = radio.GroupName ?? string.Empty,
                        Shortcut = ShortcutOf(radio),
                        IconGlyph = GlyphOf(radio.Icon),
                        Source = item,
                    });
                    break;
                case ToggleMenuFlyoutItem toggle:
                    entries.Add(new MenuEntry
                    {
                        Kind = MenuEntryKind.Toggle,
                        Text = toggle.Text ?? string.Empty,
                        IsEnabled = toggle.IsEnabled,
                        IsChecked = toggle.IsChecked,
                        Shortcut = ShortcutOf(toggle),
                        IconGlyph = GlyphOf(toggle.Icon),
                        Source = item,
                    });
                    break;
                case MenuFlyoutItem menuItem:
                    entries.Add(new MenuEntry
                    {
                        Kind = MenuEntryKind.Item,
                        Text = menuItem.Text ?? string.Empty,
                        IsEnabled = menuItem.IsEnabled,
                        Shortcut = ShortcutOf(menuItem),
                        IconGlyph = GlyphOf(menuItem.Icon),
                        Source = item,
                    });
                    break;
            }
        }

        return entries;
    }

    private static MenuShortcut? ShortcutOf(MenuFlyoutItem item)
    {
        var accelerators = item.KeyboardAccelerators;
        if (accelerators == null)
        {
            return null;
        }

        foreach (var accelerator in accelerators)
        {
            if (accelerator.IsEnabled && MenuShortcut.From(accelerator.Key, accelerator.Modifiers) is { } shortcut)
            {
                return shortcut;
            }
        }

        return null;
    }

    private static string GlyphOf(IconElement icon) => icon switch
    {
        FontIcon font when !string.IsNullOrEmpty(font.Glyph) => font.Glyph,
        SymbolIcon symbol => ((char)(int)symbol.Symbol).ToString(),
        _ => null,
    };
}
