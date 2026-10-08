using System;
using CodeBrix.Android.UI.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SkiaSharp;
using ABitmap = global::Android.Graphics.Bitmap;

namespace CodeBrix.Platform.WinUI.Graphics3DGL;

/// <summary>
/// An element that hands its PaintSurface handlers a GPU-backed Skia surface (Skia on the head's off-screen OpenGL
/// context) and shows what they drew. The same public type as the CodeBrix.Platform flavor of the add-in, ported
/// behaviour for behaviour from the pinned flavor assembly: the context is created when the element is loaded and
/// given back when it is unloaded or its window closes; the surface is the element's arranged size with its origin at
/// the top left; drawing happens on the dispatcher, once per <see cref="Invalidate"/>.
/// <para>
/// Android: the picture is read back from the GPU surface into an android.graphics.Bitmap that the element's native
/// view draws under its XAML children (the flavor reads it into a WriteableBitmap its compositor paints).
/// </para>
/// </summary>
public class SkiaGLCanvasElement : Grid
{
    private readonly Func<Window> _getWindowFunc;
    private bool _renderRequested;
    private OffscreenGLContext _context;
    private GRContext _grContext;
    private SKSurface _surface;
    private SKImageInfo _surfaceInfo;
    private ABitmap _picture;

    /// <summary>Creates the element.</summary>
    /// <param name="getWindowFunc">Returns the element's window (its Closed event gives the GPU resources back); may be null.</param>
    public SkiaGLCanvasElement(Func<Window> getWindowFunc = null)
    {
        _getWindowFunc = getWindowFunc;

        // Hit-testable like any panel with a background (the flavor keeps its back buffer in this brush).
        Background = new ImageBrush();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += (_, _) => UpdateSurface();
    }

    /// <summary>Raised on the dispatcher to draw a frame on the GPU surface.</summary>
    public event EventHandler<SkiaGLPaintSurfaceEventArgs> PaintSurface;

    /// <summary>Raised when the element asks its view to run a drawing pass (Android).</summary>
    internal event Action RenderRequested;

    /// <summary>Raised when a new picture was read back (Android).</summary>
    internal event Action PictureChanged;

    /// <summary>True when the GPU context is up, false when it could not be created, null before loading / after unloading.</summary>
    public bool? IsGpuInitialized { get; private set; }

    /// <summary>The last picture read back (Android; top-down RGBA, the surface's size).</summary>
    internal ABitmap Picture => _picture;

    /// <summary>Asks for one more frame: PaintSurface is raised on the dispatcher before the next drawing pass.</summary>
    public void Invalidate()
    {
        _renderRequested = true;
        RenderRequested?.Invoke();
    }

    /// <summary>Raises <see cref="PaintSurface"/>.</summary>
    /// <param name="args">The surface to draw on.</param>
    protected virtual void OnPaintSurface(SkiaGLPaintSurfaceEventArgs args) => PaintSurface?.Invoke(this, args);

    /// <summary>Called before each drawing pass of the window (the Skia heads' compositor paint): renders when invalidated.</summary>
    internal void OnVisualPainting()
    {
        if (!_renderRequested)
        {
            return;
        }

        if (DispatcherQueue is { } queue)
        {
            queue.TryEnqueue(Render);
        }
        else
        {
            Render();
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!OffscreenGLContext.TryCreate(XamlRoot, out _context))
            {
                Log.LogError("SkiaGLCanvasElement could not create an off-screen OpenGL context. Make sure you are running on a platform with OpenGL support.");
                IsGpuInitialized = false;
                return;
            }

            using (_context.MakeCurrent())
            {
                _grContext = _context.CreateGrContext();
            }

            UpdateSurface();
            if (GetWindow() is { } window)
            {
                window.Closed += OnClosed;
            }
            else if (XamlRoot?.Content is FrameworkElement content)
            {
                content.Unloaded += OnClosed;
            }

            IsGpuInitialized = true;
        }
        catch (Exception exception)
        {
            Log.LogError(exception, "SkiaGLCanvasElement initialization failed.");
            DisposeGpuResources();
            IsGpuInitialized = false;
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        IsGpuInitialized = null;
        if (GetWindow() is { } window)
        {
            window.Closed -= OnClosed;
        }
        else if (XamlRoot?.Content is FrameworkElement content)
        {
            content.Unloaded -= OnClosed;
        }

        DisposeGpuResources();
    }

    private void OnClosed(object sender, object args) => DisposeGpuResources();

    private void DisposeGpuResources()
    {
        if (_context != null && (_surface != null || _grContext != null))
        {
            using (_context.MakeCurrent())
            {
                _surface?.Dispose();
                _grContext?.Dispose();
            }
        }

        _surface = null;
        _grContext = null;
        _context?.Dispose();
        _context = null;
    }

    private Window GetWindow() => XamlRoot == null ? null : XamlRoot.HostWindow ?? _getWindowFunc?.Invoke();

    private void UpdateSurface()
    {
        if (!IsLoaded || _context == null || _grContext == null)
        {
            return;
        }

        var size = RenderSize;
        if (!(size.Width > 0) || !(size.Height > 0))
        {
            return;
        }

        var width = (int)size.Width;
        var height = (int)size.Height;
        using (_context.MakeCurrent())
        {
            _surface?.Dispose();

            // The flavor's order: BGRA first, RGBA when the GPU refuses it.
            _surfaceInfo = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
            _surface = SKSurface.Create(_grContext, true, _surfaceInfo);
            if (_surface == null)
            {
                _surfaceInfo = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
                _surface = SKSurface.Create(_grContext, true, _surfaceInfo);
            }

            if (_surface == null)
            {
                throw new InvalidOperationException($"Could not create a {width}x{height} GPU SKSurface on the off-screen GRContext.");
            }
        }

        if (_picture is not { IsRecycled: false } picture || picture.Width != width || picture.Height != height)
        {
            var old = _picture;
            _picture = ABitmap.CreateBitmap(width, height, ABitmap.Config.Argb8888);
            if (old is { IsRecycled: false })
            {
                old.Recycle();
            }
        }

        Invalidate();
    }

    private void Render()
    {
        _renderRequested = false;
        if (!IsLoaded || _context == null || _grContext == null || _surface == null || _picture is not { IsRecycled: false } picture)
        {
            return;
        }

        using (_context.MakeCurrent())
        {
            OnPaintSurface(new SkiaGLPaintSurfaceEventArgs(_surface, _grContext, _surfaceInfo));
            _surface.Flush();
            _grContext.Flush();

            // android.graphics.Bitmap ARGB_8888 is RGBA bytes, premultiplied.
            var destination = new SKImageInfo(picture.Width, picture.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
            var pixels = picture.LockPixels();
            try
            {
                _surface.ReadPixels(destination, pixels, destination.RowBytes, 0, 0);
            }
            finally
            {
                picture.UnlockPixels();
            }
        }

        PictureChanged?.Invoke();
    }

    private static ILogger Log => HostLog.For("CodeBrix.Android.UI.Graphics3DGL");
}
