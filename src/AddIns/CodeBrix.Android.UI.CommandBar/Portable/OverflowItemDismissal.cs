using System.Collections.Generic;

namespace CodeBrix.Android.UI.CommandBar.Portable;

/// <summary>What one element on the way up from a clicked tool button is, as far as the overflow rule is concerned.</summary>
internal enum OverflowAncestorKind
{
    /// <summary>Anything that does not decide the rule (a group, an item wrapper, a presenter, a border).</summary>
    Other,

    /// <summary>A ToolBarPanel: a bar's items host, or the panel inside a bar's overflow flyout.</summary>
    ToolBarPanel,

    /// <summary>A ToolBar: the items host above it is the bar itself, not its overflow.</summary>
    ToolBar,

    /// <summary>The child of an open popup (the flyout's presenter): the top of the flyout.</summary>
    PopupChild,
}

/// <summary>
/// The rule that closes a ToolBar's overflow flyout when one of the items in it is clicked (as a WinUI CommandBar closes its
/// overflow menu after a secondary command): the clicked item is a ToolButton or a ToolToggleButton - not a
/// ToolDropDownButton, whose click opens its own menu - and, walking up from it, a ToolBarPanel is reached and then the
/// child of an open popup with no ToolBar in between (a bar that itself sits in a popup keeps it open). Pure: the caller
/// walks the visual tree and classifies each ancestor.
/// </summary>
internal static class OverflowItemDismissal
{
    /// <summary>Decides whether a click on a tool item closes the popup it sits in.</summary>
    /// <param name="itemOpensItsOwnMenu">True for a ToolDropDownButton (its click opens its menu).</param>
    /// <param name="ancestors">The item's ancestors, nearest first, up to (at least) the first popup child.</param>
    /// <returns>True when the click closes the flyout whose popup child was reached.</returns>
    internal static bool ClosesFlyout(bool itemOpensItsOwnMenu, IEnumerable<OverflowAncestorKind> ancestors)
    {
        if (itemOpensItsOwnMenu || ancestors == null)
        {
            return false;
        }

        var inPanel = false;
        foreach (var kind in ancestors)
        {
            switch (kind)
            {
                case OverflowAncestorKind.ToolBarPanel:
                    inPanel = true;
                    break;
                case OverflowAncestorKind.ToolBar:
                    return false;
                case OverflowAncestorKind.PopupChild:
                    return inPanel;
            }
        }

        return false;
    }
}
