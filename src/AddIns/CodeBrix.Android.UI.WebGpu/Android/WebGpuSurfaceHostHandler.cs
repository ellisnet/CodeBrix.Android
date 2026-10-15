using System;
using System.Runtime.InteropServices;
using CodeBrix.Android.SkiaSharp.Views.CanvasHost;
using CodeBrix.Android.SkiaSharp.Views.Platform;
using CodeBrix.Android.SkiaSharp.Views.Portable;
using CodeBrix.Android.UI.Handlers;
using CodeBrix.Platform.Extensions;
using CodeBrix.Platform.Extensions.Logging;
using CodeBrix.Platform.UI.Contracts;
using CodeBrix.Platform.UI.WebGpu;
using Microsoft.Extensions.Logging;
using SkiaSharp;
using Windows.Foundation;
using AContext = global::Android.Content.Context;
using AFormat = global::Android.Graphics.Format;
using AJNIEnv = global::Android.Runtime.JNIEnv;
using ASurfaceView = global::Android.Views.SurfaceView;
using AViewGroup = global::Android.Views.ViewGroup;
using ISurfaceHolder = global::Android.Views.ISurfaceHolder;
using ISurfaceHolderCallback = global::Android.Views.ISurfaceHolderCallback;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace CodeBrix.Android.UI.WebGpu.Android;

/// <summary>
/// The platform view a <see cref="WebGpuRenderView"/> can host a window surface in, for the DirectSurface render path:
/// the view asks for one while the engine may take that path and lets it go otherwise.
/// </summary>
internal interface IWebGpuWindowSurfaceHost
{
    /// <summary>Shows a window surface under the element (true) or removes it (false). UI thread; idempotent.</summary>
    /// <param name="host">Whether to host one.</param>
    void HostWindowSurface(bool host);
}

/// <summary>
/// The handler of a <see cref="WebGpuRenderView"/> in an application that enabled direct GPU rendering
/// (CodeBrixApplication.UseDirectGpuRenderingMode): a clipping view group holding a SurfaceView - while the DirectSurface
/// path may be taken - UNDER the SkiaSharp.Views add-in's native Skia view, which paints the element exactly as the plain
/// canvas handler does (the read-back picture on Readback; on DirectSurface nothing but the Overlay, over a transparent
/// background, so the surface shows through). The SurfaceView keeps its default place below the window: Android punches
/// a transparent hole for it, so the element's canvas and every element drawn after it stay ABOVE the frames.
/// </summary>
/// <remarks>
/// The surface's lifetime is Android's (SurfaceHolder.Callback): <c>surfaceCreated</c> acquires the
/// <c>ANativeWindow</c> (<c>ANativeWindow_fromSurface</c>) and hands it to the view's engine, which creates the WebGPU
/// surface over it at the next frame; <c>surfaceChanged</c> makes the next frame reconfigure it; <c>surfaceDestroyed</c>
/// (the activity going to the background, the view leaving the window, the SurfaceView removed after a fallback) makes
/// the engine unconfigure and release the WebGPU surface under its frame gate, then give the window back
/// (<c>ANativeWindow_release</c>) - before Android frees the buffers.
/// </remarks>
internal sealed class WebGpuSurfaceHostHandler : ViewHandler<WebGpuRenderView, WebGpuSurfaceHostHandler.SurfaceHostLayout>, IWebGpuWindowSurfaceHost
{
    /// <summary>The view's mapper (only what every view maps).</summary>
    public static readonly PropertyMapper<WebGpuRenderView, WebGpuSurfaceHostHandler> Mapper = new(ViewMappers.ViewMapper);

    private SurfaceCallbacks _callbacks;

    /// <summary>Creates the handler.</summary>
    public WebGpuSurfaceHostHandler()
        : base(Mapper)
    {
    }

    /// <inheritdoc />
    public override ElementHandlerCapabilities Capabilities => ElementHandlerCapabilities.OwnsVisuals;

    /// <summary>The SurfaceView shown now, or null (device tests).</summary>
    internal ASurfaceView SurfaceView => (NativeView as SurfaceHostLayout)?.Surface;

    /// <inheritdoc />
    protected override SurfaceHostLayout CreatePlatformView()
    {
        var layout = new SurfaceHostLayout(Context);
        layout.Canvas.Painter = Paint;
        return layout;
    }

    /// <inheritdoc />
    protected override void ConnectHandler(SurfaceHostLayout platformView)
    {
        base.ConnectHandler(platformView);
        if (Element is not WebGpuRenderView view)
        {
            return;
        }

        if (view.Visual is AndroidSKCanvasVisual visual)
        {
            visual.View = platformView.Canvas;
        }

        _callbacks = new SurfaceCallbacks(view);
        view.WindowSurfaceHost = this;
    }

    /// <inheritdoc />
    protected override void DisconnectHandler(SurfaceHostLayout platformView)
    {
        if (Element is WebGpuRenderView view)
        {
            if (ReferenceEquals(view.WindowSurfaceHost, this))
            {
                view.WindowSurfaceHost = null;
            }

            RemoveSurface(platformView, view);
            if (view.Visual is AndroidSKCanvasVisual visual && ReferenceEquals(visual.View, platformView.Canvas))
            {
                visual.View = null;
            }
        }

        _callbacks?.Dispose();
        _callbacks = null;
        base.DisconnectHandler(platformView);
    }

    /// <inheritdoc />
    public void HostWindowSurface(bool host)
    {
        if (NativeView is not SurfaceHostLayout layout || Element is not WebGpuRenderView view || _callbacks is null)
        {
            return;
        }

        if (host && layout.Surface is null)
        {
            var surface = new ASurfaceView(layout.Context);
            surface.Holder.AddCallback(_callbacks);
            layout.AddView(surface, 0);
            layout.Surface = surface;
            view.OnWindowHostConnected(true);
        }
        else if (!host && layout.Surface is not null)
        {
            RemoveSurface(layout, view);
        }
    }

    /// <inheritdoc />
    protected override void OnArranged(Rect finalRect, bool changed)
    {
        base.OnArranged(finalRect, changed);
        if (changed)
        {
            NativeView?.Invalidate();
        }
    }

    // Removing an attached SurfaceView destroys its surface synchronously (surfaceDestroyed), so the engine has released
    // the window before the host is disconnected.
    private void RemoveSurface(SurfaceHostLayout layout, WebGpuRenderView view)
    {
        if (layout.Surface is not { } surface)
        {
            return;
        }

        layout.Surface = null;
        layout.RemoveView(surface);
        if (_callbacks is not null)
        {
            surface.Holder.RemoveCallback(_callbacks);
        }

        surface.Dispose();
        view.OnWindowHostConnected(false);
    }

    private void Paint(SKCanvas canvas, SKImageInfo info)
    {
        if (Element is not WebGpuRenderView element || element.Visual is not AndroidSKCanvasVisual visual)
        {
            return;
        }

        var width = element.ActualWidth;
        var height = element.ActualHeight;
        if (!CanvasSurfaceMath.IsPaintable(width, height))
        {
            return;
        }

        canvas.Scale(CanvasSurfaceMath.CanvasScale(Density));
        canvas.ClipRect(SKRect.Create(0, 0, (float)width, (float)height));
        visual.Render(canvas, new Size(width, height));
    }

    /// <summary>The native view: a clipping group, the SurfaceView (when hosted) at the bottom, the Skia canvas above it.</summary>
    internal sealed class SurfaceHostLayout : AViewGroup
    {
        /// <summary>Creates the view.</summary>
        /// <param name="context">The activity context.</param>
        public SurfaceHostLayout(AContext context)
            : base(context)
        {
            SetClipChildren(true);
            Canvas = new SkiaCanvasView(context);
            AddView(Canvas);
        }

        /// <summary>The canvas the element (the read-back picture, the Overlay) paints on - always the top child.</summary>
        internal SkiaCanvasView Canvas { get; }

        /// <summary>The SurfaceView, while one is hosted.</summary>
        internal ASurfaceView Surface { get; set; }

        /// <inheritdoc />
        protected override void OnMeasure(int widthMeasureSpec, int heightMeasureSpec)
        {
            var width = MeasureSpec.GetSize(widthMeasureSpec);
            var height = MeasureSpec.GetSize(heightMeasureSpec);
            SetMeasuredDimension(width, height);
            var exactWidth = MeasureSpec.MakeMeasureSpec(width, global::Android.Views.MeasureSpecMode.Exactly);
            var exactHeight = MeasureSpec.MakeMeasureSpec(height, global::Android.Views.MeasureSpecMode.Exactly);
            for (var i = 0; i < ChildCount; i++)
            {
                GetChildAt(i)?.Measure(exactWidth, exactHeight);
            }
        }

        /// <inheritdoc />
        protected override void OnLayout(bool changed, int l, int t, int r, int b)
        {
            for (var i = 0; i < ChildCount; i++)
            {
                GetChildAt(i)?.Layout(0, 0, r - l, b - t);
            }
        }
    }

    /// <summary>The SurfaceHolder callbacks: the window handed to the view's engine, and taken back.</summary>
    private sealed class SurfaceCallbacks : global::Java.Lang.Object, ISurfaceHolderCallback
    {
        private static readonly Action<nint> ReleaseWindow = NativeWindowInterop.Release;
        private readonly WeakReference<WebGpuRenderView> _view;

        internal SurfaceCallbacks(WebGpuRenderView view)
        {
            _view = new WeakReference<WebGpuRenderView>(view);
        }

        public void SurfaceCreated(ISurfaceHolder holder)
        {
            if (!_view.TryGetTarget(out var view) || holder?.Surface is not { } surface)
            {
                return;
            }

            var window = NativeWindowInterop.FromSurface(surface);
            if (window == 0)
            {
                // The DirectSurface path then waits for a window that never comes and falls back to Readback (one line).
                if (view.Log().IsEnabled(LogLevel.Warning))
                {
                    view.Log().Warn("ANativeWindow_fromSurface returned no window for the SurfaceView's surface.");
                }

                return;
            }

            var frame = holder.SurfaceFrame;
            view.OnWindowCreated(window, frame?.Width() ?? 0, frame?.Height() ?? 0, ReleaseWindow);
        }

        public void SurfaceChanged(ISurfaceHolder holder, AFormat format, int width, int height)
        {
            if (_view.TryGetTarget(out var view))
            {
                view.OnWindowChanged(width, height);
            }
        }

        public void SurfaceDestroyed(ISurfaceHolder holder)
        {
            if (_view.TryGetTarget(out var view))
            {
                view.OnWindowDestroyed();
            }
        }
    }
}

/// <summary>
/// The two entry points of the Android system library libandroid.so (part of every Android image; no package) that turn
/// a SurfaceView's <c>android.view.Surface</c> into the <c>ANativeWindow*</c> WebGPU creates its surface over, and give
/// that reference back.
/// </summary>
internal static partial class NativeWindowInterop
{
    private const string Library = "libandroid.so";

    /// <summary>Acquires the native window behind a Java surface (a reference released with <see cref="Release"/>).</summary>
    /// <param name="surface">The surface (SurfaceHolder.Surface).</param>
    /// <returns>The <c>ANativeWindow*</c>, or 0.</returns>
    internal static nint FromSurface(global::Android.Views.Surface surface) =>
        ANativeWindow_fromSurface(AJNIEnv.Handle, surface.Handle);

    /// <summary>Gives a reference acquired by <see cref="FromSurface"/> back.</summary>
    /// <param name="window">The <c>ANativeWindow*</c>.</param>
    internal static void Release(nint window)
    {
        if (window != 0)
        {
            ANativeWindow_release(window);
        }
    }

    [LibraryImport(Library)]
    private static partial nint ANativeWindow_fromSurface(nint env, nint surface);

    [LibraryImport(Library)]
    private static partial void ANativeWindow_release(nint window);
}
