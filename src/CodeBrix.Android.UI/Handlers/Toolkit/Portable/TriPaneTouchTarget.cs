using System;
using Windows.Foundation;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>The touch target of a TriPaneView divider (pure C#: host-free tests).</summary>
internal static class TriPaneTouchTarget
{
    /// <summary>
    /// The divider's bounds grown ACROSS its axis to at least <paramref name="minimum"/> DIPs, centred on the divider
    /// (a vertical side divider grows left and right, a horizontal stack divider up and down); along its axis it keeps
    /// its length. A divider already thicker than the minimum keeps its bounds.
    /// </summary>
    /// <param name="bounds">The divider's bounds (DIPs).</param>
    /// <param name="isVertical">True for the side divider (dragged left and right).</param>
    /// <param name="minimum">The minimum touch size in DIPs (Material: 48).</param>
    /// <returns>The touch target.</returns>
    internal static Rect Inflate(Rect bounds, bool isVertical, double minimum)
    {
        if (bounds.IsEmpty)
        {
            return bounds;
        }

        if (isVertical)
        {
            var width = Math.Max(bounds.Width, minimum);
            return new Rect(bounds.X - ((width - bounds.Width) / 2), bounds.Y, width, bounds.Height);
        }

        var height = Math.Max(bounds.Height, minimum);
        return new Rect(bounds.X, bounds.Y - ((height - bounds.Height) / 2), bounds.Width, height);
    }
}
