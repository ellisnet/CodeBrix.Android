// Enums.cs
//
// Pinta.Brix drawing-layer enums. These mirror the names and semantics of the
// vector-graphics API the upstream Pinta code drew with, so ported call sites
// compile unchanged; rendering is implemented on SkiaSharp.

namespace Pinta.Brix.Engine.Drawing;

public enum Format
{
	Argb32, // premultiplied BGRA in memory (little-endian), the only format the engine uses
	A8,
}

public enum Status
{
	Success,
	NoMemory,
	InvalidRestore,
	SurfaceFinished,
}

public enum Antialias
{
	Default,
	None,
	Gray,
	Subpixel,
}

public enum LineCap
{
	Butt,
	Round,
	Square,
}

public enum LineJoin
{
	Miter,
	Round,
	Bevel,
}

public enum FillRule
{
	Winding,
	EvenOdd,
}

public enum Filter
{
	Fast,
	Good,
	Best,
	Nearest,
	Bilinear,
}

public enum Extend
{
	None,
	Repeat,
	Reflect,
	Pad,
}

public enum Operator
{
	Clear,
	Source,
	Over,
	In,
	Out,
	Atop,
	Dest,
	DestOver,
	DestIn,
	DestOut,
	DestAtop,
	Xor,
	Add,
	Saturate,
	Multiply,
	Screen,
	Overlay,
	Darken,
	Lighten,
	ColorDodge,
	ColorBurn,
	HardLight,
	SoftLight,
	Difference,
	Exclusion,
	HslHue,
	HslSaturation,
	HslColor,
	HslLuminosity,
}
