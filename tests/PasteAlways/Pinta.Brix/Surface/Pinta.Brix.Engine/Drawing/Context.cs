// Context.cs
//
// Pinta.Brix drawing-layer context: mirrors the immediate-mode vector API the
// upstream Pinta code drew with (paths, sources, operators, clipping, save/
// restore), rendered with SkiaSharp into an ImageSurface's pixel memory.
//
// Fidelity notes:
// - Path coordinates are transformed by the current matrix at verb time and
//   stored in device space, matching the original API's semantics.
// - Arcs are flattened to cubic splines in user space before transforming,
//   so they remain correct under any current matrix.
// - Stroke width and dashes are scaled by the current matrix's mean scale
//   factor; non-uniform-scale stroking (elliptical pens) is approximated.

using System;
using System.Collections.Generic;
using SkiaSharp;

namespace Pinta.Brix.Engine.Drawing;

public sealed class Context : IDisposable
{
	private sealed class State
	{
		public SKMatrix Ctm = SKMatrix.Identity;
		public object? Source; // Color boxed, or Pattern (surface source is a SurfacePattern)
		public double LineWidthValue = 2.0;
		public LineCap LineCapValue = LineCap.Butt;
		public LineJoin LineJoinValue = LineJoin.Miter;
		public Operator OperatorValue = Operator.Over;
		public FillRule FillRuleValue = FillRule.Winding;
		public Antialias AntialiasValue = Antialias.Default;
		public double[]? Dashes;
		public double DashOffset;

		public State Clone () => (State) MemberwiseClone ();
	}

	private readonly ImageSurface surface;
	private readonly SKCanvas canvas;
	private readonly Stack<State> saved = new ();
	private State state = new ();

	private readonly SKPathBuilder pathBuilder = new ();
	private SKPath? pathSnapshot;                               // cache, see CurrentPath ()
	private bool pathSnapshotStale = true;
	private SKPathFillType pathFillType = SKPathFillType.Winding;
	private bool hasCurrentPoint;
	private SKPoint currentPointDevice;     // current point in device space
	private SKPoint subpathStartDevice;     // where ClosePath returns to

	public Context (ImageSurface surface)
	{
		this.surface = surface;
		canvas = new SKCanvas (surface.Bitmap);
		state.Source = new Color (0, 0, 0);
	}

	// ---- graphics state ----------------------------------------------------

	public double LineWidth {
		get => state.LineWidthValue;
		set => state.LineWidthValue = value;
	}

	public LineCap LineCap {
		get => state.LineCapValue;
		set => state.LineCapValue = value;
	}

	public LineJoin LineJoin {
		get => state.LineJoinValue;
		set => state.LineJoinValue = value;
	}

	public Operator Operator {
		get => state.OperatorValue;
		set => state.OperatorValue = value;
	}

	public FillRule FillRule {
		get => state.FillRuleValue;
		set => state.FillRuleValue = value;
	}

	public Antialias Antialias {
		get => state.AntialiasValue;
		set => state.AntialiasValue = value;
	}

	public void SetDash (double[] dashes, double offset)
	{
		state.Dashes = dashes.Length == 0 ? null : dashes;
		state.DashOffset = offset;
	}

	public void Save ()
	{
		canvas.Save (); // preserves the clip
		saved.Push (state.Clone ());
	}

	public void Restore ()
	{
		canvas.Restore ();
		if (saved.Count > 0)
			state = saved.Pop ();
	}

	// ---- transforms --------------------------------------------------------

	public void Translate (double tx, double ty)
		=> state.Ctm = state.Ctm.PreConcat (SKMatrix.CreateTranslation ((float) tx, (float) ty));

	public void Scale (double sx, double sy)
		=> state.Ctm = state.Ctm.PreConcat (SKMatrix.CreateScale ((float) sx, (float) sy));

	public void Rotate (double radians)
		=> state.Ctm = state.Ctm.PreConcat (SKMatrix.CreateRotation ((float) radians));

	public void Transform (Matrix matrix)
		=> state.Ctm = state.Ctm.PreConcat (matrix.ToSKMatrix ());

	public void SetMatrix (Matrix matrix)
		=> state.Ctm = matrix.ToSKMatrix ();

	public Matrix GetMatrix ()
		=> Matrix.FromSKMatrix (state.Ctm);

	public void IdentityMatrix ()
		=> state.Ctm = SKMatrix.Identity;

	// ---- sources -----------------------------------------------------------

	public void SetSourceColor (Color color)
		=> state.Source = color;

	public void SetSourceRgb (double r, double g, double b)
		=> state.Source = new Color (r, g, b);

	public void SetSourceRgba (double r, double g, double b, double a)
		=> state.Source = new Color (r, g, b, a);

	public void SetSource (Pattern pattern)
		=> state.Source = pattern;

	public void SetSourceSurface (Surface source, int x = 0, int y = 0)
		=> SetSourceSurface (source, (double) x, y);

	public void SetSourceSurface (Surface source, double x, double y)
	{
		SurfacePattern pattern = new (source);
		pattern.Matrix.Translate (-x, -y); // user space -> pattern space
		state.Source = pattern;
	}

	// ---- path construction -------------------------------------------------

	private SKPoint ToDevice (double x, double y)
		=> state.Ctm.MapPoint (new SKPoint ((float) x, (float) y));

	/// <summary>
	/// The path built so far. SKPathBuilder is write-only, so painting and
	/// extents go through a snapshot that is re-taken only after the path
	/// changes; the snapshot is detached, so later builder edits cannot
	/// disturb a path already handed to the canvas.
	/// </summary>
	private SKPath CurrentPath ()
	{
		if (pathSnapshotStale || pathSnapshot is null) {
			pathSnapshot?.Dispose ();
			pathSnapshot = pathBuilder.Snapshot ();
			pathSnapshotStale = false;
		}
		return pathSnapshot;
	}

	public void NewPath ()
	{
		pathBuilder.Reset ();
		pathFillType = SKPathFillType.Winding; // Reset () restores the default
		pathSnapshotStale = true;
		hasCurrentPoint = false;
	}

	public void MoveTo (double x, double y)
	{
		SKPoint p = ToDevice (x, y);
		pathBuilder.MoveTo (p);
		pathSnapshotStale = true;
		currentPointDevice = subpathStartDevice = p;
		hasCurrentPoint = true;
	}

	public void MoveTo (PointD point)
		=> MoveTo (point.X, point.Y);

	public void LineTo (double x, double y)
	{
		SKPoint p = ToDevice (x, y);
		if (!hasCurrentPoint) {
			pathBuilder.MoveTo (p);
			subpathStartDevice = p;
		} else {
			pathBuilder.LineTo (p);
		}
		pathSnapshotStale = true;
		currentPointDevice = p;
		hasCurrentPoint = true;
	}

	public void LineTo (PointD point)
		=> LineTo (point.X, point.Y);

	public void CurveTo (double x1, double y1, double x2, double y2, double x3, double y3)
	{
		SKPoint c1 = ToDevice (x1, y1);
		SKPoint c2 = ToDevice (x2, y2);
		SKPoint p = ToDevice (x3, y3);
		if (!hasCurrentPoint) {
			pathBuilder.MoveTo (c1);
			subpathStartDevice = c1;
		}
		pathBuilder.CubicTo (c1, c2, p);
		pathSnapshotStale = true;
		currentPointDevice = p;
		hasCurrentPoint = true;
	}

	public void CurveTo (PointD c1, PointD c2, PointD end)
		=> CurveTo (c1.X, c1.Y, c2.X, c2.Y, end.X, end.Y);

	public void Rectangle (double x, double y, double width, double height)
	{
		MoveTo (x, y);
		LineTo (x + width, y);
		LineTo (x + width, y + height);
		LineTo (x, y + height);
		ClosePath ();
	}

	public void ClosePath ()
	{
		if (!hasCurrentPoint)
			return;
		pathBuilder.Close ();
		pathSnapshotStale = true;
		currentPointDevice = subpathStartDevice;
	}

	/// <summary>
	/// Adds a circular arc in user space (angles from the positive X axis
	/// toward positive Y, i.e. clockwise on screen), connected by a line from
	/// the current point, matching the original API's semantics.
	/// </summary>
	public void Arc (double cx, double cy, double radius, double angle1, double angle2)
		=> ArcInternal (cx, cy, radius, angle1, angle2, negative: false);

	public void ArcNegative (double cx, double cy, double radius, double angle1, double angle2)
		=> ArcInternal (cx, cy, radius, angle1, angle2, negative: true);

	private void ArcInternal (double cx, double cy, double radius, double angle1, double angle2, bool negative)
	{
		if (negative) {
			while (angle2 > angle1)
				angle2 -= 2 * Math.PI;
		} else {
			while (angle2 < angle1)
				angle2 += 2 * Math.PI;
		}

		double startX = cx + radius * Math.Cos (angle1);
		double startY = cy + radius * Math.Sin (angle1);
		LineTo (startX, startY); // MoveTo when there is no current point

		// Flatten into <= 90-degree cubic segments in user space, then let
		// LineTo/CurveTo apply the current matrix per control point.
		double total = angle2 - angle1;
		int segments = Math.Max (1, (int) Math.Ceiling (Math.Abs (total) / (Math.PI / 2)));
		double step = total / segments;

		for (int i = 0; i < segments; i++) {
			double a = angle1 + i * step;
			double b = a + step;
			// Standard cubic approximation of a circular arc segment.
			double k = 4.0 / 3.0 * Math.Tan ((b - a) / 4);

			double cosA = Math.Cos (a), sinA = Math.Sin (a);
			double cosB = Math.Cos (b), sinB = Math.Sin (b);

			double c1x = cx + radius * (cosA - k * sinA);
			double c1y = cy + radius * (sinA + k * cosA);
			double c2x = cx + radius * (cosB + k * sinB);
			double c2y = cy + radius * (sinB - k * cosB);
			double ex = cx + radius * cosB;
			double ey = cy + radius * sinB;

			CurveTo (c1x, c1y, c2x, c2y, ex, ey);
		}
	}

	public void AppendPath (Path other)
	{
		pathBuilder.AddPath (other.SkPath, SKPathAddMode.Append);
		pathSnapshotStale = true;
		if (other.SkPath.PointCount > 0) {
			currentPointDevice = other.SkPath.LastPoint;
			hasCurrentPoint = true;
		}
	}

	public Path CopyPath ()
		=> new (pathBuilder.Snapshot ());

	public bool HasCurrentPoint => hasCurrentPoint;

	public void GetCurrentPoint (out double x, out double y)
	{
		PointD p = GetCurrentPoint ();
		x = p.X;
		y = p.Y;
	}

	public PointD GetCurrentPoint ()
	{
		if (!hasCurrentPoint)
			return new PointD (0, 0);
		if (!state.Ctm.TryInvert (out SKMatrix inverse))
			return new PointD (currentPointDevice.X, currentPointDevice.Y);
		SKPoint user = inverse.MapPoint (currentPointDevice);
		return new PointD (user.X, user.Y);
	}

	// ---- painting ----------------------------------------------------------

	private double MeanScale ()
	{
		double det = Math.Abs ((double) state.Ctm.ScaleX * state.Ctm.ScaleY - (double) state.Ctm.SkewX * state.Ctm.SkewY);
		return Math.Sqrt (det);
	}

	private SKPaint CreatePaint (bool alphaOverride = false, double alpha = 1.0)
	{
		SKPaint paint = new () {
			BlendMode = MapOperator (state.OperatorValue),
			IsAntialias = state.AntialiasValue != Antialias.None,
		};

		switch (state.Source) {
			case Color color:
				double a = alphaOverride ? color.A * alpha : color.A;
				paint.Color = new SKColor (
					(byte) Math.Round (Math.Clamp (color.R, 0, 1) * 255),
					(byte) Math.Round (Math.Clamp (color.G, 0, 1) * 255),
					(byte) Math.Round (Math.Clamp (color.B, 0, 1) * 255),
					(byte) Math.Round (Math.Clamp (a, 0, 1) * 255));
				break;
			case Pattern pattern:
				// Shader output is in user space; concatenate the current
				// matrix so it lands in device space where we draw.
				paint.Shader = pattern.ToShader ().WithLocalMatrix (state.Ctm);
				if (alphaOverride)
					paint.Color = SKColors.White.WithAlpha ((byte) Math.Round (Math.Clamp (alpha, 0, 1) * 255));
				break;
		}

		return paint;
	}

	private SKPaint CreateStrokePaint ()
	{
		SKPaint paint = CreatePaint ();
		double scale = MeanScale ();
		paint.Style = SKPaintStyle.Stroke;
		paint.StrokeWidth = (float) (state.LineWidthValue * scale);
		paint.StrokeCap = state.LineCapValue switch {
			LineCap.Round => SKStrokeCap.Round,
			LineCap.Square => SKStrokeCap.Square,
			_ => SKStrokeCap.Butt,
		};
		paint.StrokeJoin = state.LineJoinValue switch {
			LineJoin.Round => SKStrokeJoin.Round,
			LineJoin.Bevel => SKStrokeJoin.Bevel,
			_ => SKStrokeJoin.Miter,
		};
		paint.StrokeMiter = 10f;

		if (state.Dashes is { } dashes) {
			// Skia needs an even-length interval array; an odd-length dash
			// pattern repeats to become on/off pairs, same as the original.
			int n = dashes.Length % 2 == 0 ? dashes.Length : dashes.Length * 2;
			float[] intervals = new float[n];
			for (int i = 0; i < n; i++)
				intervals[i] = (float) (dashes[i % dashes.Length] * scale);
			paint.PathEffect = SKPathEffect.CreateDash (intervals, (float) (state.DashOffset * scale));
		}

		return paint;
	}

	private SKPath PreparedFillPath ()
	{
		SKPathFillType wanted = state.FillRuleValue == FillRule.EvenOdd
			? SKPathFillType.EvenOdd
			: SKPathFillType.Winding;
		if (pathFillType != wanted) {
			pathBuilder.FillType = wanted;
			pathFillType = wanted;
			pathSnapshotStale = true;
		}
		return CurrentPath ();
	}

	public void Fill ()
	{
		FillPreserve ();
		NewPath ();
	}

	public void FillPreserve ()
	{
		using SKPaint paint = CreatePaint ();
		paint.Style = SKPaintStyle.Fill;
		canvas.DrawPath (PreparedFillPath (), paint);
	}

	public void Stroke ()
	{
		StrokePreserve ();
		NewPath ();
	}

	public void StrokePreserve ()
	{
		using SKPaint paint = CreateStrokePaint ();
		canvas.DrawPath (CurrentPath (), paint);
	}

	public void Paint ()
	{
		using SKPaint paint = CreatePaint ();
		canvas.DrawPaint (paint);
	}

	public void PaintWithAlpha (double alpha)
	{
		using SKPaint paint = CreatePaint (alphaOverride: true, alpha: alpha);
		canvas.DrawPaint (paint);
	}

	public void Clip ()
	{
		ClipPreserve ();
		NewPath ();
	}

	public void ClipPreserve ()
	{
		canvas.ClipPath (PreparedFillPath (), SKClipOperation.Intersect, antialias: state.AntialiasValue != Antialias.None);
	}

	// ---- extents -----------------------------------------------------------

	public void StrokeExtents (out double x1, out double y1, out double x2, out double y2)
	{
		using SKPaint paint = CreateStrokePaint ();
		using SKPathBuilder stroked = new ();
		SKPath current = CurrentPath ();
		SKRect device;
		if (paint.GetFillPath (current, stroked)) {
			using SKPath strokedPath = stroked.Snapshot ();
			device = strokedPath.TightBounds;
		} else {
			device = current.TightBounds;
		}
		BoundsToUser (device, out x1, out y1, out x2, out y2);
	}

	public void PathExtents (out double x1, out double y1, out double x2, out double y2)
		=> BoundsToUser (CurrentPath ().TightBounds, out x1, out y1, out x2, out y2);

	public void FillExtents (out double x1, out double y1, out double x2, out double y2)
		=> BoundsToUser (CurrentPath ().TightBounds, out x1, out y1, out x2, out y2);

	private void BoundsToUser (SKRect device, out double x1, out double y1, out double x2, out double y2)
	{
		if (device.IsEmpty) {
			x1 = y1 = x2 = y2 = 0;
			return;
		}
		SKRect user = state.Ctm.TryInvert (out SKMatrix inverse) ? inverse.MapRect (device) : device;
		x1 = user.Left;
		y1 = user.Top;
		x2 = user.Right;
		y2 = user.Bottom;
	}

	// ---- lifecycle ---------------------------------------------------------

	public ImageSurface GetTarget ()
		=> surface;

	public void Dispose ()
	{
		canvas.Flush ();
		canvas.Dispose ();
		pathSnapshot?.Dispose ();
		pathBuilder.Dispose ();
		surface.MarkDirty ();
	}

	internal static SKBlendMode MapOperator (Operator op) => op switch {
		Operator.Clear => SKBlendMode.Clear,
		Operator.Source => SKBlendMode.Src,
		Operator.Over => SKBlendMode.SrcOver,
		Operator.In => SKBlendMode.SrcIn,
		Operator.Out => SKBlendMode.SrcOut,
		Operator.Atop => SKBlendMode.SrcATop,
		Operator.Dest => SKBlendMode.Dst,
		Operator.DestOver => SKBlendMode.DstOver,
		Operator.DestIn => SKBlendMode.DstIn,
		Operator.DestOut => SKBlendMode.DstOut,
		Operator.DestAtop => SKBlendMode.DstATop,
		Operator.Xor => SKBlendMode.Xor,
		Operator.Add => SKBlendMode.Plus,
		Operator.Saturate => SKBlendMode.Plus,
		Operator.Multiply => SKBlendMode.Multiply,
		Operator.Screen => SKBlendMode.Screen,
		Operator.Overlay => SKBlendMode.Overlay,
		Operator.Darken => SKBlendMode.Darken,
		Operator.Lighten => SKBlendMode.Lighten,
		Operator.ColorDodge => SKBlendMode.ColorDodge,
		Operator.ColorBurn => SKBlendMode.ColorBurn,
		Operator.HardLight => SKBlendMode.HardLight,
		Operator.SoftLight => SKBlendMode.SoftLight,
		Operator.Difference => SKBlendMode.Difference,
		Operator.Exclusion => SKBlendMode.Exclusion,
		Operator.HslHue => SKBlendMode.Hue,
		Operator.HslSaturation => SKBlendMode.Saturation,
		Operator.HslColor => SKBlendMode.Color,
		Operator.HslLuminosity => SKBlendMode.Luminosity,
		_ => SKBlendMode.SrcOver,
	};
}
