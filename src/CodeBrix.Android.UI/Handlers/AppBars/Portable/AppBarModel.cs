using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>The kind of one entry of a native app bar's menu.</summary>
internal enum AppBarEntryKind
{
    /// <summary>An AppBarButton (a menu item; a sub-menu when its Flyout is a simple MenuFlyout).</summary>
    Button,

    /// <summary>An AppBarToggleButton (a checkable menu item).</summary>
    ToggleButton,
}

/// <summary>Where an entry of a native app bar is shown.</summary>
internal enum AppBarEntryPlacement
{
    /// <summary>Always an action on the bar (IsDynamicOverflowEnabled false).</summary>
    Action,

    /// <summary>An action on the bar while there is room, else in the overflow menu (IsDynamicOverflowEnabled).</summary>
    ActionIfRoom,

    /// <summary>In the overflow menu (SecondaryCommands, or every command of a Minimal bar).</summary>
    Overflow,
}

/// <summary>One command of a CommandBar as the native app bar shows it.</summary>
/// <param name="Kind">The kind.</param>
/// <param name="Element">The AppBarButton / AppBarToggleButton.</param>
/// <param name="Label">The label (the menu item's title).</param>
/// <param name="Icon">The IconElement (null = the label is shown as text).</param>
/// <param name="IsEnabled">True when it can be invoked.</param>
/// <param name="IsChecked">True for a checked toggle.</param>
/// <param name="Placement">Where it goes.</param>
/// <param name="ShowLabel">True when an action shows its label beside its icon (DefaultLabelPosition Right).</param>
/// <param name="Group">The menu group (overflow separators start a new group).</param>
internal sealed record AppBarEntry(
    AppBarEntryKind Kind,
    ICommandBarElement Element,
    string Label,
    IconElement Icon,
    bool IsEnabled,
    bool IsChecked,
    AppBarEntryPlacement Placement,
    bool ShowLabel,
    int Group);

/// <summary>
/// AP10-A: what a WinUI CommandBar looks like as a Material app bar (plan 2.10 adaptive row "CommandBar (WinUI)",
/// tsv rows CommandBar, AppBarButton, AppBarToggleButton, AppBarSeparator, CommandBarOverflowPresenter). Pure: the
/// Android handler turns the entries into the bar's menu. PrimaryCommands become actions (shown while there is room
/// when IsDynamicOverflowEnabled, as WinUI moves them to the overflow), SecondaryCommands the overflow menu (an
/// AppBarSeparator there starts a new menu group, shown with a divider); a primary separator has no Material form
/// and is left out; a Minimal bar keeps every command in the overflow; OverflowButtonVisibility Collapsed hides the
/// secondary commands (as it makes them unreachable in WinUI). A bar whose content or commands a Material app bar
/// cannot show keeps its Fluent template (<see cref="CanMapNatively"/>).
/// </summary>
internal static class AppBarModel
{
    /// <summary>
    /// True when a Material app bar can show <paramref name="bar"/>: its Content is text (or nothing), every command
    /// is an AppBarButton, AppBarToggleButton or AppBarSeparator, and it is not the command row of a
    /// CommandBarFlyout (that one keeps the flyout's own template).
    /// </summary>
    /// <param name="bar">The CommandBar.</param>
    /// <param name="reason">Why not, when false.</param>
    /// <returns>True for the native app bar.</returns>
    internal static bool CanMapNatively(CommandBar bar, out string reason)
    {
        reason = null;
        if (bar == null)
        {
            reason = "no CommandBar";
            return false;
        }

        if (bar is Microsoft.UI.Xaml.Controls.Primitives.CommandBarFlyoutCommandBar)
        {
            reason = "it is the command row of a CommandBarFlyout (the flyout's template shows it)";
            return false;
        }

        if (bar.Content != null && bar.Content is not string)
        {
            reason = "its Content is not text (a Material app bar's title is text)";
            return false;
        }

        if (!OnlyKnownCommands(bar.PrimaryCommands) || !OnlyKnownCommands(bar.SecondaryCommands))
        {
            reason = "it has a command other than AppBarButton, AppBarToggleButton or AppBarSeparator (an AppBarElementContainer or an app element)";
            return false;
        }

        return true;
    }

    /// <summary>The entries of the native bar, primary commands first, then the overflow.</summary>
    /// <param name="bar">The CommandBar.</param>
    /// <returns>The entries.</returns>
    internal static IReadOnlyList<AppBarEntry> Entries(CommandBar bar)
    {
        var entries = new List<AppBarEntry>();
        if (bar == null)
        {
            return entries;
        }

        var minimal = bar.ClosedDisplayMode == AppBarClosedDisplayMode.Minimal;
        var primaryPlacement = minimal ? AppBarEntryPlacement.Overflow
            : bar.IsDynamicOverflowEnabled ? AppBarEntryPlacement.ActionIfRoom
            : AppBarEntryPlacement.Action;
        var labelsRight = bar.DefaultLabelPosition == CommandBarDefaultLabelPosition.Right;
        var enabled = bar.IsEnabled;
        foreach (var command in bar.PrimaryCommands)
        {
            if (Entry(command, primaryPlacement, labelsRight, enabled, 0) is { } entry)
            {
                entries.Add(entry);
            }
        }

        if (bar.OverflowButtonVisibility == CommandBarOverflowButtonVisibility.Collapsed)
        {
            return entries;
        }

        var group = 1;
        foreach (var command in bar.SecondaryCommands)
        {
            if (command is AppBarSeparator separator)
            {
                if (separator.Visibility == Visibility.Visible)
                {
                    group++;
                }

                continue;
            }

            if (Entry(command, AppBarEntryPlacement.Overflow, labelsRight, enabled, group) is { } entry)
            {
                entries.Add(entry);
            }
        }

        return entries;
    }

    /// <summary>The label of a command (its Label, else its tooltip text, else empty).</summary>
    /// <param name="command">The command.</param>
    /// <returns>The label.</returns>
    internal static string LabelOf(ICommandBarElement command) => command switch
    {
        AppBarButton button => button.Label ?? string.Empty,
        AppBarToggleButton toggle => toggle.Label ?? string.Empty,
        _ => string.Empty,
    };

    private static AppBarEntry Entry(ICommandBarElement command, AppBarEntryPlacement placement, bool labelsRight, bool barEnabled, int group)
    {
        switch (command)
        {
            case AppBarButton button when button.Visibility == Visibility.Visible:
                return new AppBarEntry(
                    AppBarEntryKind.Button,
                    button,
                    LabelOf(button),
                    button.Icon,
                    barEnabled && button.IsEnabled,
                    false,
                    placement,
                    labelsRight && button.LabelPosition != CommandBarLabelPosition.Collapsed,
                    group);
            case AppBarToggleButton toggle when toggle.Visibility == Visibility.Visible:
                return new AppBarEntry(
                    AppBarEntryKind.ToggleButton,
                    toggle,
                    LabelOf(toggle),
                    toggle.Icon,
                    barEnabled && toggle.IsEnabled,
                    toggle.IsChecked == true,
                    placement,
                    labelsRight && toggle.LabelPosition != CommandBarLabelPosition.Collapsed,
                    group);
            default:
                return null;
        }
    }

    private static bool OnlyKnownCommands(IEnumerable<ICommandBarElement> commands)
    {
        if (commands == null)
        {
            return true;
        }

        foreach (var command in commands)
        {
            if (command is not (AppBarButton or AppBarToggleButton or AppBarSeparator))
            {
                return false;
            }
        }

        return true;
    }
}
