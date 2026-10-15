// Ported from CodeBrix.Platform (Apache License 2.0; THIRD-PARTY-NOTICES.txt item 3): src/AddIns/Platform.UI.WebGpu/Internal/WebGpuHandoffBlock.skia.cs
// (branch main @ a26d07f74c1396007bd3e3480872456538e3876b; the working tree the pinned build 1.0.287.1311 was packed from).
// Verbatim.

#nullable enable

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using CodeBrix.Platform.Extensions;
using CodeBrix.Platform.Extensions.Logging;
using CodeBrix.WebGpu.Native;
using Microsoft.Extensions.Logging;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace CodeBrix.Platform.UI.WebGpu.Internal;

/// <summary>
/// The native memory behind a view's hand-offs: one block holding the <see cref="WebGpuRenderViewHandoff"/>, then the
/// <see cref="CodebrixDeviceHandoff"/> it points at, then the <see cref="CodebrixFrameHandoff"/> each frame refills.
/// Allocated once per view, never moved, freed when the view's engine is collected.
/// </summary>
/// <remarks>
/// The <c>context</c> a module passes back is a WEAK GCHandle to the engine: the hand-off never keeps a view alive.
/// After the view is gone <c>begin_frame</c> returns NULL; the block itself lives until this object's finalizer, which
/// runs only once nothing references the engine any more.
/// </remarks>
internal sealed unsafe class WebGpuHandoffBlock
{
	/// <summary>The size of the view hand-off, as its C declaration has it.</summary>
	internal const int ViewHandoffSize = 40;

	// Each member starts on a 16-byte boundary (the block itself is 16-aligned), whatever size Native gives its structs.
	private static readonly int DeviceOffset = AlignUp(ViewHandoffSize);
	private static readonly int FrameOffset = DeviceOffset + AlignUp(sizeof(CodebrixDeviceHandoff));
	private static readonly int BlockSize = FrameOffset + AlignUp(sizeof(CodebrixFrameHandoff));

	private readonly void* _memory;
	private GCHandle _context;

	/// <summary>Allocates the block for <paramref name="engine"/> and fills the constant members.</summary>
	/// <param name="engine">The engine the thunks drive.</param>
	internal WebGpuHandoffBlock(WebGpuRenderEngine engine)
	{
		_memory = NativeMemory.AlignedAlloc((nuint)BlockSize, 16);
		NativeMemory.Clear(_memory, (nuint)BlockSize);
		_context = GCHandle.Alloc(engine, GCHandleType.Weak);

		View = (WebGpuRenderViewHandoff*)_memory;
		Device = (CodebrixDeviceHandoff*)((byte*)_memory + DeviceOffset);
		Frame = (CodebrixFrameHandoff*)((byte*)_memory + FrameOffset);

		View->Size = (uint)sizeof(WebGpuRenderViewHandoff);
		View->Version = WebGpuRenderViewHandoff.CurrentVersion;
		View->Context = (void*)GCHandle.ToIntPtr(_context);
		View->BeginFrame = &BeginFrameThunk;
		View->EndFrame = &EndFrameThunk;
		View->Device = Device;
	}

	/// <summary>Frees the block once the engine is unreachable.</summary>
	~WebGpuHandoffBlock()
	{
		NativeMemory.AlignedFree(_memory);
		if (_context.IsAllocated)
		{
			_context.Free();
		}
	}

	/// <summary>The view hand-off.</summary>
	internal WebGpuRenderViewHandoff* View { get; }

	/// <summary>The device hand-off the view hand-off points at (same block, never moved).</summary>
	internal CodebrixDeviceHandoff* Device { get; }

	/// <summary>The frame hand-off, refilled by every frame.</summary>
	internal CodebrixFrameHandoff* Frame { get; }

	private static int AlignUp(int size) => (size + 15) & ~15;

	// begin_frame(context): opens a frame on the calling thread, waiting while another thread holds one. NULL = skip.
	// An exception must never cross an UnmanagedCallersOnly boundary, so every failure is logged and becomes NULL.
	[UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
	private static CodebrixFrameHandoff* BeginFrameThunk(void* context)
	{
		try
		{
			if (context != null
				&& GCHandle.FromIntPtr((nint)context).Target is WebGpuRenderEngine engine
				&& engine.OpenFrame(wait: true) == WebGpuFrameOutcome.Rendered)
			{
				return engine.FrameHandoff;
			}
		}
		catch (Exception e)
		{
			if (typeof(WebGpuHandoffBlock).Log().IsEnabled(LogLevel.Warning))
			{
				typeof(WebGpuHandoffBlock).Log().Warn("A native module's begin_frame could not open a frame: " + e.Message, e);
			}
		}

		return null;
	}

	// end_frame(context): ends the frame the calling thread opened. Failures are logged (the module has no way to
	// receive an exception).
	[UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
	private static void EndFrameThunk(void* context)
	{
		try
		{
			if (context != null && GCHandle.FromIntPtr((nint)context).Target is WebGpuRenderEngine engine)
			{
				engine.EndFrame();
			}
		}
		catch (Exception e)
		{
			if (typeof(WebGpuHandoffBlock).Log().IsEnabled(LogLevel.Warning))
			{
				typeof(WebGpuHandoffBlock).Log().Warn("A native module's end_frame failed: " + e.Message, e);
			}
		}
	}
}
