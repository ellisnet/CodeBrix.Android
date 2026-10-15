// Ported from CodeBrix.Platform (Apache License 2.0; THIRD-PARTY-NOTICES.txt item 3): src/AddIns/Platform.UI.WebGpu/IWebGpuRenderer.skia.cs
// (branch main @ a26d07f74c1396007bd3e3480872456538e3876b; the working tree the pinned build 1.0.287.1311 was packed from).
// Verbatim: the same public type in the same namespace (it is not in the WebGpu Core).

#nullable enable

using CodeBrix.WebGpu.Native;
using SkiaSharp;

namespace CodeBrix.Platform.UI.WebGpu;

/// <summary>
/// A renderer class for a <see cref="WebGpuRenderView"/>, set through its <see cref="WebGpuRenderView.Renderer"/>
/// property - the class-shaped alternative to handling the view's events. The view calls the renderer FIRST and then
/// raises the matching event, so the two can be mixed.
/// </summary>
/// <remarks>
/// Only <see cref="OnRender"/> must be written; the other three do nothing unless overridden.
/// </remarks>
public interface IWebGpuRenderer
{
	/// <summary>
	/// A device is ready (on the first frame after the view is loaded, and again after a lost device is replaced):
	/// create pipelines, buffers and other device resources here. Runs on the thread that runs the frame.
	/// </summary>
	/// <param name="device">The view's device (the view itself, as an <see cref="IWebGpuDeviceSource"/>).</param>
	void OnDeviceReady(IWebGpuDeviceSource device)
	{
	}

	/// <summary>
	/// Record this frame's WebGPU work into <see cref="WebGpuFrame.Encoder"/>, rendering into
	/// <see cref="WebGpuFrame.TextureView"/>. Runs on the thread that runs the frame (the user-interface thread for
	/// the built-in drivers).
	/// </summary>
	/// <param name="frame">The open frame.</param>
	void OnRender(WebGpuFrame frame);

	/// <summary>
	/// Draw over the frame with SkiaSharp (a HUD, labels, a selection box) in the view's effective-pixel coordinates.
	/// Runs while the head paints the view, which is not the frame's thread: never touch WebGPU objects
	/// here, and read only state that is safe to read from another thread.
	/// </summary>
	/// <param name="canvas">The canvas, clipped to the view.</param>
	/// <param name="size">The view's size in effective pixels.</param>
	void OnOverlay(SKCanvas canvas, SKSize size)
	{
	}

	/// <summary>
	/// The device is gone (or never came): release every device resource you created. A replacement device brings a
	/// new <see cref="OnDeviceReady"/>. Runs on the thread that noticed (the frame's thread, or the user-interface
	/// thread when the view is unloaded).
	/// </summary>
	/// <param name="reason">Why, in words a person can act on.</param>
	void OnDeviceLost(string reason)
	{
	}
}
