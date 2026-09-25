// Region.cs
//
// Pinta.Brix drawing-layer region: a set of integer rectangles stored as
// y-banded x-intervals, mirroring the region API the upstream Pinta code used
// (invalidation tracking and flood-fill selection limits), including
// rectangle enumeration of the banded decomposition.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Pinta.Brix.Engine.Drawing;

public struct RectangleInt
{
	public int X;
	public int Y;
	public int Width;
	public int Height;

	public RectangleInt (int x, int y, int width, int height)
	{
		X = x;
		Y = y;
		Width = width;
		Height = height;
	}

	public readonly RectangleI ToRectangleI ()
		=> new (X, Y, Width, Height);
}

public sealed class Region : IDisposable
{
	private readonly record struct Band (int Y0, int Y1, int[] Intervals); // intervals: x0,x1 pairs

	private List<Band> bands = [];

	private Region ()
	{
	}

	public static Region Create ()
		=> new ();

	public static Region CreateRectangle (RectangleInt rect)
	{
		Region r = new ();
		if (rect.Width > 0 && rect.Height > 0)
			r.bands.Add (new Band (rect.Y, rect.Y + rect.Height, [rect.X, rect.X + rect.Width]));
		return r;
	}

	public void UnionRectangle (RectangleInt rect)
		=> Combine (CreateRectangle (rect), union: true, xor: false);

	public void Union (Region other)
		=> Combine (other, union: true, xor: false);

	public void Xor (Region other)
		=> Combine (other, union: false, xor: true);

	public void Intersect (Region other)
		=> Combine (other, union: false, xor: false);

	public bool ContainsPoint (int x, int y)
	{
		foreach (Band band in bands) {
			if (y < band.Y0 || y >= band.Y1)
				continue;
			int[] iv = band.Intervals;
			for (int i = 0; i < iv.Length; i += 2)
				if (x >= iv[i] && x < iv[i + 1])
					return true;
		}
		return false;
	}

	public int GetNumRectangles ()
		=> bands.Sum (b => b.Intervals.Length / 2);

	public void GetRectangle (int index, out RectangleInt rect)
	{
		foreach (Band band in bands) {
			int count = band.Intervals.Length / 2;
			if (index >= count) {
				index -= count;
				continue;
			}
			rect = new RectangleInt (
				band.Intervals[index * 2],
				band.Y0,
				band.Intervals[index * 2 + 1] - band.Intervals[index * 2],
				band.Y1 - band.Y0);
			return;
		}
		throw new ArgumentOutOfRangeException (nameof (index));
	}

	public RectangleInt GetExtents ()
	{
		if (bands.Count == 0)
			return new RectangleInt (0, 0, 0, 0);
		int y0 = bands[0].Y0;
		int y1 = bands[^1].Y1;
		int x0 = int.MaxValue, x1 = int.MinValue;
		foreach (Band band in bands) {
			x0 = Math.Min (x0, band.Intervals[0]);
			x1 = Math.Max (x1, band.Intervals[^1]);
		}
		return new RectangleInt (x0, y0, x1 - x0, y1 - y0);
	}

	private void Combine (Region other, bool union, bool xor)
	{
		// Sweep the union of band boundaries; per output band, combine the
		// two regions' x-intervals with the requested boolean operation.
		List<int> edges = [];
		foreach (Band b in bands) {
			edges.Add (b.Y0);
			edges.Add (b.Y1);
		}
		foreach (Band b in other.bands) {
			edges.Add (b.Y0);
			edges.Add (b.Y1);
		}
		if (edges.Count == 0)
			return;

		int[] ys = [.. edges.Distinct ().OrderBy (v => v)];
		List<Band> result = [];

		for (int i = 0; i < ys.Length - 1; i++) {
			int y0 = ys[i], y1 = ys[i + 1];
			int[] a = IntervalsAt (bands, y0);
			int[] b = IntervalsAt (other.bands, y0);
			int[] combined = CombineIntervals (a, b, union, xor);
			if (combined.Length == 0)
				continue;
			// Merge with the previous band when spans match to keep bands tall.
			if (result.Count > 0 && result[^1].Y1 == y0 && result[^1].Intervals.AsSpan ().SequenceEqual (combined)) {
				result[^1] = result[^1] with { Y1 = y1 };
			} else {
				result.Add (new Band (y0, y1, combined));
			}
		}

		bands = result;
	}

	private static int[] IntervalsAt (List<Band> source, int y)
	{
		foreach (Band band in source)
			if (y >= band.Y0 && y < band.Y1)
				return band.Intervals;
		return [];
	}

	private static int[] CombineIntervals (int[] a, int[] b, bool union, bool xor)
	{
		// Boolean combine of two sorted interval lists via boundary sweep.
		List<(int x, int deltaA, int deltaB)> events = [];
		for (int i = 0; i < a.Length; i += 2) {
			events.Add ((a[i], 1, 0));
			events.Add ((a[i + 1], -1, 0));
		}
		for (int i = 0; i < b.Length; i += 2) {
			events.Add ((b[i], 0, 1));
			events.Add ((b[i + 1], 0, -1));
		}
		events.Sort ((p, q) => p.x.CompareTo (q.x));

		List<int> result = [];
		int inA = 0, inB = 0;
		bool inside = false;
		int start = 0;

		foreach (var (x, dA, dB) in events) {
			bool wasInside = inside;
			inA += dA;
			inB += dB;
			inside = xor
				? (inA > 0) ^ (inB > 0)
				: union
					? inA > 0 || inB > 0
					: inA > 0 && inB > 0;

			if (!wasInside && inside) {
				start = x;
			} else if (wasInside && !inside) {
				if (result.Count > 0 && result[^1] == start)
					result[^1] = x; // adjacent intervals merge
				else {
					result.Add (start);
					result.Add (x);
				}
			}
		}

		return [.. result];
	}

	public void Dispose ()
	{
	}
}
