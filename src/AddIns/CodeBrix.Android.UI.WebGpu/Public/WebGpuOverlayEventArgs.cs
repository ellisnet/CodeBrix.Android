// Ported from CodeBrix.Platform (Apache License 2.0; THIRD-PARTY-NOTICES.txt item 3): src/AddIns/Platform.UI.WebGpu/WebGpuOverlayEventArgs.skia.cs
// (branch main @ a26d07f74c1396007bd3e3480872456538e3876b; the working tree the pinned build 1.0.287.1311 was packed from).
// Verbatim: the same public type in the same namespace (it is not in the WebGpu Core).

#nullable enable

using System;
using SkiaSharp;

namespace CodeBrix.Platform.UI.WebGpu;

/// <summary>
/// Event args for <see cref="WebGpuRenderView.Overlay"/>. Raised while the head paints the view (not on the frame's thread); the
/// view reuses ONE instance for every paint, so keep nothing (the canvas in particular is valid only for the call).
/// </summary>
public sealed class WebGpuOverlayEventArgs : EventArgs
{
	internal WebGpuOverlayEventArgs()
	{
		Canvas = null!;
	}

	/// <summary>The canvas, clipped to the view, in the view's effective-pixel coordinates. Valid only for the call.</summary>
	public SKCanvas Canvas { get; internal set; }

	/// <summary>The view's size in effective pixels.</summary>
	public SKSize Size { get; internal set; }
}
