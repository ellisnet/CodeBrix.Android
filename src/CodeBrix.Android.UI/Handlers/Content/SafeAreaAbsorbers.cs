using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>A handler whose element absorbs the window's safe-area insets (Page, plan D-P10).</summary>
internal interface ISafeAreaAbsorber : IAndroidElementHandler
{
    /// <summary>
    /// Recomputes the insets from the element's final position (after a layout pass, or when the window's
    /// safe area changed) and invalidates the element's measure when they changed.
    /// </summary>
    void RevalidateSafeArea();
}

/// <summary>
/// The live safe-area absorbers. An element's window position is only final once the whole layout pass
/// is over (a parent is arranged after its children in Core), so the absorbers re-check their insets
/// after every layout tick of their XamlRoot; a change costs one more layout pass.
/// </summary>
internal static class SafeAreaAbsorbers
{
    private static readonly List<WeakReference<ISafeAreaAbsorber>> _absorbers = new();

    /// <summary>Tracks a connected absorber.</summary>
    internal static void Add(ISafeAreaAbsorber absorber)
    {
        Remove(absorber);
        _absorbers.Add(new WeakReference<ISafeAreaAbsorber>(absorber));
    }

    /// <summary>Forgets an absorber (and every collected one).</summary>
    internal static void Remove(ISafeAreaAbsorber absorber) =>
        _absorbers.RemoveAll(w => !w.TryGetTarget(out var live) || ReferenceEquals(live, absorber));

    /// <summary>Called after each Core layout tick of <paramref name="xamlRoot"/>.</summary>
    internal static void Revalidate(XamlRoot xamlRoot)
    {
        if (_absorbers.Count == 0)
        {
            return;
        }

        foreach (var weak in _absorbers.ToArray())
        {
            if (weak.TryGetTarget(out var absorber) && absorber.Element is { } element && ReferenceEquals(element.XamlRoot, xamlRoot))
            {
                absorber.RevalidateSafeArea();
            }
        }
    }
}
