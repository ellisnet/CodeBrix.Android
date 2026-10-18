using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>
/// Lets go of the platform views that retained handlers (<see cref="CodeBrix.Platform.UI.Contracts.ElementHandlerCapabilities.RetainedAcrossLeave"/>)
/// keep for elements that will not enter a live tree again - a list row the recycled-view pool dropped: every element
/// of the subtree that keeps a disconnected handler has the handler's kept view released
/// (<see cref="ElementHandler.ReleaseRetained"/>) and Core forgets the handler (UIElement.ReleaseRetainedHandler), so
/// the next Enter would create a new handler and view.
/// </summary>
internal static class RetainedHandlers
{
    /// <summary>Releases the retained handlers of <paramref name="root"/> and its visual descendants.</summary>
    /// <param name="root">The root of the dropped subtree (it is out of the live tree).</param>
    /// <returns>How many retained handlers were released.</returns>
    internal static int ReleaseSubtree(UIElement root)
    {
        if (root == null)
        {
            return 0;
        }

        var released = 0;
        var pending = new Stack<DependencyObject>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var node = pending.Pop();
            if (node is UIElement element && element.HasRetainedHandler)
            {
                if (element.Handler is ElementHandler handler)
                {
                    handler.ReleaseRetained();
                }

                if (element.ReleaseRetainedHandler())
                {
                    released++;
                }
            }

            var count = VisualTreeHelper.GetChildrenCount(node);
            for (var i = 0; i < count; i++)
            {
                if (VisualTreeHelper.GetChild(node, i) is { } child)
                {
                    pending.Push(child);
                }
            }
        }

        return released;
    }
}
