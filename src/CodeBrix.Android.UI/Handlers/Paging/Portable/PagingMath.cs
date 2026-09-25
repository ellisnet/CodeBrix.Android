using System;
using System.Collections.Generic;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>What a native PagerControl shows between its buttons.</summary>
internal enum PagerSelectorKind
{
    /// <summary>A drop-down list of the pages (DisplayMode ComboBox, or Auto with fewer than ten pages).</summary>
    DropDown,

    /// <summary>A number field (DisplayMode NumberBox, or Auto with ten pages or more / an unknown count).</summary>
    NumberField,

    /// <summary>A row of page-number buttons with ellipses (DisplayMode ButtonPanel).</summary>
    ButtonPanel,
}

/// <summary>
/// AP10-A: the pure arithmetic of the native paging controls (tsv rows PipsPager, PagerControl): which pips a
/// PipsPager shows (at most MaxVisiblePips, the selected one kept centred as WinUI scrolls its pips), which
/// selector a PagerControl shows for its DisplayMode (Core's Auto rule: a number box for ten pages or more or an
/// unknown count, else a combo box), which page numbers its button panel lists, and the step of a previous / next
/// button with or without wrapping.
/// </summary>
internal static class PagingMath
{
    /// <summary>The ellipsis entry of a button panel.</summary>
    internal const int Ellipsis = -1;

    /// <summary>The first pip shown and how many pips are shown.</summary>
    /// <param name="numberOfPages">NumberOfPages (negative or zero: none).</param>
    /// <param name="maxVisible">MaxVisiblePips.</param>
    /// <param name="selected">SelectedPageIndex.</param>
    /// <returns>The first index and the count.</returns>
    internal static (int First, int Count) PipWindow(int numberOfPages, int maxVisible, int selected)
    {
        var pages = Math.Max(0, numberOfPages);
        var count = Math.Min(pages, Math.Max(1, maxVisible));
        if (pages == 0)
        {
            return (0, 0);
        }

        var first = Math.Clamp(selected - (count / 2), 0, pages - count);
        return (first, count);
    }

    /// <summary>The page a previous (-1) or next (+1) button goes to, or null when it cannot move.</summary>
    /// <param name="numberOfPages">NumberOfPages.</param>
    /// <param name="selected">SelectedPageIndex.</param>
    /// <param name="step">-1 or +1.</param>
    /// <param name="wrap">True when the pager wraps around.</param>
    /// <returns>The page, or null.</returns>
    internal static int? Step(int numberOfPages, int selected, int step, bool wrap)
    {
        if (numberOfPages == 0)
        {
            return null;
        }

        var target = selected + step;
        if (numberOfPages < 0)
        {
            return target < 0 ? null : target;
        }

        if (target >= 0 && target < numberOfPages)
        {
            return target;
        }

        return wrap ? (target + numberOfPages) % numberOfPages : null;
    }

    /// <summary>The selector a PagerControl shows.</summary>
    /// <param name="displayMode">The DisplayMode as its name (Auto, ComboBox, NumberBox, ButtonPanel).</param>
    /// <param name="numberOfPages">NumberOfPages (-1 = unknown).</param>
    /// <returns>The selector kind.</returns>
    internal static PagerSelectorKind Selector(string displayMode, int numberOfPages) => displayMode switch
    {
        "ComboBox" => PagerSelectorKind.DropDown,
        "NumberBox" => PagerSelectorKind.NumberField,
        "ButtonPanel" => PagerSelectorKind.ButtonPanel,
        _ => numberOfPages < 0 || numberOfPages >= 10 ? PagerSelectorKind.NumberField : PagerSelectorKind.DropDown,
    };

    /// <summary>The page indexes a button panel lists (<see cref="Ellipsis"/> for a gap): all pages up to seven, else
    /// the first, the last and the selected page with its neighbours.</summary>
    /// <param name="numberOfPages">NumberOfPages.</param>
    /// <param name="selected">SelectedPageIndex.</param>
    /// <returns>The entries.</returns>
    internal static IReadOnlyList<int> PanelNumbers(int numberOfPages, int selected)
    {
        var list = new List<int>();
        if (numberOfPages <= 0)
        {
            return list;
        }

        if (numberOfPages <= 7)
        {
            for (var i = 0; i < numberOfPages; i++)
            {
                list.Add(i);
            }

            return list;
        }

        var last = numberOfPages - 1;
        if (selected < 4)
        {
            for (var i = 0; i <= 4; i++)
            {
                list.Add(i);
            }

            list.Add(Ellipsis);
            list.Add(last);
        }
        else if (selected > last - 4)
        {
            list.Add(0);
            list.Add(Ellipsis);
            for (var i = last - 4; i <= last; i++)
            {
                list.Add(i);
            }
        }
        else
        {
            list.Add(0);
            list.Add(Ellipsis);
            list.Add(selected - 1);
            list.Add(selected);
            list.Add(selected + 1);
            list.Add(Ellipsis);
            list.Add(last);
        }

        return list;
    }
}
