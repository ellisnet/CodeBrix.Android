using System;
using System.Collections.Generic;
using CodeBrix.Platform.UI.TextLayout;
using SkiaSharp;
using SkiaSharp.Views.Windows;

namespace CodeBrix.Platform.UI.AddIn.TextLayout.UIReqs.Support;

/// <summary>
/// The three ways a scenario can put a layout on the canvas. They are the three halves of the
/// add-in's painting surface: the fast filled-glyph path, the outline path a consumer strokes
/// or fills itself, and the rectangles a consumer paints behind or around the text.
/// </summary>
public enum TextLayoutPaintMode
{
	/// <summary>Paint nothing at all, which is what an untouched canvas must look like.</summary>
	Blank,

	/// <summary>Draw the layout, which fills every glyph in one colour (or in each run's own).</summary>
	Draw,

	/// <summary>Stroke the one combined outline path the layout hands back.</summary>
	Outline,

	/// <summary>Fill the FIRST positioned glyph outline, and nothing else.</summary>
	FirstOutline,

	/// <summary>Fill every positioned glyph outline, each at its own origin.</summary>
	AllOutlines,

	/// <summary>Fill the selection rectangles, then draw the text over them.</summary>
	Selection,
}

/// <summary>
/// The element a TextLayout scenario looks at: a Skia canvas whose paint handler clears the
/// surface and then paints the layout the scenario built, in one of the
/// <see cref="TextLayoutPaintMode"/> modes, at a stated origin.
/// </summary>
/// <remarks>
/// <para>
/// The clear is the FIRST line of the handler and it is not optional. This surface keeps its
/// staging buffer between frames and leaves clearing to the handler, so a handler that forgot
/// it would carry the previous frame's ink into this one and every two-frame comparison in
/// these features would be about a picture nobody painted. A scenario states the clear as a
/// requirement of its own by capturing the untouched canvas and asserting that it is blank.
/// </para>
/// <para>
/// The surface never invalidates itself: a step changes what it holds and then asks it to
/// repaint, so that the frame handshake decides when a picture exists. Paths handed out by
/// the layout belong to the caller and are disposed inside the handler that asked for them;
/// a glyph's font belongs to the engine's cache and is never touched.
/// </para>
/// </remarks>
public sealed class TextLayoutSurface : SKXamlCanvas
{
	/// <summary>How wide the stroke is when the combined outline path is stroked.</summary>
	public const float OutlineStrokeWidth = 1f;

	private TextLayoutResult? _layout;

	/// <summary>Builds the surface with nothing on it.</summary>
	public TextLayoutSurface() => PaintSurface += Paint;

	/// <summary>
	/// The layout the surface paints, or <c>null</c> while the scenario has not built one.
	/// </summary>
	/// <remarks>
	/// Setting a new layout releases the one it replaces, which is where a layout is let go of:
	/// a framework element is already disposable with an empty implementation the surface cannot
	/// override, and a layout owns nothing unmanaged in any case - its own documentation says so.
	/// </remarks>
	public TextLayoutResult? Layout
	{
		get => _layout;
		set
		{
			if (ReferenceEquals(_layout, value))
			{
				return;
			}

			_layout?.Dispose();
			_layout = value;
		}
	}

	/// <summary>Which of the three painting paths the next repaint takes.</summary>
	public TextLayoutPaintMode Mode { get; set; } = TextLayoutPaintMode.Blank;

	/// <summary>Where the layout's top left corner lands on the canvas.</summary>
	public SKPoint Origin { get; set; } = SKPoint.Empty;

	/// <summary>The colour the glyphs are painted in, unless a run carries its own.</summary>
	public SKColor InkColor { get; set; } = SKColors.Black;

	/// <summary>The colour the selection rectangles are filled with.</summary>
	public SKColor SelectionColor { get; set; } = SKColors.Yellow;

	/// <summary>The first text index of the range <see cref="TextLayoutPaintMode.Selection"/> fills.</summary>
	public int SelectionStart { get; set; }

	/// <summary>How many characters of the range <see cref="TextLayoutPaintMode.Selection"/> fills.</summary>
	public int SelectionLength { get; set; }

	private void Paint(object? sender, SKPaintSurfaceEventArgs e)
	{
		ArgumentNullException.ThrowIfNull(e);

		var canvas = e.Surface.Canvas;

		// FIRST, always: this surface does not clear between frames.
		canvas.Clear(SKColors.Transparent);

		var layout = _layout;
		if (layout is null)
		{
			return;
		}

		switch (Mode)
		{
			case TextLayoutPaintMode.Draw:
				DrawGlyphs(canvas, layout);
				break;
			case TextLayoutPaintMode.Outline:
				StrokeCombinedOutline(canvas, layout);
				break;
			case TextLayoutPaintMode.FirstOutline:
				FillGlyphOutlines(canvas, layout, firstOnly: true);
				break;
			case TextLayoutPaintMode.AllOutlines:
				FillGlyphOutlines(canvas, layout, firstOnly: false);
				break;
			case TextLayoutPaintMode.Selection:
				FillSelection(canvas, layout);
				DrawGlyphs(canvas, layout);
				break;
			default:
				break;
		}
	}

	private void DrawGlyphs(SKCanvas canvas, TextLayoutResult layout)
	{
		using var paint = new SKPaint { Color = InkColor, IsAntialias = true };
		layout.Draw(canvas, Origin, paint);
	}

	private void StrokeCombinedOutline(SKCanvas canvas, TextLayoutResult layout)
	{
		// The path is the caller's, so it is disposed here rather than left to a finalizer.
		using var path = layout.GetOutlinePath();
		using var paint = new SKPaint
		{
			Color = InkColor,
			IsAntialias = true,
			Style = SKPaintStyle.Stroke,
			StrokeWidth = OutlineStrokeWidth,
		};

		var saved = canvas.Save();
		canvas.Translate(Origin.X, Origin.Y);
		canvas.DrawPath(path, paint);
		canvas.RestoreToCount(saved);
	}

	private void FillGlyphOutlines(SKCanvas canvas, TextLayoutResult layout, bool firstOnly)
	{
		IReadOnlyList<GlyphOutline> outlines = layout.GetGlyphOutlines();
		try
		{
			using var paint = new SKPaint { Color = InkColor, IsAntialias = true };
			for (var index = 0; index < outlines.Count; index++)
			{
				if (firstOnly && index != 0)
				{
					break;
				}

				var outline = outlines[index];
				if (outline.Path is null || outline.Path.IsEmpty)
				{
					// A space, and every other glyph with nothing to draw, advances without ink.
					continue;
				}

				// The path is drawn at the origin; the glyph's own origin is what places it.
				var saved = canvas.Save();
				canvas.Translate(Origin.X + outline.Origin.X, Origin.Y + outline.Origin.Y);
				canvas.DrawPath(outline.Path, paint);
				canvas.RestoreToCount(saved);
			}
		}
		finally
		{
			// Each outline owns its path. The font inside it belongs to the engine's cache and
			// is deliberately not touched.
			foreach (var outline in outlines)
			{
				outline.Dispose();
			}
		}
	}

	private void FillSelection(SKCanvas canvas, TextLayoutResult layout)
	{
		var rectangles = layout.GetSelectionRects(SelectionStart, SelectionLength);
		using var paint = new SKPaint
		{
			Color = SelectionColor,
			IsAntialias = false,
			Style = SKPaintStyle.Fill,
		};

		foreach (var rectangle in rectangles)
		{
			// Whole pixels, no antialiasing: a fill a scenario asserts a colour on must not have
			// a blended edge of its own.
			canvas.DrawRect(
				new SKRect(
					MathF.Round(Origin.X + rectangle.Left),
					MathF.Round(Origin.Y + rectangle.Top),
					MathF.Round(Origin.X + rectangle.Right),
					MathF.Round(Origin.Y + rectangle.Bottom)),
				paint);
		}
	}
}
