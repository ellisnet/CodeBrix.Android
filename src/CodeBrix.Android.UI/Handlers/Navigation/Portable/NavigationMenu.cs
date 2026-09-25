using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>What a NavigationView menu entry is.</summary>
internal enum NavigationMenuEntryKind
{
    /// <summary>A destination (a NavigationViewItem or a data item).</summary>
    Item,

    /// <summary>The Settings destination (IsSettingsVisible).</summary>
    Settings,

    /// <summary>A NavigationViewItemHeader.</summary>
    Header,

    /// <summary>A NavigationViewItemSeparator.</summary>
    Separator,
}

/// <summary>One entry of a NavigationView's menu as a native navigation container shows it.</summary>
/// <param name="Kind">What it is.</param>
/// <param name="Item">The menu item (a NavigationViewItem, or the data item of MenuItemsSource; the Settings item for Settings).</param>
/// <param name="Text">Its label.</param>
/// <param name="Icon">Its IconElement, or null.</param>
/// <param name="IsEnabled">False for a disabled item.</param>
internal sealed record NavigationMenuEntry(NavigationMenuEntryKind Kind, object Item, string Text, IconElement Icon, bool IsEnabled)
{
    /// <summary>True for an entry the user can navigate to (an item or Settings).</summary>
    internal bool IsDestination => Kind is NavigationMenuEntryKind.Item or NavigationMenuEntryKind.Settings;
}

/// <summary>
/// The menu of a NavigationView as the native adaptive containers show it (plan 3 rows NavigationView /
/// NavigationViewItem: "Menu items of the active navigation container; templated menu item content: text +
/// icon only"), and the rule for when a NavigationView can be shown natively at all: a NavigationView whose pane
/// carries things a Material container has no place for (a pane header or footer, an AutoSuggestBox, custom pane
/// content, footer items, item templates, nested items, element content in an item) keeps Core's Fluent template.
/// </summary>
internal static class NavigationMenu
{
    /// <summary>The label of the Settings destination.</summary>
    internal const string SettingsLabel = "Settings";

    /// <summary>
    /// True when <paramref name="view"/> can be shown in a native navigation container; otherwise
    /// <paramref name="reason"/> says what keeps the Fluent template.
    /// </summary>
    /// <param name="view">The NavigationView.</param>
    /// <param name="reason">Why not (null when it can).</param>
    /// <returns>True when native containers can show it.</returns>
    internal static bool CanMapNatively(NavigationView view, out string reason)
    {
        reason = null;
        if (view == null)
        {
            reason = "no NavigationView";
            return false;
        }

        if (view.ReadLocalValue(Control.TemplateProperty) != DependencyProperty.UnsetValue)
        {
            reason = "the app gave it a Template";
        }
        else if (view.PaneHeader != null || view.PaneFooter != null || view.PaneCustomContent != null)
        {
            reason = "it has a PaneHeader, PaneFooter or PaneCustomContent";
        }
        else if (view.AutoSuggestBox != null)
        {
            reason = "it has an AutoSuggestBox";
        }
        else if (view.MenuItemTemplate != null || view.MenuItemTemplateSelector != null)
        {
            reason = "it has a MenuItemTemplate";
        }
        else if (view.FooterMenuItemsSource != null || (view.FooterMenuItems != null && view.FooterMenuItems.Count > 0))
        {
            reason = "it has footer menu items";
        }
        else
        {
            foreach (var item in Items(view))
            {
                if (item is NavigationViewItem nested && (nested.MenuItemsSource != null || (nested.MenuItems != null && nested.MenuItems.Count > 0)))
                {
                    reason = "an item has nested items";
                    break;
                }

                if (item is ContentControl { Content: UIElement })
                {
                    reason = "an item has element content";
                    break;
                }

                if (item is UIElement and not NavigationViewItemBase)
                {
                    reason = "a menu item is not a NavigationViewItem";
                    break;
                }
            }
        }

        return reason == null;
    }

    /// <summary>The entries a native container shows, in order (Settings last when visible).</summary>
    /// <param name="view">The NavigationView.</param>
    /// <param name="settingsItem">The item standing for Settings (null: no Settings entry).</param>
    /// <returns>The entries.</returns>
    internal static IReadOnlyList<NavigationMenuEntry> Entries(NavigationView view, NavigationViewItem settingsItem)
    {
        var entries = new List<NavigationMenuEntry>();
        if (view == null)
        {
            return entries;
        }

        foreach (var item in Items(view))
        {
            switch (item)
            {
                case NavigationViewItemSeparator:
                    entries.Add(new NavigationMenuEntry(NavigationMenuEntryKind.Separator, item, string.Empty, null, true));
                    break;
                case NavigationViewItemHeader header:
                    entries.Add(new NavigationMenuEntry(NavigationMenuEntryKind.Header, item, TextOf(header.Content), null, true));
                    break;
                case NavigationViewItem navigationItem:
                    entries.Add(new NavigationMenuEntry(NavigationMenuEntryKind.Item, item, TextOf(navigationItem.Content), navigationItem.Icon, navigationItem.IsEnabled));
                    break;
                case null:
                    break;
                default:
                    entries.Add(new NavigationMenuEntry(NavigationMenuEntryKind.Item, item, TextOf(item), null, true));
                    break;
            }
        }

        if (view.IsSettingsVisible && settingsItem != null)
        {
            entries.Add(new NavigationMenuEntry(NavigationMenuEntryKind.Settings, settingsItem, TextOf(settingsItem.Content), settingsItem.Icon, true));
        }

        return entries;
    }

    /// <summary>How many destinations (items and Settings) the entries hold: what decides a bottom bar or a drawer.</summary>
    /// <param name="entries">The entries.</param>
    /// <returns>The count.</returns>
    internal static int DestinationCount(IReadOnlyList<NavigationMenuEntry> entries)
    {
        var count = 0;
        foreach (var entry in entries)
        {
            if (entry.IsDestination)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>The index of the entry showing <paramref name="selectedItem"/>, or -1.</summary>
    /// <param name="entries">The entries.</param>
    /// <param name="selectedItem">The NavigationView's SelectedItem.</param>
    /// <returns>The index.</returns>
    internal static int IndexOf(IReadOnlyList<NavigationMenuEntry> entries, object selectedItem)
    {
        if (selectedItem == null)
        {
            return -1;
        }

        for (var i = 0; i < entries.Count; i++)
        {
            var item = entries[i].Item;
            if (ReferenceEquals(item, selectedItem) || (item is not DependencyObject && Equals(item, selectedItem)))
            {
                return i;
            }

            if (item is NavigationViewItem { Content: { } content } && selectedItem is not DependencyObject && Equals(content, selectedItem))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>The label text of a content value.</summary>
    /// <param name="content">The content.</param>
    /// <returns>The text.</returns>
    internal static string TextOf(object content) => content switch
    {
        null => string.Empty,
        string text => text,
        _ => Convert.ToString(content, System.Globalization.CultureInfo.CurrentCulture) ?? string.Empty,
    };

    private static IEnumerable<object> Items(NavigationView view)
    {
        if (view.MenuItemsSource is IEnumerable source)
        {
            foreach (var item in source)
            {
                yield return item;
            }

            yield break;
        }

        if (view.MenuItems != null)
        {
            foreach (var item in view.MenuItems)
            {
                yield return item;
            }
        }
    }
}
