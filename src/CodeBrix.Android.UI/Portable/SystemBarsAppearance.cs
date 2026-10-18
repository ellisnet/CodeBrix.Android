using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CodeBrix.Android.UI.Portable;

/// <summary>
/// The decisions behind the system bars' icon colour (status bar and navigation bar): the bars use dark icons
/// ("appearance light") when the theme the window's content shows is Light. The ActualTheme of the content's top element
/// wins, because an app that switches theme on an element (FrameworkElement.RequestedTheme) never changes
/// Application.RequestedTheme; the application's theme is the fallback while the window has no content yet.
/// </summary>
internal static class SystemBarsAppearance
{
    /// <summary>
    /// The element whose ActualTheme the system bars follow: the window's content, descended through single-content hosts
    /// (a Frame or ContentControl to its content, a Page or UserControl to its content, a Border to its child) to the top
    /// element of the page - where apps set RequestedTheme (on the root element, or on the page's root grid). Its
    /// ActualTheme also changes when an ancestor's RequestedTheme changes, so watching it covers every level above it.
    /// </summary>
    /// <param name="windowContent">Window.Content (null while the window has none).</param>
    /// <returns>The theme source, or null when the content is not a FrameworkElement.</returns>
    internal static FrameworkElement FindThemeSource(UIElement windowContent)
    {
        var current = windowContent as FrameworkElement;
        for (var depth = 0; current != null && depth < 32; depth++)
        {
            var next = current switch
            {
                UserControl userControl => userControl.Content as FrameworkElement,
                ContentControl contentControl => contentControl.Content as FrameworkElement,
                Border border => border.Child as FrameworkElement,
                _ => null,
            };
            if (next == null)
            {
                break;
            }

            current = next;
        }

        return current;
    }

    /// <summary>Decides whether the system bars draw dark icons for a light background.</summary>
    /// <param name="hasContent">The XAML window has a root FrameworkElement as its content.</param>
    /// <param name="contentActualTheme">That content's ActualTheme (ignored when <paramref name="hasContent"/> is false).</param>
    /// <param name="applicationTheme">Application.RequestedTheme.</param>
    /// <returns>True for light bars (dark icons), false for dark bars (light icons).</returns>
    internal static bool UseLightBars(bool hasContent, ElementTheme contentActualTheme, ApplicationTheme applicationTheme)
    {
        if (hasContent && contentActualTheme != ElementTheme.Default)
        {
            return contentActualTheme == ElementTheme.Light;
        }

        return applicationTheme == ApplicationTheme.Light;
    }
}
