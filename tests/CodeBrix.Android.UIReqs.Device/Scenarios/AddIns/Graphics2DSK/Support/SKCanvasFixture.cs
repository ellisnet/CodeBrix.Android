using System;
using System.Globalization;
using CodeBrix.Platform.UI.Core.UIReqs.Support;
using CodeBrix.Platform.WinUI.Graphics2DSK;
using SkiaSharp;
using Size = Windows.Foundation.Size;

namespace CodeBrix.Platform.UI.AddIn.Graphics2DSK.UIReqs.Support;

/// <summary>
/// What every Graphics2DSK fixture element has in common: a drawing that is nothing but flat
/// stripes of the colours a scenario named, and the two facts a scenario asks the element about
/// afterwards - how often it has been asked to draw, and the area it was handed the last time.
/// <para>
/// The rules a fixture that draws inside the compositor's own painting session has to keep are
/// all here, in one place: it NEVER invalidates itself (an element that does is dirty forever,
/// the dispatcher never goes idle and every frame the harness asks for times out), it never
/// blocks or waits on anything inside the draw, and it allocates nothing that would have to be
/// disposed after the draw has returned. Every rectangle it paints is on whole pixels with
/// antialiasing off, so that a region assertion is about a fill rather than about the blend at
/// an edge.
/// </para>
/// </summary>
public abstract class SKCanvasFixture : SKCanvasElement
{
	private SKColor[] _stripes = [];

	/// <summary>
	/// How many times the framework has asked this element to draw itself since it was built.
	/// A requirement compares this - "at least once", "more than it was" - and never asks for an
	/// exact number: any recompose of the tree repaints the element, so the count grows for
	/// reasons a scenario did not ask for.
	/// </summary>
	public int RenderCount { get; private set; }

	/// <summary>
	/// The area the framework handed the most recent draw. This is what a scenario means by
	/// "the drawing area", and it is the fact that says whether the area the element is given
	/// to draw in is the size the layout arranged it at.
	/// </summary>
	public Size DrawnArea { get; private set; }

	/// <summary>
	/// The colours this element paints, as a feature file writes them: one name or one
	/// "#AARRGGBB" value per stripe, separated by commas, laid left to right across the whole
	/// drawing area. A stripe whose colour is fully transparent is not painted at all, which is
	/// how a scenario asks for a part of the area to be left exactly as the element found it.
	/// </summary>
	/// <param name="value">The comma-separated colours, as the feature file wrote them.</param>
	/// <exception cref="FormatException">The text names no colour, or names none at all.</exception>
	public void SetFill(string value)
	{
		var parts = (value ?? string.Empty)
			.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

		if (parts.Length == 0)
		{
			throw new FormatException(
				$"\"{value}\" names no colour to fill with. Write one colour, or several separated "
				+ "by commas, such as \"Red,Blue\".");
		}

		var stripes = new SKColor[parts.Length];
		for (var index = 0; index < parts.Length; index++)
		{
			var color = Colors.Parse(parts[index]);
			stripes[index] = new SKColor(color.R, color.G, color.B, color.A);
		}

		_stripes = stripes;
	}

	/// <summary>The colours this element is currently painting, as "#AARRGGBB" values.</summary>
	/// <returns>The stripes, in the order they are painted.</returns>
	public string DescribeFill() => _stripes.Length == 0
		? "nothing"
		: string.Join(", ", Array.ConvertAll(_stripes, stripe => stripe.ToString()));

	/// <summary>
	/// Paints the stripes the scenario asked for, and records what the framework handed over.
	/// Called by the framework on the UI thread, inside the compositor's painting session.
	/// </summary>
	/// <param name="canvas">The canvas to draw on, with the origin at this element's top left.</param>
	/// <param name="area">The area this element was arranged at.</param>
	protected override void RenderOverride(SKCanvas canvas, Size area)
	{
		RenderCount++;
		DrawnArea = area;

		if (_stripes.Length == 0)
		{
			return;
		}

		using var paint = new SKPaint { IsAntialias = false, Style = SKPaintStyle.Fill };
		var width = WholePixels(area.Width);
		var height = WholePixels(area.Height);
		if (width <= 0 || height <= 0)
		{
			return;
		}

		for (var index = 0; index < _stripes.Length; index++)
		{
			// A fully transparent stripe is skipped rather than painted: painting it would be a
			// no-op anyway, and skipping it is what a scenario about "the canvas is not cleared"
			// needs - the pixels under it stay exactly as the element found them.
			if (_stripes[index].Alpha == 0)
			{
				continue;
			}

			// Integer arithmetic, so the stripes tile the area exactly with no seam and no
			// rounded edge: stripe i runs from width*i/n to width*(i+1)/n.
			var left = (width * index) / _stripes.Length;
			var right = (width * (index + 1)) / _stripes.Length;
			paint.Color = _stripes[index];
			canvas.DrawRect(SKRect.Create(left, 0, right - left, height), paint);
		}
	}

	/// <summary>A dimension of the drawing area as whole device pixels.</summary>
	/// <param name="value">The dimension, in the logical pixels the framework hands over.</param>
	/// <returns>The dimension rounded to a whole number of pixels.</returns>
	protected static int WholePixels(double value) =>
		(int) Math.Round(value, MidpointRounding.AwayFromZero);

	/// <summary>The area of the most recent draw, as "w x h".</summary>
	/// <param name="area">The area to describe.</param>
	/// <returns>The description.</returns>
	protected internal static string DescribeArea(Size area) => string.Create(CultureInfo.InvariantCulture,
		$"{area.Width} x {area.Height}");
}
