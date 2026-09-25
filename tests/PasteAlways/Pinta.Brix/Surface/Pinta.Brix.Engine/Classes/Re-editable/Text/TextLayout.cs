//
// TextLayout.cs
//
// Author:
//       Cameron White <cameronwhite91@gmail.com>
//
// Copyright (c) 2015 Cameron White
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

// Pinta.Brix note: upstream wrapped a Pango.Layout; this wraps the
// CodeBrix.Platform text-layout add-in's TextLayoutResult with the same
// public shape, so TextTool ports largely unchanged. Indices are .NET char
// indices (G3), alignment is applied with a measure-then-align second pass
// (G1), and drawing goes through the outline path so the tool's Cairo-style
// Context can fill, stroke and clip it uniformly (G4's fallback for both
// antialiasing modes).

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using CodeBrix.Platform.UI.TextLayout;
using SkiaSharp;

//was previously: namespace Pinta.Core;
namespace Pinta.Brix.Engine;

public sealed class TextLayout
{
	private TextEngine engine = null!; // NRT - Engine is always assigned before use, as upstream

	private TextLayoutResult? result;
	private bool is_empty;

	public TextEngine Engine {
		get => engine;
		set {
			if (engine != null)
				engine.Modified -= OnEngineModified;
			engine = value;
			engine.Modified += OnEngineModified;
			OnEngineModified (this, EventArgs.Empty);
		}
	}

	/// <summary>
	/// The cached layout, rebuilt whenever the engine reports a modification.
	/// Empty text is laid out as a single space so caret and line metrics
	/// stay meaningful; <see cref="is_empty"/> remembers the truth.
	/// </summary>
	private TextLayoutResult Result => result ??= BuildResult ();

	public int FontHeight => (int) Math.Ceiling (Result.LineHeight); // G6: line height stands in for the caret height

	public ImmutableArray<RectangleI> GetSelectionRectangles ()
	{
		if (is_empty)
			return [];

		var regions = engine.SelectionRegions;
		var rects = ImmutableArray.CreateBuilder<RectangleI> ();

		foreach (var region in regions) {
			int start = engine.PositionToCharIndex (region.Key);
			int end = engine.PositionToCharIndex (region.Value);
			if (end <= start)
				continue;

			foreach (SKRect rect in Result.GetSelectionRects (start, end - start)) {
				rects.Add (new RectangleI (
					(int) rect.Left + engine.Origin.X,
					(int) rect.Top + engine.Origin.Y,
					(int) Math.Ceiling (rect.Width),
					(int) Math.Ceiling (rect.Height)));
			}
		}

		return rects.ToImmutable ();
	}

	public RectangleI GetCursorLocation ()
	{
		int index = is_empty ? 0 : engine.PositionToCharIndex (engine.CurrentPosition);
		SKRect caret = Result.GetCaretRect (index, 1f);

		return new (
			(int) caret.Left + engine.Origin.X,
			(int) caret.Top + engine.Origin.Y,
			Math.Max (1, (int) caret.Width),
			(int) Math.Ceiling (caret.Height));
	}

	public RectangleI GetLayoutBounds ()
	{
		// G5: logical size rather than ink extents; upstream already replaced
		// Pango's height with cursor height times line count.
		int width = is_empty ? 0 : (int) Math.Ceiling (Result.Size.Width);
		int height = FontHeight * Math.Max (1, engine.LineCount);

		return new (engine.Origin.X, engine.Origin.Y, width, height);
	}

	public TextPosition PointToTextPosition (PointI point)
	{
		if (is_empty)
			return new TextPosition (0, 0);

		int index = Result.GetNearestIndexAt (new SKPoint (
			point.X - engine.Origin.X,
			point.Y - engine.Origin.Y));

		return engine.CharIndexToPosition (Math.Clamp (index, 0, Result.Text.Length));
	}

	public PointI TextPositionToPoint (TextPosition p)
	{
		int index = is_empty ? 0 : engine.PositionToCharIndex (p);
		SKRect rect = Result.GetCaretRect (index, 1f);

		return new PointI (
			(int) rect.Left + engine.Origin.X,
			(int) rect.Top + engine.Origin.Y);
	}

	/// <summary>
	/// The text outline as a path in canvas coordinates (already offset by
	/// the engine's origin). Fill it for the text body, stroke it for the
	/// outline style; an empty path when there is no text.
	/// </summary>
	public Drawing.Path GetOutline ()
	{
		SKPathBuilder builder = new ();

		if (!is_empty) {
			using SKPath outline = Result.GetOutlinePath ();
			builder.AddPath (outline, SKMatrix.CreateTranslation (engine.Origin.X, engine.Origin.Y));
		}

		return new Drawing.Path (builder.Snapshot ());
	}

	/// <summary>
	/// Underline rules, one rectangle per line, in canvas coordinates. The
	/// add-in has no text-decoration concept (G2), so the rule geometry is
	/// derived from the line metrics here.
	/// </summary>
	public ImmutableArray<RectangleD> GetUnderlineRectangles ()
	{
		if (is_empty || !engine.Underline)
			return [];

		var rects = ImmutableArray.CreateBuilder<RectangleD> ();

		double thickness = Math.Max (1.0, engine.Font.Size / 14.0);
		int index = 0;

		foreach (string line in engine.Lines) {
			if (line.Length > 0) {
				foreach (SKRect rect in Result.GetSelectionRects (index, line.Length)) {
					rects.Add (new RectangleD (
						rect.Left + engine.Origin.X,
						rect.Bottom - 2 * thickness + engine.Origin.Y,
						rect.Width,
						thickness));
				}
			}
			index += line.Length + 1;
		}

		return rects.ToImmutable ();
	}

	/// <summary>
	/// The engine's text as the layout sees it: lines joined with a single '\n'
	/// so every line break is exactly one char, matching the "+1 for the
	/// newline" that PositionToCharIndex and CharIndexToPosition assume.
	/// TextEngine.ToString() joins with Environment.NewLine, which is "\r\n" on
	/// Windows, and that put every line after the first out of step with the
	/// layout's char indices.
	/// </summary>
	private string LayoutText => string.Join ('\n', engine.Lines);

	private TextLayoutResult BuildResult ()
	{
		string text = LayoutText;
		is_empty = text.Length == 0;

		FontDescription font = engine.Font;

		// G8: clamp the weight onto the add-in's 100..900 scale.
		TextFontWeight weight = (TextFontWeight) Math.Clamp (font.Weight / 100 * 100, 100, 900);

		TextRunDescriptor run = new (
			is_empty ? " " : text,
			font.Family,
			(float) Math.Max (1.0, font.Size),
			weight,
			font.Italic ? TextFontStyle.Italic : TextFontStyle.Normal);

		// G1: alignment has no effect without a width, and Pinta aligns
		// without wrapping - so measure the natural width first, then lay out
		// again at that width with the wanted alignment.
		TextAlign alignment = engine.Alignment switch {
			TextAlignment.Center => TextAlign.Center,
			TextAlignment.Right => TextAlign.Right,
			_ => TextAlign.Left,
		};

		TextLayoutResult first = TextLayoutEngine.Layout ([run], null);

		if (alignment == TextAlign.Left || is_empty)
			return first;

		float width = first.Size.Width;
		first.Dispose ();

		return TextLayoutEngine.Layout ([run], new TextLayoutOptions {
			MaxWidth = width,
			Alignment = alignment,
		});
	}

	private void OnEngineModified (object? sender, EventArgs e)
	{
		result?.Dispose ();
		result = null;
	}
}

/// <summary>
/// Enumerates the system's installed font families for the text tool's font
/// picker (G10 - the layout add-in resolves whatever family it is handed;
/// enumeration is answered by SkiaSharp directly).
/// </summary>
public static class SystemFonts
{
	private static string[]? families;

	public static IReadOnlyList<string> Families {
		get {
			if (families is null) {
				families = SKFontManager.Default.GetFontFamilies ();
				Array.Sort (families, StringComparer.OrdinalIgnoreCase);
			}
			return families;
		}
	}
}
