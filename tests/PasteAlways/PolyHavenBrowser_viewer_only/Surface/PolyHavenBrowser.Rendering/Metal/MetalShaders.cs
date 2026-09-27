namespace PolyHavenBrowser.Rendering;

/// <summary>
/// The Metal Shading Language (MSL) source for <see cref="MetalSceneRenderer"/>, compiled to a
/// <c>MTLLibrary</c> at runtime with <c>newLibraryWithSource:</c>. Unlike the Vulkan backend -
/// which embeds pre-compiled SPIR-V because Vulkan cannot compile GLSL itself - Metal compiles
/// MSL from source on the device, so this backend needs <b>no offline shader toolchain</b> at
/// build or run time: the string below is the whole shader artifact.
/// <para>
/// The shading intentionally mirrors <see cref="GlModelSceneRenderer"/> and
/// <see cref="VulkanShaders"/>: 0.25 ambient + 0.75 diffuse (N·L, double-sided via <c>abs</c>
/// when flagged) modulated by the material base color and an optional base-color texture.
/// Per-draw values arrive in one 112-byte <c>Uniforms</c> block (mvp, baseColorFactor,
/// lightDirection, flags) bound at buffer index 3 in both stages; the base-color texture and its
/// sampler are at texture/sampler index 0. The <c>float4x4</c> is read column-major by MSL, which
/// applies the same implicit transpose the GL/Vulkan backends rely on when they hand over
/// System.Numerics' row-major matrix unchanged (see the depth-ordering regression test).
/// </para>
/// </summary>
internal static class MetalShaders
{
    /// <summary>The buffer index the <c>Uniforms</c> block is bound at in both shader stages.</summary>
    internal const uint UniformBufferIndex = 3;

    /// <summary>The MSL source for both the vertex and fragment functions.</summary>
    internal const string Source = """
        #include <metal_stdlib>
        using namespace metal;

        struct Uniforms
        {
            float4x4 mvp;
            float4 baseColorFactor;
            float4 lightDirection;
            int4 flags; // x = hasTexture, y = doubleSided
        };

        struct VertexIn
        {
            float3 position [[attribute(0)]];
            float3 normal   [[attribute(1)]];
            float2 texCoord [[attribute(2)]];
        };

        struct VertexOut
        {
            float4 position [[position]];
            float3 normal;
            float2 texCoord;
        };

        vertex VertexOut vertex_main(VertexIn in [[stage_in]],
                                     constant Uniforms& u [[buffer(3)]])
        {
            VertexOut out;
            out.position = u.mvp * float4(in.position, 1.0);
            out.normal = in.normal;
            out.texCoord = in.texCoord;
            return out;
        }

        fragment float4 fragment_main(VertexOut in [[stage_in]],
                                      constant Uniforms& u [[buffer(3)]],
                                      texture2d<float> baseColorTexture [[texture(0)]],
                                      sampler baseColorSampler [[sampler(0)]])
        {
            float3 normal = normalize(in.normal);
            float nDotL = dot(normal, normalize(u.lightDirection.xyz));
            // Double-sided (abs) lights back faces too, which reads correctly for a headlit
            // model or double-sided foliage; single-sided gives a solid shape real form.
            float diffuse = (u.flags.y == 1) ? abs(nDotL) : max(nDotL, 0.0);
            float light = 0.25 + 0.75 * diffuse;
            float4 baseColor = u.baseColorFactor;
            if (u.flags.x == 1)
            {
                baseColor *= baseColorTexture.sample(baseColorSampler, in.texCoord);
            }
            return float4(baseColor.rgb * light, baseColor.a);
        }
        """;
}
