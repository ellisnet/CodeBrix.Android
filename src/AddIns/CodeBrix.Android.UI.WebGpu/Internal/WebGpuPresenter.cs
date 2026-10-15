// Ported from CodeBrix.Platform (Apache License 2.0; THIRD-PARTY-NOTICES.txt item 3): src/AddIns/Platform.UI.WebGpu/Internal/WebGpuPresenter.skia.cs
// (branch main @ a26d07f74c1396007bd3e3480872456538e3876b; the working tree the pinned build 1.0.287.1311 was packed from).
// Changes are marked ANDROID PORT: (1) the class summary names the Android presenters (the desktop direct presenters are not ported; Android's direct path is a SurfaceView, not an adopted device); (2) IsWaiting (the DirectSurface path waiting for its window).

#nullable enable

using System;
using CodeBrix.WebGpu.Native;
using CodeBrix.WebGpu.Skia;

namespace CodeBrix.Platform.UI.WebGpu.Internal;

/// <summary>
/// One render path's GPU side, as the render engine drives it: the device the view's frames run on, the target the
/// next frame renders into, and how a finished frame reaches the surface. <see cref="WebGpuReadbackPresenter"/> is the
/// default (its own device, one readback per frame).
/// ANDROID PORT (1): the desktop direct presenters (adopting a head's Vulkan / Metal device) are not ported - Android's
/// direct path, <see cref="WebGpuSurfacePresenter"/>, presents to a SurfaceView's window surface instead; the virtual
/// members the desktop presenters override stay, so the engine keeps the Platform's shape.
/// </summary>
/// <remarks>
/// Every member runs on the thread that holds the view's frame gate (one at a time, never concurrently), except
/// where a derived class says otherwise.
/// </remarks>
internal abstract class WebGpuPresenter : IDisposable
{
	/// <summary>
	/// The texture format every render path renders in: BGRA, which is what the Readback bitmaps hold and what the
	/// heads' own surfaces use - so pipelines a renderer made for one path are valid on every other.
	/// </summary>
	internal const TextureFormat Format = TextureFormat.BGRA8Unorm;

	/// <summary>The render path this presenter implements.</summary>
	internal abstract WebGpuRenderPath Path { get; }

	/// <summary>The device the view's frames run on.</summary>
	internal abstract WebGpuSkiaDevice Device { get; }

	/// <summary>The target the open (or next) frame renders into, or null before the first sized frame.</summary>
	internal abstract WebGpuSkiaTarget? Target { get; }

	/// <summary>
	/// Makes sure <see cref="Target"/> is a usable <paramref name="width"/> x <paramref name="height"/> target for the
	/// frame about to open.
	/// </summary>
	/// <param name="width">The width in pixels.</param>
	/// <param name="height">The height in pixels.</param>
	/// <param name="reason">Why there is no usable target, or null.</param>
	/// <returns>True when a usable target of that size exists.</returns>
	internal abstract bool EnsureTarget(int width, int height, out string? reason);

	/// <summary>Called once the frame's encoder is open (before the clear and the renderer).</summary>
	internal virtual void OnFrameOpened()
	{
	}

	/// <summary>
	/// Ends the frame's pass: validates and submits the frame's commands (with whatever the path adds to the same
	/// submission).
	/// </summary>
	/// <param name="encoder">The frame's encoder (the one the target's BeginWebGpuPass handed out).</param>
	/// <exception cref="WebGpuSkiaValidationException">The commands failed validation; nothing was submitted.</exception>
	internal abstract void EndPass(CommandEncoder encoder);

	/// <summary>Shows the frame <see cref="EndPass"/> submitted.</summary>
	/// <param name="host">The engine's owner, which presents to the surface.</param>
	internal abstract void Show(IWebGpuRenderEngineHost host);

	/// <summary>
	/// Errors the device reported since the last call that mean its state can no longer be trusted, joined into one
	/// message; null when there were none.
	/// </summary>
	/// <returns>The message, or null.</returns>
	internal abstract string? TakeErrors();

	/// <summary>Opens whatever error capture device-level work outside a frame needs (DeviceReady).</summary>
	internal virtual void BeginDeviceWork()
	{
	}

	/// <summary>Closes the capture <see cref="BeginDeviceWork"/> opened.</summary>
	/// <returns>The error it caught, or null.</returns>
	internal virtual string? EndDeviceWork() => null;

	/// <summary>
	/// Why this presenter can no longer present (a direct path whose paint side failed), or null while it works.
	/// Read by the engine at the start of every frame.
	/// </summary>
	internal virtual string? FailureReason => null;

	/// <summary>True when <see cref="FailureReason"/> is the head's device going away (its window closing), not a fault.</summary>
	internal virtual bool HeadDeviceGone => false;

	/// <summary>
	/// ANDROID PORT (2): true after an <see cref="EnsureTarget"/> that found nothing to present to YET (the DirectSurface
	/// path's window does not exist now): the frame is skipped quietly - not a failure, and no log line.
	/// </summary>
	internal virtual bool IsWaiting => false;

	/// <summary>Releases the targets, then the device.</summary>
	public abstract void Dispose();
}
