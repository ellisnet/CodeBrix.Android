// Ported from CodeBrix.Platform (Apache License 2.0; THIRD-PARTY-NOTICES.txt item 3): src/AddIns/Platform.UI.WebGpu/Internal/WebGpuFrameSurface.skia.cs
// (branch main @ a26d07f74c1396007bd3e3480872456538e3876b; the working tree the pinned build 1.0.287.1311 was packed from).
// Changes are marked ANDROID PORT: (1) no direct-frame source (the desktop direct paths are not ported): Present/ClearFrame/CaptureFrame/Paint hold only the read-back picture;
// (2) a remarks paragraph on where the paint runs on Android; (3) the DirectSurface path's frames (PresentSurfaceFrame: counted, held as "a frame is shown", the overlay repainted above them; no picture to capture).

#nullable enable

using System;
using Windows.Foundation;
using Microsoft.UI.Xaml.Media;
using SkiaSharp;

namespace CodeBrix.Platform.UI.WebGpu.Internal;

/// <summary>
/// The paint side of the view: holds the most recently presented frame under a gate and draws it - with the view's
/// <see cref="Stretch"/>, then the overlay - when the head's canvas visual asks for a paint.
/// </summary>
/// <remarks>
/// <para>
/// The paint callback runs when the head paints the window: on the desktop heads that is the user-interface thread
/// RECORDING the window's frame into a picture, which the head's render thread (on X11 a thread-pool timer callback)
/// replays later - so whatever the paint draws may be drawn again after this class has been handed a newer frame - and
/// the frame that produced the picture may have run on a third thread (the Manual driver). Nothing here may therefore touch WebGPU objects:
/// the frame hands this class a finished, immutable picture and this class only blits it, under the same gate that
/// swaps it (the VideoPlayer surface's rule, for the same reason). The overlay is called inside that paint, so it is
/// bound by the same rule.
/// </para>
/// <para>
/// ANDROID PORT (2): on Android the paint runs inside the SkiaSharp.Views add-in's native Skia view's draw pass, on the
/// main (user-interface) thread, drawing straight into that view's own pixel buffer - nothing is recorded and replayed
/// later. The read-back pictures keep their reference-counted buffers anyway (the presenter is the Platform's).
/// </para>
/// <para>
/// ANDROID PORT (3): on the DirectSurface path the frames never come here - they are presented to a window surface UNDER
/// the native view, which the operating system composites below it. This class then only counts them and paints the
/// overlay (over a transparent canvas) above the surface.
/// </para>
/// <para>
/// <b>Resize behaviour.</b> While the element's size keeps changing, presents do not ask for repaints (the size
/// change itself does); live repainting resumes once the size has been quiet for the settle period - without that, a
/// backlog of full-size blits piles up on the render thread during a drag.
/// </para>
/// </remarks>
internal sealed class WebGpuFrameSurface
{
	private static readonly SKSamplingOptions Sampling = new(SKFilterMode.Linear, SKMipmapMode.None);

	private readonly object _frameGate = new();
	private readonly Action _requestRepaint;
	private readonly Action<SKCanvas, SKSize>? _overlay;

	private SKImage? _frame;
	private bool _hasSurfaceFrame; // ANDROID PORT (3)
	private volatile bool _isResizing;
	private long _framesPresented;

	/// <summary>Creates the surface.</summary>
	/// <param name="requestRepaint">Asks the head for a repaint; callable from any thread (the view marshals it).</param>
	/// <param name="overlay">Draws the overlay over the frame on the paint thread, or null.</param>
	internal WebGpuFrameSurface(Action requestRepaint, Action<SKCanvas, SKSize>? overlay)
	{
		_requestRepaint = requestRepaint ?? throw new ArgumentNullException(nameof(requestRepaint));
		_overlay = overlay;
	}

	/// <summary>How the frame fits the area. Read on the paint thread; written on the user-interface thread.</summary>
	internal Stretch Stretch { get; set; } = Stretch.Fill;

	/// <summary>True while the element is being resized and presents do not ask for repaints.</summary>
	internal bool IsResizing
	{
		get => _isResizing;
		set => _isResizing = value;
	}

	/// <summary>How many frames have been presented since the surface was created.</summary>
	internal long FramesPresented => System.Threading.Interlocked.Read(ref _framesPresented);

	/// <summary>Whether a picture is being held - or, ANDROID PORT (3), a frame was presented to the window surface.</summary>
	internal bool HasFrame
	{
		get
		{
			lock (_frameGate)
			{
				return _frame is not null || _hasSurfaceFrame;
			}
		}
	}

	/// <summary>
	/// ANDROID PORT (3): a frame was presented to the DirectSurface path's window surface. Counts it and, when
	/// <paramref name="repaintOverlay"/> (an overlay is drawn), asks for a repaint so the overlay follows the frame. Any
	/// thread.
	/// </summary>
	/// <param name="repaintOverlay">Whether the overlay above the surface must be repainted.</param>
	internal void PresentSurfaceFrame(bool repaintOverlay)
	{
		SKImage? previous;
		lock (_frameGate)
		{
			// A read-back picture from before a path change must not cover the surface.
			previous = _frame;
			_frame = null;
			_hasSurfaceFrame = true;
		}

		previous?.Dispose();
		System.Threading.Interlocked.Increment(ref _framesPresented);
		if ((repaintOverlay || previous is not null) && !_isResizing)
		{
			_requestRepaint();
		}
	}

	/// <summary>
	/// Publishes a new picture and asks for a repaint. Any thread; ownership of <paramref name="frame"/> transfers
	/// here (the previous picture is disposed).
	/// </summary>
	/// <param name="frame">The picture to show from now on.</param>
	internal void Present(SKImage frame)
	{
		SKImage? previous;
		lock (_frameGate)
		{
			previous = _frame;
			_frame = frame;
		}

		previous?.Dispose();
		System.Threading.Interlocked.Increment(ref _framesPresented);

		if (!_isResizing)
		{
			_requestRepaint();
		}
	}

	/// <summary>
	/// Drops the picture (device lost, view unloaded), so nothing stale is left on screen - and, more importantly, so
	/// no wrapper of read-back pixels outlives the bitmaps it points into. Any thread.
	/// </summary>
	internal void ClearFrame()
	{
		SKImage? previous;
		lock (_frameGate)
		{
			previous = _frame;
			_frame = null;
			_hasSurfaceFrame = false;
		}

		previous?.Dispose();
		_requestRepaint();
	}

	/// <summary>
	/// Returns an independent copy of the picture being shown (the caller owns it), or null when there is none. Safe
	/// from any thread: taken under the gate the present path uses. ANDROID PORT (3): null on the DirectSurface path,
	/// whose frames never exist in processor memory (read the window surface itself, e.g. with PixelCopy).
	/// </summary>
	/// <returns>The copy, or null.</returns>
	internal SKImage? CaptureFrame()
	{
		lock (_frameGate)
		{
			if (_frame is not { } frame)
			{
				return null;
			}

			var info = new SKImageInfo(frame.Width, frame.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
			var copy = new SKBitmap(info);
			if (!frame.ReadPixels(info, copy.GetPixels(), copy.RowBytes, 0, 0))
			{
				copy.Dispose();
				return null;
			}

			copy.SetImmutable();
			return SKImage.FromBitmap(copy);
		}
	}

	/// <summary>
	/// Paints the picture (or nothing, when there is none, so what is behind the view shows through), then the overlay.
	/// Runs on the head's paint thread.
	/// </summary>
	/// <param name="canvas">The canvas, clipped to the area and scaled to the rasterization scale by the head.</param>
	/// <param name="area">The area in effective pixels.</param>
	internal void Paint(SKCanvas canvas, Size area)
	{
		var width = (float)area.Width;
		var height = (float)area.Height;
		var save = canvas.Save();
		try
		{
			canvas.ClipRect(new SKRect(0, 0, width, height));

			lock (_frameGate)
			{
				// Drawn inside the lock: disposal only happens in Present and ClearFrame under this same gate, and
				// the draw is a fast blit of an already-rasterized picture. Never a Clear: the head's canvas is shared
				// with everything painted behind the view, and a clear would erase it. The frame is composited over
				// it (source-over), so a transparent ClearColor shows what is behind, as does no frame at all.
				// ANDROID PORT (1): only the read-back picture (no direct-frame source).
				var image = _frame;
				if (image is not null)
				{
					canvas.DrawImage(image, ComputeDestinationRect(image.Width, image.Height, area, Stretch), Sampling);
				}
			}

			_overlay?.Invoke(canvas, new SKSize(width, height));
		}
		finally
		{
			canvas.RestoreToCount(save);
		}
	}

	/// <summary>
	/// Where a picture of <paramref name="frameWidth"/> x <paramref name="frameHeight"/> pixels goes inside
	/// <paramref name="area"/> under <paramref name="stretch"/>, centred - the VideoPlayer surface's arithmetic: None
	/// draws one pixel per area unit, Fill takes the whole area, Uniform fits inside it and UniformToFill covers it
	/// (the caller clips).
	/// </summary>
	/// <param name="frameWidth">The picture's width in pixels.</param>
	/// <param name="frameHeight">The picture's height in pixels.</param>
	/// <param name="area">The area available.</param>
	/// <param name="stretch">How to fit.</param>
	/// <returns>The destination rectangle.</returns>
	internal static SKRect ComputeDestinationRect(int frameWidth, int frameHeight, Size area, Stretch stretch)
	{
		var areaWidth = (float)area.Width;
		var areaHeight = (float)area.Height;

		if (frameWidth <= 0 || frameHeight <= 0)
		{
			return new SKRect(0, 0, areaWidth, areaHeight);
		}

		var scaleX = areaWidth / frameWidth;
		var scaleY = areaHeight / frameHeight;

		var (width, height) = stretch switch
		{
			Stretch.None => ((float)frameWidth, (float)frameHeight),
			Stretch.Uniform => Scaled(Math.Min(scaleX, scaleY)),
			Stretch.UniformToFill => Scaled(Math.Max(scaleX, scaleY)),
			_ => (areaWidth, areaHeight), // Stretch.Fill (the default: the target IS the element's size)
		};

		var left = (areaWidth - width) / 2;
		var top = (areaHeight - height) / 2;
		return new SKRect(left, top, left + width, top + height);

		(float, float) Scaled(float scale) => (frameWidth * scale, frameHeight * scale);
	}
}
