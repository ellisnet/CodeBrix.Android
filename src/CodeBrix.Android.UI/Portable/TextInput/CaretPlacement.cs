using System;

namespace CodeBrix.Android.UI.Portable.TextInput;

/// <summary>
/// [AP8-S item L] Where the soft-keyboard session's focus view (CoreTextInputView) sits while a CUSTOM text control
/// (TerminalView, AdvancedTextEdit) has the keyboard: on the control's caret, so that Android's adjustPan - which brings
/// the FOCUSED view's rectangle above the keyboard - brings the caret into view (a native EditText gets the same from
/// its own focused rectangle, its caret line). Plain arithmetic, host-free: the caret and the part of the control that
/// can show it, both in the window content's DIPs (Core's XamlRoot coordinates), become one rectangle in the focus
/// layer's physical pixels.
/// </summary>
/// <remarks>
/// Android pans only to a focused rectangle that lies inside the focused view's own visible bounds, so the view is laid
/// out ON the caret (its size is the caret's) rather than reporting a rectangle away from itself.
/// </remarks>
internal static class CaretPlacement
{
    /// <summary>
    /// The pixel rectangle of the focus view for a caret, or false when the caret is not in the visible part of its
    /// control (scrolled out, empty): the view then goes back to its parked 1x1 place at the layer's origin.
    /// </summary>
    /// <param name="caret">The caret (x, y, width, height) in DIPs; a zero width (a thin caret) counts as one pixel wide.</param>
    /// <param name="visible">The part of the control that can show the caret, in DIPs (the caret is clipped to it).</param>
    /// <param name="density">Physical pixels per DIP (greater than 0).</param>
    /// <param name="originX">The window content's origin in the focus layer, x, in pixels.</param>
    /// <param name="originY">The window content's origin in the focus layer, y, in pixels.</param>
    /// <param name="rect">The rectangle, in the focus layer's pixels (at least 1 x 1).</param>
    /// <returns>True when the caret has a place.</returns>
    internal static bool TryPlace(
        (double X, double Y, double Width, double Height) caret,
        (double X, double Y, double Width, double Height) visible,
        double density,
        int originX,
        int originY,
        out CaretPixels rect)
    {
        rect = default;
        if (!(density > 0) || !IsFinite(caret) || !IsFinite(visible) || caret.Height <= 0 || visible.Width <= 0 || visible.Height <= 0)
        {
            return false;
        }

        // Clip the caret to the visible part of its control; a caret on the right edge (width 0 there) still counts.
        var left = Math.Max(caret.X, visible.X);
        var top = Math.Max(caret.Y, visible.Y);
        var right = Math.Min(caret.X + Math.Max(caret.Width, 0), visible.X + visible.Width);
        var bottom = Math.Min(caret.Y + caret.Height, visible.Y + visible.Height);
        if (right < left || bottom <= top)
        {
            return false;
        }

        var pixelLeft = (int)Math.Floor(left * density);
        var pixelTop = (int)Math.Floor(top * density);
        var pixelRight = Math.Max((int)Math.Ceiling(right * density), pixelLeft + 1);
        var pixelBottom = Math.Max((int)Math.Ceiling(bottom * density), pixelTop + 1);
        rect = new CaretPixels(originX + pixelLeft, originY + pixelTop, pixelRight - pixelLeft, pixelBottom - pixelTop);
        return true;
    }

    private static bool IsFinite((double X, double Y, double Width, double Height) r) =>
        double.IsFinite(r.X) && double.IsFinite(r.Y) && double.IsFinite(r.Width) && double.IsFinite(r.Height);
}

/// <summary>A rectangle in physical pixels (left, top, width, height).</summary>
/// <param name="Left">The left edge.</param>
/// <param name="Top">The top edge.</param>
/// <param name="Width">The width (at least 1).</param>
/// <param name="Height">The height (at least 1).</param>
internal readonly record struct CaretPixels(int Left, int Top, int Width, int Height);
