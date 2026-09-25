using System;
using System.Collections.Generic;
using System.Globalization;
using CodeBrix.Platform.UI.Core.UIReqs.Support;
using Microsoft.UI.Xaml.Input;
using SkiaSharp;
using SkiaSharp.Views.Windows;
using Windows.Foundation;
using Windows.UI;

namespace CodeBrix.Platform.UI.AddIn.SkiaSharpViews.UIReqs.Support;

/// <summary>
/// The canvas the scenarios of this group paint with: an SKXamlCanvas with an ordinary
/// PaintSurface handler, exactly as an application writes one, drawing whatever the scenario
/// last told it to draw - a run of colour bands across the surface, a gradient, a square, or
/// nothing at all.
/// <para>
/// Three things about it are deliberate. It never invalidates itself: every repaint in a
/// scenario is one the scenario asked for, which is what makes "nothing else repaints it" a
/// requirement that can be stated. It clears the surface first ONLY while
/// <see cref="ClearsTheSurfaceFirst"/> says so, because the pixel buffer is reused between
/// paints and one scenario turns that into a requirement rather than a surprise. And every
/// paint is counted into the harness's <see cref="EventRecorder"/> under the name the scenario
/// gave the element, so a requirement can say that a collapsed canvas did not paint.
/// </para>
/// </summary>
public sealed class PaintedCanvas : SKXamlCanvas
{
	/// <summary>The name a paint is recorded under, which is the event the element raises.</summary>
	public const string PaintSurfaceEventName = "PaintSurface";

	/// <summary>The name a pointer press is recorded under.</summary>
	public const string PointerPressedEventName = "PointerPressed";

	private readonly List<Color> _bands = [];

	private Color _markerColor;
	private bool _hasMarker;
	private int _markerX;
	private int _markerY;
	private int _markerWidth;
	private int _markerHeight;
	private Color _gradientStart;
	private Color _gradientEnd;
	private bool _hasGradient;
	private bool _gradientIsVertical;

	/// <summary>Builds a canvas that has been told to paint nothing yet.</summary>
	public PaintedCanvas()
	{
		PaintSurface += OnFixturePaintSurface;
		PointerPressed += OnFixturePointerPressed;
	}

	/// <summary>
	/// Whether the handler clears the surface before it draws. True is what an application
	/// does; a scenario sets it to false to show what the reused buffer still holds.
	/// </summary>
	public bool ClearsTheSurfaceFirst { get; set; } = true;

	/// <summary>How often the handler has been called since the element was built.</summary>
	public int PaintCount { get; private set; }

	/// <summary>
	/// The size of the surface the handler was last given, in pixels - the user-visible size
	/// the event args carried, which at this panel's fixed scale of 1.0 is also the size of the
	/// buffer behind it.
	/// </summary>
	public SKSizeI PaintedSurfaceSize { get; private set; }

	/// <summary>
	/// Where the last pointer press landed, in the element's own coordinates - the same
	/// coordinate system the handler draws in - or <c>null</c> when nothing has pressed it.
	/// </summary>
	public Point? LastPointerPosition { get; private set; }

	/// <summary>
	/// Paints the surface as a row of equal colour bands, left to right. One colour fills the
	/// whole surface; two paint the left and the right half. Nothing is repainted until the
	/// scenario invalidates the canvas.
	/// </summary>
	/// <param name="colors">The colours, in the order they are drawn from the left edge.</param>
	public void PaintBands(IReadOnlyList<Color> colors)
	{
		ArgumentNullException.ThrowIfNull(colors);

		_bands.Clear();
		_bands.AddRange(colors);
		_hasGradient = false;
	}

	/// <summary>
	/// Paints the surface as a gradient between two colours, so that a scenario can state that
	/// what a Skia shader produced reaches the panel as it was drawn.
	/// </summary>
	/// <param name="start">The colour at the left, or top, edge.</param>
	/// <param name="end">The colour at the right, or bottom, edge.</param>
	/// <param name="vertical">Whether the gradient runs down the surface rather than across it.</param>
	public void PaintGradient(Color start, Color end, bool vertical)
	{
		_bands.Clear();
		_gradientStart = start;
		_gradientEnd = end;
		_gradientIsVertical = vertical;
		_hasGradient = true;
	}

	/// <summary>
	/// Paints one square and nothing else: no bands, no gradient. With
	/// <see cref="ClearsTheSurfaceFirst"/> off, this is what shows whether the previous frame
	/// is still in the buffer.
	/// </summary>
	/// <param name="width">The square's width in surface pixels.</param>
	/// <param name="height">The square's height in surface pixels.</param>
	/// <param name="x">Where its left edge is, in surface pixels.</param>
	/// <param name="y">Where its top edge is, in surface pixels.</param>
	/// <param name="color">The colour to paint it.</param>
	public void PaintOnlyASquare(int width, int height, int x, int y, Color color)
	{
		_bands.Clear();
		_hasGradient = false;
		SetTheSquare(width, height, x, y, color);
	}

	/// <summary>Paints one square over whatever else the handler already draws.</summary>
	/// <param name="width">The square's width in surface pixels.</param>
	/// <param name="height">The square's height in surface pixels.</param>
	/// <param name="x">Where its left edge is, in surface pixels.</param>
	/// <param name="y">Where its top edge is, in surface pixels.</param>
	/// <param name="color">The colour to paint it.</param>
	public void AlsoPaintASquare(int width, int height, int x, int y, Color color) =>
		SetTheSquare(width, height, x, y, color);

	/// <summary>A one-line description of what the handler has been told to paint.</summary>
	/// <returns>The description, for a failure message.</returns>
	public string DescribeTheHandler()
	{
		var subject = _hasGradient
			? "a gradient"
			: string.Create(CultureInfo.InvariantCulture, $"{_bands.Count} colour band(s)");
		var square = _hasMarker
			? string.Create(CultureInfo.InvariantCulture,
				$", and a {_markerWidth} by {_markerHeight} square at {_markerX}, {_markerY}")
			: string.Empty;
		var clearing = ClearsTheSurfaceFirst ? "clears the surface and paints " : "paints, without clearing, ";

		return clearing + subject + square;
	}

	private void SetTheSquare(int width, int height, int x, int y, Color color)
	{
		_markerWidth = width;
		_markerHeight = height;
		_markerX = x;
		_markerY = y;
		_markerColor = color;
		_hasMarker = true;
	}

	private void OnFixturePaintSurface(object? sender, SKPaintSurfaceEventArgs e)
	{
		var info = e.Info;
		var canvas = e.Surface.Canvas;

		PaintCount++;
		PaintedSurfaceSize = new SKSizeI(info.Width, info.Height);
		Record(PaintSurfaceEventName);

		if (ClearsTheSurfaceFirst)
		{
			// Transparent rather than a colour: what the handler does not paint is then the
			// panel showing through, which is what "paints nothing" has to look like.
			canvas.Clear(SKColors.Transparent);
		}

		if (_hasGradient)
		{
			PaintTheGradient(canvas, info);
		}
		else if (_bands.Count > 0)
		{
			PaintTheBands(canvas, info);
		}

		if (_hasMarker)
		{
			PaintTheSquare(canvas);
		}
	}

	private void PaintTheBands(SKCanvas canvas, SKImageInfo info)
	{
		using var paint = new SKPaint { IsAntialias = false };
		var edge = 0;

		for (var band = 0; band < _bands.Count; band++)
		{
			// The edges are computed from the far end of each band rather than by adding a
			// width, so that N bands cover every column of the surface exactly once however
			// badly N divides the width.
			var next = (int) Math.Round((double) info.Width * (band + 1) / _bands.Count,
				MidpointRounding.AwayFromZero);
			paint.Color = ToSkia(_bands[band]);
			canvas.DrawRect(SKRect.Create(edge, 0, next - edge, info.Height), paint);
			edge = next;
		}
	}

	private void PaintTheGradient(SKCanvas canvas, SKImageInfo info)
	{
		var end = _gradientIsVertical
			? new SKPoint(0, info.Height)
			: new SKPoint(info.Width, 0);

		using var shader = SKShader.CreateLinearGradient(
			new SKPoint(0, 0),
			end,
			[ToSkia(_gradientStart), ToSkia(_gradientEnd)],
			SKShaderTileMode.Clamp);
		using var paint = new SKPaint { Shader = shader, IsAntialias = false };
		canvas.DrawRect(SKRect.Create(0, 0, info.Width, info.Height), paint);
	}

	private void PaintTheSquare(SKCanvas canvas)
	{
		using var paint = new SKPaint { Color = ToSkia(_markerColor), IsAntialias = false };
		canvas.DrawRect(SKRect.Create(_markerX, _markerY, _markerWidth, _markerHeight), paint);
	}

	private void OnFixturePointerPressed(object sender, PointerRoutedEventArgs e)
	{
		LastPointerPosition = e.GetCurrentPoint(this).Position;
		Record(PointerPressedEventName);
	}

	private void Record(string eventName)
	{
		// The element factory names an element after it has been built, and the very first
		// paint - the one the base constructor asks for - happens before that. It paints
		// nothing (the element has no size yet), so there is nothing to count either.
		if (!string.IsNullOrEmpty(Name))
		{
			EventRecorder.Record(Name, eventName);
		}
	}

	private static SKColor ToSkia(Color color) => new(color.R, color.G, color.B, color.A);
}
