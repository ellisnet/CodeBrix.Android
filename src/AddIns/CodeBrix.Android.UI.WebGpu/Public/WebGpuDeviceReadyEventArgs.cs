// Ported from CodeBrix.Platform (Apache License 2.0; THIRD-PARTY-NOTICES.txt item 3): src/AddIns/Platform.UI.WebGpu/WebGpuDeviceReadyEventArgs.skia.cs
// (branch main @ a26d07f74c1396007bd3e3480872456538e3876b; the working tree the pinned build 1.0.287.1311 was packed from).
// Verbatim: the same public type in the same namespace (it is not in the WebGpu Core).

#nullable enable

using System;
using CodeBrix.WebGpu.Native;

namespace CodeBrix.Platform.UI.WebGpu;

/// <summary>Event args for <see cref="WebGpuRenderView.DeviceReady"/>.</summary>
public sealed class WebGpuDeviceReadyEventArgs : EventArgs
{
	internal WebGpuDeviceReadyEventArgs(IWebGpuDeviceSource device, WebGpuDeviceInfo deviceInfo)
	{
		Device = device;
		DeviceInfo = deviceInfo;
	}

	/// <summary>
	/// The view's device (the view itself, as an <see cref="IWebGpuDeviceSource"/>): pass it to a library that accepts
	/// a device source, or read its handles directly. The handles stay valid until <see cref="WebGpuRenderView.DeviceLost"/>.
	/// </summary>
	public IWebGpuDeviceSource Device { get; }

	/// <summary>
	/// What the device is: adapter, backend, and the features and limits it was created with. Check a renderer's or a
	/// module's requirements against it before initialising (also <see cref="WebGpuRenderView.DeviceInfo"/>).
	/// </summary>
	public WebGpuDeviceInfo DeviceInfo { get; }
}
