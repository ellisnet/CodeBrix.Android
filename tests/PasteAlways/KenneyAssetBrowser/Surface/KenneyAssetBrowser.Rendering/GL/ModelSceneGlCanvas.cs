using CodeBrix.Platform.OpenGL;
using CodeBrix.Platform.WinUI.Graphics3DGL;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;

namespace KenneyAssetBrowser.Rendering;

/// <summary>
/// The self-contained 3D model preview: a <see cref="GLCanvasElement"/> that draws the bound
/// <see cref="Model"/> with an orbit camera and lets the user rotate (drag) and zoom (wheel) it.
/// <para>
/// Everything the preview needs lives inside this control: the cross-platform OpenGL context,
/// off-screen framebuffer, and pixel read-back are provided by the <see cref="GLCanvasElement"/>
/// base (so this works on every CodeBrix.Platform head that exposes a native GL context, not
/// just Linux); the actual drawing is done by the framework-free <see cref="GlModelSceneRenderer"/>;
/// and pointer input is turned into camera motion here. The hosting page only places the control
/// and binds <see cref="Model"/> — it contains no rendering code.
/// </para>
/// </summary>
public sealed class ModelSceneGlCanvas : GLCanvasElement
{
    // Orbit sensitivity: how many degrees the camera turns per pixel (DIP) of drag. The base
    // renders at the element's logical size, so drag deltas are measured in the same units.
    private const float OrbitDegreesPerPixel = 0.25f;

    // The dark solid clear colour behind the model (linear RGBA in [0, 1]).
    private static readonly (float R, float G, float B, float A) SolidBackground = (0.13f, 0.13f, 0.15f, 1f);

    // The framework-free shader renderer. It owns the OrbitCamera and knows nothing about the
    // element, the framebuffer, or pointer input; this control drives it through the
    // GLCanvasElement lifecycle (Init/RenderOverride/OnDestroy) on the GL thread.
    private readonly IModelSceneRenderer _renderer = new GlModelSceneRenderer();

    private bool _dragging;
    private double _lastX;
    private double _lastY;

    /// <summary>Creates the preview control and wires its pointer (rotate/zoom) input.</summary>
    // getWindowFunc is only used on WinUI; on CodeBrix.Platform heads it is null.
    public ModelSceneGlCanvas() : base(null)
    {
        _renderer.BackgroundColor = SolidBackground;

        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        PointerCaptureLost += OnPointerCaptureLost;
        PointerWheelChanged += OnPointerWheelChanged;
    }

    /// <summary>
    /// Identifies the <see cref="Model"/> dependency property — the 3D model to preview, or
    /// <see langword="null"/> to clear it.
    /// </summary>
    public static readonly DependencyProperty ModelProperty =
        DependencyProperty.Register(
            nameof(Model),
            typeof(LoadedModel),
            typeof(ModelSceneGlCanvas),
            new PropertyMetadata(null, OnModelChanged));

    /// <summary>
    /// The 3D model to preview, or <see langword="null"/> to show an empty scene. Set this
    /// (typically via a binding); the camera re-frames to each new model automatically.
    /// </summary>
    public LoadedModel? Model
    {
        get => (LoadedModel?)GetValue(ModelProperty);
        set => SetValue(ModelProperty, value);
    }

    private static void OnModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var canvas = (ModelSceneGlCanvas)d;

        // Re-frame each new model from the same default angle/margins the app has always used,
        // even if the user had orbited/zoomed the previous one.
        ApplyDefaultFraming(canvas._renderer.Camera);
        canvas._renderer.SetModel(e.NewValue as LoadedModel, frameCamera: true);
        canvas.Invalidate();
    }

    /// <summary>
    /// Identifies the <see cref="AnimationClip"/> dependency property — the baked animation
    /// to play over the bound <see cref="Model"/>, or <see langword="null"/> for a still pose.
    /// </summary>
    public static readonly DependencyProperty AnimationClipProperty =
        DependencyProperty.Register(
            nameof(AnimationClip),
            typeof(ModelAnimationClip),
            typeof(ModelSceneGlCanvas),
            new PropertyMetadata(null, OnAnimationClipChanged));

    /// <summary>
    /// The baked animation to play, or <see langword="null"/> for a still pose. The clip must
    /// have been baked against the bound <see cref="Model"/>. Setting it shows the clip's
    /// first frame; playback runs while <see cref="IsAnimationPlaying"/> is true.
    /// </summary>
    public ModelAnimationClip? AnimationClip
    {
        get => (ModelAnimationClip?)GetValue(AnimationClipProperty);
        set => SetValue(AnimationClipProperty, value);
    }

    /// <summary>Identifies the <see cref="IsAnimationPlaying"/> dependency property.</summary>
    public static readonly DependencyProperty IsAnimationPlayingProperty =
        DependencyProperty.Register(
            nameof(IsAnimationPlaying),
            typeof(bool),
            typeof(ModelSceneGlCanvas),
            new PropertyMetadata(false, OnIsAnimationPlayingChanged));

    /// <summary>Whether the bound <see cref="AnimationClip"/> is advancing (it loops).</summary>
    public bool IsAnimationPlaying
    {
        get => (bool)GetValue(IsAnimationPlayingProperty);
        set => SetValue(IsAnimationPlayingProperty, value);
    }

    private DispatcherTimer? _animationTimer;
    private int _animationFrameIndex;

    private static void OnAnimationClipChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var canvas = (ModelSceneGlCanvas)d;
        canvas._animationFrameIndex = 0;
        canvas.PushAnimationFrame();
        canvas.UpdateAnimationTimer();
    }

    private static void OnIsAnimationPlayingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((ModelSceneGlCanvas)d).UpdateAnimationTimer();

    //Playback is a plain UI-thread timer at the clip's bake rate: each tick pushes the next
    //baked frame's vertices to the renderer and invalidates. Stops itself when nothing plays.
    private void UpdateAnimationTimer()
    {
        var clip = AnimationClip;
        if (clip == null || !IsAnimationPlaying)
        {
            _animationTimer?.Stop();
            return;
        }

        _animationTimer ??= CreateAnimationTimer();
        _animationTimer.Interval = TimeSpan.FromSeconds(1d / clip.FrameRate);
        _animationTimer.Start();
    }

    private DispatcherTimer CreateAnimationTimer()
    {
        var timer = new DispatcherTimer();
        timer.Tick += (_, _) =>
        {
            var clip = AnimationClip;
            if (clip == null || !IsAnimationPlaying)
            {
                timer.Stop();
                return;
            }

            _animationFrameIndex = (_animationFrameIndex + 1) % clip.Frames.Count;
            PushAnimationFrame();
        };
        return timer;
    }

    private void PushAnimationFrame()
    {
        var clip = AnimationClip;
        if (clip == null || clip.Frames.Count == 0) { return; }

        _renderer.SetFrameVertices(clip.Frames[_animationFrameIndex % clip.Frames.Count].Primitives);
        Invalidate();
    }

    private bool _rendererInitialized;

    /// <inheritdoc />
    protected override void Init(GL gl) => EnsureInitialized(gl);

    // Compiles the renderer's GL resources exactly once. Called from both Init and RenderOverride
    // so it does not matter which the host invokes first: GLCanvasElement does not guarantee Init
    // runs before the first RenderOverride on every head (e.g. a canvas that starts collapsed).
    private void EnsureInitialized(GL gl)
    {
        if (_rendererInitialized)
        {
            return;
        }
        _renderer.Initialize(gl);
        _rendererInitialized = true;
    }

    /// <inheritdoc />
    protected override void RenderOverride(GL gl)
    {
        // Defensive: GLCanvasElement calls Init before the first RenderOverride, but initializing
        // here too (idempotent) keeps the control robust against any host lifecycle ordering.
        EnsureInitialized(gl);

        // Both this preview and the head's own Skia renderer share the GL context, so save the
        // state we touch and restore it afterwards (the GLCanvasElement contract). The base has
        // already bound the off-screen framebuffer and set the viewport before calling us.
        var depthWasEnabled = gl.IsEnabled(EnableCap.DepthTest);
        var cullWasEnabled = gl.IsEnabled(EnableCap.CullFace);
        try
        {
            _renderer.Render(gl, (uint)RenderSize.Width, (uint)RenderSize.Height);
        }
        finally
        {
            if (depthWasEnabled) { gl.Enable(EnableCap.DepthTest); } else { gl.Disable(EnableCap.DepthTest); }
            if (cullWasEnabled) { gl.Enable(EnableCap.CullFace); } else { gl.Disable(EnableCap.CullFace); }
            gl.BindVertexArray(0);
            gl.UseProgram(0);
        }
    }

    /// <inheritdoc />
    protected override void OnDestroy(GL gl)
    {
        _renderer.Uninitialize(gl);
        _rendererInitialized = false;
    }

    // The starting camera framing for every model: a gentle three-quarter angle with the whole
    // model comfortably inside the frame (Kenney kit pieces are compact and boxy, so a tight
    // crop would clip their corners). Applied before framing so FitToModel uses these margins.
    private static void ApplyDefaultFraming(OrbitCamera camera)
    {
        camera.FovDegrees = 45f;
        camera.YawDegrees = 30f;
        camera.PitchDegrees = 20f;
        camera.FitMargin = 1.05f;
        camera.VerticalFramingBias = 0.05f;
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(this);
        if (!point.Properties.IsLeftButtonPressed) { return; }

        _dragging = true;
        _lastX = point.Position.X;
        _lastY = point.Position.Y;
        CapturePointer(e.Pointer);
        e.Handled = true;
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_dragging) { return; }

        var position = e.GetCurrentPoint(this).Position;
        var deltaYaw = (float)(position.X - _lastX) * OrbitDegreesPerPixel;
        var deltaPitch = (float)(position.Y - _lastY) * OrbitDegreesPerPixel;
        _lastX = position.X;
        _lastY = position.Y;

        // Grab-and-drag feel: dragging right rolls the model's near face to the right, and
        // dragging up rolls its top toward you. Invalidate coalesces to one paint per frame.
        _renderer.Camera.Orbit(-deltaYaw, deltaPitch);
        Invalidate();
        e.Handled = true;
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        _dragging = false;
        ReleasePointerCapture(e.Pointer);
        e.Handled = true;
    }

    private void OnPointerCaptureLost(object sender, PointerRoutedEventArgs e) => _dragging = false;

    private void OnPointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        var delta = e.GetCurrentPoint(this).Properties.MouseWheelDelta;
        _renderer.Camera.Zoom(delta > 0 ? 0.9f : 1.1f);
        Invalidate();
        e.Handled = true;
    }
}
