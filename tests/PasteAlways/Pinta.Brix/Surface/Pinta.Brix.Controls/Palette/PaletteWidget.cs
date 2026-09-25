// PaletteWidget.cs
//
// The status bar's colour widget - upstream's StatusBarColorPaletteWidget.
// It is a DrawingArea upstream and every measurement is a constant, so this is
// a Skia-drawn control rather than a composition of XAML elements: that keeps
// the geometry identical to upstream instead of approximating it with layout.
//
// Layout, all in device-independent pixels, widget height 42:
//   primary swatch    (4, 3, 24, 24)      \ overlapping, over a 16px checkerboard,
//   secondary swatch  (17, 16, 24, 24)    / each with a white inner + black outer border
//   swap arrows       (27, 2, 15, 15)
//   reset to b/w      (2, 27, 15, 15)
//   recently used     from x = 50,  2 rows x (MaxRecentlyUsedColor / 2) cols, 19px, ROW-major
//   palette           from recent.Right + 10, 2 rows, 19px, COLUMN-major

using System;
using System.Collections.Generic;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Input;
using Pinta.Brix.Engine;
using SkiaSharp;
using SkiaSharp.Views.Windows;
using Drawing = Pinta.Brix.Engine.Drawing;

namespace Pinta.Brix.Controls;

/// <summary>
/// The primary/secondary colour swatches, the recently-used strip and the
/// palette strip, as one drawn control.
/// </summary>
public sealed class PaletteWidget : SKXamlCanvas
{
	private const int WidgetHeight = 42;
	private const int SwatchSize = 24;
	private const int PaletteSwatchSize = 19;
	private const int PaletteRows = 2;
	private const int CheckerSize = 8;

	private static readonly SKRect PrimaryRect = SKRect.Create (4, 3, SwatchSize, SwatchSize);
	private static readonly SKRect SecondaryRect = SKRect.Create (17, 16, SwatchSize, SwatchSize);
	private static readonly SKRect SwapRect = SKRect.Create (27, 2, 15, 15);
	private static readonly SKRect ResetRect = SKRect.Create (2, 27, 15, 15);

	private const int RecentStripLeft = 50;

	private SKRect recent_rect;
	private SKRect palette_rect;

	/// <summary>
	/// Raised when the user asks to edit a colour - a click on either swatch,
	/// or a modifier-click on a palette entry.
	/// </summary>
	public event EventHandler<PaletteColorEditEventArgs>? ColorEditRequested;

	/// <summary>Creates the widget.</summary>
	public PaletteWidget ()
	{
		Height = WidgetHeight;
		MinWidth = 300;

		PaintSurface += OnPaintSurface;
		PointerPressed += OnPointerPressedHandler;

		PintaCore.Palette.PrimaryColorChanged += OnPaletteChanged;
		PintaCore.Palette.SecondaryColorChanged += OnPaletteChanged;
		PintaCore.Palette.RecentColorsChanged += OnPaletteChanged;
		PintaCore.Palette.CurrentPalette.PaletteChanged += OnPaletteChanged;
	}

	private void OnPaletteChanged (object? sender, EventArgs e) => Invalidate ();

	// ---- Drawing -----------------------------------------------------------

	private void OnPaintSurface (object? sender, SKPaintSurfaceEventArgs e)
	{
		SKCanvas canvas = e.Surface.Canvas;
		canvas.Clear (SKColors.Transparent);

		PaletteManager palette = PintaCore.Palette;

		DrawSwatch (canvas, SecondaryRect, palette.SecondaryColor);
		DrawSwatch (canvas, PrimaryRect, palette.PrimaryColor);

		DrawSwapArrows (canvas);
		DrawResetIcon (canvas);

		DrawRecentlyUsed (canvas, palette);
		DrawPalette (canvas, palette);
	}

	private static void DrawSwatch (SKCanvas canvas, SKRect rect, Drawing.Color color)
	{
		DrawCheckerboard (canvas, rect);

		using SKPaint fill = new () { Color = ToSKColor (color), IsAntialias = false };
		canvas.DrawRect (rect, fill);

		// White inner border inside a black outer border, exactly as upstream.
		using SKPaint white = new () { Color = SKColors.White, IsStroke = true, StrokeWidth = 1, IsAntialias = false };
		canvas.DrawRect (SKRect.Inflate (rect, -1, -1), white);

		using SKPaint black = new () { Color = SKColors.Black, IsStroke = true, StrokeWidth = 1, IsAntialias = false };
		canvas.DrawRect (rect, black);
	}

	private static void DrawCheckerboard (SKCanvas canvas, SKRect rect)
	{
		canvas.Save ();
		canvas.ClipRect (rect);

		using SKPaint light = new () { Color = new SKColor (0xFF, 0xFF, 0xFF), IsAntialias = false };
		using SKPaint dark = new () { Color = new SKColor (0xC0, 0xC0, 0xC0), IsAntialias = false };

		int rows = (int) Math.Ceiling (rect.Height / CheckerSize);
		int columns = (int) Math.Ceiling (rect.Width / CheckerSize);

		for (int row = 0; row < rows; row++) {
			for (int column = 0; column < columns; column++) {

				SKRect cell = SKRect.Create (
					rect.Left + (column * CheckerSize),
					rect.Top + (row * CheckerSize),
					CheckerSize,
					CheckerSize);

				canvas.DrawRect (cell, (row + column) % 2 == 0 ? light : dark);
			}
		}

		canvas.Restore ();
	}

	private static void DrawSwapArrows (SKCanvas canvas)
	{
		using SKPaint paint = new () {
			Color = new SKColor (0xB0, 0xB0, 0xB0),
			IsStroke = true,
			StrokeWidth = 1.4f,
			IsAntialias = true,
		};

		// A small "swap" glyph: an arrow out to the right and back down-left.
		using SKPathBuilder builder = new ();
		builder.MoveTo (SwapRect.Left + 2, SwapRect.Bottom - 4);
		builder.LineTo (SwapRect.Left + 2, SwapRect.Top + 3);
		builder.LineTo (SwapRect.Right - 2, SwapRect.Top + 3);
		builder.MoveTo (SwapRect.Right - 5, SwapRect.Top);
		builder.LineTo (SwapRect.Right - 2, SwapRect.Top + 3);
		builder.LineTo (SwapRect.Right - 5, SwapRect.Top + 6);

		using SKPath path = builder.Snapshot ();
		canvas.DrawPath (path, paint);
	}

	private static void DrawResetIcon (SKCanvas canvas)
	{
		// Two overlapping mini-swatches, black over white: the "reset to black
		// and white" affordance.
		SKRect back = SKRect.Create (ResetRect.Left + 4, ResetRect.Top + 4, 9, 9);
		SKRect front = SKRect.Create (ResetRect.Left, ResetRect.Top, 9, 9);

		using SKPaint white = new () { Color = SKColors.White, IsAntialias = false };
		using SKPaint black = new () { Color = SKColors.Black, IsAntialias = false };
		using SKPaint outline = new () { Color = new SKColor (0x80, 0x80, 0x80), IsStroke = true, StrokeWidth = 1, IsAntialias = false };

		canvas.DrawRect (back, white);
		canvas.DrawRect (back, outline);
		canvas.DrawRect (front, black);
		canvas.DrawRect (front, outline);
	}

	private void DrawRecentlyUsed (SKCanvas canvas, PaletteManager palette)
	{
		int columns = Math.Max (1, palette.MaxRecentlyUsedColor / PaletteRows);

		recent_rect = SKRect.Create (
			RecentStripLeft,
			(WidgetHeight - (PaletteRows * PaletteSwatchSize)) / 2f,
			columns * PaletteSwatchSize,
			PaletteRows * PaletteSwatchSize);

		IReadOnlyList<Drawing.Color> colors = palette.RecentlyUsedColors;

		// Row-major: the most recent colour is top-left and the strip fills
		// across before wrapping.
		for (int i = 0; i < colors.Count; i++) {

			int row = i / columns;
			int column = i % columns;

			if (row >= PaletteRows)
				break;

			DrawPaletteSwatch (canvas, RecentSwatchRect (row, column), colors[i]);
		}
	}

	private void DrawPalette (SKCanvas canvas, PaletteManager palette)
	{
		IReadOnlyList<Drawing.Color> colors = palette.CurrentPalette.Colors;

		int columns = (int) Math.Ceiling (colors.Count / (double) PaletteRows);

		palette_rect = SKRect.Create (
			recent_rect.Right + 10,
			(WidgetHeight - (PaletteRows * PaletteSwatchSize)) / 2f,
			columns * PaletteSwatchSize,
			PaletteRows * PaletteSwatchSize);

		// Column-major: upstream fills top-then-bottom before moving right.
		for (int i = 0; i < colors.Count; i++) {

			int column = i / PaletteRows;
			int row = i % PaletteRows;

			DrawPaletteSwatch (canvas, PaletteSwatchRect (row, column), colors[i]);
		}
	}

	private SKRect RecentSwatchRect (int row, int column) => SKRect.Create (
		recent_rect.Left + (column * PaletteSwatchSize),
		recent_rect.Top + (row * PaletteSwatchSize),
		PaletteSwatchSize,
		PaletteSwatchSize);

	private SKRect PaletteSwatchRect (int row, int column) => SKRect.Create (
		palette_rect.Left + (column * PaletteSwatchSize),
		palette_rect.Top + (row * PaletteSwatchSize),
		PaletteSwatchSize,
		PaletteSwatchSize);

	private static void DrawPaletteSwatch (SKCanvas canvas, SKRect rect, Drawing.Color color)
	{
		DrawCheckerboard (canvas, rect);

		using SKPaint fill = new () { Color = ToSKColor (color), IsAntialias = false };
		canvas.DrawRect (rect, fill);

		using SKPaint outline = new () { Color = new SKColor (0x60, 0x60, 0x60), IsStroke = true, StrokeWidth = 1, IsAntialias = false };
		canvas.DrawRect (rect, outline);
	}

	private static SKColor ToSKColor (Drawing.Color color) => new (
		(byte) Math.Clamp (color.R * 255, 0, 255),
		(byte) Math.Clamp (color.G * 255, 0, 255),
		(byte) Math.Clamp (color.B * 255, 0, 255),
		(byte) Math.Clamp (color.A * 255, 0, 255));

	// ---- Input -------------------------------------------------------------

	private void OnPointerPressedHandler (object sender, PointerRoutedEventArgs e)
	{
		PointerPoint point = e.GetCurrentPoint (this);
		SKPoint position = new ((float) point.Position.X, (float) point.Position.Y);

		bool secondary = point.Properties.IsRightButtonPressed;
		bool edit = point.Properties.IsMiddleButtonPressed;

		e.Handled = true;

		if (SwapRect.Contains (position)) {
			PintaCore.Palette.SwapColors ();
			return;
		}

		if (ResetRect.Contains (position)) {
			PintaCore.Palette.SetColor (setPrimary: true, Drawing.Color.Black, addToRecent: false);
			PintaCore.Palette.SetColor (setPrimary: false, Drawing.Color.White, addToRecent: false);
			return;
		}

		// The primary swatch is drawn on top, so it is tested first.
		if (PrimaryRect.Contains (position)) {
			ColorEditRequested?.Invoke (this, new PaletteColorEditEventArgs (PaletteColorTarget.Primary, -1));
			return;
		}

		if (SecondaryRect.Contains (position)) {
			ColorEditRequested?.Invoke (this, new PaletteColorEditEventArgs (PaletteColorTarget.Secondary, -1));
			return;
		}

		if (recent_rect.Contains (position)) {
			HandleRecentClick (position, secondary);
			return;
		}

		if (palette_rect.Contains (position))
			HandlePaletteClick (position, secondary, edit);
	}

	private void HandleRecentClick (SKPoint position, bool secondary)
	{
		int columns = Math.Max (1, PintaCore.Palette.MaxRecentlyUsedColor / PaletteRows);
		int column = (int) ((position.X - recent_rect.Left) / PaletteSwatchSize);
		int row = (int) ((position.Y - recent_rect.Top) / PaletteSwatchSize);
		int index = (row * columns) + column;

		IReadOnlyList<Drawing.Color> colors = PintaCore.Palette.RecentlyUsedColors;

		if (index < 0 || index >= colors.Count)
			return;

		PintaCore.Palette.SetColor (!secondary, colors[index]);
	}

	private void HandlePaletteClick (SKPoint position, bool secondary, bool edit)
	{
		int column = (int) ((position.X - palette_rect.Left) / PaletteSwatchSize);
		int row = (int) ((position.Y - palette_rect.Top) / PaletteSwatchSize);
		int index = (column * PaletteRows) + row;

		IReadOnlyList<Drawing.Color> colors = PintaCore.Palette.CurrentPalette.Colors;

		if (index < 0 || index >= colors.Count)
			return;

		if (edit) {
			ColorEditRequested?.Invoke (this, new PaletteColorEditEventArgs (PaletteColorTarget.PaletteEntry, index));
			return;
		}

		PintaCore.Palette.SetColor (!secondary, colors[index]);
	}
}

/// <summary>Which colour the user asked to edit.</summary>
public enum PaletteColorTarget
{
	/// <summary>The primary colour.</summary>
	Primary,

	/// <summary>The secondary colour.</summary>
	Secondary,

	/// <summary>An entry in the current palette.</summary>
	PaletteEntry,
}

/// <summary>Describes a request to edit a colour.</summary>
/// <param name="Target">Which colour was asked for.</param>
/// <param name="PaletteIndex">
/// The palette index when <paramref name="Target"/> is
/// <see cref="PaletteColorTarget.PaletteEntry"/>, otherwise -1.
/// </param>
public sealed record PaletteColorEditEventArgs (PaletteColorTarget Target, int PaletteIndex);
