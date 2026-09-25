using System;
using CodeBrix.Platform.UI.Core.UIReqs.Support;
using CodeBrix.Platform.WinUI.Graphics3DGL;
using SkiaSharp;

namespace CodeBrix.Platform.UI.AddIn.Graphics3DGL.UIReqs.Support;

/// <summary>
/// The GPU-Skia fixture element, behind the noun "SkiaGlCanvas": it clears its surface, puts a
/// square in the corner the surface calls 0,0 and a circle in the middle, using nothing but the
/// ordinary drawing API a person would use on any other canvas.
/// <para>
/// The square is what makes the origin a claim with a right and a wrong answer: a GPU surface
/// has its origin at the TOP LEFT, unlike the raw framebuffer a
/// <see cref="GLCanvasElement"/> reads back, so the square drawn at 0,0 must appear at the top
/// left of the element and not at the bottom left.
/// </para>
/// <para>
/// Like every fixture here it never invalidates itself, never blocks inside a paint, and paints
/// with antialiasing off on whole-pixel rectangles, so that a region assertion is about a fill
/// rather than about the blend at an edge.
/// </para>
/// </summary>
public sealed class SkiaGlCanvas : SkiaGLCanvasElement
{
	/// <summary>How far the corner square runs from the surface's origin, in pixels.</summary>
	public const int MarkSize = 100;

	/// <summary>The radius of the circle drawn in the middle of the surface, in pixels.</summary>
	public const int CircleRadius = 60;

	/// <summary>Builds the fixture. The owning-window function is a WinUI concern; here it is null.</summary>
	public SkiaGlCanvas()
		: base(null)
	{
	}

	/// <summary>How many times the element has painted its surface since it was built.</summary>
	public int PaintCount { get; private set; }

	/// <summary>The width in pixels of the surface the last paint was handed.</summary>
	public int SurfaceWidth { get; private set; }

	/// <summary>The height in pixels of the surface the last paint was handed.</summary>
	public int SurfaceHeight { get; private set; }

	/// <summary>The colour the whole surface is cleared to.</summary>
	public SKColor ClearColor { get; private set; } = SKColors.Blue;

	/// <summary>The colour of the square at the surface's own 0,0.</summary>
	public SKColor MarkColor { get; private set; } = SKColors.Lime;

	/// <summary>The colour of the circle in the middle of the surface.</summary>
	public SKColor CircleColor { get; private set; } = SKColors.Red;

	/// <summary>Sets the colour the surface is cleared to, as a feature file writes it.</summary>
	/// <param name="value">A colour name or an "#AARRGGBB" value.</param>
	public void SetClearColor(string value) => ClearColor = ToSkia(value);

	/// <summary>Sets the colour of the corner square, as a feature file writes it.</summary>
	/// <param name="value">A colour name or an "#AARRGGBB" value.</param>
	public void SetMarkColor(string value) => MarkColor = ToSkia(value);

	/// <summary>Sets the colour of the middle circle, as a feature file writes it.</summary>
	/// <param name="value">A colour name or an "#AARRGGBB" value.</param>
	public void SetCircleColor(string value) => CircleColor = ToSkia(value);

	/// <summary>
	/// Paints the surface. Called by the add-in on the UI thread with the off-screen OpenGL
	/// context current and a GPU-backed surface to draw on.
	/// </summary>
	/// <param name="args">The surface, its context and its pixel geometry.</param>
	protected override void OnPaintSurface(SkiaGLPaintSurfaceEventArgs args)
	{
		ArgumentNullException.ThrowIfNull(args);

		PaintCount++;
		SurfaceWidth = args.Info.Width;
		SurfaceHeight = args.Info.Height;

		var canvas = args.Surface.Canvas;
		canvas.Clear(ClearColor);

		using var paint = new SKPaint { IsAntialias = false, Style = SKPaintStyle.Fill };

		paint.Color = MarkColor;
		canvas.DrawRect(SKRect.Create(0, 0, MarkSize, MarkSize), paint);

		paint.Color = CircleColor;
		canvas.DrawCircle(args.Info.Width / 2f, args.Info.Height / 2f, CircleRadius, paint);

		// Deliberately NO Invalidate() here: an element that repaints itself from inside its own
		// paint is dirty forever, and no frame the harness asks for would ever arrive.
	}

	private static SKColor ToSkia(string value)
	{
		var color = Colors.Parse(GherkinValue.Unquote(value));
		return new SKColor(color.R, color.G, color.B, color.A);
	}
}
