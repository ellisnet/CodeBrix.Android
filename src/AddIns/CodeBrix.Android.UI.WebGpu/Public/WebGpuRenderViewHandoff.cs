// Ported from CodeBrix.Platform (Apache License 2.0; THIRD-PARTY-NOTICES.txt item 3): src/AddIns/Platform.UI.WebGpu/WebGpuRenderViewHandoff.skia.cs
// (branch main @ a26d07f74c1396007bd3e3480872456538e3876b; the working tree the pinned build 1.0.287.1311 was packed from).
// Verbatim: the same public type in the same namespace and the same 40-byte layout (it is not in the WebGpu Core).

#nullable enable

using System;
using System.Runtime.InteropServices;
using CodeBrix.WebGpu.Native;

namespace CodeBrix.Platform.UI.WebGpu;

/// <summary>
/// A <see cref="WebGpuRenderView"/> as a native module sees it: two C-callable function pointers that open and close
/// the view's frames, so a module (Rust, C, C++) may own its render loop on its own thread, and a POINTER to the view's
/// device hand-off. C declaration (40 bytes, 8-byte aligned; the AGENT-README has it in full):
/// <code>
/// typedef struct WGPUCodebrixRenderViewHandoff {
///     uint32_t size;                                             /* 40 */
///     uint32_t version;                                          /* 1  */
///     void* context;                                             /* offset 8  */
///     WGPUCodebrixFrameHandoff* (*begin_frame)(void* context);   /* offset 16 */
///     void (*end_frame)(void* context);                          /* offset 24 */
///     WGPUCodebrixDeviceHandoff const * device;                  /* offset 32 */
/// } WGPUCodebrixRenderViewHandoff;
/// </code>
/// </summary>
/// <remarks>
/// <para>
/// Obtained from <see cref="WebGpuRenderView.GetViewHandoff"/>: one block of native memory per view, at the same
/// address for the view's whole lifetime. The device hand-off is referenced, not embedded, so CodeBrix.WebGpu.Native
/// may grow its struct without moving any member of this one: validate THIS struct's <c>size</c>/<c>version</c>, then
/// <c>device-&gt;size</c>/<c>device-&gt;version</c>. <c>device</c> points into the same block and never changes.
/// </para>
/// <para>
/// <c>begin_frame(context)</c> opens a frame exactly as <see cref="WebGpuRenderView.BeginFrame"/> does (waiting while
/// another thread holds one) and returns the frame's hand-off, or NULL when the view cannot render now (not loaded, no
/// device, no size) - the module then skips the frame and must NOT call <c>end_frame</c>. After a non-NULL
/// <c>begin_frame</c>, the module records into the frame's <c>commandEncoder</c> and calls <c>end_frame(context)</c>
/// on the SAME thread. Read <c>*device</c> after a successful <c>begin_frame</c>: it is refilled whenever the view
/// creates a new device (its <c>device</c> member is NULL while the view has none).
/// </para>
/// <para>
/// Both functions are safe to call from any thread and never throw across the boundary; a failure is logged and
/// reported as NULL (begin) or ignored (end). A module stops calling them before the application lets go of the view.
/// </para>
/// </remarks>
[StructLayout(LayoutKind.Explicit, Size = 40)]
public unsafe struct WebGpuRenderViewHandoff : IEquatable<WebGpuRenderViewHandoff>
{
	/// <summary>The layout version this build writes.</summary>
	public const uint CurrentVersion = 1u;

	/// <summary>sizeof(WGPUCodebrixRenderViewHandoff) as filled by the view (40).</summary>
	[FieldOffset(0)]
	public uint Size;

	/// <summary>The layout version (<see cref="CurrentVersion"/>); refuse a version you were not built for.</summary>
	[FieldOffset(4)]
	public uint Version;

	/// <summary>The opaque context to pass to <see cref="BeginFrame"/> and <see cref="EndFrame"/>.</summary>
	[FieldOffset(8)]
	public void* Context;

	/// <summary><c>begin_frame</c>: opens a frame; returns its hand-off, or null when the view cannot render now.</summary>
	[FieldOffset(16)]
	public delegate* unmanaged[Cdecl]<void*, CodebrixFrameHandoff*> BeginFrame;

	/// <summary><c>end_frame</c>: validates, submits and presents the open frame.</summary>
	[FieldOffset(24)]
	public delegate* unmanaged[Cdecl]<void*, void> EndFrame;

	/// <summary>
	/// The view's device hand-off (CodeBrix.WebGpu.Native's <c>WGPUCodebrixDeviceHandoff</c>): owned by the view, at the
	/// same address for the view's lifetime; its <c>Device</c> is null while the view has no device. Check its own
	/// <c>Size</c> and <c>Version</c> before reading its members.
	/// </summary>
	[FieldOffset(32)]
	public CodebrixDeviceHandoff* Device;

	/// <summary>Whether two hand-offs carry the same members (the device hand-off by address).</summary>
	/// <param name="other">The other hand-off.</param>
	/// <returns>True when every member is equal.</returns>
	public readonly bool Equals(WebGpuRenderViewHandoff other) =>
		Size == other.Size
		&& Version == other.Version
		&& Context == other.Context
		&& (nint)BeginFrame == (nint)other.BeginFrame
		&& (nint)EndFrame == (nint)other.EndFrame
		&& Device == other.Device;

	/// <inheritdoc/>
	public override readonly bool Equals(object? obj) => obj is WebGpuRenderViewHandoff other && Equals(other);

	/// <inheritdoc/>
	public override readonly int GetHashCode() => HashCode.Combine((nint)Context, (nint)Device, Version, Size);

	/// <summary>Member-wise equality.</summary>
	/// <param name="left">The first hand-off.</param>
	/// <param name="right">The second hand-off.</param>
	/// <returns>True when equal.</returns>
	public static bool operator ==(WebGpuRenderViewHandoff left, WebGpuRenderViewHandoff right) => left.Equals(right);

	/// <summary>Member-wise inequality.</summary>
	/// <param name="left">The first hand-off.</param>
	/// <param name="right">The second hand-off.</param>
	/// <returns>True when any member differs.</returns>
	public static bool operator !=(WebGpuRenderViewHandoff left, WebGpuRenderViewHandoff right) => !left.Equals(right);
}
