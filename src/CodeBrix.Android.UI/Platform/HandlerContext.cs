using CodeBrix.Android.UI.Hosting;
using CodeBrix.Platform.UI.Hosting;
using Microsoft.UI.Xaml;
using AContext = global::Android.Content.Context;

namespace CodeBrix.Android.UI.Platform;

/// <summary>
/// The Android context and the density an element's native view is created and laid out with.
/// Density is PER XAMLROOT (plan D-P10): the element's XamlRoot.RasterizationScale, which the
/// window wrapper sets from its activity's display metrics - never a process-wide static, so
/// windows on displays of different densities each get their own.
/// </summary>
internal static class HandlerContext
{
    /// <summary>The activity showing the element's window, else the current activity, else the application.</summary>
    /// <param name="element">The element (may be null).</param>
    /// <returns>A context to create native views with.</returns>
    internal static AContext For(UIElement element)
    {
        if (element?.XamlRoot is { } root && XamlRootMap.GetHostForRoot(root) is AndroidXamlRootHost { Wrapper.Activity: { } activity })
        {
            return activity;
        }

        return (AContext)ActivityRegistry.Current ?? global::Android.App.Application.Context;
    }

    /// <summary>Physical pixels per DIP for the element: its XamlRoot's scale, else the default display's.</summary>
    /// <param name="element">The element (may be null).</param>
    /// <returns>The density (always positive).</returns>
    internal static double Density(UIElement element)
    {
        var scale = element?.XamlRoot?.RasterizationScale ?? 0;
        if (scale > 0)
        {
            return scale;
        }

        var metrics = ActivityRegistry.Current?.Resources?.DisplayMetrics
            ?? global::Android.App.Application.Context?.Resources?.DisplayMetrics;
        return metrics?.Density > 0 ? metrics.Density : 1.0;
    }
}
