using System;
using Microsoft.UI.Xaml;
using Windows.Foundation;

namespace CodeBrix.Android.UI.Portable.Layout;

/// <summary>
/// The layout of a content host (a ContentControl, UserControl, Page or Frame whose handler hosts the
/// content directly, HostsContent): what its template's presenter would have done - the content is
/// measured inside the control's border, padding and absorbed insets, and arranged in that inner
/// rectangle, sized and placed by the content alignments (Stretch fills). In DIPs.
/// </summary>
internal static class ContentHostMath
{
    /// <summary>The inner edges (border + padding + insets), per edge.</summary>
    internal static Thickness Inner(Thickness border, Thickness padding, SafeAreaPadding insets) => new(
        border.Left + padding.Left + insets.Left,
        border.Top + padding.Top + insets.Top,
        border.Right + padding.Right + insets.Right,
        border.Bottom + padding.Bottom + insets.Bottom);

    /// <summary>The size the content is offered: <paramref name="available"/> minus the inner edges (infinity stays infinity).</summary>
    internal static Size Deflate(Size available, Thickness inner) => new(
        double.IsInfinity(available.Width) ? available.Width : Math.Max(0, available.Width - inner.Left - inner.Right),
        double.IsInfinity(available.Height) ? available.Height : Math.Max(0, available.Height - inner.Top - inner.Bottom));

    /// <summary>The host's desired size: the content's desired size plus the inner edges.</summary>
    internal static Size Inflate(Size contentDesired, Thickness inner) => new(
        Math.Max(0, contentDesired.Width) + inner.Left + inner.Right,
        Math.Max(0, contentDesired.Height) + inner.Top + inner.Bottom);

    /// <summary>
    /// The content's arrange rectangle inside a host of <paramref name="finalSize"/>: the inner rectangle,
    /// narrowed to the content's desired size and aligned when the alignment is not Stretch.
    /// </summary>
    internal static Rect ArrangeRect(
        Size finalSize, Thickness inner, Size contentDesired,
        HorizontalAlignment horizontal, VerticalAlignment vertical)
    {
        var slotWidth = Math.Max(0, finalSize.Width - inner.Left - inner.Right);
        var slotHeight = Math.Max(0, finalSize.Height - inner.Top - inner.Bottom);
        var x = inner.Left;
        var y = inner.Top;
        var width = slotWidth;
        var height = slotHeight;

        if (horizontal != HorizontalAlignment.Stretch)
        {
            width = Math.Min(slotWidth, Math.Max(0, contentDesired.Width));
            x += horizontal switch
            {
                HorizontalAlignment.Center => (slotWidth - width) / 2,
                HorizontalAlignment.Right => slotWidth - width,
                _ => 0,
            };
        }

        if (vertical != VerticalAlignment.Stretch)
        {
            height = Math.Min(slotHeight, Math.Max(0, contentDesired.Height));
            y += vertical switch
            {
                VerticalAlignment.Center => (slotHeight - height) / 2,
                VerticalAlignment.Bottom => slotHeight - height,
                _ => 0,
            };
        }

        return new Rect(x, y, width, height);
    }
}
