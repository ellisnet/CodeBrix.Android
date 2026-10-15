// Ported from CodeBrix.Platform (Apache License 2.0; THIRD-PARTY-NOTICES.txt item 3): src/AddIns/Platform.UI.WebGpu/WebGpuRenderEventArgs.skia.cs
// (branch main @ a26d07f74c1396007bd3e3480872456538e3876b; the working tree the pinned build 1.0.287.1311 was packed from).
// Verbatim: the same public type in the same namespace (it is not in the WebGpu Core).

#nullable enable

using System;

namespace CodeBrix.Platform.UI.WebGpu;

/// <summary>
/// Event args for <see cref="WebGpuRenderView.Render"/>. The view reuses ONE instance for every frame: read
/// <see cref="Frame"/> inside the handler and keep nothing.
/// </summary>
public sealed class WebGpuRenderEventArgs : EventArgs
{
	internal WebGpuRenderEventArgs(WebGpuFrame frame)
	{
		Frame = frame;
	}

	/// <summary>The open frame to record into.</summary>
	public WebGpuFrame Frame { get; }
}
