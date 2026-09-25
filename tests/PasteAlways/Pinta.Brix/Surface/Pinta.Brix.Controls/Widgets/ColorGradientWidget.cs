//
// ColorGradientWidget.cs
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
using System.Linq;
using Microsoft.UI.Xaml.Input;
using Pinta.Brix.Engine;
using SkiaSharp;
using SkiaSharp.Views.Windows;

namespace Pinta.Brix.Controls;
//was previously: namespace Pinta.Gui.Widgets;

/// <summary>
/// A vertical colour gradient with two or three draggable value triangles;
/// the Levels dialog uses one for the input range and one for the output
/// range. Upstream is a Gtk.DrawingArea; here it is a Skia-drawn control.
/// </summary>
public sealed class ColorGradientWidget : SKXamlCanvas
{
	private const double X_pad = 0.15; // gradient horizontal padding
	private const double Y_pad = 0.03; // gradient vertical padding

	private double[] vals;
	private PointI last_mouse_pos = new (0, 0);
	private bool pointer_down;

	public ColorGradientWidget (int count)
	{
		if (count < 2 || count > 3)
			throw new ArgumentOutOfRangeException (nameof (count), count, "Count must be 2 or 3");

		vals = new double[count];
		int step = 256 / (count - 1);
		for (int i = 0; i < count; i++)
			vals[i] = i * step - ((i != 0) ? 1 : 0);

		ValueIndex = -1;

		PaintSurface += OnPaintSurface;
		PointerMoved += OnPointerMovedHandler;
		PointerPressed += OnPointerPressedHandler;
		PointerReleased += OnPointerReleasedHandler;
		PointerExited += OnPointerExitedHandler;
	}

	public int Count => vals.Length;

	private SKColor max_color = SKColors.White;
	public SKColor MaxColor {
		get => max_color;
		set {
			max_color = value;
			Invalidate ();
		}
	}

	public int ValueIndex { get; private set; }

	/// <summary>Raised when a value triangle starts being dragged.</summary>
	public event EventHandler? DragBegun;

	/// <summary>Raised when a value triangle stops being dragged.</summary>
	public event EventHandler? DragEnded;

	public event EventHandler<IndexEventArgs>? ValueChanged;

	public int GetValue (int i)
		=> (int) vals[i];

	public void SetValue (int i, int val)
	{
		if ((int) vals[i] == val)
			return;
		vals[i] = val;
		OnValueChanged (i);
		Invalidate ();
	}

	private SKRect Allocation
		=> SKRect.Create (0, 0, (float) ActualWidth, (float) ActualHeight);

	private SKRect GradientRectangle {
		get {
			SKRect rect = Allocation;
			return SKRect.Create (
				(float) (rect.Left + X_pad * rect.Width),
				(float) (rect.Top + Y_pad * rect.Height),
				(float) ((1 - 2 * X_pad) * rect.Width),
				(float) ((1 - 2 * Y_pad) * rect.Height));
		}
	}

	private double GetYFromValue (double val)
	{
		SKRect rect = GradientRectangle;
		SKRect all = Allocation;
		return all.Top + Y_pad * all.Height + rect.Height * (255 - val) / 255;
	}

	private double NormalizeY (int index, double py)
	{
		SKRect rect = GradientRectangle;

		var yvals =
			vals
			.Select (GetYFromValue)
			.Concat ([(double) rect.Top, rect.Top + rect.Height])
			.OrderByDescending (v => v)
			.ToArray ();

		index++;

		if (py >= yvals[index - 1])
			return yvals[index - 1];
		else if (py < yvals[index + 1])
			return yvals[index + 1];
		else
			return py;
	}

	private int GetValueFromY (double py)
	{
		SKRect rect = GradientRectangle;
		SKRect all = Allocation;
		double y = py - (all.Top + Y_pad * all.Height);
		return (int) (255 * (rect.Height - y) / rect.Height);
	}

	private int FindValueIndex (int y)
	{
		if (ValueIndex != -1)
			return ValueIndex;

		var yvals = vals.Select (GetYFromValue).ToArray ();
		int count = Count - 1;

		for (int i = 0; i < count; i++) {
			double y1 = yvals[i];
			double y2 = yvals[i + 1];
			double h = (y1 - y2) / 2;

			// pointer is below the lowest value triangle
			if (i == 0 && y1 < y)
				return i;

			// pointer is above the highest value triangle
			if (i == (count - 1) && y2 > y)
				return i + 1;

			// pointer is outside i and i + 1 value triangles
			if (!(y1 >= y && y >= y2))
				continue;

			// pointer is closer to lower value triangle
			if (y1 - y <= h)
				return i;

			// pointer is closer to higher value triangle
			if (y - y2 <= h)
				return i + 1;
		}

		return -1;
	}

	private void OnPointerPressedHandler (object sender, PointerRoutedEventArgs e)
	{
		var position = e.GetCurrentPoint (this).Position;
		int index = FindValueIndex ((int) position.Y);

		if (index != -1)
			ValueIndex = index;

		pointer_down = true;
		CapturePointer (e.Pointer);
		DragBegun?.Invoke (this, EventArgs.Empty);
		e.Handled = true;
	}

	private void OnPointerMovedHandler (object sender, PointerRoutedEventArgs e)
	{
		var position = e.GetCurrentPoint (this).Position;

		if (pointer_down && ValueIndex != -1) {
			PointI p = new ((int) position.X, (int) NormalizeY (ValueIndex, position.Y));
			vals[ValueIndex] = GetValueFromY (p.Y);
			OnValueChanged (ValueIndex);
			last_mouse_pos = p;
			Invalidate ();
			return;
		}

		int index = FindValueIndex ((int) position.Y);
		last_mouse_pos = new PointI ((int) position.X, (int) NormalizeY (index, position.Y));

		// to avoid unnecessary costly redrawing
		if (index != -1)
			Invalidate ();
	}

	private void OnPointerReleasedHandler (object sender, PointerRoutedEventArgs e)
	{
		pointer_down = false;
		ValueIndex = -1;
		ReleasePointerCapture (e.Pointer);
		DragEnded?.Invoke (this, EventArgs.Empty);
		Invalidate ();
	}

	private void OnPointerExitedHandler (object sender, PointerRoutedEventArgs e)
	{
		if (!pointer_down)
			ValueIndex = -1;
		Invalidate ();
	}

	private void DrawGradient (SKCanvas canvas)
	{
		SKRect rect = GradientRectangle;

		using SKShader shader = SKShader.CreateLinearGradient (
			new SKPoint (rect.Left, rect.Top),
			new SKPoint (rect.Left, rect.Bottom),
			[max_color, SKColors.Black],
			null,
			SKShaderTileMode.Clamp);

		using SKPaint paint = new () { Shader = shader };
		canvas.DrawRect (rect, paint);
	}

	private void DrawTriangles (SKCanvas canvas)
	{
		SKColor hoverColor = new (0xE6, 0xE6, 0xE6);
		SKColor inactiveColor = hoverColor.WithAlpha (0x80);

		int px = last_mouse_pos.X;
		int py = last_mouse_pos.Y;

		SKRect rect = GradientRectangle;
		SKRect all = Allocation;

		int index = FindValueIndex (py);

		for (int i = 0; i < Count; i++) {
			double val = vals[i];
			float y = (float) GetYFromValue (val);
			bool hover = (index == i) && (all.Contains (px, py) || ValueIndex != -1);
			SKColor color = hover ? hoverColor : inactiveColor;

			using SKPaint paint = new () {
				Color = color,
				Style = SKPaintStyle.Fill,
				IsAntialias = true,
			};

			// left triangle
			SKPathBuilder left = new ();
			left.MoveTo (rect.Left, y);
			left.LineTo ((float) (rect.Left - X_pad * rect.Width), (float) (y + Y_pad * rect.Height));
			left.LineTo ((float) (rect.Left - X_pad * rect.Width), (float) (y - Y_pad * rect.Height));
			left.Close ();
			using SKPath leftPath = left.Snapshot ();
			canvas.DrawPath (leftPath, paint);

			// right triangle
			SKPathBuilder right = new ();
			right.MoveTo (rect.Right, y);
			right.LineTo ((float) (rect.Right + X_pad * rect.Width), (float) (y + Y_pad * rect.Height));
			right.LineTo ((float) (rect.Right + X_pad * rect.Width), (float) (y - Y_pad * rect.Height));
			right.Close ();
			using SKPath rightPath = right.Snapshot ();
			canvas.DrawPath (rightPath, paint);
		}
	}

	private void OnPaintSurface (object? sender, SKPaintSurfaceEventArgs e)
	{
		SKCanvas canvas = e.Surface.Canvas;
		canvas.Clear (SKColors.Transparent);

		if (ActualWidth <= 0 || ActualHeight <= 0)
			return;

		//The surface is physical pixels; draw in the element's logical space.
		canvas.Scale (e.Info.Width / (float) ActualWidth, e.Info.Height / (float) ActualHeight);

		DrawGradient (canvas);
		DrawTriangles (canvas);
	}

	private void OnValueChanged (int index)
		=> ValueChanged?.Invoke (this, new IndexEventArgs (index));
}
