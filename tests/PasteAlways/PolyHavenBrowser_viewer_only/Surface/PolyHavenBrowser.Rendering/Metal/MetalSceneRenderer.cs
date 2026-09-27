using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using static PolyHavenBrowser.Rendering.MetalInterop;

namespace PolyHavenBrowser.Rendering;

/// <summary>
/// A self-contained <b>offscreen Metal renderer</b> for previewing one <see cref="LoadedModel"/>
/// with an orbit camera - the direct-to-Metal counterpart of <see cref="VulkanSceneRenderer"/>
/// and <see cref="GlModelSceneRenderer"/>, drawing the same scene with the same shading (the MSL
/// in <see cref="MetalShaders"/> mirrors the GL/SPIR-V shaders). It talks to Metal through the
/// raw Objective-C runtime (see <see cref="MetalInterop"/>): no MoltenVK, no Silk.NET, no managed
/// Apple bindings, and no NuGet packages.
/// <para>
/// Like the Vulkan renderer, Metal has no ambient thread context - so this class owns the whole
/// stack: device, command queue, shader library, pipeline + depth-stencil states, offscreen
/// color + depth targets, per-model buffers/textures, and the CPU readback. Everything is created
/// lazily on the first <see cref="RenderFrame"/> call and it never touches any window system, so
/// it cannot disturb the host head's own renderer. <see cref="SetModel"/> may be called from any
/// thread; the GPU upload happens on the next render.
/// </para>
/// <para>
/// <b>Both Macs.</b> Every GPU→CPU and CPU→GPU transfer goes through a <c>MTLStorageModeShared</c>
/// staging <i>buffer</i> blitted to/from a <c>MTLStorageModePrivate</c> texture. That combination
/// is supported identically on Apple Silicon, on Intel, and on an x86-64 process under Rosetta 2,
/// unlike shared/managed <i>textures</i> whose availability splits by GPU family. Buffer↔texture
/// blits on macOS require 256-byte row alignment, so staging rows are padded to
/// <see cref="RowAlignment"/> and de-padded on the way out.
/// </para>
/// <para>
/// Coordinate conventions: the camera's System.Numerics matrices are used <b>unmodified</b>. MSL
/// reads <c>float4x4</c> column-major, applying the same implicit transpose the GL/Vulkan
/// backends rely on. The projection maps depth to [0, 1], Metal's native range. Metal's clip-space
/// Y points up while its framebuffer origin is top-left, so the readback comes out <b>top-down</b>
/// - the Metal engine therefore reports <c>IsBottomUp: false</c> (unlike GL and Vulkan). Culling
/// is disabled, matching the GL renderer, because Poly Haven models frequently rely on
/// double-sided geometry.
/// </para>
/// </summary>
public sealed unsafe class MetalSceneRenderer : IDisposable
{
    // ---- Metal enum constants (as plain integers, so no Metal headers are needed) ----------
    private const uint PixelFormatRGBA8Unorm = 70;
    private const uint PixelFormatDepth32Float = 252;
    private const uint TextureType2D = 2;
    private const uint TextureUsageShaderRead = 1;
    private const uint TextureUsageRenderTarget = 4;
    private const uint StorageModePrivate = 2;
    private const uint ResourceStorageModeShared = 0;
    private const uint LoadActionDontCare = 0;
    private const uint LoadActionClear = 2;
    private const uint StoreActionDontCare = 0;
    private const uint StoreActionStore = 1;
    private const uint PrimitiveTypeTriangle = 3;
    private const uint IndexTypeUInt32 = 1;
    private const uint VertexFormatFloat2 = 29;
    private const uint VertexFormatFloat3 = 30;
    private const uint StepFunctionPerVertex = 1;
    private const uint CompareFunctionLess = 1;
    private const uint CullModeNone = 0;
    private const uint BlendOperationAdd = 0;
    private const uint BlendFactorOne = 1;
    private const uint BlendFactorSourceAlpha = 4;
    private const uint BlendFactorOneMinusSourceAlpha = 5;
    private const uint SamplerFilterLinear = 1;
    private const uint SamplerMipNotMipmapped = 0;
    private const uint SamplerAddressRepeat = 2;

    // Buffer↔texture blit copies on macOS require the row byte-count be a multiple of 256.
    private const int RowAlignment = 256;

    // The 112-byte per-draw block; layout matches the MSL `Uniforms` struct (mvp, baseColorFactor,
    // lightDirection, flags). The Matrix4x4 is written in .NET's row-major order and MSL reads a
    // mat4 column-major - the same implicit transpose the GL/Vulkan backends rely on.
    [StructLayout(LayoutKind.Sequential)]
    private struct Uniforms
    {
        public Matrix4x4 Mvp;
        public Vector4 BaseColorFactor;
        public Vector4 LightDirection;
        public int HasTexture;
        public int DoubleSided;
        public int Pad0;
        public int Pad1;
    }

    private sealed record GpuPrimitive(
        IntPtr Positions, IntPtr Normals, IntPtr TexCoords, IntPtr Indices,
        nuint IndexCount, int MaterialIndex);

    private readonly object _pendingLock = new();
    private LoadedModel? _pendingModel;
    private bool _pendingFrameCamera;
    private bool _hasPendingModel;

    private LoadedModel? _currentModel;
    private readonly List<GpuPrimitive> _gpuPrimitives = [];
    private readonly Dictionary<int, IntPtr> _materialTextures = [];

    private IntPtr _device;
    private IntPtr _queue;
    private IntPtr _library;
    private IntPtr _vertexFunction;
    private IntPtr _fragmentFunction;
    private IntPtr _opaquePipeline;   // blending disabled, depth writes on
    private IntPtr _blendPipeline;    // straight-alpha "over", depth writes off (via _depthNoWrite)
    private IntPtr _depthWrite;       // depth test Less, writes enabled
    private IntPtr _depthNoWrite;     // depth test Less, writes disabled (translucent pass)
    private IntPtr _sampler;
    private IntPtr _whiteTexture;     // 1x1 white fallback bound for untextured materials

    private IntPtr _colorTexture;
    private IntPtr _depthTexture;
    private IntPtr _readbackBuffer;
    private nuint _readbackRowBytes;
    private uint _targetWidth;
    private uint _targetHeight;

    private bool _initialized;
    private bool _disposed;

    /// <summary>The orbit camera driven by the host's pointer/scroll input.</summary>
    public OrbitCamera Camera { get; } = new();

    /// <summary>
    /// A fixed world-space light direction (pointing toward the light), or <see langword="null"/>
    /// (the default) for a camera headlight with double-sided lighting - the same semantics as
    /// <see cref="GlModelSceneRenderer.FixedLightDirection"/>.
    /// </summary>
    public Vector3? FixedLightDirection { get; set; }

    /// <summary>
    /// Whether a usable Metal device is present at runtime (macOS on a supported process
    /// architecture). Intended for tests; the app itself gates Metal on
    /// <see cref="MetalPlatformSupport"/> instead.
    /// </summary>
    public static bool IsRuntimeAvailable()
    {
        if (!MetalPlatformSupport.IsSupportedProcessArchitecture)
        {
            return false;
        }

        try
        {
            var device = MTLCreateSystemDefaultDevice();
            if (device == IntPtr.Zero)
            {
                return false;
            }
            Release(device);
            return true;
        }
        catch
        {
            // Metal.framework is absent (non-macOS) or otherwise unavailable.
            return false;
        }
    }

    /// <summary>
    /// Sets the model to display (or <see langword="null"/> to clear). Takes effect on the next
    /// <see cref="RenderFrame"/>; when <paramref name="frameCamera"/> is true the camera is
    /// re-framed to the model's bounds at that time. Safe to call from any thread.
    /// </summary>
    public void SetModel(LoadedModel? model, bool frameCamera = true)
    {
        lock (_pendingLock)
        {
            _pendingModel = model;
            _pendingFrameCamera = frameCamera;
            _hasPendingModel = true;
        }
    }

    /// <summary>
    /// Renders the current model at the given pixel size over the given background colour and
    /// returns the frame as tightly packed RGBA bytes. As explained in the class remarks the
    /// image is top-down; call on the render thread.
    /// </summary>
    public byte[] RenderFrame(int width, int height, (float R, float G, float B, float A) background)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);

        // Every autoreleased object created below (command buffer, encoders, the per-frame render
        // pass descriptor) is drained when this pool is released - essential off the main thread,
        // where there is no ambient pool to catch them.
        var pool = NewAutoreleasePool();
        try
        {
            EnsureInitialized();
            ApplyPendingModel();
            EnsureRenderTargets((uint)width, (uint)height);

            var commandBuffer = GetId(_queue, "commandBuffer");

            // Build the render pass: clear the colour target to the background and the depth
            // target to the far plane; keep only the colour result (depth is transient).
            var pass = GetId(Cls("MTLRenderPassDescriptor"), "renderPassDescriptor");
            var colorAttachment = GetIdAt(GetId(pass, "colorAttachments"), "objectAtIndexedSubscript:", 0);
            SetPtr(colorAttachment, "setTexture:", _colorTexture);
            SetUInt(colorAttachment, "setLoadAction:", LoadActionClear);
            SetUInt(colorAttachment, "setStoreAction:", StoreActionStore);
            SendV(colorAttachment, Sel("setClearColor:"), new MTLClearColor
            {
                Red = background.R, Green = background.G, Blue = background.B, Alpha = background.A,
            });
            var depthAttachment = GetId(pass, "depthAttachment");
            SetPtr(depthAttachment, "setTexture:", _depthTexture);
            SetUInt(depthAttachment, "setLoadAction:", LoadActionClear);
            SetUInt(depthAttachment, "setStoreAction:", StoreActionDontCare);
            SendV(depthAttachment, Sel("setClearDepth:"), 1.0);

            var encoder = Send(commandBuffer, Sel("renderCommandEncoderWithDescriptor:"), pass);
            SetUInt(encoder, "setCullMode:", CullModeNone);
            SendV(encoder, Sel("setViewport:"), new MTLViewport
            {
                OriginX = 0, OriginY = 0, Width = width, Height = height, ZNear = 0, ZFar = 1,
            });

            if (_currentModel is not null && _gpuPrimitives.Count > 0)
            {
                var uniforms = BuildFrameUniforms(width, height);

                // Two passes, exactly as the GL/Vulkan renderers: opaque (and mask) first with
                // depth writes on, then translucent (BLEND) primitives with straight-alpha "over"
                // blending and depth writes off, so glass surfaces show what's behind them.
                SetPtr(encoder, "setRenderPipelineState:", _opaquePipeline);
                SetPtr(encoder, "setDepthStencilState:", _depthWrite);
                DrawPrimitives(encoder, uniforms, blendPass: false);

                SetPtr(encoder, "setRenderPipelineState:", _blendPipeline);
                SetPtr(encoder, "setDepthStencilState:", _depthNoWrite);
                DrawPrimitives(encoder, uniforms, blendPass: true);
            }

            Call(encoder, "endEncoding");

            // Copy the rendered colour target into the host-visible readback buffer.
            var blit = GetId(commandBuffer, "blitCommandEncoder");
            SendCopyTextureToBuffer(
                blit, Sel("copyFromTexture:sourceSlice:sourceLevel:sourceOrigin:sourceSize:toBuffer:destinationOffset:destinationBytesPerRow:destinationBytesPerImage:"),
                _colorTexture, 0, 0, default, new MTLSize { Width = (nuint)width, Height = (nuint)height, Depth = 1 },
                _readbackBuffer, 0, _readbackRowBytes, _readbackRowBytes * (nuint)height);
            Call(blit, "endEncoding");

            Call(commandBuffer, "commit");
            Call(commandBuffer, "waitUntilCompleted");

            return ReadbackToBytes(width, height);
        }
        finally
        {
            Release(pool);
        }
    }

    // Fills in the per-frame values shared by every primitive: the camera MVP, the light
    // direction, and the double-sided flag. BaseColorFactor and HasTexture are set per primitive.
    private Uniforms BuildFrameUniforms(int width, int height)
    {
        // MVP = view * projection (node transforms baked at load time), uploaded WITHOUT an extra
        // transpose - MSL's column-major read applies the transpose GL/Vulkan also rely on.
        var mvp = Camera.GetViewMatrix() * Camera.GetProjectionMatrix(width / (float)height);

        var lightDirection = FixedLightDirection is { } fixedLight && fixedLight != Vector3.Zero
            ? Vector3.Normalize(fixedLight)
            : Vector3.Normalize(Camera.GetEyePosition() - Camera.Target);

        return new Uniforms
        {
            Mvp = mvp,
            LightDirection = new Vector4(lightDirection, 0f),
            DoubleSided = FixedLightDirection is null ? 1 : 0,
        };
    }

    // Draws either the opaque/mask primitives (blendPass = false) or the translucent BLEND
    // primitives (blendPass = true). BLEND materials get a fixed preview opacity so glass surfaces
    // let the geometry behind them show through. `uniforms` is a by-value copy we mutate per draw.
    private void DrawPrimitives(IntPtr encoder, Uniforms uniforms, bool blendPass)
    {
        foreach (var primitive in _gpuPrimitives)
        {
            var material = primitive.MaterialIndex >= 0 && primitive.MaterialIndex < _currentModel!.Materials.Count
                ? _currentModel.Materials[primitive.MaterialIndex]
                : null;
            var isBlend = material?.AlphaMode == ModelAlphaMode.Blend;
            if (isBlend != blendPass)
            {
                continue;
            }

            var baseColor = material?.BaseColorFactor ?? Vector4.One;
            var alpha = isBlend ? ModelMaterial.BlendPreviewOpacity * baseColor.W : baseColor.W;
            var hasTexture = _materialTextures.TryGetValue(primitive.MaterialIndex, out var texture);

            uniforms.BaseColorFactor = new Vector4(baseColor.X, baseColor.Y, baseColor.Z, alpha);
            uniforms.HasTexture = hasTexture ? 1 : 0;

            SendV(encoder, Sel("setVertexBuffer:offset:atIndex:"), primitive.Positions, 0, 0);
            SendV(encoder, Sel("setVertexBuffer:offset:atIndex:"), primitive.Normals, 0, 1);
            SendV(encoder, Sel("setVertexBuffer:offset:atIndex:"), primitive.TexCoords, 0, 2);

            // A local unmanaged struct: its address is stack-based, so no `fixed` is needed.
            var block = uniforms;
            SendV(encoder, Sel("setVertexBytes:length:atIndex:"), (IntPtr)(&block), (nuint)sizeof(Uniforms), MetalShaders.UniformBufferIndex);
            SendV(encoder, Sel("setFragmentBytes:length:atIndex:"), (IntPtr)(&block), (nuint)sizeof(Uniforms), MetalShaders.UniformBufferIndex);

            // The shader's hasTexture flag skips the sample when false, but Metal still needs a
            // valid texture bound at index 0 - the 1x1 white fallback serves untextured materials.
            SendV(encoder, Sel("setFragmentTexture:atIndex:"), hasTexture ? texture : _whiteTexture, 0);
            SendV(encoder, Sel("setFragmentSamplerState:atIndex:"), _sampler, 0);

            SendV(encoder, Sel("drawIndexedPrimitives:indexCount:indexType:indexBuffer:indexBufferOffset:"),
                PrimitiveTypeTriangle, primitive.IndexCount, IndexTypeUInt32, primitive.Indices, 0);
        }
    }

    // Copies the readback buffer's 256-aligned rows into a tightly packed RGBA byte array.
    private byte[] ReadbackToBytes(int width, int height)
    {
        var pixels = new byte[width * height * 4];
        var rowBytes = width * 4;
        var contents = GetId(_readbackBuffer, "contents");
        for (var y = 0; y < height; y++)
        {
            Marshal.Copy(IntPtr.Add(contents, y * (int)_readbackRowBytes), pixels, y * rowBytes, rowBytes);
        }
        return pixels;
    }

    #region | One-time initialization: device, shaders, pipelines |

    private void EnsureInitialized()
    {
        if (_initialized) { return; }

        _device = MTLCreateSystemDefaultDevice();
        if (_device == IntPtr.Zero)
        {
            throw new InvalidOperationException("No Metal device is available on this machine.");
        }
        _queue = GetId(_device, "newCommandQueue");

        CreateLibraryAndFunctions();
        CreatePipelines();
        CreateDepthStates();
        CreateSampler();
        _whiteTexture = UploadTexture([255, 255, 255, 255], 1, 1);

        _initialized = true;
    }

    private void CreateLibraryAndFunctions()
    {
        var source = NSString(MetalShaders.Source);
        _library = Send(_device, Sel("newLibraryWithSource:options:error:"), source, IntPtr.Zero, out var error);
        if (_library == IntPtr.Zero)
        {
            throw new InvalidOperationException(
                "Metal shader compilation failed: " + (NSStringToManaged(GetId(error, "localizedDescription")) ?? "unknown error"));
        }

        _vertexFunction = Send(_library, Sel("newFunctionWithName:"), NSString("vertex_main"));
        _fragmentFunction = Send(_library, Sel("newFunctionWithName:"), NSString("fragment_main"));
        if (_vertexFunction == IntPtr.Zero || _fragmentFunction == IntPtr.Zero)
        {
            throw new InvalidOperationException("The Metal shader library is missing an expected function.");
        }
    }

    private void CreatePipelines()
    {
        var vertexDescriptor = BuildVertexDescriptor();
        try
        {
            _opaquePipeline = BuildPipeline(vertexDescriptor, blend: false);
            _blendPipeline = BuildPipeline(vertexDescriptor, blend: true);
        }
        finally
        {
            Release(vertexDescriptor);
        }
    }

    // Three separate vertex buffers (position/normal/texcoord) at buffer indices 0/1/2, matching
    // the MSL attribute layout; the uniform block rides at index 3 (set with setVertexBytes).
    private static IntPtr BuildVertexDescriptor()
    {
        var descriptor = New("MTLVertexDescriptor");
        var attributes = GetId(descriptor, "attributes");
        var layouts = GetId(descriptor, "layouts");

        ConfigureAttribute(attributes, 0, VertexFormatFloat3, bufferIndex: 0);
        ConfigureAttribute(attributes, 1, VertexFormatFloat3, bufferIndex: 1);
        ConfigureAttribute(attributes, 2, VertexFormatFloat2, bufferIndex: 2);
        ConfigureLayout(layouts, 0, stride: 12);
        ConfigureLayout(layouts, 1, stride: 12);
        ConfigureLayout(layouts, 2, stride: 8);

        return descriptor;
    }

    private static void ConfigureAttribute(IntPtr attributes, nuint index, uint format, nuint bufferIndex)
    {
        var attribute = GetIdAt(attributes, "objectAtIndexedSubscript:", index);
        SetUInt(attribute, "setFormat:", format);
        SetUInt(attribute, "setOffset:", 0);
        SetUInt(attribute, "setBufferIndex:", bufferIndex);
    }

    private static void ConfigureLayout(IntPtr layouts, nuint index, nuint stride)
    {
        var layout = GetIdAt(layouts, "objectAtIndexedSubscript:", index);
        SetUInt(layout, "setStride:", stride);
        SetUInt(layout, "setStepFunction:", StepFunctionPerVertex);
    }

    private IntPtr BuildPipeline(IntPtr vertexDescriptor, bool blend)
    {
        var descriptor = New("MTLRenderPipelineDescriptor");
        try
        {
            SetPtr(descriptor, "setVertexFunction:", _vertexFunction);
            SetPtr(descriptor, "setFragmentFunction:", _fragmentFunction);
            SetPtr(descriptor, "setVertexDescriptor:", vertexDescriptor);
            SetUInt(descriptor, "setDepthAttachmentPixelFormat:", PixelFormatDepth32Float);

            var colorAttachment = GetIdAt(GetId(descriptor, "colorAttachments"), "objectAtIndexedSubscript:", 0);
            SetUInt(colorAttachment, "setPixelFormat:", PixelFormatRGBA8Unorm);
            if (blend)
            {
                // Straight-alpha "over": colour weighted by source alpha, alpha channel accumulates
                // coverage (One, OneMinusSrcAlpha) so a region already opaque behind the glass
                // stays opaque for the Skia composite - the GL BlendFuncSeparate, mirrored.
                SetBool(colorAttachment, "setBlendingEnabled:", true);
                SetUInt(colorAttachment, "setRgbBlendOperation:", BlendOperationAdd);
                SetUInt(colorAttachment, "setAlphaBlendOperation:", BlendOperationAdd);
                SetUInt(colorAttachment, "setSourceRGBBlendFactor:", BlendFactorSourceAlpha);
                SetUInt(colorAttachment, "setDestinationRGBBlendFactor:", BlendFactorOneMinusSourceAlpha);
                SetUInt(colorAttachment, "setSourceAlphaBlendFactor:", BlendFactorOne);
                SetUInt(colorAttachment, "setDestinationAlphaBlendFactor:", BlendFactorOneMinusSourceAlpha);
            }

            var pipeline = Send(_device, Sel("newRenderPipelineStateWithDescriptor:error:"), descriptor, out var error);
            if (pipeline == IntPtr.Zero)
            {
                throw new InvalidOperationException(
                    "Metal pipeline creation failed: " + (NSStringToManaged(GetId(error, "localizedDescription")) ?? "unknown error"));
            }
            return pipeline;
        }
        finally
        {
            Release(descriptor);
        }
    }

    private void CreateDepthStates()
    {
        _depthWrite = BuildDepthState(writeEnabled: true);
        _depthNoWrite = BuildDepthState(writeEnabled: false);
    }

    private IntPtr BuildDepthState(bool writeEnabled)
    {
        var descriptor = New("MTLDepthStencilDescriptor");
        try
        {
            SetUInt(descriptor, "setDepthCompareFunction:", CompareFunctionLess);
            SetBool(descriptor, "setDepthWriteEnabled:", writeEnabled);
            return Send(_device, Sel("newDepthStencilStateWithDescriptor:"), descriptor);
        }
        finally
        {
            Release(descriptor);
        }
    }

    private void CreateSampler()
    {
        var descriptor = New("MTLSamplerDescriptor");
        try
        {
            SetUInt(descriptor, "setMinFilter:", SamplerFilterLinear);
            SetUInt(descriptor, "setMagFilter:", SamplerFilterLinear);
            SetUInt(descriptor, "setMipFilter:", SamplerMipNotMipmapped);
            SetUInt(descriptor, "setSAddressMode:", SamplerAddressRepeat);
            SetUInt(descriptor, "setTAddressMode:", SamplerAddressRepeat);
            _sampler = Send(_device, Sel("newSamplerStateWithDescriptor:"), descriptor);
        }
        finally
        {
            Release(descriptor);
        }
    }

    #endregion

    #region | Render targets, model upload, texture upload |

    // Creates (or re-creates on a size change) the offscreen colour + depth targets and the
    // host-visible readback buffer sized to the (256-aligned) colour rows.
    private void EnsureRenderTargets(uint width, uint height)
    {
        if (_colorTexture != IntPtr.Zero && _targetWidth == width && _targetHeight == height)
        {
            return;
        }

        DestroyRenderTargets();

        _colorTexture = NewPrivateTexture(PixelFormatRGBA8Unorm, width, height, TextureUsageRenderTarget);
        _depthTexture = NewPrivateTexture(PixelFormatDepth32Float, width, height, TextureUsageRenderTarget);
        _readbackRowBytes = Align((nuint)width * 4);
        _readbackBuffer = Send(_device, Sel("newBufferWithLength:options:"), _readbackRowBytes * height, (nuint)ResourceStorageModeShared);

        _targetWidth = width;
        _targetHeight = height;
    }

    private IntPtr NewPrivateTexture(uint pixelFormat, uint width, uint height, uint usage)
    {
        var descriptor = New("MTLTextureDescriptor");
        try
        {
            SetUInt(descriptor, "setTextureType:", TextureType2D);
            SetUInt(descriptor, "setPixelFormat:", pixelFormat);
            SetUInt(descriptor, "setWidth:", width);
            SetUInt(descriptor, "setHeight:", height);
            SetUInt(descriptor, "setUsage:", usage);
            SetUInt(descriptor, "setStorageMode:", StorageModePrivate);
            return Send(_device, Sel("newTextureWithDescriptor:"), descriptor);
        }
        finally
        {
            Release(descriptor);
        }
    }

    private void ApplyPendingModel()
    {
        LoadedModel? model;
        bool frameCamera;
        lock (_pendingLock)
        {
            if (!_hasPendingModel)
            {
                return;
            }
            model = _pendingModel;
            frameCamera = _pendingFrameCamera;
            _pendingModel = null;
            _hasPendingModel = false;
        }

        ReleaseModelResources();
        _currentModel = model;
        if (model is null)
        {
            return;
        }

        foreach (var primitive in model.Primitives)
        {
            _gpuPrimitives.Add(new GpuPrimitive(
                UploadFloatBuffer(primitive.Positions),
                UploadFloatBuffer(primitive.Normals),
                UploadFloatBuffer(primitive.TexCoords),
                UploadIndexBuffer(primitive.Indices),
                (nuint)primitive.Indices.Length,
                primitive.MaterialIndex));
        }

        for (var materialIndex = 0; materialIndex < model.Materials.Count; materialIndex++)
        {
            var material = model.Materials[materialIndex];
            if (material.BaseColorTextureRgba is { Length: > 0 } rgba)
            {
                _materialTextures[materialIndex] = UploadTexture(
                    rgba, (uint)material.BaseColorTextureWidth, (uint)material.BaseColorTextureHeight);
            }
        }

        if (frameCamera)
        {
            Camera.FitToModel(model);
        }
    }

    private IntPtr UploadFloatBuffer(float[] data)
    {
        fixed (float* p = data)
        {
            return Send(_device, Sel("newBufferWithBytes:length:options:"),
                (IntPtr)p, (nuint)(data.Length * sizeof(float)), (nuint)ResourceStorageModeShared);
        }
    }

    private IntPtr UploadIndexBuffer(uint[] data)
    {
        fixed (uint* p = data)
        {
            return Send(_device, Sel("newBufferWithBytes:length:options:"),
                (IntPtr)p, (nuint)(data.Length * sizeof(uint)), (nuint)ResourceStorageModeShared);
        }
    }

    // Uploads an RGBA image to a private, sampleable 2D texture via a shared staging buffer (the
    // one path that works on Apple Silicon, Intel, and Rosetta alike); rows are 256-padded for the
    // buffer→texture blit.
    private IntPtr UploadTexture(byte[] rgba, uint width, uint height)
    {
        var texture = NewPrivateTexture(PixelFormatRGBA8Unorm, width, height, TextureUsageShaderRead);

        var tightRow = (int)width * 4;
        var alignedRow = Align((nuint)tightRow);
        var staging = Send(_device, Sel("newBufferWithLength:options:"), alignedRow * height, (nuint)ResourceStorageModeShared);
        try
        {
            var contents = GetId(staging, "contents");
            for (var y = 0; y < height; y++)
            {
                Marshal.Copy(rgba, y * tightRow, IntPtr.Add(contents, y * (int)alignedRow), tightRow);
            }

            var commandBuffer = GetId(_queue, "commandBuffer");
            var blit = GetId(commandBuffer, "blitCommandEncoder");
            SendCopyBufferToTexture(
                blit, Sel("copyFromBuffer:sourceOffset:sourceBytesPerRow:sourceBytesPerImage:sourceSize:toTexture:destinationSlice:destinationLevel:destinationOrigin:"),
                staging, 0, alignedRow, alignedRow * height, new MTLSize { Width = width, Height = height, Depth = 1 },
                texture, 0, 0, default);
            Call(blit, "endEncoding");
            Call(commandBuffer, "commit");
            Call(commandBuffer, "waitUntilCompleted");
        }
        finally
        {
            Release(staging);
        }

        return texture;
    }

    #endregion

    #region | Teardown |

    private void ReleaseModelResources()
    {
        foreach (var primitive in _gpuPrimitives)
        {
            Release(primitive.Positions);
            Release(primitive.Normals);
            Release(primitive.TexCoords);
            Release(primitive.Indices);
        }
        _gpuPrimitives.Clear();

        foreach (var texture in _materialTextures.Values)
        {
            Release(texture);
        }
        _materialTextures.Clear();

        _currentModel = null;
    }

    private void DestroyRenderTargets()
    {
        Release(_colorTexture); _colorTexture = IntPtr.Zero;
        Release(_depthTexture); _depthTexture = IntPtr.Zero;
        Release(_readbackBuffer); _readbackBuffer = IntPtr.Zero;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) { return; }
        _disposed = true;

        ReleaseModelResources();
        DestroyRenderTargets();

        Release(_whiteTexture); _whiteTexture = IntPtr.Zero;
        Release(_sampler); _sampler = IntPtr.Zero;
        Release(_depthWrite); _depthWrite = IntPtr.Zero;
        Release(_depthNoWrite); _depthNoWrite = IntPtr.Zero;
        Release(_opaquePipeline); _opaquePipeline = IntPtr.Zero;
        Release(_blendPipeline); _blendPipeline = IntPtr.Zero;
        Release(_vertexFunction); _vertexFunction = IntPtr.Zero;
        Release(_fragmentFunction); _fragmentFunction = IntPtr.Zero;
        Release(_library); _library = IntPtr.Zero;
        Release(_queue); _queue = IntPtr.Zero;
        Release(_device); _device = IntPtr.Zero;
    }

    #endregion

    // Rounds a byte count up to the 256-byte row alignment macOS requires for buffer↔texture blits.
    private static nuint Align(nuint bytes) => (bytes + (RowAlignment - 1)) & ~(nuint)(RowAlignment - 1);
}
