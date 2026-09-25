using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.System;

namespace CodeBrix.Android.UI.Overlay;

/// <summary>One keyboard shortcut the app offers: what it does, its key and modifiers.</summary>
/// <param name="Label">What the shortcut does (the element's automation name, menu text, tooltip or content).</param>
/// <param name="Key">The key.</param>
/// <param name="Modifiers">The modifiers.</param>
internal readonly record struct KeyboardShortcut(string Label, VirtualKey Key, VirtualKeyModifiers Modifiers);

/// <summary>
/// Collects the enabled KeyboardAccelerators of a window's visual tree (and of the MenuFlyouts its MenuBar and
/// buttons carry) with a label for each - what Android's keyboard shortcuts helper (Meta + /) lists
/// (Activity.OnProvideKeyboardShortcuts). An accelerator whose element has no label is left out.
/// </summary>
internal static class KeyboardShortcutCollector
{
    /// <summary>The shortcuts under <paramref name="root"/>, in tree order, without duplicates.</summary>
    internal static IReadOnlyList<KeyboardShortcut> Collect(DependencyObject root)
    {
        var shortcuts = new List<KeyboardShortcut>();
        var seen = new HashSet<(VirtualKey, VirtualKeyModifiers)>();
        Walk(root, shortcuts, seen);
        return shortcuts;
    }

    /// <summary>The label of an element's shortcuts, or null.</summary>
    internal static string LabelOf(DependencyObject element)
    {
        if (element is UIElement ui && AutomationProperties.GetName(ui) is { Length: > 0 } name)
        {
            return name;
        }

        return element switch
        {
            MenuFlyoutItem item when !string.IsNullOrEmpty(item.Text) => item.Text,
            MenuBarItem bar when !string.IsNullOrEmpty(bar.Title) => bar.Title,
            ContentControl { Content: string content } when content.Length > 0 => content,
            UIElement ui2 when ToolTipService.GetToolTip(ui2) is string tip && tip.Length > 0 => tip,
            _ => null,
        };
    }

    private static void Walk(DependencyObject node, List<KeyboardShortcut> shortcuts, HashSet<(VirtualKey, VirtualKeyModifiers)> seen)
    {
        if (node == null || node is UIElement { Visibility: not Visibility.Visible })
        {
            return;
        }

        if (node is UIElement element)
        {
            Add(element, shortcuts, seen);
            if (element is MenuBar menuBar)
            {
                foreach (var barItem in menuBar.Items)
                {
                    AddMenuItems(barItem.Items, shortcuts, seen);
                }
            }
            else if (element is Button { Flyout: MenuFlyout flyout })
            {
                AddMenuItems(flyout.Items, shortcuts, seen);
            }
        }

        var count = VisualTreeHelper.GetChildrenCount(node);
        for (var i = 0; i < count; i++)
        {
            Walk(VisualTreeHelper.GetChild(node, i), shortcuts, seen);
        }
    }

    private static void AddMenuItems(IEnumerable<MenuFlyoutItemBase> items, List<KeyboardShortcut> shortcuts, HashSet<(VirtualKey, VirtualKeyModifiers)> seen)
    {
        foreach (var item in items)
        {
            if (item is MenuFlyoutSubItem sub)
            {
                AddMenuItems(sub.Items, shortcuts, seen);
            }
            else
            {
                Add(item, shortcuts, seen);
            }
        }
    }

    private static void Add(UIElement element, List<KeyboardShortcut> shortcuts, HashSet<(VirtualKey, VirtualKeyModifiers)> seen)
    {
        var accelerators = element.KeyboardAccelerators;
        if (accelerators == null || accelerators.Count == 0 || LabelOf(element) is not { } label)
        {
            return;
        }

        foreach (var accelerator in accelerators)
        {
            if (accelerator.IsEnabled && seen.Add((accelerator.Key, accelerator.Modifiers)))
            {
                shortcuts.Add(new KeyboardShortcut(label, accelerator.Key, accelerator.Modifiers));
            }
        }
    }
}
