// Path.cs
//
// Pinta.Brix drawing-layer retained path: an opaque copy of a Context's
// current path, mirroring the path-object API the upstream Pinta code used.

using System;
using SkiaSharp;

namespace Pinta.Brix.Engine.Drawing;

public sealed class Path : IDisposable
{
	internal SKPath SkPath { get; }

	internal Path (SKPath path)
	{
		SkPath = path;
	}

	public void Dispose ()
	{
		SkPath.Dispose ();
	}
}
