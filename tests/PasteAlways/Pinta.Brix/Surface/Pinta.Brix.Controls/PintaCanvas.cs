// PintaCanvas.cs
//
// The document canvas control for Pinta.Brix: composites the active
// document's layers (via the ported CanvasRenderer) into an unscaled
// offscreen surface, draws it zoomed with the appropriate resampling, and
// overlays the selection outline. Mirrors the role of the upstream GTK
// canvas widget, hosted on SKXamlCanvas.

using System;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Pinta.Brix.Engine;
using Pinta.Brix.Engine.Drawing;
using SkiaSharp;
using SkiaSharp.Views.Windows;
using Drawing = Pinta.Brix.Engine.Drawing;

namespace Pinta.Brix.Controls;

public sealed class PintaCanvas : SKXamlCanvas, ICanvasView
{
	private Document? document;
	private CanvasRenderer? renderer;
	private Drawing.ImageSurface? canvas_surface;
	private RectangleI? pending_dirty; // union of invalidated canvas rects; null = everything
	private bool surface_stale = true;
	private ToolCursor? tool_cursor;

	//Marching ants: the dash offset ticks backwards while a selection is
	//visible (upstream ticks -1 per timer fire), wrapping at the dash
	//pattern's period. The redraw is display-only.
	private readonly Microsoft.UI.Xaml.DispatcherTimer ants_timer = new () {
		Interval = TimeSpan.FromMilliseconds (100),
	};
	private float ants_offset;

	public PintaCanvas ()
	{
		PaintSurface += OnPaintSurface;
		IsTabStop = true;

		PointerPressed += OnCanvasPointerPressed;
		PointerMoved += OnCanvasPointerMoved;
		PointerReleased += OnCanvasPointerReleased;
		PointerWheelChanged += OnCanvasPointerWheelChanged;
		KeyDown += OnCanvasKeyDown;
		KeyUp += OnCanvasKeyUp;

		ants_timer.Tick += (_, _) => {
			if (document is null || !document.Selection.Visible || document.Selection.SelectionPolygons.Count == 0)
				return;
			ants_offset -= 1;
			if (ants_offset < 0)
				ants_offset += 8; // dash pattern period: 4 on + 4 off
			Invalidate ();
		};

		//The timer only runs while the canvas is in the tree, so closed
		//documents do not keep ticking.
		Loaded += (_, _) => ants_timer.Start ();
		Unloaded += (_, _) => ants_timer.Stop ();
	}

	/// <summary>The document this canvas displays; wires invalidation events.</summary>
	public Document? Document {
		get => document;
		set {
			if (document == value)
				return;

			if (document is not null) {
				document.Workspace.CanvasInvalidated -= OnCanvasInvalidated;
				document.Workspace.ViewSizeChanged -= OnViewSizeChanged;
				document.SelectionChanged -= OnSelectionChanged;
			}

			document = value;
			canvas_surface?.Dispose ();
			canvas_surface = null;
			renderer = null;
			surface_stale = true;

			if (document is not null) {
				document.Workspace.CanvasInvalidated += OnCanvasInvalidated;
				document.Workspace.ViewSizeChanged += OnViewSizeChanged;
				document.SelectionChanged += OnSelectionChanged;
				document.Workspace.Canvas = this;
				renderer = new CanvasRenderer (PintaCore.LivePreview, PintaCore.Workspace, enableLivePreview: true, enableBackgroundPattern: false);
				SyncViewSize ();
			}

			Invalidate ();
		}
	}

	private void OnCanvasInvalidated (object? sender, CanvasInvalidatedEventArgs e)
	{
		if (e.EntireSurface || pending_dirty is null && surface_stale) {
			surface_stale = true;
			pending_dirty = null;
		} else if (pending_dirty is { } dirty) {
			pending_dirty = dirty.Union (e.Rectangle);
		} else {
			pending_dirty = e.Rectangle;
		}

		Invalidate ();
	}

	/// <summary>
	/// A selection change alters only the overlay pass, not the layer
	/// composite, so the cached surface is left intact and just the overlay
	/// is repainted. Without this the marching ants and the selection tools'
	/// handles never appear, because changing a selection dirties no pixels
	/// and therefore never raises CanvasInvalidated.
	/// </summary>
	private void OnSelectionChanged (object? sender, EventArgs e)
		=> Invalidate ();

	private void OnViewSizeChanged (object? sender, EventArgs e)
	{
		SyncViewSize ();
		surface_stale = true;
		Invalidate ();
	}

	private void SyncViewSize ()
	{
		if (document is null)
			return;
		Width = document.Workspace.ViewSize.Width;
		Height = document.Workspace.ViewSize.Height;
	}

	private void OnPaintSurface (object? sender, SKPaintSurfaceEventArgs e)
	{
		SKCanvas canvas = e.Surface.Canvas;
		canvas.Clear (SKColors.Transparent);

		if (document is null || renderer is null)
			return;

		Size imageSize = document.ImageSize;
		Size viewSize = document.Workspace.ViewSize;

		// 1. Refresh the unscaled composite of all layers.
		canvas_surface ??= new Drawing.ImageSurface (Drawing.Format.Argb32, imageSize.Width, imageSize.Height);

		if (surface_stale || pending_dirty is not null) {
			renderer.Initialize (imageSize, imageSize);
			System.Collections.Generic.List<Layer> layers = [.. document.Layers.GetLayersToPaint ()];
			RectangleI dirty = surface_stale
				? new RectangleI (0, 0, imageSize.Width, imageSize.Height)
				: pending_dirty!.Value;
			renderer.Render (layers, canvas_surface, new PointI (0, 0), dirty);
			surface_stale = false;
			pending_dirty = null;
		}

		// 2. Checkerboard behind transparent content, sized to the view.
		DrawCheckerboard (canvas, viewSize);

		// 3. The composite, scaled for zoom: nearest-neighbor when zoomed in,
		//    linear when zoomed out (matching upstream).
		double scale = document.Workspace.Scale;
		SKSamplingOptions sampling = scale >= 1
			? new SKSamplingOptions (SKFilterMode.Nearest)
			: new SKSamplingOptions (SKFilterMode.Linear);
		canvas.DrawBitmap (
			canvas_surface.Bitmap,
			new SKRect (0, 0, viewSize.Width, viewSize.Height),
			sampling);

		// 4. Selection outline - the marching ants, animated by ants_timer.
		DrawSelection (canvas);

		// 5. Tool overlay handles, drawn in view space at constant size.
		if (PintaCore.Tools.CurrentTool is { } tool) {
			foreach (IToolHandle handle in tool.Handles) {
				if (handle.Active)
					handle.Draw (canvas);
			}
		}
	}

	private static void DrawCheckerboard (SKCanvas canvas, Size viewSize)
	{
		const int cell = 8;
		using SKPaint light = new () { Color = new SKColor (0xFF, 0xFF, 0xFF) };
		using SKPaint dark = new () { Color = new SKColor (0xC7, 0xC7, 0xC7) };
		canvas.Save ();
		canvas.ClipRect (new SKRect (0, 0, viewSize.Width, viewSize.Height));
		canvas.DrawPaint (light);
		for (int y = 0; y * cell < viewSize.Height; y++) {
			for (int x = (y % 2); x * cell < viewSize.Width; x += 2) {
				canvas.DrawRect (x * cell, y * cell, cell, cell, dark);
			}
		}
		canvas.Restore ();
	}

	private void DrawSelection (SKCanvas canvas)
	{
		if (document is null || !document.Selection.Visible)
			return;

		var polygons = document.Selection.SelectionPolygons;
		if (polygons.Count == 0)
			return;

		double scale = document.Workspace.Scale;
		using SKPathBuilder builder = new ();
		foreach (var polygon in polygons) {
			bool first = true;
			foreach (var point in polygon) {
				SKPoint p = new ((float) (point.X * scale), (float) (point.Y * scale));
				if (first) {
					builder.MoveTo (p);
					first = false;
				} else {
					builder.LineTo (p);
				}
			}
			builder.Close ();
		}
		using SKPath path = builder.Snapshot ();

		using SKPaint white = new () {
			Style = SKPaintStyle.Stroke,
			StrokeWidth = 1,
			Color = SKColors.White,
			IsAntialias = true,
		};
		using SKPaint black = new () {
			Style = SKPaintStyle.Stroke,
			StrokeWidth = 1,
			Color = SKColors.Black,
			IsAntialias = true,
			PathEffect = SKPathEffect.CreateDash ([4f, 4f], ants_offset),
		};
		canvas.DrawPath (path, white);
		canvas.DrawPath (path, black);
	}

	// ---- ICanvasView -------------------------------------------------------

	public ToolCursor? Cursor {
		get => tool_cursor;
		set {
			tool_cursor = value;
			ProtectedCursor = InputSystemCursor.Create (MapCursor (value));
		}
	}

	private static InputSystemCursorShape MapCursor (ToolCursor? cursor)
	{
		if (cursor is null)
			return InputSystemCursorShape.Arrow;

		// Icon/image cursors are approximated with a crosshair until custom
		// bitmap cursors are supported platform-side; tools also draw brush
		// outlines as canvas overlays, which carries most of the meaning.
		if (cursor.IconName is not null || cursor.Image is not null)
			return InputSystemCursorShape.Cross;

		return cursor.Shape switch {
			StandardCursor.Crosshair => InputSystemCursorShape.Cross,
			StandardCursor.Hand => InputSystemCursorShape.Hand,
			StandardCursor.Move => InputSystemCursorShape.SizeAll,
			StandardCursor.IBeam => InputSystemCursorShape.IBeam,
			StandardCursor.NotAllowed => InputSystemCursorShape.UniversalNo,
			StandardCursor.SizeNWSE => InputSystemCursorShape.SizeNorthwestSoutheast,
			StandardCursor.SizeNESW => InputSystemCursorShape.SizeNortheastSouthwest,
			StandardCursor.SizeNS => InputSystemCursorShape.SizeNorthSouth,
			StandardCursor.SizeWE => InputSystemCursorShape.SizeWestEast,
			_ => InputSystemCursorShape.Arrow,
		};
	}

	public void QueueDraw ()
		=> Invalidate ();

	public void GrabFocus ()
		=> Focus (FocusState.Programmatic);

	// ---- Input -------------------------------------------------------------

	private ToolMouseEventArgs BuildMouseArgs (PointerRoutedEventArgs e)
	{
		var point = e.GetCurrentPoint (this);
		PointD viewPoint = new (point.Position.X, point.Position.Y);
		PointD canvasPoint = document?.Workspace.ViewPointToCanvas (viewPoint) ?? viewPoint;

		MouseButton button = MouseButton.None;
		var props = point.Properties;
		if (props.IsLeftButtonPressed)
			button = MouseButton.Left;
		else if (props.IsRightButtonPressed)
			button = MouseButton.Right;
		else if (props.IsMiddleButtonPressed)
			button = MouseButton.Middle;

		return new ToolMouseEventArgs {
			State = InputMapper.ToModifierType (e.KeyModifiers, props),
			MouseButton = button,
			PointDouble = canvasPoint,
			WindowPoint = viewPoint,
			RootPoint = viewPoint,
		};
	}

	private void OnCanvasPointerPressed (object sender, PointerRoutedEventArgs e)
	{
		if (document is null)
			return;
		GrabFocus ();
		CapturePointer (e.Pointer);
		PintaCore.Tools.DoMouseDown (document, BuildMouseArgs (e));
		e.Handled = true;
	}

	private void OnCanvasPointerMoved (object sender, PointerRoutedEventArgs e)
	{
		if (document is null)
			return;
		ToolMouseEventArgs args = BuildMouseArgs (e);
		PintaCore.Chrome.LastCanvasCursorPoint = args.Point;
		PintaCore.Tools.DoMouseMove (document, args);
	}

	private void OnCanvasPointerReleased (object sender, PointerRoutedEventArgs e)
	{
		if (document is null)
			return;
		// The pressed-button flags are cleared by release time; recover the
		// released button from the update kind.
		ToolMouseEventArgs args = BuildMouseArgs (e);
		var kind = e.GetCurrentPoint (this).Properties.PointerUpdateKind;
		MouseButton released = kind switch {
			PointerUpdateKind.LeftButtonReleased => MouseButton.Left,
			PointerUpdateKind.RightButtonReleased => MouseButton.Right,
			PointerUpdateKind.MiddleButtonReleased => MouseButton.Middle,
			_ => args.MouseButton,
		};
		if (args.MouseButton != released) {
			args = new ToolMouseEventArgs {
				State = args.State,
				MouseButton = released,
				PointDouble = args.PointDouble,
				WindowPoint = args.WindowPoint,
				RootPoint = args.RootPoint,
			};
		}
		ReleasePointerCapture (e.Pointer);
		PintaCore.Tools.DoMouseUp (document, args);
		e.Handled = true;
	}

	private void OnCanvasPointerWheelChanged (object sender, PointerRoutedEventArgs e)
	{
		if (document is null)
			return;
		if (!e.KeyModifiers.HasFlag (Windows.System.VirtualKeyModifiers.Control))
			return;

		var point = e.GetCurrentPoint (this);
		PointD viewPoint = new (point.Position.X, point.Position.Y);
		int delta = point.Properties.MouseWheelDelta;

		if (delta > 0)
			document.Workspace.ZoomInAroundViewPoint (viewPoint);
		else if (delta < 0)
			document.Workspace.ZoomOutAroundViewPoint (viewPoint);

		e.Handled = true;
	}

	private void OnCanvasKeyDown (object sender, KeyRoutedEventArgs e)
	{
		InputMapper.NoteKey (e.Key, down: true);
		if (document is null)
			return;
		ToolKeyEventArgs args = InputMapper.ToKeyArgs (e);
		e.Handled = PintaCore.Tools.DoKeyDown (document, args);
	}

	private void OnCanvasKeyUp (object sender, KeyRoutedEventArgs e)
	{
		InputMapper.NoteKey (e.Key, down: false);
		if (document is null)
			return;
		ToolKeyEventArgs args = InputMapper.ToKeyArgs (e);
		e.Handled = PintaCore.Tools.DoKeyUp (document, args);
	}
}
