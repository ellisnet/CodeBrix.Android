using System;
using System.IO;
using System.Numerics;
using CodeBrix.WebGpu.Graphics;
using CodeBrix.WebGpu.Native;
using Gltf = CodeBrix.Graphics3D.Gltf2.Schema2;

namespace WebGpuDemo.Rendering;

/// <summary>
/// The Models page's scene, drawn with the OPTIONAL higher layer, CodeBrix.WebGpu.Graphics, straight into the view:
/// <see cref="GraphicsContext.Wrap(IWebGpuDeviceSource)"/> over the view's device (DeviceReady) and
/// <see cref="GraphicsContext.BeginFrame(IWebGpuTextureTarget, GpuTexture)"/> over the view itself (Render) - the
/// two sharing contracts the view implements, so neither package knows about the other. Three Cube Pets animals and
/// the Avian stand round a treasure chest that opens and closes, on a grass plane built in memory with
/// CodeBrix.Graphics3D.Gltf2, lit by a sun and a procedural sky (image-based lighting) that the skybox also shows; an
/// orbit camera turns round them.
/// </summary>
public sealed class ModelScene : IDisposable
{
    private static readonly Vector3 SunDirection = Vector3.Normalize(new Vector3(-0.45f, -1f, -0.35f));

    private readonly GraphicsContext _context;
    private readonly ModelTextureCache _textures = new();
    private readonly Model[] _models;
    private readonly Model _ground;
    private readonly ModelInstance[] _instances;
    private readonly ModelInstance _groundInstance;
    private readonly ModelRenderer _renderer;
    private readonly EnvironmentMap _sky;
    private readonly SkyboxRenderer _skybox;
    private readonly SceneLighting _lighting;
    private readonly Camera _camera = new() { FieldOfView = 50f * MathF.PI / 180f, NearPlane = 0.1f, FarPlane = 200f };
    private GpuTexture _depth;
    private int _depthWidth;
    private int _depthHeight;
    private double _lastTime;

    /// <summary>Wraps the view's device and loads the models (call it from DeviceReady).</summary>
    /// <param name="device">The device the view handed over.</param>
    /// <param name="assetsFolder">The folder that holds models/ (the application's Assets folder).</param>
    public ModelScene(IWebGpuDeviceSource device, string assetsFolder)
    {
        _context = GraphicsContext.Wrap(device);
        var options = new ModelLoadOptions { TextureCache = _textures };
        Model Load(string pack, string name) => _context.LoadModel(Path.Combine(assetsFolder, "models", pack, name + ".glb"), options);

        _models =
        [
            Load("survival-kit", "chest"),
            Load("cube-pets", "animal-cat"),
            Load("cube-pets", "animal-dog"),
            Load("cube-pets", "animal-fox"),
            Load("crystals-and-avian", "avian"),
        ];

        // (model, clip, place): the chest in the middle, the animals round it facing in, the Avian hovering behind.
        _instances =
        [
            Place(_models[0], "open-close", Matrix4x4.CreateScale(2.4f) * Matrix4x4.CreateRotationY(0.5f)),
            Place(_models[1], "idle", Ring(0.6f, 2.6f)),
            Place(_models[2], "dance", Ring(2.7f, 2.6f)),
            Place(_models[3], "eat", Ring(4.6f, 2.6f)),
            Place(_models[4], "fly", Matrix4x4.CreateScale(0.4f) * Matrix4x4.CreateRotationY(MathF.PI) * Matrix4x4.CreateTranslation(0, 2.6f, -2.8f)),
        ];

        _ground = CreateGround(_context);
        _groundInstance = _ground.CreateInstance();
        _renderer = _context.CreateModelRenderer();
        _sky = _context.CreateProceduralSky(new ProceduralSkyOptions
        {
            SunDirection = SunDirection,
            ZenithColor = new Color4(0.14f, 0.32f, 0.70f),
            HorizonColor = new Color4(0.40f, 0.58f, 0.85f),
            GroundColor = new Color4(0.10f, 0.15f, 0.07f),
            Turbidity = 2f,
        });
        _skybox = _context.CreateSkyboxRenderer();
        _lighting = new SceneLighting
        {
            LightDirection = SunDirection,
            LightColor = new Color4(1f, 0.95f, 0.86f),
            LightIntensity = 1.6f,
            Environment = _sky,
            EnvironmentIntensity = 1f,
            Exposure = 1f,
        };
    }

    /// <summary>The orbit controller (the page's slider adds to its yaw).</summary>
    public OrbitCamera Orbit { get; } = new() { Target = new Vector3(0, 0.9f, 0), Distance = 8.5f, Pitch = 0.32f };

    /// <summary>An extra camera angle in radians, added to the automatic turn.</summary>
    public float UserYaw { get; set; }

    /// <summary>How many model files were loaded.</summary>
    public int ModelCount => _models.Length;

    /// <summary>Records the scene into the view's open frame: a pass with depth that clears to the horizon colour,
    /// the models, then the skybox where nothing covered the sky.</summary>
    /// <param name="frame">The open frame (its timing and size).</param>
    /// <param name="view">The view, as the texture target Graphics renders into.</param>
    public void Render(CodeBrix.Platform.UI.WebGpu.WebGpuFrame frame, IWebGpuTextureTarget view)
    {
        EnsureDepth(frame.Width, frame.Height);
        var delta = (float)Math.Max(0, frame.ElapsedSeconds - _lastTime);
        _lastTime = frame.ElapsedSeconds;
        AnimationPlayer.UpdateAll(_instances, delta);
        Orbit.Yaw = 0.4f + (float)(frame.ElapsedSeconds * 0.2) + UserYaw;
        Orbit.Apply(_camera);

        using (var graphicsFrame = _context.BeginFrame(view, _depth))
        {
            using (var pass = graphicsFrame.BeginRenderPass(new Color4(0.40f, 0.58f, 0.85f)))
            {
                _renderer.Begin(pass, _camera, _lighting);
                _renderer.Draw(_groundInstance);
                foreach (var instance in _instances) _renderer.Draw(instance);
                _renderer.End();
                _skybox.Draw(pass, _camera, _lighting);
            }

            // Ends the view's pass hand-out; the view itself submits, reads back and shows the frame at EndFrame.
            graphicsFrame.Submit();
        }
    }

    /// <summary>Releases everything this scene created, the wrapped context last (call it from DeviceLost: the view
    /// still has its device then).</summary>
    public void Dispose()
    {
        _depth?.Dispose();
        _renderer.Dispose();
        _skybox.Dispose();
        foreach (var model in _models) model.Dispose();
        _ground.Dispose();
        _textures.Dispose();
        _sky.Dispose();
        _context.Dispose();
    }

    private static ModelInstance Place(Model model, string clip, Matrix4x4 world)
    {
        var instance = model.CreateInstance(world);
        if (model.FindAnimation(clip) >= 0) instance.Animation.Play(clip);
        return instance;
    }

    private static Matrix4x4 Ring(float angle, float radius) =>
        Matrix4x4.CreateScale(0.55f) * Matrix4x4.CreateRotationY(angle + MathF.PI)
        * Matrix4x4.CreateTranslation(MathF.Sin(angle) * radius, 0, MathF.Cos(angle) * radius);

    private void EnsureDepth(int width, int height)
    {
        if (_depth != null && _depthWidth == width && _depthHeight == height) return;
        _depth?.Dispose();
        _depth = _context.CreateDepthTexture(width, height, TextureFormat.Depth24Plus, "WebGpuDemo depth");
        _depthWidth = width;
        _depthHeight = height;
    }

    // A grass-green plane built in memory with CodeBrix.Graphics3D.Gltf2 (one quad, rough, non-metallic) - the
    // WebGpuModelViewer sample's ground, smaller.
    private static Model CreateGround(GraphicsContext context)
    {
        const float Half = 40f;
        var root = Gltf.ModelRoot.CreateModel();
        var material = Gltf.Toolkit.WithPBRMetallicRoughness(root.CreateMaterial("grass"));
        Gltf.Toolkit.WithChannelColor(material, "BaseColor", new Vector4(Linear(110), Linear(168), Linear(84), 1f));
        Gltf.Toolkit.WithChannelFactor(material, "MetallicRoughness", "MetallicFactor", 0f);
        Gltf.Toolkit.WithChannelFactor(material, "MetallicRoughness", "RoughnessFactor", 0.95f);

        var primitive = root.CreateMesh("ground").CreatePrimitive();
        Gltf.Toolkit.WithVertexAccessor(primitive, "POSITION", new[] { new Vector3(-Half, 0, Half), new Vector3(Half, 0, Half), new Vector3(Half, 0, -Half), new Vector3(-Half, 0, -Half) });
        Gltf.Toolkit.WithVertexAccessor(primitive, "NORMAL", new[] { Vector3.UnitY, Vector3.UnitY, Vector3.UnitY, Vector3.UnitY });
        Gltf.Toolkit.WithIndicesAccessor(primitive, Gltf.PrimitiveType.TRIANGLES, new[] { 0, 1, 2, 0, 2, 3 });
        Gltf.Toolkit.WithMaterial(primitive, material);
        root.UseScene(0).CreateNode("ground").Mesh = root.LogicalMeshes[0];

        using var glb = new MemoryStream(root.WriteGLB().ToArray());
        return context.LoadModel(glb, null, new ModelLoadOptions { Label = "ground" });
    }

    private static float Linear(byte srgb)
    {
        var c = srgb / 255f;
        return c <= 0.04045f ? c / 12.92f : MathF.Pow((c + 0.055f) / 1.055f, 2.4f);
    }
}
