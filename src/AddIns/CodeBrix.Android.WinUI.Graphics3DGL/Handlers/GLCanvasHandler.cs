using System;
using System.Runtime.InteropServices.WindowsRuntime;
using CodeBrix.Android.UI.Handlers;
using CodeBrix.Android.UI.Platform;
using CodeBrix.Android.WinUI.Graphics3DGL.Android;
using CodeBrix.Android.WinUI.Graphics3DGL.Platform;
using CodeBrix.Android.WinUI.Graphics3DGL.Portable;
using CodeBrix.Platform.WinUI.Graphics3DGL;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

namespace CodeBrix.Android.WinUI.Graphics3DGL.Handlers;

/// <summary>
/// Shows a GLCanvasElement (the add-in Core's element; applications subclass it) natively.
/// <para>
/// The Core renders on the dispatcher into its framebuffer object and reads the picture back into the WriteableBitmap
/// it keeps as the ImageSource of its Background ImageBrush, then invalidates that bitmap. This handler follows the
/// brush's current bitmap, copies each new picture into an android.graphics.Bitmap (top-down RGBA) and has the
/// view draw it under the element's children. The view calls the element's OnVisualPainting before every drawing
/// pass of the window - which is what makes an invalidated element render, exactly as the Skia heads' compositor
/// does.
/// </para>
/// </summary>
internal sealed class GLCanvasHandler : BorderedViewGroupHandler<GLCanvasElement>
{
    /// <summary>The mapper (the panel's; Background is also where the Core keeps its back buffer).</summary>
    public static readonly PropertyMapper<GLCanvasElement, GLCanvasHandler> Mapper = new(ViewMappers.ViewMapper)
    {
        [FrameworkElement.BackgroundProperty] = MapBackground,
    };

    private readonly PictureBuffer _picture = new();
    private ImageBrush _brush;
    private long _brushToken;
    private WriteableBitmap _backBuffer;
    private byte[] _bytes = [];

    /// <summary>Creates the handler.</summary>
    public GLCanvasHandler()
        : base(Mapper)
    {
    }

    /// <summary>How many pictures the handler copied from the Core's back buffer (diagnostics, gates).</summary>
    internal int PictureCount { get; private set; }

    /// <summary>Maps Background: the panel background, and the back buffer the Core keeps in it.</summary>
    /// <param name="handler">The handler.</param>
    /// <param name="element">The element.</param>
    public static void MapBackground(GLCanvasHandler handler, GLCanvasElement element)
    {
        MapBorder(handler, element);
        handler.FollowBrush(element.Background as ImageBrush);
    }

    /// <inheritdoc />
    protected override CodeBrixContentViewGroup CreatePlatformView() => new GLPictureViewGroup(Context);

    /// <inheritdoc />
    protected override void ConnectHandler(CodeBrixContentViewGroup platformView)
    {
        base.ConnectHandler(platformView);
        if (platformView is GLPictureViewGroup view)
        {
            view.Painting = OnPainting;
            view.Picture = _picture.Bitmap;
        }

        if (Element?.Visual is GLCanvasAndroidPlatform.GLCanvasVisual visual)
        {
            // Invalidate() -> a drawing pass of this view -> OnVisualPainting -> the Core renders.
            visual.Invalidated = OnVisualInvalidated;
        }

        FollowBrush((Element as GLCanvasElement)?.Background as ImageBrush);
    }

    /// <inheritdoc />
    protected override void DisconnectHandler(CodeBrixContentViewGroup platformView)
    {
        if (Element?.Visual is GLCanvasAndroidPlatform.GLCanvasVisual visual)
        {
            visual.Invalidated = null;
        }

        FollowBrush(null);
        if (platformView is GLPictureViewGroup view)
        {
            view.Painting = null;
            view.Picture = null;
        }

        _picture.Free();
        base.DisconnectHandler(platformView);
    }

    private void OnVisualInvalidated() => NativeView?.PostInvalidateOnAnimation();

    private void OnPainting()
    {
        if (Element is GLCanvasElement element && element.IsLoaded)
        {
            element.OnVisualPainting();
        }
    }

    private void FollowBrush(ImageBrush brush)
    {
        if (!ReferenceEquals(brush, _brush))
        {
            if (_brush != null)
            {
                _brush.UnregisterPropertyChangedCallback(ImageBrush.ImageSourceProperty, _brushToken);
            }

            _brush = brush;
            _brushToken = brush?.RegisterPropertyChangedCallback(ImageBrush.ImageSourceProperty, OnImageSourceChanged) ?? 0;
        }

        FollowBackBuffer(brush?.ImageSource as WriteableBitmap);
    }

    private void OnImageSourceChanged(DependencyObject sender, DependencyProperty property) =>
        FollowBackBuffer(_brush?.ImageSource as WriteableBitmap);

    private void FollowBackBuffer(WriteableBitmap backBuffer)
    {
        if (ReferenceEquals(backBuffer, _backBuffer))
        {
            return;
        }

        if (_backBuffer != null)
        {
            _backBuffer.Invalidated -= OnBackBufferInvalidated;
        }

        _backBuffer = backBuffer;
        if (_backBuffer != null)
        {
            _backBuffer.Invalidated += OnBackBufferInvalidated;
        }
    }

    private void OnBackBufferInvalidated()
    {
        if (_backBuffer is not { } backBuffer || NativeView is not GLPictureViewGroup view)
        {
            return;
        }

        var width = backBuffer.PixelWidth;
        var height = backBuffer.PixelHeight;
        var length = width * height * GlReadback.BytesPerPixel;
        var pixels = backBuffer.PixelBuffer;
        if (width <= 0 || height <= 0 || pixels == null || pixels.Length < length)
        {
            return;
        }

        if (_bytes.Length != length)
        {
            _bytes = new byte[length];
        }

        pixels.CopyTo(0, _bytes, 0, length);
        _picture.FillFromBgraBottomUp(_bytes, width, height);
        PictureCount++;
        view.Picture = _picture.Bitmap;
        view.Invalidate();
    }
}
