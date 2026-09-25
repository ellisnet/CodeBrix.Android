using System;
using CodeBrix.Android.UI.Portable.Drawing;
using CodeBrix.Platform.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>
/// Looks up theme resources the way a control's template would (plan 2.11, D-O4 "honor"): a
/// lightweight-styling key (ButtonBackgroundPointerOver, CheckBoxCheckBackgroundFillChecked, ...)
/// resolves from the element's own resources up through its ancestors, then the application's
/// resources (merged and theme dictionaries included), then Core's top-level and system resources.
/// An app that re-keys a brush therefore changes the native widget, exactly as it would change the
/// Fluent template; with no re-key the Fluent value the Core theme holds applies (the AP5 theme
/// bridge decides what that value is).
/// </summary>
internal static class ThemeResources
{
    /// <summary>Finds a resource by key as seen from <paramref name="element"/>.</summary>
    /// <param name="element">The element whose resource scope is searched (may be null: application scope only).</param>
    /// <param name="key">The resource key.</param>
    /// <param name="value">The resource found.</param>
    /// <returns>True when the key resolves.</returns>
    internal static bool TryFind(DependencyObject element, string key, out object value)
    {
        value = null;
        if (string.IsNullOrEmpty(key))
        {
            return false;
        }

        for (var current = element; current != null; current = Parent(current))
        {
            if (current is FrameworkElement { Resources: { Count: > 0 } resources } && TryGet(resources, key, out value))
            {
                return true;
            }
        }

        if (Application.Current?.Resources is { } appResources && TryGet(appResources, key, out value))
        {
            return true;
        }

        try
        {
            value = ResourceResolver.ResolveResourceStatic(key, typeof(object), null);
            return value != null;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            value = null;
            return false;
        }
    }

    /// <summary>The brush a key resolves to from <paramref name="element"/>, or null.</summary>
    /// <param name="element">The element.</param>
    /// <param name="key">The resource key.</param>
    /// <returns>The brush, or null.</returns>
    internal static Brush FindBrush(DependencyObject element, string key) =>
        TryFind(element, key, out var value) ? value as Brush : null;

    /// <summary>The single ARGB colour a key's brush shows, or null when the key does not resolve to a brush.</summary>
    /// <param name="element">The element.</param>
    /// <param name="key">The resource key.</param>
    /// <returns>The colour, or null.</returns>
    internal static int? FindColor(DependencyObject element, string key)
    {
        if (!TryFind(element, key, out var value))
        {
            return null;
        }

        return value switch
        {
            Brush brush => BrushPaint.SingleColor(brush, 0),
            global::Windows.UI.Color color => (color.A << 24) | (color.R << 16) | (color.G << 8) | color.B,
            _ => null,
        };
    }

    /// <summary>The single ARGB colour of a brush (null for no brush).</summary>
    /// <param name="brush">The brush.</param>
    /// <returns>The colour, or null.</returns>
    internal static int? ColorOf(Brush brush) => brush == null ? null : BrushPaint.SingleColor(brush, 0);

    /// <summary>True when the element's value of <paramref name="property"/> was set locally (by the app, not a style).</summary>
    /// <param name="element">The element.</param>
    /// <param name="property">The property.</param>
    /// <returns>True for a local value.</returns>
    internal static bool IsLocal(DependencyObject element, DependencyProperty property) =>
        element != null && element.ReadLocalValue(property) != DependencyProperty.UnsetValue;

    private static bool TryGet(ResourceDictionary dictionary, string key, out object value)
    {
        try
        {
            return dictionary.TryGetValue(key, out value) && value != null;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            value = null;
            return false;
        }
    }

    private static DependencyObject Parent(DependencyObject current)
    {
        if (current is FrameworkElement { Parent: { } logical })
        {
            return logical;
        }

        return VisualTreeHelper.GetParent(current);
    }
}
