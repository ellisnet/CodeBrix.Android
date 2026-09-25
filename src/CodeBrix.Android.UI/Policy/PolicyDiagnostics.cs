using CodeBrix.Android.UI.Handlers;
using Microsoft.UI.Xaml;

namespace CodeBrix.Android.UI.Policy;

/// <summary>
/// Diagnostics of the presentation-policy layer for tools and tests outside this assembly (the element's handler
/// is internal to the Core seam).
/// </summary>
internal static class PolicyDiagnostics
{
    /// <summary>The CodeBrix.Android handler of a live element, or null.</summary>
    /// <param name="element">The element.</param>
    /// <returns>The handler.</returns>
    internal static IAndroidElementHandler HandlerOf(UIElement element) => element?.Handler as IAndroidElementHandler;
}
