// Ported from CodeBrix.Platform (Apache License 2.0; THIRD-PARTY-NOTICES.txt item 3): src/AddIns/Platform.UI.WebGpu/WebGpuFrame.skia.cs
// (branch main @ a26d07f74c1396007bd3e3480872456538e3876b; the working tree the pinned build 1.0.287.1311 was packed from).
// Verbatim: the same public type in the same namespace (it is not in the WebGpu Core).

#nullable enable

using System;
using CodeBrix.Platform.UI.WebGpu.Internal;
using CodeBrix.WebGpu.Native;

namespace CodeBrix.Platform.UI.WebGpu;

/// <summary>
/// One frame of a <see cref="WebGpuRenderView"/>: the command encoder to record into and the texture it renders to.
/// Handed to <see cref="IWebGpuRenderer.OnRender"/> and the <see cref="WebGpuRenderView.Render"/> event by the
/// built-in drivers, and returned by <see cref="WebGpuRenderView.BeginFrame"/> under the Manual driver.
/// </summary>
/// <remarks>
/// <para>
/// Every member is valid only while the frame is open - from <c>BeginFrame</c> to <c>EndFrame</c> (or
/// <see cref="Dispose"/>, which is <c>EndFrame</c>) - and only on the thread that opened it. The view keeps ONE
/// instance and reuses it for every frame, so never keep a reference past the frame and never compare two frames by
/// reference: <see cref="FrameIndex"/> tells them apart.
/// </para>
/// <para>
/// The texture has already been cleared to the view's <c>ClearColor</c> when the frame opens. Record render passes
/// with <c>LoadOp.Load</c> to draw over that, or <c>LoadOp.Clear</c> to clear again. Record into <see cref="Encoder"/>
/// only: never finish, submit or release it (the view finishes, validates and submits it at <c>EndFrame</c>), and close
/// every pass you begin.
/// </para>
/// </remarks>
public sealed class WebGpuFrame : IDisposable
{
	private readonly WebGpuRenderEngine _engine;

	internal WebGpuFrame(WebGpuRenderEngine engine)
	{
		_engine = engine;
	}

	/// <summary>The command encoder of this frame's pass. Owned by the view.</summary>
	public CommandEncoder Encoder { get; internal set; }

	/// <summary>The texture the frame renders into (format <see cref="Format"/>). Owned by the view.</summary>
	public Texture Texture { get; internal set; }

	/// <summary>A default view of <see cref="Texture"/>, usable as a render attachment. Owned by the view.</summary>
	public TextureView TextureView { get; internal set; }

	/// <summary>The texture's format: what every render pipeline that draws into it must target.</summary>
	public TextureFormat Format { get; internal set; }

	/// <summary>The texture's width in pixels (the element's width times its rasterization scale).</summary>
	public int Width { get; internal set; }

	/// <summary>The texture's height in pixels (the element's height times its rasterization scale).</summary>
	public int Height { get; internal set; }

	/// <summary>
	/// The number of this frame: 0 for the first frame on a device, counting up by one per opened frame. Starts again
	/// at 0 after a new device is created (after <see cref="WebGpuRenderView.DeviceReady"/>).
	/// </summary>
	public long FrameIndex { get; internal set; }

	/// <summary>Seconds since the first frame on the current device opened (0 for that frame).</summary>
	public double ElapsedSeconds { get; internal set; }

	/// <summary>Seconds since the previous frame opened (0 for the first frame on a device).</summary>
	public double DeltaSeconds { get; internal set; }

	/// <summary>True between <c>BeginFrame</c> and <c>EndFrame</c>.</summary>
	public bool IsOpen { get; internal set; }

	/// <summary>
	/// The frame as a native-module hand-off (CodeBrix.WebGpu.Native's <c>WGPUCodebrixFrameHandoff</c>): format, size,
	/// sample count, usage, texture, view and this frame's command encoder, filled when the frame opened. Pass it to a
	/// module's per-frame call; the module records into its <c>commandEncoder</c> under the same rules as
	/// <see cref="Encoder"/>.
	/// </summary>
	/// <returns>A pointer into memory the view owns (the same address every frame; valid for the view's lifetime,
	/// meaningful only while the frame is open).</returns>
	public unsafe CodebrixFrameHandoff* GetHandoff() => _engine.FrameHandoff;

	/// <summary>Ends the frame: the same as <see cref="WebGpuRenderView.EndFrame"/>. Does nothing when it is not open.</summary>
	/// <exception cref="CodeBrix.WebGpu.Skia.WebGpuSkiaValidationException">The frame's commands failed WebGPU
	/// validation and were not submitted.</exception>
	public void Dispose()
	{
		if (IsOpen)
		{
			_engine.EndFrame();
		}
	}
}
