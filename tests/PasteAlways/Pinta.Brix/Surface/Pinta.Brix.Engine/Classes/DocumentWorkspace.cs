//
// DocumentWorkspace.cs
//
// Author:
//       Jonathan Pobst <monkey@jpobst.com>
//
// Copyright (c) 2010 Jonathan Pobst
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Collections.Immutable;

//was previously: namespace Pinta.Core;
namespace Pinta.Brix.Engine;

// Pinta.Brix note: upstream reached into GTK viewport adjustments and the
// zoom combo box for all zoom/scroll math. In the port the workspace itself
// owns the zoom scale (raising ZoomChanged for the UI to mirror), and talks
// to the canvas through the ICanvasView / ICanvasScrollView interfaces the
// UI layer's canvas control implements.
public sealed class DocumentWorkspace
{
	private readonly Document document;

	private double scale = 1.0;

	private enum ZoomType
	{
		ZoomIn,
		ZoomOut,
	}

	/// <summary>
	/// Zoom presets, in percent, matching the upstream zoom list (descending).
	/// </summary>
	public static ImmutableArray<double> ZoomPresets { get; } =
		[3600, 2400, 1600, 1200, 800, 700, 600, 500, 400, 300, 200, 175, 150, 125, 100, 66, 50, 33, 25, 16, 12, 8, 5, 2, 1];

	internal DocumentWorkspace (Document document)
	{
		this.document = document;

		History = new DocumentHistory (document);
	}

	#region Public Events
	public event EventHandler<CanvasInvalidatedEventArgs>? CanvasInvalidated;
	public event EventHandler? ViewSizeChanged;
	public event EventHandler? ZoomChanged;
	#endregion

	#region Public Properties
	/// <summary>The canvas control displaying this document; set by the UI layer soon after creation.</summary>
	public ICanvasView? Canvas { get; set; }

	/// <summary>The scrollable view hosting the canvas; set by the UI layer soon after creation.</summary>
	public ICanvasScrollView? CanvasWindow { get; set; }

	/// <summary>
	/// Returns whether the zoomed image fits in the window without requiring scrolling.
	/// </summary>
	public bool ImageViewFitsInWindow {
		get {
			Size viewport = CanvasWindow?.ViewportSize ?? new Size (0, 0);
			return ViewSize.Width <= viewport.Width && ViewSize.Height <= viewport.Height;
		}
	}

	/// <summary>
	/// Size of the zoomed image.
	/// </summary>
	public Size ViewSize {
		get => view_size;
		set {
			if (view_size == value) return;
			view_size = value;
			OnViewSizeChanged ();
		}
	}
	private Size view_size;

	public DocumentHistory History { get; }

	/// <summary>
	/// Returns whether the image (at 100% zoom) would fit in the window without requiring scrolling.
	/// </summary>
	public bool ImageFitsInWindow {
		get {
			Size viewport = CanvasWindow?.ViewportSize ?? new Size (0, 0);
			return document.ImageSize.Width <= viewport.Width && document.ImageSize.Height <= viewport.Height;
		}
	}

	/// <summary>
	/// Scale factor for the zoomed image.
	/// </summary>
	public double Scale {
		get => scale;
		set {
			if (value == scale && ViewSize == GetNewViewSize (document.ImageSize, scale))
				return;

			scale = value;
			document.ImageSize = CoercedToPositive (document.ImageSize);
			ViewSize = GetNewViewSize (document.ImageSize, scale);

			Invalidate ();
			ZoomChanged?.Invoke (this, EventArgs.Empty);
		}
	}

	/// <summary>
	/// Ensures that the size has a width and a height of at least 1
	/// </summary>
	private static Size CoercedToPositive (Size baseSize)
		=> new (
			Width: Math.Max (baseSize.Width, 1),
			Height: Math.Max (baseSize.Height, 1));

	private static Size GetNewViewSize (Size imageSize, double scale)
	{
		int new_x = Math.Max ((int) (imageSize.Width * scale), 1);
		int new_y = Math.Max ((int) ((long) new_x * imageSize.Height / imageSize.Width), 1);
		return new (new_x, new_y);
	}

	#endregion

	#region Public Methods

	/// <summary>
	/// Re-derives the view size from the current scale, e.g. after the image
	/// was resized. (Upstream re-imposed the zoom combo's value here.)
	/// </summary>
	public void UpdateCanvasScale ()
	{
		document.ImageSize = CoercedToPositive (document.ImageSize);
		ViewSize = GetNewViewSize (document.ImageSize, scale);
		Invalidate ();
	}

	public void Invalidate ()
	{
		OnCanvasInvalidated (new CanvasInvalidatedEventArgs ());
	}

	/// <summary>
	/// Repaints a rectangle region on the canvas.
	/// </summary>
	/// <param name='canvasRect'>
	/// The rectangle region of the canvas requiring repainting
	/// </param>
	public void Invalidate (RectangleI canvasRect)
	{
		OnCanvasInvalidated (new CanvasInvalidatedEventArgs (canvasRect));
	}

	/// <summary>
	/// Repaints a rectangle region in the window.
	/// Note that this overload uses window coordinates, whereas Invalidate() uses canvas coordinates.
	/// </summary>
	public void InvalidateWindowRect (RectangleI windowRect)
	{
		PointD windowTopLeft = new (windowRect.Left, windowRect.Top);
		PointD windowBtmRight = new (windowRect.Right + 1, windowRect.Bottom + 1);

		PointD canvasTopLeft = ViewPointToCanvas (windowTopLeft);
		PointD canvasBtmRight = ViewPointToCanvas (windowBtmRight);

		RectangleI canvasRect = RectangleD.FromPoints (canvasTopLeft, canvasBtmRight).ToInt ();
		OnCanvasInvalidated (new CanvasInvalidatedEventArgs (canvasRect));
	}

	/// <summary>
	/// Grabs focus to the canvas widget. This can be used to avoid leaving focus in
	/// toolbar widgets, for example.
	/// </summary>
	public void GrabFocusToCanvas ()
	{
		bool gained_focus = CanvasWindow?.GrabFocus () ?? false;
		// Log a warning if something went wrong, e.g. there is a non-focusable widget
		// in the hierarchy.
		if (!gained_focus)
			Console.Error.WriteLine ("Failed to gain focus on the canvas widget!");
	}

	/// <summary>
	/// Determines whether the rectangle lies (at least partially) outside the canvas area.
	/// </summary>
	public bool IsPartiallyOffscreen (RectangleI rect)
		=> rect.IsEmpty || rect.Left < 0 || rect.Top < 0;

	public bool PointInCanvas (PointD point)
	{
		if (point.X < 0 || point.Y < 0)
			return false;

		if (point.X >= document.ImageSize.Width || point.Y >= document.ImageSize.Height)
			return false;

		return true;
	}

	public void RecenterView (PointD point)
	{
		if (CanvasWindow is not { } view)
			return;

		Size viewport = view.ViewportSize;
		view.ScrollOffset = new PointD (
			Math.Clamp (point.X * Scale - viewport.Width / 2.0, 0, Math.Max (0, ViewSize.Width - viewport.Width)),
			Math.Clamp (point.Y * Scale - viewport.Height / 2.0, 0, Math.Max (0, ViewSize.Height - viewport.Height)));
	}

	public void ScrollCanvas (PointI delta)
	{
		if (CanvasWindow is not { } view)
			return;

		Size viewport = view.ViewportSize;
		PointD offset = view.ScrollOffset;
		view.ScrollOffset = new PointD (
			Math.Clamp (delta.X + offset.X, 0, Math.Max (0, ViewSize.Width - viewport.Width)),
			Math.Clamp (delta.Y + offset.Y, 0, Math.Max (0, ViewSize.Height - viewport.Height)));
	}

	/// <summary>
	/// Converts a point from image view coordinates to canvas coordinates
	/// </summary>
	/// <param name='viewPoint'>
	/// The view point
	/// </param>
	public PointD ViewPointToCanvas (PointD viewPoint)
	{
		Fraction<int> sf = ScaleFactor.CreateClamped (document.ImageSize.Width, ViewSize.Width);
		PointD pt = sf.ScalePoint (viewPoint);
		return new (pt.X, pt.Y);
	}

	/// <summary>
	/// Converts a point from canvas coordinates to view coordinates
	/// </summary>
	public PointD CanvasPointToView (PointD canvasPoint)
	{
		Fraction<int> sf = ScaleFactor.CreateClamped (document.ImageSize.Width, ViewSize.Width);
		return sf.UnscalePoint (canvasPoint);
	}

	public void ZoomIn ()
	{
		ZoomAroundCenter (ZoomType.ZoomIn);
	}

	public void ZoomOut ()
	{
		ZoomAroundCenter (ZoomType.ZoomOut);
	}

	public void ZoomInAroundViewPoint (in PointD view_point)
	{
		ZoomAndRecenterView (ZoomType.ZoomIn, view_point); // Zoom in relative to mouse position.
	}

	public void ZoomInAroundCanvasPoint (in PointD canvas_point)
	{
		ZoomInAroundViewPoint (CanvasPointToView (canvas_point));
	}

	public void ZoomOutAroundViewPoint (in PointD view_point)
	{
		ZoomAndRecenterView (ZoomType.ZoomOut, view_point); // Zoom out relative to mouse position.
	}

	public void ZoomOutAroundCanvasPoint (in PointD canvas_point)
	{
		ZoomOutAroundViewPoint (CanvasPointToView (canvas_point));
	}

	/// <summary>
	/// Applies a zoom level chosen by the user (e.g. typed into the zoom
	/// combo), as a scale multiplier (1.0 = 100%).
	/// </summary>
	public void ZoomManually (double newScale)
	{
		ZoomToScaleAroundViewCenter (newScale);
	}

	public void ZoomToCanvasRectangle (RectangleD rect)
	{
		double ratio =
			(document.ImageSize.Width / rect.Width <= document.ImageSize.Height / rect.Height)
			? document.ImageSize.Width / rect.Width
			: document.ImageSize.Height / rect.Height;

		Scale = ratio;
		CanvasWindow?.UpdateLayout ();

		PointD newPoint = new (
			X: rect.X + rect.Width / 2,
			Y: rect.Y + rect.Height / 2);

		RecenterView (newPoint);
	}
	#endregion

	#region Private Methods
	private void OnCanvasInvalidated (CanvasInvalidatedEventArgs e)
	{
		CanvasInvalidated?.Invoke (this, e);
	}

	public void OnViewSizeChanged ()
	{
		ViewSizeChanged?.Invoke (this, EventArgs.Empty);
	}

	/// <summary>
	/// Zoom in/out around the center of the screen.
	/// </summary>
	private void ZoomAroundCenter (ZoomType zoomType)
	{
		PointD center;
		if (CanvasWindow is { } view) {
			Size viewport = view.ViewportSize;
			PointD offset = view.ScrollOffset;
			center = new (
				offset.X + viewport.Width / 2.0,
				offset.Y + viewport.Height / 2.0);
		} else {
			center = new (ViewSize.Width / 2.0, ViewSize.Height / 2.0);
		}

		ZoomAndRecenterView (zoomType, center);
	}

	private double NextPresetScale (ZoomType zoomType)
	{
		double zoomPercent = Math.Min (Scale * 100, 3600);

		// The preset list is descending; pick the neighboring preset in the
		// requested direction, matching the upstream zoom-list walk.
		switch (zoomType) {
			case ZoomType.ZoomIn:
				for (int i = ZoomPresets.Length - 1; i >= 0; i--) {
					if (ZoomPresets[i] > zoomPercent)
						return ZoomPresets[i] / 100.0;
				}
				return ZoomPresets[0] / 100.0;
			default:
				for (int i = 0; i < ZoomPresets.Length; i++) {
					if (ZoomPresets[i] < zoomPercent)
						return ZoomPresets[i] / 100.0;
				}
				return ZoomPresets[^1] / 100.0;
		}
	}

	/// <summary>
	/// Zoom in/out around a specific point.
	/// </summary>
	/// <param name="center_point">Center point to zoom around, in view coordinates</param>
	private void ZoomAndRecenterView (ZoomType zoomType, PointD center_point)
	{
		if (zoomType == ZoomType.ZoomOut && (ViewSize.Width == 1 || ViewSize.Height == 1))
			return; //Can't zoom in past a 1x1 px canvas

		ZoomToScaleAroundViewPoint (NextPresetScale (zoomType), center_point);
	}

	private void ZoomToScaleAroundViewCenter (double newScale)
	{
		PointD center;
		if (CanvasWindow is { } view) {
			Size viewport = view.ViewportSize;
			PointD offset = view.ScrollOffset;
			center = new (
				offset.X + viewport.Width / 2.0,
				offset.Y + viewport.Height / 2.0);
		} else {
			center = new (ViewSize.Width / 2.0, ViewSize.Height / 2.0);
		}

		ZoomToScaleAroundViewPoint (newScale, center);
	}

	private void ZoomToScaleAroundViewPoint (double newScale, PointD center_point)
	{
		PointD offset = CanvasWindow?.ScrollOffset ?? new PointD (0, 0);

		double scroll_offset_x = center_point.X - offset.X;
		double scroll_offset_y = center_point.Y - offset.Y;

		PointD canvas_point = ViewPointToCanvas (center_point);

		Scale = Math.Min (newScale, 36.0);

		// Make sure the scroll extents match the new view size before
		// recentering. (Upstream pumped the GTK main loop here.)
		CanvasWindow?.UpdateLayout ();

		// Scroll so that the canvas position under 'center_point' is still the same after zooming.
		PointD new_center_point = CanvasPointToView (canvas_point);
		if (CanvasWindow is { } view) {
			view.ScrollOffset = new PointD (
				new_center_point.X - scroll_offset_x,
				new_center_point.Y - scroll_offset_y);
		}
	}

	#endregion
}
