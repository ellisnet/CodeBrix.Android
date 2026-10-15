// Ported from CodeBrix.Platform (Apache License 2.0; THIRD-PARTY-NOTICES.txt item 3): src/AddIns/Platform.UI.WebGpu.Tests/TrianglePipeline.cs
// (branch main @ a26d07f74c1396007bd3e3480872456538e3876b; the working tree the pinned build 1.0.287.1311 was packed from).
// Changes are marked ANDROID PORT: (1) the namespace of this test assembly.

#nullable enable

using System;
using CodeBrix.Platform.UI.WebGpu;
using CodeBrix.WebGpu.Native;
using Wgpu = CodeBrix.WebGpu.Native.WebGpu;

namespace CodeBrix.Android.UI.WebGpu.Tests; // ANDROID PORT (1)

/// <summary>
/// A real WebGPU pipeline the GPU tests draw with: one opaque triangle of a fixed colour, covering the centre of the
/// target and none of its corners, recorded into a frame's encoder with LoadOp.Load (so the view's clear colour stays
/// around it).
/// </summary>
internal sealed unsafe class TrianglePipeline : IDisposable
{
	// The triangle's colour: pure green, so it can never be confused with a clear colour the tests choose.
	private const string Wgsl =
		"@vertex fn vs(@builtin(vertex_index) index: u32) -> @builtin(position) vec4<f32> {\n"
		+ "    var corners = array<vec2<f32>, 3>(vec2<f32>(-0.5, -0.5), vec2<f32>(0.5, -0.5), vec2<f32>(0.0, 0.6));\n"
		+ "    return vec4<f32>(corners[index], 0.0, 1.0);\n"
		+ "}\n"
		+ "@fragment fn fs() -> @location(0) vec4<f32> { return vec4<f32>(0.0, 1.0, 0.0, 1.0); }\n";

	private readonly ShaderModule _module;
	private readonly RenderPipeline _pipeline;

	/// <summary>Builds the pipeline on <paramref name="device"/> for <paramref name="format"/>.</summary>
	/// <param name="device">The device.</param>
	/// <param name="format">The colour-target format (the view's).</param>
	internal TrianglePipeline(Device device, TextureFormat format)
	{
		var code = System.Text.Encoding.UTF8.GetBytes(Wgsl);
		fixed (byte* text = code)
		{
			var wgsl = new ShaderSourceWGSL { Code = WgpuString.FromPinned(text, code.Length) };
			wgsl.Chain.SType = ShaderSourceWGSL.StructType;
			var descriptor = new ShaderModuleDescriptor { NextInChain = &wgsl.Chain };
			_module = Wgpu.DeviceCreateShaderModule(device, &descriptor);
		}

		fixed (byte* vs = "vs"u8)
		fixed (byte* fs = "fs"u8)
		{
			var target = new ColorTargetState { Format = format, WriteMask = ColorWriteMask.All };
			var fragment = new FragmentState
			{
				Module = _module,
				EntryPoint = WgpuString.FromPinned(fs, 2),
				TargetCount = 1,
				Targets = &target,
			};
			var descriptor = new RenderPipelineDescriptor
			{
				Vertex = new VertexState { Module = _module, EntryPoint = WgpuString.FromPinned(vs, 2) },
				Primitive = new PrimitiveState { Topology = PrimitiveTopology.TriangleList, FrontFace = FrontFace.CCW, CullMode = CullMode.None },
				Multisample = new MultisampleState { Count = 1, Mask = 0xFFFFFFFF },
				Fragment = &fragment,
			};
			_pipeline = Wgpu.DeviceCreateRenderPipeline(device, &descriptor);
		}
	}

	/// <summary>Records the triangle into <paramref name="encoder"/>, drawing over <paramref name="view"/>.</summary>
	/// <param name="encoder">The frame's encoder.</param>
	/// <param name="view">The frame's texture view.</param>
	internal void Draw(CommandEncoder encoder, TextureView view) => Draw(encoder, view, null);

	/// <summary>
	/// Records the triangle through a native-module function table instead of the binding - what a module that links
	/// to nothing does.
	/// </summary>
	/// <param name="encoder">The frame's encoder.</param>
	/// <param name="view">The frame's texture view.</param>
	/// <param name="procs">The function table, or null for the binding.</param>
	internal void Draw(CommandEncoder encoder, TextureView view, CodebrixProcTable* procs)
	{
		var attachment = new RenderPassColorAttachment
		{
			View = view,
			DepthSlice = Constants.DepthSliceUndefined,
			LoadOp = LoadOp.Load,
			StoreOp = StoreOp.Store,
		};
		var descriptor = new RenderPassDescriptor { ColorAttachmentCount = 1, ColorAttachments = &attachment };
		if (procs is null)
		{
			var pass = Wgpu.CommandEncoderBeginRenderPass(encoder, &descriptor);
			Wgpu.RenderPassEncoderSetPipeline(pass, _pipeline);
			Wgpu.RenderPassEncoderDraw(pass, 3, 1, 0, 0);
			Wgpu.RenderPassEncoderEnd(pass);
			Wgpu.RenderPassEncoderRelease(pass);
		}
		else
		{
			var pass = procs->CommandEncoderBeginRenderPass(encoder, &descriptor);
			procs->RenderPassEncoderSetPipeline(pass, _pipeline);
			procs->RenderPassEncoderDraw(pass, 3, 1, 0, 0);
			procs->RenderPassEncoderEnd(pass);
			procs->RenderPassEncoderRelease(pass);
		}
	}

	public void Dispose()
	{
		if (!_pipeline.IsNull)
		{
			Wgpu.RenderPipelineRelease(_pipeline);
		}

		if (!_module.IsNull)
		{
			Wgpu.ShaderModuleRelease(_module);
		}
	}
}
