using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>
/// The native Expander's rules as pure math (host-testable): when an Expander can be shown natively (a text
/// header opening downwards; anything else keeps the Fluent template), the header text, and the layout - a
/// header row of at least <see cref="MinHeaderHeight"/> DIPs on top and, while expanded, the content box
/// under it (the control's BorderThickness and Padding around the content, aligned by the content alignments,
/// as the Fluent template's content presenter does).
/// </summary>
internal static class ExpanderLayout
{
    /// <summary>The header row's minimum height (Fluent ExpanderMinHeight; Material's minimum touch target).</summary>
    internal const double MinHeaderHeight = 48;

    /// <summary>
    /// True when the native Expander can show this header: no header template (or selector), a header that is
    /// not an element (text, or any object shown by its ToString), and ExpandDirection Down.
    /// </summary>
    /// <param name="header">The Header.</param>
    /// <param name="headerTemplate">The HeaderTemplate.</param>
    /// <param name="headerTemplateSelector">The HeaderTemplateSelector.</param>
    /// <param name="direction">The ExpandDirection.</param>
    /// <returns>True for the native Expander.</returns>
    internal static bool CanMapNatively(object header, DataTemplate headerTemplate, DataTemplateSelector headerTemplateSelector, ExpandDirection direction) =>
        header is not UIElement && headerTemplate == null && headerTemplateSelector == null && direction == ExpandDirection.Down;

    /// <summary>The text the header row shows.</summary>
    /// <param name="header">The Header.</param>
    /// <returns>The text ("" for no header).</returns>
    internal static string HeaderText(object header) => header switch
    {
        null => string.Empty,
        string text => text,
        var other => other.ToString() ?? string.Empty,
    };

    /// <summary>The header row's height: its native text height, at least <see cref="MinHeaderHeight"/>.</summary>
    /// <param name="nativeHeight">The header row's measured height in DIPs.</param>
    /// <returns>The height in DIPs.</returns>
    internal static double HeaderHeight(double nativeHeight) =>
        double.IsNaN(nativeHeight) ? MinHeaderHeight : Math.Max(MinHeaderHeight, nativeHeight);

    /// <summary>The size the content is measured with: the available width inside the box, any height.</summary>
    /// <param name="available">The Expander's available size (DIPs).</param>
    /// <param name="inner">BorderThickness + Padding of the content box.</param>
    /// <returns>The content's available size.</returns>
    internal static Size ContentAvailable(Size available, Thickness inner) => new(
        double.IsInfinity(available.Width) ? double.PositiveInfinity : Math.Max(0, available.Width - inner.Left - inner.Right),
        double.PositiveInfinity);

    /// <summary>The Expander's desired size.</summary>
    /// <param name="available">The available size (DIPs).</param>
    /// <param name="headerWidth">The header row's natural width (DIPs).</param>
    /// <param name="headerHeight">The header row's height (DIPs).</param>
    /// <param name="expanded">True while expanded.</param>
    /// <param name="contentDesired">The content's desired size (ignored while collapsed).</param>
    /// <param name="inner">BorderThickness + Padding of the content box.</param>
    /// <returns>The desired size.</returns>
    internal static Size Desired(Size available, double headerWidth, double headerHeight, bool expanded, Size contentDesired, Thickness inner)
    {
        var width = Math.Max(0, headerWidth);
        var height = headerHeight;
        if (expanded)
        {
            width = Math.Max(width, contentDesired.Width + inner.Left + inner.Right);
            height += contentDesired.Height + inner.Top + inner.Bottom;
        }

        if (!double.IsInfinity(available.Width))
        {
            width = Math.Min(width, available.Width);
        }

        return new Size(width, height);
    }

    /// <summary>
    /// The content's rectangle in an Expander arranged at <paramref name="finalSize"/>: in the box under the
    /// header, inside BorderThickness + Padding, aligned by the content alignments; zero height while collapsed.
    /// </summary>
    /// <param name="finalSize">The Expander's arranged size.</param>
    /// <param name="headerHeight">The header row's height.</param>
    /// <param name="expanded">True while expanded.</param>
    /// <param name="contentDesired">The content's desired size.</param>
    /// <param name="inner">BorderThickness + Padding of the content box.</param>
    /// <param name="horizontal">HorizontalContentAlignment.</param>
    /// <param name="vertical">VerticalContentAlignment.</param>
    /// <returns>The content rectangle relative to the Expander.</returns>
    internal static Rect ContentRect(Size finalSize, double headerHeight, bool expanded, Size contentDesired, Thickness inner,
        HorizontalAlignment horizontal, VerticalAlignment vertical)
    {
        var boxHeight = Math.Max(0, finalSize.Height - headerHeight);
        var slotWidth = Math.Max(0, finalSize.Width - inner.Left - inner.Right);
        var slotHeight = expanded ? Math.Max(0, boxHeight - inner.Top - inner.Bottom) : 0;
        var width = horizontal == HorizontalAlignment.Stretch ? slotWidth : Math.Min(slotWidth, Math.Max(0, contentDesired.Width));
        var x = inner.Left + horizontal switch
        {
            HorizontalAlignment.Center => (slotWidth - width) / 2,
            HorizontalAlignment.Right => slotWidth - width,
            _ => 0,
        };
        var height = vertical == VerticalAlignment.Stretch ? slotHeight : Math.Min(slotHeight, Math.Max(0, contentDesired.Height));
        var y = headerHeight + inner.Top + vertical switch
        {
            VerticalAlignment.Center => (slotHeight - height) / 2,
            VerticalAlignment.Bottom => slotHeight - height,
            _ => 0,
        };
        return new Rect(x, y, width, height);
    }

    /// <summary>True when a point (relative to the Expander) is on its header row.</summary>
    /// <param name="point">The point in DIPs.</param>
    /// <param name="width">The Expander's width.</param>
    /// <param name="headerHeight">The header row's height.</param>
    /// <returns>True on the header.</returns>
    internal static bool IsOnHeader(Point point, double width, double headerHeight) =>
        point.X >= 0 && point.Y >= 0 && point.X < width && point.Y < headerHeight;
}
