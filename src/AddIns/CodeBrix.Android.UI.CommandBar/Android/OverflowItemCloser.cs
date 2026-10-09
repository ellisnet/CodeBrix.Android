using System.Collections.Generic;
using System.Runtime.CompilerServices;
using CodeBrix.Android.UI.CommandBar.Portable;
using CodeBrix.Android.UI.Handlers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using ToolBar = CodeBrix.Platform.UI.CommandBar.ToolBar;
using ToolBarPanel = CodeBrix.Platform.UI.CommandBar.ToolBarPanel;
using ToolButton = CodeBrix.Platform.UI.CommandBar.ToolButton;
using ToolDropDownButton = CodeBrix.Platform.UI.CommandBar.ToolDropDownButton;

namespace CodeBrix.Android.UI.CommandBar.Android;

/// <summary>
/// Closes a ToolBar's overflow flyout when an item in it is clicked (<see cref="OverflowItemDismissal"/>): the item's
/// Click runs first (the application's handlers and command), then the flyout's popup closes, which hides the flyout
/// through its own close path - so the next tap and the next Back act on the page, not on an open flyout.
/// </summary>
/// <remarks>
/// PLATFORM-QUEUE 2026-10-08 (FIXLIST_platform_republish_queue_2026-10-07.txt [Q8]): the CommandBar Core's ToolBar never
/// closes its overflow flyout after an item click (every head; the flyout stays open until a light dismiss, which eats the
/// next tap). Remove when the Platform CommandBar add-in that closes it itself is the pinned intake (then this registration
/// would only find the flyout already closed). Every ToolButton (and ToolToggleButton / ToolDropDownButton) keeps the
/// handler it had - the registration wraps the factory that served it.
/// </remarks>
internal static class OverflowItemCloser
{
    private static readonly object _watchedMark = new();
    private static readonly ConditionalWeakTable<ToolButton, object> _watched = new();

    /// <summary>Wraps the handler registration that serves ToolButton and its subclasses (call once).</summary>
    internal static void Register()
    {
        var registry = CodeBrixHandlers.Registry;
        var inner = registry.FactoryFor(typeof(ToolButton));
        registry.Register<ToolButton>(element =>
        {
            Watch(element as ToolButton);
            return inner(element);
        });
    }

    /// <summary>Follows the clicks of a tool item (once per item, however often it re-enters the tree).</summary>
    /// <param name="button">The item.</param>
    internal static void Watch(ToolButton button)
    {
        if (button == null || _watched.TryGetValue(button, out _))
        {
            return;
        }

        _watched.Add(button, _watchedMark);
        button.Click += OnClick;
    }

    private static void OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not ToolButton button || button.XamlRoot is not { } root)
        {
            return;
        }

        var popups = VisualTreeHelper.GetOpenPopupsForXamlRoot(root);
        if (popups == null || popups.Count == 0)
        {
            return;
        }

        Popup owner = null;
        var ancestors = new List<OverflowAncestorKind>();
        for (var node = VisualTreeHelper.GetParent(button); node != null; node = VisualTreeHelper.GetParent(node))
        {
            if (PopupWithChild(popups, node) is { } popup)
            {
                ancestors.Add(OverflowAncestorKind.PopupChild);
                owner = popup;
                break;
            }

            ancestors.Add(node switch
            {
                ToolBar => OverflowAncestorKind.ToolBar,
                ToolBarPanel => OverflowAncestorKind.ToolBarPanel,
                _ => OverflowAncestorKind.Other,
            });
        }

        if (owner != null && OverflowItemDismissal.ClosesFlyout(button is ToolDropDownButton, ancestors))
        {
            owner.IsOpen = false;
        }
    }

    private static Popup PopupWithChild(IReadOnlyList<Popup> popups, DependencyObject node)
    {
        for (var i = popups.Count - 1; i >= 0; i--)
        {
            if (ReferenceEquals(popups[i].Child, node))
            {
                return popups[i];
            }
        }

        return null;
    }
}
