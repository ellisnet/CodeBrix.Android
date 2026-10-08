using CodeBrix.Android.UI.Handlers;
using Microsoft.UI.Xaml;
using AView = global::Android.Views.View;

namespace CodeBrix.Android.UI.Diagnostics;

/// <summary>
/// Finds the native view an element handler shows an element with (diagnostics and the UIReqs
/// geometry audit: Core rectangle vs native view rectangle).
/// </summary>
internal static class NativeViewLocator
{
    /// <summary>The element's native view, or null when it has no handler view.</summary>
    internal static AView ViewOf(UIElement element) => (element?.Handler as IViewHandler)?.NativeView;

    /// <summary>AP9-3: the view that carries the element's automation name (its handler's accessibility view), or null.</summary>
    internal static AView AccessibilityViewOf(UIElement element) => (element?.Handler as IViewHandler)?.AccessibilityView;

    /// <summary>The element's rectangle from Core's last arrange (relative to its visual parent, DIPs), or null.</summary>
    internal static Windows.Foundation.Rect? ArrangedRectOf(UIElement element) =>
        element?.Handler is IAndroidElementHandler { HasArranged: true } handler ? handler.ArrangedRect : null;
}
