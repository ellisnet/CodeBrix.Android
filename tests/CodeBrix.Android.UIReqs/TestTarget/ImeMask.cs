using System;
using System.Text.Json.Nodes;

namespace CodeBrix.Android.UIReqs.TestTarget;

/// <summary>
/// [AP7-B TerminalView RE-GATE 2] The soft keyboard (the IME) is system UI, not the product: its content (Gboard's
/// suggestion strip, language label, learned words, recent-clip chip) is not deterministic, so a saved frame with the
/// keyboard up could never compare strictly. When the device app asks for a capture while the IME is visible it sends the
/// IME rectangle in SCREEN pixels; the host fills that rectangle with ONE flat colour in the copy it SAVES (frame files,
/// scenario archives). The pixels sent back to the device - which the scenarios assert on, and which the keyboard
/// warm-up compares - are never masked. The saved frame thus still records THAT the keyboard was up and WHERE, but
/// nothing of what it showed: a frame compare inside the masked rectangle is meaningless.
/// </summary>
public static class ImeMask
{
    /// <summary>
    /// The mask colour: an opaque mid grey (#808080). The frame compare never sees keyboard content, only this; no
    /// scenario asserts on the saved copy (assertions run on the device's unmasked pixels).
    /// </summary>
    public static readonly (byte R, byte G, byte B, byte A) Colour = (0x80, 0x80, 0x80, 0xFF);

    /// <summary>
    /// [AP8-S batch 0] How many pixel rows ABOVE the device-reported IME rectangle are masked too (8), across the
    /// rectangle's full width. The keyboard draws a 1-3 px top SHADOW just above its own inset: ime() reports the
    /// keyboard's window area only, so the shadow row fell outside the mask and was two-state between runs (AP7-B CLOSE:
    /// Landscape TerminalView/TerminalInput, one row at y = top - 1, max 2/255). 8 px covers the shadow with margin and is
    /// far smaller than anything a keyboard scenario asserts on (the assertions run on the device's unmasked pixels anyway).
    /// </summary>
    public const int ShadowBand = 8;

    /// <summary>
    /// Reads the IME rectangle a capture request carries (<c>"ime": [left, top, right, bottom]</c>, screen pixels,
    /// right/bottom exclusive), or null when the request has none (keyboard not visible, or a warm-up capture).
    /// </summary>
    /// <param name="request">The device's capture request.</param>
    /// <returns>The rectangle, or null.</returns>
    public static (int Left, int Top, int Right, int Bottom)? ReadRect(JsonObject request)
    {
        if (request?["ime"] is not JsonArray { Count: 4 } a)
        {
            return null;
        }

        return ((int)a[0]!, (int)a[1]!, (int)a[2]!, (int)a[3]!);
    }

    /// <summary>
    /// Returns a copy of <paramref name="frame"/> with <paramref name="rect"/> (screen pixels = frame pixels: the frame is
    /// the screencap cropped from the top-left corner), widened upwards by <see cref="ShadowBand"/> rows (the keyboard's
    /// top shadow), clipped to the frame and filled with <see cref="Colour"/>; the frame itself when the clipped
    /// rectangle is empty. The input frame's pixels are never changed.
    /// </summary>
    /// <param name="frame">The captured frame.</param>
    /// <param name="rect">The IME rectangle as the device reports it (right/bottom exclusive).</param>
    /// <param name="applied">The rectangle actually filled (widened by the shadow band, clipped), or null when nothing was filled.</param>
    /// <returns>The masked copy, or <paramref name="frame"/>.</returns>
    public static TestFrame Apply(TestFrame frame, (int Left, int Top, int Right, int Bottom) rect, out (int Left, int Top, int Right, int Bottom)? applied)
    {
        ArgumentNullException.ThrowIfNull(frame);
        var left = Math.Max(0, rect.Left);
        var top = Math.Max(0, rect.Top - ShadowBand);
        var right = Math.Min(frame.Width, rect.Right);
        var bottom = Math.Min(frame.Height, rect.Bottom);
        if (right <= left || bottom <= top)
        {
            applied = null;
            return frame;
        }

        var rgba = (byte[])frame.Rgba.Clone();
        for (var y = top; y < bottom; y++)
        {
            var i = ((y * frame.Width) + left) * 4;
            for (var x = left; x < right; x++, i += 4)
            {
                rgba[i] = Colour.R;
                rgba[i + 1] = Colour.G;
                rgba[i + 2] = Colour.B;
                rgba[i + 3] = Colour.A;
            }
        }

        applied = (left, top, right, bottom);
        return new TestFrame(rgba, frame.Width, frame.Height, frame.Sequence);
    }
}
