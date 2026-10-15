// Ported from CodeBrix.Platform (Apache License 2.0; THIRD-PARTY-NOTICES.txt item 3): src/AddIns/Platform.UI.WebGpu.Tests/WebGpuRenderViewHandoffTests.cs
// (branch main @ a26d07f74c1396007bd3e3480872456538e3876b; the working tree the pinned build 1.0.287.1311 was packed from).
// Changes are marked ANDROID PORT: (1) the namespace of this test assembly; the Android port's hand-off struct is held to the same 40-byte layout.

#nullable enable

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using CodeBrix.Platform.UI.WebGpu;
using CodeBrix.WebGpu.Native;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.WebGpu.Tests; // ANDROID PORT (1)

/// <summary>
/// The view hand-off's layout against the C declaration the AGENT-README publishes:
/// <code>
/// typedef struct WGPUCodebrixRenderViewHandoff {
///     uint32_t size; uint32_t version;                         // 0, 4
///     void* context;                                           // 8
///     WGPUCodebrixFrameHandoff* (*begin_frame)(void* context); // 16
///     void (*end_frame)(void* context);                        // 24
///     WGPUCodebrixDeviceHandoff const * device;                // 32
/// } WGPUCodebrixRenderViewHandoff;                             // 40
/// </code>
/// A native module is compiled against those numbers; a change here breaks every module, so it is fenced. The device
/// hand-off is referenced by pointer, so its growth in CodeBrix.WebGpu.Native can never move a member of this struct.
/// </summary>
public unsafe class WebGpuRenderViewHandoffTests
{
	[Fact]
	public void the_struct_is_40_bytes()
	{
		//Assert
		sizeof(WebGpuRenderViewHandoff).Should().Be(40);
		Unsafe.SizeOf<WebGpuRenderViewHandoff>().Should().Be(40);
		Marshal.SizeOf<WebGpuRenderViewHandoff>().Should().Be(40);
	}

	[Fact]
	public void the_members_sit_at_the_offsets_of_the_c_declaration()
	{
		//Assert
		Marshal.OffsetOf<WebGpuRenderViewHandoff>(nameof(WebGpuRenderViewHandoff.Size)).ToInt32().Should().Be(0);
		Marshal.OffsetOf<WebGpuRenderViewHandoff>(nameof(WebGpuRenderViewHandoff.Version)).ToInt32().Should().Be(4);
		Marshal.OffsetOf<WebGpuRenderViewHandoff>(nameof(WebGpuRenderViewHandoff.Context)).ToInt32().Should().Be(8);
		Marshal.OffsetOf<WebGpuRenderViewHandoff>(nameof(WebGpuRenderViewHandoff.BeginFrame)).ToInt32().Should().Be(16);
		Marshal.OffsetOf<WebGpuRenderViewHandoff>(nameof(WebGpuRenderViewHandoff.EndFrame)).ToInt32().Should().Be(24);
		Marshal.OffsetOf<WebGpuRenderViewHandoff>(nameof(WebGpuRenderViewHandoff.Device)).ToInt32().Should().Be(32);
	}

	[Fact]
	public void the_view_members_come_first_and_the_device_is_a_pointer()
	{
		//Arrange
		WebGpuRenderViewHandoff handoff = default;

		//Act
		var deviceOffset = (byte*)&handoff.Device - (byte*)&handoff;

		//Assert
		sizeof(CodebrixDeviceHandoff*).Should().Be(8);
		(deviceOffset % 8).Should().Be(0);
		(deviceOffset + sizeof(CodebrixDeviceHandoff*)).Should().Be(sizeof(WebGpuRenderViewHandoff));
	}

	[Fact]
	public void the_referenced_hand_offs_have_the_sizes_their_package_publishes()
	{
		//Assert
		sizeof(CodebrixDeviceHandoff).Should().Be(64);
		sizeof(CodebrixFrameHandoff).Should().Be(56);
	}

	[Fact]
	public void the_layout_version_is_one()
	{
		//Assert
		WebGpuRenderViewHandoff.CurrentVersion.Should().Be(1u);
	}

	[Fact]
	public void equality_is_member_wise()
	{
		//Arrange
		var a = new WebGpuRenderViewHandoff { Size = 40, Version = 1, Context = (void*)0x10, Device = (CodebrixDeviceHandoff*)0x100 };
		var b = new WebGpuRenderViewHandoff { Size = 40, Version = 1, Context = (void*)0x10, Device = (CodebrixDeviceHandoff*)0x100 };
		var c = new WebGpuRenderViewHandoff { Size = 40, Version = 1, Context = (void*)0x20, Device = (CodebrixDeviceHandoff*)0x100 };
		var d = new WebGpuRenderViewHandoff { Size = 40, Version = 1, Context = (void*)0x10, Device = (CodebrixDeviceHandoff*)0x200 };

		//Assert
		(a == b).Should().BeTrue();
		(a != c).Should().BeTrue();
		(a != d).Should().BeTrue();
		a.GetHashCode().Should().Be(b.GetHashCode());
	}
}
