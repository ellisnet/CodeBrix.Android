using System;
using System.Text;
using CodeBrix.WebGpu.Native;
using GpuBuffer = CodeBrix.WebGpu.Native.Buffer;
using Wgpu = CodeBrix.WebGpu.Native.WebGpu;

namespace WebGpuDemo.Rendering;

/// <summary>
/// The raw-binding plumbing the demo's own renderers share: one WGSL module with a "vs" and an "fs" entry point,
/// one render pipeline for the view's colour format (the layout derived from the shader), and one small uniform
/// buffer bound at group 0, binding 0. Everything is created once (in DeviceReady) and released in DeviceLost.
/// </summary>
public sealed unsafe class WgslPipeline : IDisposable
{
    private readonly Device _device;
    private readonly Queue _queue;
    private ShaderModule _module;
    private RenderPipeline _pipeline;
    private GpuBuffer _uniforms;
    private BindGroup _bindGroup;

    /// <summary>Builds the module, the pipeline and the uniform buffer.</summary>
    /// <param name="device">The view's device.</param>
    /// <param name="queue">The view's queue.</param>
    /// <param name="wgsl">The shader source (entry points "vs" and "fs"; one uniform at group 0, binding 0).</param>
    /// <param name="format">The colour-target format: the view's PreferredFormat.</param>
    /// <param name="uniformBytes">The uniform block's size in bytes (a multiple of 16).</param>
    public WgslPipeline(Device device, Queue queue, string wgsl, TextureFormat format, int uniformBytes)
    {
        _device = device;
        _queue = queue;
        UniformBytes = uniformBytes;

        var code = Encoding.UTF8.GetBytes(wgsl);
        fixed (byte* text = code)
        {
            var source = new ShaderSourceWGSL { Code = WgpuString.FromPinned(text, code.Length) };
            source.Chain.SType = ShaderSourceWGSL.StructType;
            var descriptor = new ShaderModuleDescriptor { NextInChain = &source.Chain };
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

        var bufferDescriptor = new BufferDescriptor { Usage = BufferUsage.Uniform | BufferUsage.CopyDst, Size = (ulong)uniformBytes };
        _uniforms = Wgpu.DeviceCreateBuffer(device, &bufferDescriptor);

        var layout = Wgpu.RenderPipelineGetBindGroupLayout(_pipeline, 0);
        var entry = new BindGroupEntry { Binding = 0, Buffer = _uniforms, Offset = 0, Size = (ulong)uniformBytes };
        var groupDescriptor = new BindGroupDescriptor { Layout = layout, EntryCount = 1, Entries = &entry };
        _bindGroup = Wgpu.DeviceCreateBindGroup(device, &groupDescriptor);
        Wgpu.BindGroupLayoutRelease(layout);
    }

    /// <summary>The uniform block's size in bytes.</summary>
    public int UniformBytes { get; }

    /// <summary>Uploads the uniform block; the write runs at the start of the frame's submit.</summary>
    /// <param name="data">The block (at least <see cref="UniformBytes"/> bytes).</param>
    public void WriteUniforms(ReadOnlySpan<float> data)
    {
        fixed (float* values = data)
        {
            Wgpu.QueueWriteBuffer(_queue, _uniforms, 0, values, (nuint)UniformBytes);
        }
    }

    /// <summary>Records one pass over <paramref name="view"/> that keeps what is there (LoadOp.Load) and draws
    /// <paramref name="vertexCount"/> vertices <paramref name="instanceCount"/> times.</summary>
    /// <param name="encoder">The frame's encoder.</param>
    /// <param name="view">The frame's texture view.</param>
    /// <param name="vertexCount">Vertices per instance.</param>
    /// <param name="instanceCount">Instances.</param>
    public void Draw(CommandEncoder encoder, TextureView view, uint vertexCount, uint instanceCount = 1)
    {
        var attachment = new RenderPassColorAttachment
        {
            View = view,
            DepthSlice = Constants.DepthSliceUndefined,
            LoadOp = LoadOp.Load,
            StoreOp = StoreOp.Store,
        };
        var descriptor = new RenderPassDescriptor { ColorAttachmentCount = 1, ColorAttachments = &attachment };
        var pass = Wgpu.CommandEncoderBeginRenderPass(encoder, &descriptor);
        Wgpu.RenderPassEncoderSetPipeline(pass, _pipeline);
        Wgpu.RenderPassEncoderSetBindGroup(pass, 0, _bindGroup, 0, null);
        Wgpu.RenderPassEncoderDraw(pass, vertexCount, instanceCount, 0, 0);
        Wgpu.RenderPassEncoderEnd(pass);
        Wgpu.RenderPassEncoderRelease(pass);
    }

    /// <summary>Releases everything this object created (never the device or the queue: the view owns those).</summary>
    public void Dispose()
    {
        if (!_bindGroup.IsNull) { Wgpu.BindGroupRelease(_bindGroup); _bindGroup = default; }
        if (!_uniforms.IsNull) { Wgpu.BufferRelease(_uniforms); _uniforms = default; }
        if (!_pipeline.IsNull) { Wgpu.RenderPipelineRelease(_pipeline); _pipeline = default; }
        if (!_module.IsNull) { Wgpu.ShaderModuleRelease(_module); _module = default; }
    }
}
