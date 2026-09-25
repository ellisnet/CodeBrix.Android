// Pattern.cs
//
// Pinta.Brix drawing-layer paint sources (surface patterns and gradients),
// mirroring the pattern API the upstream Pinta code used; realized as
// SkiaSharp shaders at draw time.

using System;
using System.Collections.Generic;
using SkiaSharp;

namespace Pinta.Brix.Engine.Drawing;

public abstract class Pattern : IDisposable
{
	/// <summary>Pattern matrix mapping user space to pattern space.</summary>
	public Matrix Matrix { get; set; } = new ();

	/// <summary>Copies this pattern's matrix into the provided matrix.</summary>
	public void GetMatrix (Matrix into)
		=> into.Init (Matrix.Xx, Matrix.Yx, Matrix.Xy, Matrix.Yy, Matrix.X0, Matrix.Y0);

	public void SetMatrix (Matrix matrix)
		=> Matrix = matrix.Clone ();

	private protected SKMatrix InverseMatrix ()
	{
		Matrix inverse = Matrix.Clone ();
		inverse.Invert ();
		return inverse.ToSKMatrix ();
	}

	internal abstract SKShader ToShader ();

	public virtual void Dispose ()
	{
		GC.SuppressFinalize (this);
	}
}

public sealed class SurfacePattern : Pattern
{
	private readonly Surface surface;

	public SurfacePattern (Surface surface)
	{
		this.surface = surface;
	}

	public Filter Filter { get; set; } = Filter.Good;

	public Extend Extend { get; set; } = Extend.None;

	internal static SKSamplingOptions ToSampling (Filter filter) => filter switch {
		Filter.Nearest or Filter.Fast => new SKSamplingOptions (SKFilterMode.Nearest),
		_ => new SKSamplingOptions (SKFilterMode.Linear),
	};

	internal static SKShaderTileMode ToTileMode (Extend extend) => extend switch {
		Extend.Repeat => SKShaderTileMode.Repeat,
		Extend.Reflect => SKShaderTileMode.Mirror,
		Extend.Pad => SKShaderTileMode.Clamp,
		// The original API's Extend.None samples transparent outside the
		// surface; Decal is the SkiaSharp equivalent.
		_ => SKShaderTileMode.Decal,
	};

	internal override SKShader ToShader ()
	{
		// The pattern matrix maps USER space to PATTERN space; the shader's
		// local matrix maps the other way, so apply the inverse.
		return surface.Bitmap.ToShader (
			ToTileMode (Extend),
			ToTileMode (Extend),
			ToSampling (Filter),
			InverseMatrix ());
	}
}

public abstract class Gradient : Pattern
{
	private protected readonly List<float> offsets = [];
	private protected readonly List<SKColor> colors = [];

	public void AddColorStop (double offset, Color color)
		=> AddColorStopRgba (offset, color.R, color.G, color.B, color.A);

	public void AddColorStopRgba (double offset, double r, double g, double b, double a)
	{
		offsets.Add ((float) offset);
		colors.Add (new SKColor (
			(byte) Math.Round (Math.Clamp (r, 0, 1) * 255),
			(byte) Math.Round (Math.Clamp (g, 0, 1) * 255),
			(byte) Math.Round (Math.Clamp (b, 0, 1) * 255),
			(byte) Math.Round (Math.Clamp (a, 0, 1) * 255)));
	}
}

public sealed class LinearGradient : Gradient
{
	private readonly SKPoint start;
	private readonly SKPoint end;

	public LinearGradient (double x0, double y0, double x1, double y1)
	{
		start = new SKPoint ((float) x0, (float) y0);
		end = new SKPoint ((float) x1, (float) y1);
	}

	internal override SKShader ToShader ()
		=> SKShader.CreateLinearGradient (start, end, [.. colors], [.. offsets], SKShaderTileMode.Clamp)
			.WithLocalMatrix (InverseMatrix ());
}

public sealed class RadialGradient : Gradient
{
	private readonly SKPoint innerCenter;
	private readonly float innerRadius;
	private readonly SKPoint outerCenter;
	private readonly float outerRadius;

	public RadialGradient (double cx0, double cy0, double radius0, double cx1, double cy1, double radius1)
	{
		innerCenter = new SKPoint ((float) cx0, (float) cy0);
		innerRadius = (float) radius0;
		outerCenter = new SKPoint ((float) cx1, (float) cy1);
		outerRadius = (float) radius1;
	}

	internal override SKShader ToShader ()
		=> SKShader.CreateTwoPointConicalGradient (
			innerCenter, innerRadius, outerCenter, outerRadius,
			[.. colors], [.. offsets], SKShaderTileMode.Clamp)
			.WithLocalMatrix (InverseMatrix ());
}
