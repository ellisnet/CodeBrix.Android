// Matrix.cs
//
// Pinta.Brix drawing-layer 2-D affine matrix, mirroring the member names of
// the matrix type the upstream Pinta code used (layer transforms, selection
// transforms). Implemented over plain doubles with SkiaSharp interop.

using SkiaSharp;

namespace Pinta.Brix.Engine.Drawing;

/// <summary>
/// 2-D affine transform:
/// x' = Xx * x + Xy * y + X0 ;  y' = Yx * x + Yy * y + Y0
/// </summary>
public sealed class Matrix
{
	public double Xx { get; set; }
	public double Yx { get; set; }
	public double Xy { get; set; }
	public double Yy { get; set; }
	public double X0 { get; set; }
	public double Y0 { get; set; }

	public Matrix ()
	{
		InitIdentity ();
	}

	public Matrix (double xx, double yx, double xy, double yy, double x0, double y0)
	{
		Init (xx, yx, xy, yy, x0, y0);
	}

	public void InitIdentity ()
		=> Init (1, 0, 0, 1, 0, 0);

	public void Init (double xx, double yx, double xy, double yy, double x0, double y0)
	{
		Xx = xx;
		Yx = yx;
		Xy = xy;
		Yy = yy;
		X0 = x0;
		Y0 = y0;
	}

	public bool IsIdentity ()
		=> Xx == 1 && Yx == 0 && Xy == 0 && Yy == 1 && X0 == 0 && Y0 == 0;

	public void Translate (double tx, double ty)
	{
		// Prepends, matching the semantics of the original API.
		X0 += Xx * tx + Xy * ty;
		Y0 += Yx * tx + Yy * ty;
	}

	public void Scale (double sx, double sy)
	{
		Xx *= sx;
		Yx *= sx;
		Xy *= sy;
		Yy *= sy;
	}

	public void Rotate (double radians)
	{
		double s = System.Math.Sin (radians);
		double c = System.Math.Cos (radians);
		double xx = Xx, yx = Yx, xy = Xy, yy = Yy;
		Xx = xx * c + xy * s;
		Yx = yx * c + yy * s;
		Xy = xy * c - xx * s;
		Yy = yy * c - yx * s;
	}

	/// <summary>this = this * other (this applied first, matching the original API).</summary>
	public void Multiply (Matrix other)
		=> Multiply (this, other);

	/// <summary>this = a * b (a applied first, matching the original API).</summary>
	public void Multiply (Matrix a, Matrix b)
	{
		double xx = a.Xx * b.Xx + a.Yx * b.Xy;
		double yx = a.Xx * b.Yx + a.Yx * b.Yy;
		double xy = a.Xy * b.Xx + a.Yy * b.Xy;
		double yy = a.Xy * b.Yx + a.Yy * b.Yy;
		double x0 = a.X0 * b.Xx + a.Y0 * b.Xy + b.X0;
		double y0 = a.X0 * b.Yx + a.Y0 * b.Yy + b.Y0;
		Init (xx, yx, xy, yy, x0, y0);
	}

	public Status Invert ()
	{
		double det = Xx * Yy - Yx * Xy;
		if (det == 0)
			return Status.InvalidRestore;

		double xx = Yy / det;
		double yx = -Yx / det;
		double xy = -Xy / det;
		double yy = Xx / det;
		double x0 = (Xy * Y0 - Yy * X0) / det;
		double y0 = (Yx * X0 - Xx * Y0) / det;
		Init (xx, yx, xy, yy, x0, y0);
		return Status.Success;
	}

	public void TransformPoint (ref double x, ref double y)
	{
		double nx = Xx * x + Xy * y + X0;
		double ny = Yx * x + Yy * y + Y0;
		x = nx;
		y = ny;
	}

	public void TransformDistance (ref double dx, ref double dy)
	{
		double nx = Xx * dx + Xy * dy;
		double ny = Yx * dx + Yy * dy;
		dx = nx;
		dy = ny;
	}

	public Matrix Clone ()
		=> new (Xx, Yx, Xy, Yy, X0, Y0);

	internal SKMatrix ToSKMatrix ()
		=> new ((float) Xx, (float) Xy, (float) X0, (float) Yx, (float) Yy, (float) Y0, 0, 0, 1);

	internal static Matrix FromSKMatrix (in SKMatrix m)
		=> new (m.ScaleX, m.SkewY, m.SkewX, m.ScaleY, m.TransX, m.TransY);
}
