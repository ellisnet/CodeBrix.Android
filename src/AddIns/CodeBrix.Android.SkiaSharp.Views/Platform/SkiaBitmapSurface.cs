using System;
using SkiaSharp;
using ABitmap = global::Android.Graphics.Bitmap;
using ACanvas = global::Android.Graphics.Canvas;

namespace CodeBrix.Android.SkiaSharp.Views.Platform;

/// <summary>
/// A software Skia surface shared with an android.graphics.Bitmap: Skia paints straight into the
/// bitmap's pixels (ARGB_8888 is RGBA byte order, premultiplied), and the bitmap is drawn on the
/// view's canvas. The bitmap is KEPT between paints of the same size, so a paint that does not clear
/// draws over the previous frame (the SKXamlCanvas contract); a new size gets a new, transparent one.
/// </summary>
internal sealed class SkiaBitmapSurface : IDisposable
{
    private ABitmap _bitmap;

    /// <summary>The buffer (null when there is none).</summary>
    internal ABitmap Bitmap => _bitmap;

    /// <summary>The pixel width of the current buffer (0 when there is none).</summary>
    internal int Width => _bitmap?.Width ?? 0;

    /// <summary>The pixel height of the current buffer (0 when there is none).</summary>
    internal int Height => _bitmap?.Height ?? 0;

    /// <summary>The image info of a buffer of the given size.</summary>
    /// <param name="width">Width in pixels.</param>
    /// <param name="height">Height in pixels.</param>
    /// <returns>The info (RGBA 8888, premultiplied).</returns>
    internal static SKImageInfo InfoFor(int width, int height) => new(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);

    /// <summary>Makes the buffer <paramref name="width"/> x <paramref name="height"/> pixels.</summary>
    /// <param name="width">Width in pixels (positive).</param>
    /// <param name="height">Height in pixels (positive).</param>
    /// <param name="recycleOld">
    /// False when another view may still show the old buffer (it is then left to the garbage collector instead
    /// of being recycled under that view).
    /// </param>
    /// <returns>True when a new (transparent) buffer was created.</returns>
    internal bool EnsureSize(int width, int height, bool recycleOld = true)
    {
        if (_bitmap != null && _bitmap.Width == width && _bitmap.Height == height)
        {
            return false;
        }

        if (recycleOld)
        {
            Free();
        }
        _bitmap = ABitmap.CreateBitmap(width, height, ABitmap.Config.Argb8888);
        return true;
    }

    /// <summary>Runs <paramref name="paint"/> on a Skia surface over the buffer's pixels.</summary>
    /// <param name="paint">The paint (the surface is valid only during the call).</param>
    internal void Paint(Action<SKSurface, SKImageInfo> paint)
    {
        if (_bitmap == null)
        {
            return;
        }

        var info = InfoFor(_bitmap.Width, _bitmap.Height);
        var pixels = _bitmap.LockPixels();
        try
        {
            using var surface = SKSurface.Create(info, pixels, info.RowBytes);
            if (surface == null)
            {
                return;
            }

            paint(surface, info);
            surface.Flush();
        }
        finally
        {
            // Unlocking also bumps the bitmap's generation, so a hardware canvas uploads the new pixels.
            _bitmap.UnlockPixels();
        }
    }

    /// <summary>Draws the buffer at the canvas origin.</summary>
    /// <param name="canvas">The view's canvas.</param>
    internal void DrawOn(ACanvas canvas)
    {
        if (_bitmap != null)
        {
            canvas.DrawBitmap(_bitmap, 0f, 0f, null);
        }
    }

    /// <summary>Releases the buffer.</summary>
    internal void Free()
    {
        _bitmap?.Recycle();
        _bitmap?.Dispose();
        _bitmap = null;
    }

    /// <inheritdoc />
    public void Dispose() => Free();
}
