#if __ANDROID__
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using CodeBrix.Android.UI.Handlers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using AColorStateList = global::Android.Content.Res.ColorStateList;
using AResource = global::Android.Resource;

namespace CodeBrix.Android.UI.Policy;

/// <summary>
/// Theme-key lookups for the policy appliers: which keys an APP defines (its application dictionary, its merged
/// dictionaries, the resources of the element and its ancestors - never the framework's theme dictionaries), and
/// state colour lists built from a family's keys.
/// </summary>
internal static class ThemeKeys
{
    private static readonly ConditionalWeakTable<ResourceDictionary, OwnKeys> _ownKeys = new();

    /// <summary>
    /// True when the app defines <paramref name="key"/> itself (in scope of <paramref name="element"/>): the key is
    /// re-keyed, so the policy honors it on the native widget; a key only the framework defines leaves the native
    /// widget as its handler made it (Material defaults, D-P14).
    /// </summary>
    /// <param name="element">The element whose scope is searched (may be null: application scope only).</param>
    /// <param name="key">The key.</param>
    /// <returns>True for an app key.</returns>
    internal static bool IsAppKey(DependencyObject element, string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return false;
        }

        for (var current = element; current != null; current = Parent(current))
        {
            if (current is FrameworkElement { Resources: { Count: > 0 } resources } && Defines(resources, key, 0))
            {
                return true;
            }
        }

        return Application.Current?.Resources is { } application && Defines(application, key, 0);
    }

    /// <summary>True when any of <paramref name="keys"/> is an app key.</summary>
    /// <param name="element">The element.</param>
    /// <param name="keys">The keys.</param>
    /// <returns>True when the app re-keyed at least one.</returns>
    internal static bool AnyAppKey(DependencyObject element, IEnumerable<string> keys)
    {
        foreach (var key in keys)
        {
            if (IsAppKey(element, key))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The colour of a key from <paramref name="element"/>'s scope, or <paramref name="fallback"/>.</summary>
    /// <param name="element">The element.</param>
    /// <param name="key">The key.</param>
    /// <param name="fallback">The colour when the key does not resolve.</param>
    /// <returns>The ARGB colour.</returns>
    internal static int Color(DependencyObject element, string key, int fallback) => ThemeResources.FindColor(element, key) ?? fallback;

    /// <summary>
    /// A colour state list from a family's per-state keys: disabled, pressed, hovered, focused and normal (and
    /// the checked variants when <paramref name="checkedStates"/> is given), each state falling back to Normal.
    /// </summary>
    /// <param name="element">The element whose scope is searched.</param>
    /// <param name="normalKey">The key of the Normal state.</param>
    /// <param name="fallback">The Normal colour when that key does not resolve.</param>
    /// <param name="pointerOverKey">The PointerOver key (or null).</param>
    /// <param name="pressedKey">The Pressed key (or null).</param>
    /// <param name="disabledKey">The Disabled key (or null).</param>
    /// <param name="focusedKey">The Focused key (or null).</param>
    /// <param name="checkedStates">The checked variants (normal, pointer-over, pressed, disabled keys), or null.</param>
    /// <returns>The list.</returns>
    internal static AColorStateList States(
        DependencyObject element,
        string normalKey,
        int fallback,
        string pointerOverKey = null,
        string pressedKey = null,
        string disabledKey = null,
        string focusedKey = null,
        (string Normal, string PointerOver, string Pressed, string Disabled)? checkedStates = null)
    {
        var normal = Color(element, normalKey, fallback);
        int Of(string key, int otherwise) => key != null ? Color(element, key, otherwise) : otherwise;

        var enabled = AResource.Attribute.StateEnabled;
        var pressed = AResource.Attribute.StatePressed;
        var hovered = AResource.Attribute.StateHovered;
        var focused = AResource.Attribute.StateFocused;
        var @checked = AResource.Attribute.StateChecked;
        var states = new List<int[]>();
        var colors = new List<int>();
        void Add(int color, params int[] state)
        {
            states.Add(state);
            colors.Add(color);
        }

        if (checkedStates is { } on)
        {
            var onNormal = Of(on.Normal, normal);
            Add(Of(on.Disabled, Of(disabledKey, onNormal)), -enabled, @checked);
            Add(Of(disabledKey, normal), -enabled);
            Add(Of(on.Pressed, onNormal), @checked, pressed);
            Add(Of(on.PointerOver, onNormal), @checked, hovered);
            Add(onNormal, @checked);
        }
        else
        {
            Add(Of(disabledKey, normal), -enabled);
        }

        Add(Of(pressedKey, normal), pressed);
        if (focusedKey != null)
        {
            Add(Of(focusedKey, normal), focused);
        }

        Add(Of(pointerOverKey, normal), hovered);
        Add(normal);
        return new AColorStateList(states.ToArray(), colors.ToArray());
    }

    /// <summary>The single colour of a brush, or null.</summary>
    /// <param name="brush">The brush.</param>
    /// <returns>The ARGB colour.</returns>
    internal static int? ColorOf(Brush brush) => ThemeResources.ColorOf(brush);

    private static bool Defines(ResourceDictionary dictionary, string key, int depth)
    {
        if (dictionary == null || depth > 8 || ThemeRefresh.IsFrameworkDictionary(dictionary))
        {
            return false;
        }

        if (OwnKeysOf(dictionary).Contains(key))
        {
            return true;
        }

        foreach (var merged in dictionary.MergedDictionaries)
        {
            if (Defines(merged, key, depth + 1))
            {
                return true;
            }
        }

        return false;
    }

    private static HashSet<string> OwnKeysOf(ResourceDictionary dictionary)
    {
        if (_ownKeys.TryGetValue(dictionary, out var cached) && cached.Count == dictionary.Count)
        {
            return cached.Keys;
        }

        var keys = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            foreach (var pair in dictionary)
            {
                if (pair.Key is string text)
                {
                    keys.Add(text);
                }
            }
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // A dictionary that cannot be enumerated defines nothing the policy can see.
            _ = exception;
        }

        _ownKeys.AddOrUpdate(dictionary, new OwnKeys(keys, dictionary.Count));
        return keys;
    }

    private static DependencyObject Parent(DependencyObject current) =>
        current is FrameworkElement { Parent: { } logical } ? logical : VisualTreeHelper.GetParent(current);

    private sealed record OwnKeys(HashSet<string> Keys, int Count);
}
#endif
