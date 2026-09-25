using System.Collections.Generic;
using Windows.System;

namespace CodeBrix.Android.UI.Overlay;

/// <summary>The kind of an entry of a menu shown natively.</summary>
internal enum MenuEntryKind
{
    /// <summary>A MenuFlyoutItem: runs when chosen.</summary>
    Item,

    /// <summary>A ToggleMenuFlyoutItem: a check mark that flips when chosen.</summary>
    Toggle,

    /// <summary>A RadioMenuFlyoutItem: one checked item per group.</summary>
    Radio,

    /// <summary>A MenuFlyoutSubItem: opens its own entries.</summary>
    SubMenu,

    /// <summary>A MenuFlyoutSeparator: starts a new group.</summary>
    Separator,
}

/// <summary>One entry of a menu shown natively, read from a MenuFlyout's items (see MenuModelBuilder).</summary>
internal sealed class MenuEntry
{
    /// <summary>The kind.</summary>
    internal MenuEntryKind Kind { get; init; }

    /// <summary>The text shown.</summary>
    internal string Text { get; init; } = string.Empty;

    /// <summary>False to show the entry disabled.</summary>
    internal bool IsEnabled { get; init; } = true;

    /// <summary>The check state of a Toggle or Radio entry.</summary>
    internal bool IsChecked { get; init; }

    /// <summary>The group name of a Radio entry.</summary>
    internal string RadioGroup { get; init; } = string.Empty;

    /// <summary>The keyboard shortcut shown next to the entry, or null.</summary>
    internal MenuShortcut? Shortcut { get; init; }

    /// <summary>The glyph of the entry's FontIcon / SymbolIcon, or null.</summary>
    internal string IconGlyph { get; init; }

    /// <summary>The entries of a SubMenu.</summary>
    internal IReadOnlyList<MenuEntry> Children { get; init; } = new List<MenuEntry>();

    /// <summary>The Core item this entry was read from (the MenuFlyoutItemBase).</summary>
    internal object Source { get; init; }
}

/// <summary>A menu entry placed for a native menu: its group and how that group checks.</summary>
/// <param name="Entry">The entry.</param>
/// <param name="GroupId">The native group id (a separator starts the next group; radio runs get their own).</param>
/// <param name="ExclusiveGroup">True when the entry's group is an exclusive (radio) group.</param>
internal readonly record struct PlacedMenuEntry(MenuEntry Entry, int GroupId, bool ExclusiveGroup);

/// <summary>A keyboard shortcut as an Android menu shows it: one letter or digit plus modifier meta flags.</summary>
/// <param name="Character">The lower-case letter or digit.</param>
/// <param name="MetaState">Android KeyEvent META_* flags (Ctrl 0x1000, Shift 0x1, Alt 0x2, Meta 0x10000).</param>
internal readonly record struct MenuShortcut(char Character, int MetaState)
{
    /// <summary>KeyEvent.META_SHIFT_ON.</summary>
    internal const int MetaShift = 0x1;

    /// <summary>KeyEvent.META_ALT_ON.</summary>
    internal const int MetaAlt = 0x2;

    /// <summary>KeyEvent.META_CTRL_ON.</summary>
    internal const int MetaCtrl = 0x1000;

    /// <summary>KeyEvent.META_META_ON.</summary>
    internal const int MetaMeta = 0x10000;

    /// <summary>
    /// The shortcut of a WinUI KeyboardAccelerator, when Android menus can show it (a letter or a digit key);
    /// null otherwise.
    /// </summary>
    /// <param name="key">The accelerator key.</param>
    /// <param name="modifiers">The accelerator modifiers.</param>
    /// <returns>The shortcut, or null.</returns>
    internal static MenuShortcut? From(VirtualKey key, VirtualKeyModifiers modifiers)
    {
        char character;
        if (key >= VirtualKey.A && key <= VirtualKey.Z)
        {
            character = (char)('a' + (key - VirtualKey.A));
        }
        else if (key >= VirtualKey.Number0 && key <= VirtualKey.Number9)
        {
            character = (char)('0' + (key - VirtualKey.Number0));
        }
        else
        {
            return null;
        }

        var meta = 0;
        if ((modifiers & VirtualKeyModifiers.Control) != 0)
        {
            meta |= MetaCtrl;
        }

        if ((modifiers & VirtualKeyModifiers.Shift) != 0)
        {
            meta |= MetaShift;
        }

        if ((modifiers & VirtualKeyModifiers.Menu) != 0)
        {
            meta |= MetaAlt;
        }

        if ((modifiers & VirtualKeyModifiers.Windows) != 0)
        {
            meta |= MetaMeta;
        }

        return new MenuShortcut(character, meta);
    }
}

/// <summary>
/// Lays a menu's entries out in native groups: a separator ends a group (separators at the start, at the end
/// or next to each other show nothing), and a run of radio entries with one group name becomes its own
/// exclusive group, as Android checks radio items per group.
/// </summary>
internal static class MenuLayout
{
    /// <summary>Places the entries (separators are consumed).</summary>
    /// <param name="entries">The entries in menu order.</param>
    /// <returns>The entries to show, with their groups.</returns>
    internal static IReadOnlyList<PlacedMenuEntry> Place(IReadOnlyList<MenuEntry> entries)
    {
        var placed = new List<PlacedMenuEntry>();
        var group = 0;
        var groupHasEntries = false;
        string radioGroup = null;

        foreach (var entry in entries)
        {
            if (entry.Kind == MenuEntryKind.Separator)
            {
                if (groupHasEntries)
                {
                    group++;
                    groupHasEntries = false;
                }

                radioGroup = null;
                continue;
            }

            if (entry.Kind == MenuEntryKind.Radio)
            {
                if (radioGroup == null || radioGroup != entry.RadioGroup)
                {
                    if (groupHasEntries)
                    {
                        group++;
                    }

                    radioGroup = entry.RadioGroup;
                }
            }
            else if (radioGroup != null)
            {
                group++;
                radioGroup = null;
            }

            placed.Add(new PlacedMenuEntry(entry, group, entry.Kind == MenuEntryKind.Radio));
            groupHasEntries = true;
        }

        return placed;
    }

    /// <summary>
    /// The entry to check in each radio group: the LAST checked one (WinUI keeps one checked item per group;
    /// a menu built from code may say two).
    /// </summary>
    /// <param name="placed">The placed entries.</param>
    /// <returns>The checked entries.</returns>
    internal static ISet<MenuEntry> CheckedRadioEntries(IReadOnlyList<PlacedMenuEntry> placed)
    {
        var byGroup = new Dictionary<int, MenuEntry>();
        foreach (var p in placed)
        {
            if (p.ExclusiveGroup && p.Entry.IsChecked)
            {
                byGroup[p.GroupId] = p.Entry;
            }
        }

        return new HashSet<MenuEntry>(byGroup.Values);
    }
}
