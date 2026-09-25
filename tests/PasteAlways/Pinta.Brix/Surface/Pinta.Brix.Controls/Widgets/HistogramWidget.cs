/////////////////////////////////////////////////////////////////////////////////
// Copyright (C) dotPDN LLC, Rick Brewster, Tom Jackson, and contributors.     //
// Portions Copyright (C) Microsoft Corporation. All Rights Reserved.          //
// See license-pdn.txt for full licensing and attribution details.             //
//                                                                             //
// Ported to Pinta by: Krzysztof Marecki <marecki.krzysztof@gmail.com>         //
/////////////////////////////////////////////////////////////////////////////////

// Additional code:
//
// HistogramWidget.cs
//
// Author:
//      Krzysztof Marecki <marecki.krzysztof@gmail.com>
//
// Copyright (c) 2010 Krzysztof Marecki
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
using Pinta.Brix.Engine;
using SkiaSharp;
using SkiaSharp.Views.Windows;

namespace Pinta.Brix.Controls;
//was previously: namespace Pinta.Gui.Widgets;

/// <summary>
/// Draws an RGB histogram as one smoothed polygon per channel; used by the
/// Levels dialog for its input and output histograms. Upstream is a
/// Gtk.DrawingArea; here it is a Skia-drawn control.
/// </summary>
public sealed class HistogramWidget : SKXamlCanvas
{
	private readonly bool[] selected = [true, true, true];

	public HistogramWidget ()
	{
		PaintSurface += OnPaintSurface;
		Histogram.HistogramChanged += (_, _) => Invalidate ();
	}

	public bool FlipHorizontal { get; set; }

	public bool FlipVertical { get; set; }

	public HistogramRgb Histogram { get; private set; } = new ();

	public void ResetHistogram ()
	{
		Histogram = new HistogramRgb ();
		Histogram.HistogramChanged += (_, _) => Invalidate ();
		Invalidate ();
	}

	public void SetSelected (int channel, bool val)
	{
		selected[channel] = val;
		Invalidate ();
	}

	private static SKPoint CheckedPoint (SKRect rect, SKPoint point)
	{
		float x = Math.Clamp (point.X, rect.Left, rect.Right);
		float y = Math.Clamp (point.Y, rect.Top, rect.Bottom);
		return new SKPoint (x, y);
	}

	private void DrawChannel (SKCanvas canvas, SKRect rect, ColorBgra color, int channel, long max)
	{
		int l = (int) rect.Left;
		int t = (int) rect.Top;
		int r = (int) rect.Right;
		int b = (int) rect.Bottom;

		int entryCount = Histogram.Entries;
		var hist = Histogram.HistogramValues[channel];

		++max;

		if (FlipHorizontal)
			(l, r) = (r, l);

		if (!FlipVertical)
			(t, b) = (b, t);

		var points = new SKPoint[entryCount + 2];

		points[entryCount] = new SKPoint (
			(float) Mathematics.Lerp<double> (l, r, -1),
			(float) Mathematics.Lerp<double> (t, b, 20));
		points[entryCount + 1] = new SKPoint (
			(float) Mathematics.Lerp<double> (l, r, -1),
			(float) Mathematics.Lerp<double> (b, t, 20));

		for (int i = 0; i < entryCount; i += entryCount - 1) {
			points[i] = CheckedPoint (rect, new SKPoint (
				(float) Mathematics.Lerp<double> (l, r, hist[i] / (float) max),
				(float) Mathematics.Lerp<double> (t, b, i / (float) entryCount)));
		}

		long sum3 = hist[0] + hist[1];

		for (int i = 1; i < entryCount - 1; ++i) {
			sum3 += hist[i + 1];

			points[i] = CheckedPoint (rect, new SKPoint (
				(float) Mathematics.Lerp<double> (l, r, sum3 / (float) (max * 3.1f)),
				(float) Mathematics.Lerp<double> (t, b, i / (float) entryCount)));

			sum3 -= hist[i - 1];
		}

		byte intensity = selected[channel] ? (byte) 96 : (byte) 32;
		ColorBgra penColor = ColorBgra.Lerp (ColorBgra.Black, color, intensity);
		ColorBgra brushColor = color.NewAlpha (intensity);

		SKPathBuilder builder = new ();
		builder.MoveTo (points[0]);
		for (int i = 1; i < points.Length; i++)
			builder.LineTo (points[i]);
		builder.Close ();
		using SKPath path = builder.Snapshot ();

		canvas.Save ();
		canvas.ClipRect (rect);

		using SKPaint fill = new () {
			Color = new SKColor (brushColor.R, brushColor.G, brushColor.B, brushColor.A),
			Style = SKPaintStyle.Fill,
			IsAntialias = true,
		};
		canvas.DrawPath (path, fill);

		using SKPaint stroke = new () {
			Color = new SKColor (penColor.R, penColor.G, penColor.B),
			Style = SKPaintStyle.Stroke,
			StrokeWidth = 1,
			StrokeCap = SKStrokeCap.Square,
			IsAntialias = true,
		};
		canvas.DrawPath (path, stroke);

		canvas.Restore ();
	}

	private void OnPaintSurface (object? sender, SKPaintSurfaceEventArgs e)
	{
		SKCanvas canvas = e.Surface.Canvas;
		canvas.Clear (SKColors.Transparent);

		if (ActualWidth <= 0 || ActualHeight <= 0)
			return;

		//The surface is physical pixels; draw in the element's logical space.
		canvas.Scale (e.Info.Width / (float) ActualWidth, e.Info.Height / (float) ActualHeight);

		SKRect rect = SKRect.Create (0, 0, (float) ActualWidth, (float) ActualHeight);

		long max = Histogram.GetMax ();
		int channelCount = Histogram.Channels;

		for (int i = 0; i < channelCount; ++i)
			DrawChannel (canvas, rect, Histogram.GetVisualColor (i), i, max);
	}
}
