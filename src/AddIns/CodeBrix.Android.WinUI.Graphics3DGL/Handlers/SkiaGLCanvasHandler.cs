using CodeBrix.Android.UI.Handlers;
using CodeBrix.Android.UI.Platform;
using CodeBrix.Android.WinUI.Graphics3DGL.Platform;
using CodeBrix.Platform.WinUI.Graphics3DGL;
using Microsoft.UI.Xaml;

namespace CodeBrix.Android.WinUI.Graphics3DGL.Handlers;

/// <summary>
/// Shows a SkiaGLCanvasElement natively: its picture under its XAML children; an invalidation asks the view for a
/// drawing pass, before which the element renders (on the dispatcher), and a new picture redraws the view.
/// </summary>
internal sealed class SkiaGLCanvasHandler : BorderedViewGroupHandler<SkiaGLCanvasElement>
{
    /// <summary>The mapper (the panel's).</summary>
    public static readonly PropertyMapper<SkiaGLCanvasElement, SkiaGLCanvasHandler> Mapper = new(ViewMappers.ViewMapper)
    {
        [FrameworkElement.BackgroundProperty] = MapBorder,
    };

    /// <summary>Creates the handler.</summary>
    public SkiaGLCanvasHandler()
        : base(Mapper)
    {
    }

    /// <inheritdoc />
    protected override CodeBrixContentViewGroup CreatePlatformView() => new GLPictureViewGroup(Context);

    /// <inheritdoc />
    protected override void ConnectHandler(CodeBrixContentViewGroup platformView)
    {
        base.ConnectHandler(platformView);
        if (platformView is GLPictureViewGroup view && Element is SkiaGLCanvasElement element)
        {
            view.Painting = element.OnVisualPainting;
            view.Picture = element.Picture;
            element.RenderRequested += OnRenderRequested;
            element.PictureChanged += OnPictureChanged;
            view.Invalidate();
        }
    }

    /// <inheritdoc />
    protected override void DisconnectHandler(CodeBrixContentViewGroup platformView)
    {
        if (Element is SkiaGLCanvasElement element)
        {
            element.RenderRequested -= OnRenderRequested;
            element.PictureChanged -= OnPictureChanged;
        }

        if (platformView is GLPictureViewGroup view)
        {
            view.Painting = null;
            view.Picture = null;
        }

        base.DisconnectHandler(platformView);
    }

    private void OnRenderRequested() => NativeView?.Invalidate();

    private void OnPictureChanged()
    {
        if (NativeView is GLPictureViewGroup view && Element is SkiaGLCanvasElement element)
        {
            view.Picture = element.Picture;
            view.Invalidate();
        }
    }
}
