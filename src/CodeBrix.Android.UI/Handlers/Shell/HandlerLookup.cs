using Microsoft.UI.Xaml;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>AP10-A: the handler Core attached to an element, typed (tests and diagnostics).</summary>
internal static class HandlerLookup
{
    /// <summary>The element's handler as <typeparamref name="THandler"/>, or null.</summary>
    /// <typeparam name="THandler">The handler type.</typeparam>
    /// <param name="element">The element.</param>
    /// <returns>The handler, or null.</returns>
    internal static THandler Of<THandler>(UIElement element)
        where THandler : class => element?.Handler as THandler;
}
