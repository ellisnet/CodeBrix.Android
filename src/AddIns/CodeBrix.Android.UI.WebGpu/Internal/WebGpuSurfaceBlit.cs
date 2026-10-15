#nullable enable

using System;
using CodeBrix.WebGpu.Native;
using Wgpu = CodeBrix.WebGpu.Native.WebGpu;

namespace CodeBrix.Platform.UI.WebGpu.Internal;

/// <summary>
/// The DirectSurface path's one GPU pass: draws the view's finished frame (a BGRA8Unorm texture, the format every render
/// path renders in) onto a window surface's frame of another colour format (Android surfaces offer RGBA8Unorm, not BGRA),
/// texel for texel - one full-screen triangle that loads each texel, no sampling and no copy to processor memory.
/// </summary>
/// <remarks>
/// <para>
/// The pipeline and its bind-group layout are made once per destination format; the bind group once per source texture
/// view (a new frame texture comes only with a new size), so recording a frame allocates nothing.
/// </para>
/// <para>
/// Every creation runs inside a validation error scope: a failure is reported as a reason, never as a device error.
/// Every member runs on the thread that holds the view's frame gate.
/// </para>
/// </remarks>
internal sealed unsafe class WebGpuSurfaceBlit : IDisposable
{
	// A triangle that covers the whole target; each fragment loads the source texel at its own pixel position (the
	// source and the destination have the same size). Opaque output: the window surface is configured opaque.
	private const string Wgsl =
		"@group(0) @binding(0) var source: texture_2d<f32>;\n"
		+ "@vertex fn vs(@builtin(vertex_index) index: u32) -> @builtin(position) vec4<f32> {\n"
		+ "    var corners = array<vec2<f32>, 3>(vec2<f32>(-1.0, -1.0), vec2<f32>(3.0, -1.0), vec2<f32>(-1.0, 3.0));\n"
		+ "    return vec4<f32>(corners[index], 0.0, 1.0);\n"
		+ "}\n"
		+ "@fragment fn fs(@builtin(position) position: vec4<f32>) -> @location(0) vec4<f32> {\n"
		+ "    let size = vec2<i32>(textureDimensions(source));\n"
		+ "    let texel = min(vec2<i32>(position.xy), size - vec2<i32>(1, 1));\n"
		+ "    return vec4<f32>(textureLoad(source, texel, 0).rgb, 1.0);\n"
		+ "}\n";

	private readonly Instance _instance;
	private readonly Device _device;
	private ShaderModule _module;
	private RenderPipeline _pipeline;
	private BindGroupLayout _layout;
	private BindGroup _bindGroup;
	private TextureView _boundSource;

	private WebGpuSurfaceBlit(Instance instance, Device device, TextureFormat destinationFormat)
	{
		_instance = instance;
		_device = device;
		DestinationFormat = destinationFormat;
	}

	/// <summary>The colour format the pass draws into.</summary>
	internal TextureFormat DestinationFormat { get; }

	/// <summary>Creates the pass for a destination format on a device.</summary>
	/// <param name="instance">The device's instance (its error scopes are pumped through it).</param>
	/// <param name="device">The device.</param>
	/// <param name="destinationFormat">The window surface's format.</param>
	/// <param name="reason">Why the pass could not be created, or null.</param>
	/// <returns>The pass, or null (with <paramref name="reason"/>).</returns>
	internal static WebGpuSurfaceBlit? Create(Instance instance, Device device, TextureFormat destinationFormat, out string? reason)
	{
		var blit = new WebGpuSurfaceBlit(instance, device, destinationFormat);
		WebGpuErrorScope.Push(device);
		blit.CreatePipeline();
		var error = WebGpuErrorScope.Pop(instance, device);
		if (error is not null || blit._pipeline.IsNull || blit._layout.IsNull)
		{
			blit.Dispose();
			reason = "the pass that draws the frame onto the window surface could not be created: " + (error ?? "no pipeline");
			return null;
		}

		reason = null;
		return blit;
	}

	/// <summary>
	/// Records the pass: <paramref name="source"/> (a frame texture view of the destination's size) drawn onto
	/// <paramref name="destination"/>.
	/// </summary>
	/// <param name="encoder">The encoder.</param>
	/// <param name="source">The view's finished frame.</param>
	/// <param name="destination">The window surface frame's view.</param>
	/// <param name="reason">Why nothing was recorded, or null.</param>
	/// <returns>True when the pass was recorded.</returns>
	internal bool Record(CommandEncoder encoder, TextureView source, TextureView destination, out string? reason)
	{
		if (!EnsureBindGroup(source, out reason))
		{
			return false;
		}

		var attachment = new RenderPassColorAttachment
		{
			View = destination,
			DepthSlice = Constants.DepthSliceUndefined,
			LoadOp = LoadOp.Clear,
			StoreOp = StoreOp.Store,
			ClearValue = new Color { R = 0, G = 0, B = 0, A = 1 },
		};
		var descriptor = new RenderPassDescriptor { ColorAttachmentCount = 1, ColorAttachments = &attachment };
		var pass = Wgpu.CommandEncoderBeginRenderPass(encoder, &descriptor);
		Wgpu.RenderPassEncoderSetPipeline(pass, _pipeline);
		Wgpu.RenderPassEncoderSetBindGroup(pass, 0, _bindGroup, 0, null);
		Wgpu.RenderPassEncoderDraw(pass, 3, 1, 0, 0);
		Wgpu.RenderPassEncoderEnd(pass);
		Wgpu.RenderPassEncoderRelease(pass);
		return true;
	}

	/// <summary>Forgets the bind group of a source texture view that is about to be released.</summary>
	internal void ForgetSource()
	{
		if (!_bindGroup.IsNull)
		{
			Wgpu.BindGroupRelease(_bindGroup);
		}

		_bindGroup = BindGroup.Null;
		_boundSource = TextureView.Null;
	}

	/// <summary>Releases the pipeline objects.</summary>
	public void Dispose()
	{
		ForgetSource();
		if (!_pipeline.IsNull)
		{
			Wgpu.RenderPipelineRelease(_pipeline);
			_pipeline = RenderPipeline.Null;
		}

		if (!_layout.IsNull)
		{
			Wgpu.BindGroupLayoutRelease(_layout);
			_layout = BindGroupLayout.Null;
		}

		if (!_module.IsNull)
		{
			Wgpu.ShaderModuleRelease(_module);
			_module = ShaderModule.Null;
		}
	}

	private bool EnsureBindGroup(TextureView source, out string? reason)
	{
		reason = null;
		if (!_bindGroup.IsNull && _boundSource.Handle == source.Handle)
		{
			return true;
		}

		ForgetSource();
		var entry = new BindGroupEntry { Binding = 0, TextureView = source };
		var descriptor = new BindGroupDescriptor { Layout = _layout, EntryCount = 1, Entries = &entry };
		WebGpuErrorScope.Push(_device);
		var group = Wgpu.DeviceCreateBindGroup(_device, &descriptor);
		var error = WebGpuErrorScope.Pop(_instance, _device);
		if (error is not null || group.IsNull)
		{
			if (!group.IsNull)
			{
				Wgpu.BindGroupRelease(group);
			}

			reason = "the frame texture could not be bound for the window surface pass: " + (error ?? "no bind group");
			return false;
		}

		_bindGroup = group;
		_boundSource = source;
		return true;
	}

	private void CreatePipeline()
	{
		var code = System.Text.Encoding.UTF8.GetBytes(Wgsl);
		fixed (byte* text = code)
		{
			var wgsl = new ShaderSourceWGSL { Code = WgpuString.FromPinned(text, code.Length) };
			wgsl.Chain.SType = ShaderSourceWGSL.StructType;
			var descriptor = new ShaderModuleDescriptor { NextInChain = &wgsl.Chain };
			_module = Wgpu.DeviceCreateShaderModule(_device, &descriptor);
		}

		if (_module.IsNull)
		{
			return;
		}

		var layoutEntry = new BindGroupLayoutEntry
		{
			Binding = 0,
			Visibility = ShaderStage.Fragment,
			Texture = new TextureBindingLayout
			{
				SampleType = TextureSampleType.UnfilterableFloat,
				ViewDimension = TextureViewDimension.Dimension2D,
				Multisampled = 0,
			},
		};
		var layoutDescriptor = new BindGroupLayoutDescriptor { EntryCount = 1, Entries = &layoutEntry };
		_layout = Wgpu.DeviceCreateBindGroupLayout(_device, &layoutDescriptor);
		if (_layout.IsNull)
		{
			return;
		}

		var layout = _layout;
		var pipelineLayoutDescriptor = new PipelineLayoutDescriptor { BindGroupLayoutCount = 1, BindGroupLayouts = &layout };
		var pipelineLayout = Wgpu.DeviceCreatePipelineLayout(_device, &pipelineLayoutDescriptor);
		if (pipelineLayout.IsNull)
		{
			return;
		}

		fixed (byte* vs = "vs"u8)
		fixed (byte* fs = "fs"u8)
		{
			var target = new ColorTargetState { Format = DestinationFormat, WriteMask = ColorWriteMask.All };
			var fragment = new FragmentState
			{
				Module = _module,
				EntryPoint = WgpuString.FromPinned(fs, 2),
				TargetCount = 1,
				Targets = &target,
			};
			var descriptor = new RenderPipelineDescriptor
			{
				Layout = pipelineLayout,
				Vertex = new VertexState { Module = _module, EntryPoint = WgpuString.FromPinned(vs, 2) },
				Primitive = new PrimitiveState { Topology = PrimitiveTopology.TriangleList, FrontFace = FrontFace.CCW, CullMode = CullMode.None },
				Multisample = new MultisampleState { Count = 1, Mask = 0xFFFFFFFF },
				Fragment = &fragment,
			};
			_pipeline = Wgpu.DeviceCreateRenderPipeline(_device, &descriptor);
		}

		Wgpu.PipelineLayoutRelease(pipelineLayout);
	}
}
