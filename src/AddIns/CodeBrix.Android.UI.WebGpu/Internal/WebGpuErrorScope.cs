// Ported from CodeBrix.Platform (Apache License 2.0; THIRD-PARTY-NOTICES.txt item 3): src/AddIns/Platform.UI.WebGpu/Internal/WebGpuErrorScope.skia.cs
// (branch main @ a26d07f74c1396007bd3e3480872456538e3876b; the working tree the pinned build 1.0.287.1311 was packed from).
// Changes are marked ANDROID PORT: (1) the class summary also names the Android DirectSurface path, which validates its surface pass with it.

#nullable enable

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using CodeBrix.WebGpu.Native;
using Wgpu = CodeBrix.WebGpu.Native.WebGpu;

namespace CodeBrix.Platform.UI.WebGpu.Internal;

/// <summary>
/// Validation error scopes on a device that has no uncaptured-error handler of ours - an ADOPTED device, where the
/// WebGPU engine ends the process on the first validation error no scope catches. The direct render paths open one
/// around every piece of work the view runs on such a device (a frame, the DeviceReady callbacks) and turn what it
/// caught into a log line and DeviceLost instead.
/// ANDROID PORT (1): no device is adopted on Android; the DirectSurface path opens one around the finish of its surface
/// pass, because submitting a command buffer that failed validation ends the process (the engine's documented limit).
/// </summary>
/// <remarks>
/// Allocation-free when nothing was caught: the callback writes into a stack struct and a message string is only
/// made for an actual error.
/// </remarks>
internal static unsafe class WebGpuErrorScope
{
	// How many times Pop pumps the instance before giving up on the callback (each pump is followed by a 1 ms sleep
	// only while it has not come). The callback normally arrives on the first pump.
	private const int PumpAttempts = 2000;

	/// <summary>Opens a validation error scope on <paramref name="device"/>.</summary>
	/// <param name="device">The device.</param>
	internal static void Push(Device device) => Wgpu.DevicePushErrorScope(device, ErrorFilter.Validation);

	/// <summary>Closes the innermost error scope and returns what it caught.</summary>
	/// <param name="instance">The device's instance (pumped until the callback arrives).</param>
	/// <param name="device">The device.</param>
	/// <returns>WebGPU's message for the error the scope caught, or null when it caught none.</returns>
	internal static string? Pop(Instance instance, Device device)
	{
		PopState state = default;
		var info = new PopErrorScopeCallbackInfo
		{
			Mode = CallbackMode.AllowProcessEvents,
			Callback = &OnPopped,
			Userdata1 = &state,
		};

		Wgpu.DevicePopErrorScope(device, info);
		for (var attempt = 0; attempt < PumpAttempts && Volatile.Read(ref state.Called) == 0; attempt++)
		{
			Wgpu.InstanceProcessEvents(instance);
			if (Volatile.Read(ref state.Called) == 0)
			{
				Thread.Sleep(1);
			}
		}

		var message = TakeMessage(state.Message);
		if (state.Called == 0 || state.Status != (uint)PopErrorScopeStatus.Success || state.Type == (uint)ErrorType.NoError)
		{
			return null;
		}

		return string.IsNullOrEmpty(message) ? "WebGPU reported an error with no message." : message;
	}

	private static string? TakeMessage(nint handle)
	{
		if (handle == 0)
		{
			return null;
		}

		var message = GCHandle.FromIntPtr(handle);
		var text = message.Target as string;
		message.Free();
		return text;
	}

	[UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
	private static void OnPopped(PopErrorScopeStatus status, ErrorType type, StringView message, void* userdata1, void* userdata2)
	{
		var state = (PopState*)userdata1;
		state->Status = (uint)status;
		state->Type = (uint)type;

		// The message is only valid during the callback; copy it out only when there is an error to report.
		if (status == PopErrorScopeStatus.Success && type != ErrorType.NoError)
		{
			state->Message = GCHandle.ToIntPtr(GCHandle.Alloc(WgpuString.ToManaged(message) ?? string.Empty));
		}

		Volatile.Write(ref state->Called, 1);
	}

	private struct PopState
	{
		public int Called;
		public uint Status;
		public uint Type;
		public nint Message;
	}
}
