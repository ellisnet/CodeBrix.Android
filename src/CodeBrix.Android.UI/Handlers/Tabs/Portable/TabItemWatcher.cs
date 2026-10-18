using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>
/// Watches the tab items a native tab strip shows for the properties the strip draws (TabViewItem.Header and
/// IsClosable): a renamed tab, or an app that marks a tab dirty with a "*" in its header, refreshes the strip. The set
/// of watched items follows <see cref="Sync"/> (each refresh of the strip); items no longer shown are released, and
/// <see cref="Clear"/> releases every registration (the handler's disconnect).
/// </summary>
internal sealed class TabItemWatcher
{
    private readonly Action _changed;
    private readonly Dictionary<TabViewItem, (long Header, long IsClosable)> _watched = new(ReferenceEqualityComparer.Instance);

    /// <summary>Creates a watcher.</summary>
    /// <param name="changed">Called (on the UI thread) when a watched property of a watched item changed.</param>
    /// <exception cref="ArgumentNullException"><paramref name="changed"/> is null.</exception>
    internal TabItemWatcher(Action changed)
    {
        ArgumentNullException.ThrowIfNull(changed);
        _changed = changed;
    }

    /// <summary>How many items are watched (diagnostics, tests).</summary>
    internal int Count => _watched.Count;

    /// <summary>Watches exactly <paramref name="items"/> (null entries are skipped).</summary>
    /// <param name="items">The tab items the strip shows now.</param>
    internal void Sync(IEnumerable<TabViewItem> items)
    {
        var current = new HashSet<TabViewItem>(ReferenceEqualityComparer.Instance);
        if (items != null)
        {
            foreach (var item in items)
            {
                if (item != null)
                {
                    current.Add(item);
                }
            }
        }

        foreach (var item in new List<TabViewItem>(_watched.Keys))
        {
            if (!current.Contains(item))
            {
                Release(item);
            }
        }

        foreach (var item in current)
        {
            if (!_watched.ContainsKey(item))
            {
                _watched[item] = (
                    item.RegisterPropertyChangedCallback(TabViewItem.HeaderProperty, OnChanged),
                    item.RegisterPropertyChangedCallback(TabViewItem.IsClosableProperty, OnChanged));
            }
        }
    }

    /// <summary>Releases every registration.</summary>
    internal void Clear()
    {
        foreach (var item in new List<TabViewItem>(_watched.Keys))
        {
            Release(item);
        }
    }

    private void Release(TabViewItem item)
    {
        if (_watched.Remove(item, out var tokens))
        {
            item.UnregisterPropertyChangedCallback(TabViewItem.HeaderProperty, tokens.Header);
            item.UnregisterPropertyChangedCallback(TabViewItem.IsClosableProperty, tokens.IsClosable);
        }
    }

    private void OnChanged(DependencyObject sender, DependencyProperty property) => _changed();
}
