// Ported from CodeBrix.Platform (Apache License 2.0; THIRD-PARTY-NOTICES.txt item 3): src/AddIns/Platform.UI.VideoPlayer.Skia/Internal/VideoSurfaceElement.skia.cs
// (branch platform-split @ ea87ba019bba176b3e7b5b297471c0baf0ef0bbc; the working tree the pinned build 1.0.271.248 was packed from).
// Verbatim: its visual comes from the SkiaSharp.Views add-in's canvas-host factory, shown by that add-in's canvas-element handler (registered by this assembly's bootstrap).

#nullable enable

using System;
using Windows.Foundation;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using CodeBrix.Platform.Foundation.Extensibility;
using CodeBrix.Platform.UI.Graphics;
using SkiaSharp;

namespace CodeBrix.Platform.UI.VideoPlayer.Skia.Internal;

/// <summary>
/// The element the <see cref="VideoPlayer"/> puts its picture on. It paints the most recently
/// presented video frame straight into the Skia scene, so the video composites like any other XAML
/// content - clipping, transforms and z-order all behave normally and there is no native airspace
/// involved. The XAML <see cref="Stretch"/> mode is applied here, at paint time.
/// </summary>
/// <remarks>
/// <para>
/// The paint callback runs on whatever thread the running head renders on, which on several heads
/// is NOT the user-interface thread (the X11 head, for instance, renders from a timer callback on
/// the thread pool). Nothing here may therefore touch the playback presenter, whose surface belongs
/// to one thread: the driver hands this element a finished, immutable picture and this element only
/// blits it, under the same gate that swaps it. That is also why both render paths - graphics and
/// processor - go through one readback: it is what makes the picture safe to hand across threads.
/// </para>
/// <para>
/// <b>Resize behaviour.</b> A resize drag raises <c>SizeChanged</c> continuously. While it does,
/// live presents are suppressed and the last picture is simply re-blitted, letterboxed, at the new
/// size; live presenting resumes once the size has been quiet for
/// <see cref="ResizeSettleMilliseconds"/>. Without that, a backlog of full-size blits piles up on
/// the render thread and the window "goes chunky, then catches up".
/// </para>
/// </remarks>
internal sealed class VideoSurfaceElement : FrameworkElement
{
	/// <summary>Quiet period after the last size change before live presenting resumes.</summary>
	private const double ResizeSettleMilliseconds = 500;

	private readonly Compositor _compositor;
	private readonly object _frameGate = new();
	private readonly DispatcherTimer _resizeSettleTimer;

	private SKCanvasVisualBase? _canvasVisual;
	private SKImage? _frame;
	private Stretch _stretch = Stretch.Uniform;
	private bool _isResizing;

	/// <param name="compositor">
	/// The shared compositor, obtained from an existing visual (the hosting VideoPlayer's) rather
	/// than Compositor.GetSharedCompositor, which is internal to the Composition assembly - this
	/// AddIn only holds InternalsVisibleTo grants from Platform.UI and Platform.UWP.
	/// </param>
	public VideoSurfaceElement(Compositor compositor)
	{
		_compositor = compositor;

		_resizeSettleTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ResizeSettleMilliseconds) };
		_resizeSettleTimer.Tick += OnResizeSettled;
		SizeChanged += OnSizeChanged;
	}

	private protected override ContainerVisual CreateElementVisual()
	{
		if (ApiExtensibility.CreateInstance<SKCanvasVisualBaseFactory>(this, out var factory))
		{
			return _canvasVisual = factory.CreateInstance((o, size) => PaintFrame((SKCanvas)o, size), _compositor);
		}

		throw new InvalidOperationException(
			$"Failed to create an instance of {nameof(SKCanvasVisualBase)} - the video player requires a Skia composition target.");
	}

	internal override bool IsViewHit() => true;

	/// <summary>
	/// The stretch mode to apply when painting frames. Set from the UI thread by the owning
	/// element; takes effect on the next repaint.
	/// </summary>
	internal Stretch Stretch
	{
		get => _stretch;
		set
		{
			_stretch = value;
			_canvasVisual?.Invalidate();
		}
	}

	/// <summary>True while the element is being resized and live presents are being suppressed.</summary>
	internal bool IsResizing => _isResizing;

	/// <summary>
	/// Publishes a new video frame and requests a repaint. Called on the UI thread by the render
	/// driver; ownership of <paramref name="frame"/> transfers to this element (the previous frame
	/// is disposed).
	/// </summary>
	/// <param name="frame">The picture to show from now on.</param>
	internal void PresentFrame(SKImage frame)
	{
		SKImage? previous;
		lock (_frameGate)
		{
			previous = _frame;
			_frame = frame;
		}
		previous?.Dispose();

		// A resize re-blits the picture it already has at the new size; pushing live frames while
		// the drag runs only piles work onto the render thread.
		if (!_isResizing)
		{
			_canvasVisual?.Invalidate();
		}
	}

	/// <summary>
	/// Drops the current frame (when the source changes, say), so a stale picture is not left on
	/// screen.
	/// </summary>
	internal void ClearFrame()
	{
		SKImage? previous;
		lock (_frameGate)
		{
			previous = _frame;
			_frame = null;
		}
		previous?.Dispose();
		_canvasVisual?.Invalidate();
	}

	/// <summary>Requests a repaint of whatever picture is current.</summary>
	internal void Invalidate() => _canvasVisual?.Invalidate();

	/// <summary>
	/// Returns an independent copy of the picture on screen, which the caller owns and must
	/// dispose, or null when nothing has been presented yet.
	/// </summary>
	/// <remarks>
	/// Taken under the same gate the present path uses, so it is safe from any thread and can never
	/// read a frame that is being replaced. This is the screenshot hook.
	/// </remarks>
	internal SKImage? CapturePresentedFrame()
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

	/// <summary>Releases the picture this element is holding.</summary>
	internal void Teardown()
	{
		SizeChanged -= OnSizeChanged;
		_resizeSettleTimer.Stop();
		_resizeSettleTimer.Tick -= OnResizeSettled;
		ClearFrame();
	}

	private void OnSizeChanged(object sender, SizeChangedEventArgs e)
	{
		_isResizing = true;
		_resizeSettleTimer.Stop();
		_resizeSettleTimer.Start();
		_canvasVisual?.Invalidate();
	}

	private void OnResizeSettled(object? sender, object e)
	{
		_resizeSettleTimer.Stop();
		_isResizing = false;
		_canvasVisual?.Invalidate();
	}

	private void PaintFrame(SKCanvas canvas, Size area)
	{
		lock (_frameGate)
		{
			// Keep the reference alive while drawing: disposal only happens in PresentFrame and
			// ClearFrame under this same gate, so drawing inside the lock is the simple, safe
			// option (the draw is a fast blit of an already-rasterized image).
			if (_frame is { } frame)
			{
				var dest = ComputeDestinationRect(frame.Width, frame.Height, area, _stretch);
				canvas.Save();
				canvas.ClipRect(new SKRect(0, 0, (float)area.Width, (float)area.Height));
				canvas.Clear(SKColors.Black);
				canvas.DrawImage(frame, dest, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None));
				canvas.Restore();
				return;
			}
		}

		// No frame yet (nothing opened, or nothing decoded so far): leave the area transparent, so
		// whatever the application put behind the player shows through until the first picture.
		canvas.Clear(SKColors.Transparent);
	}

	/// <summary>
	/// Works out where a picture of <paramref name="frameWidth"/> x <paramref name="frameHeight"/>
	/// goes inside <paramref name="area"/> under <paramref name="stretch"/>, centred, with the
	/// letterboxing the mode implies.
	/// </summary>
	/// <param name="frameWidth">The picture's width in pixels.</param>
	/// <param name="frameHeight">The picture's height in pixels.</param>
	/// <param name="area">The area available to draw into.</param>
	/// <param name="stretch">How to fit the picture into that area.</param>
	/// <returns>The rectangle the picture should be drawn into.</returns>
	/// <remarks>
	/// A pure function, internal so the test suite can hold it to the same four behaviours the
	/// MediaPlayer host has: None keeps the pixel size, Fill takes the whole area, Uniform fits
	/// inside it and UniformToFill covers it (the caller clips).
	/// </remarks>
	internal static SKRect ComputeDestinationRect(int frameWidth, int frameHeight, Size area, Stretch stretch)
	{
		var areaWidth = (float)area.Width;
		var areaHeight = (float)area.Height;

		if (frameWidth <= 0 || frameHeight <= 0)
		{
			return new SKRect(0, 0, areaWidth, areaHeight);
		}

		float scaleX = areaWidth / frameWidth;
		float scaleY = areaHeight / frameHeight;

		var (width, height) = stretch switch
		{
			Stretch.None => ((float)frameWidth, (float)frameHeight),
			Stretch.Fill => (areaWidth, areaHeight),
			Stretch.UniformToFill => Scaled(Math.Max(scaleX, scaleY)),
			_ => Scaled(Math.Min(scaleX, scaleY)), // Stretch.Uniform (the default)
		};

		var left = (areaWidth - width) / 2;
		var top = (areaHeight - height) / 2;
		return new SKRect(left, top, left + width, top + height);

		(float, float) Scaled(float scale) => (frameWidth * scale, frameHeight * scale);
	}
}
