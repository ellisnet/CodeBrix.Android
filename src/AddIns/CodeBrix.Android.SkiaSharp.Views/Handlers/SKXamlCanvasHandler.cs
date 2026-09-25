using CodeBrix.Android.SkiaSharp.Views.Android;
using CodeBrix.Android.SkiaSharp.Views.Platform;
using CodeBrix.Android.UI.Handlers;
using CodeBrix.Android.UI.Platform;
using Microsoft.UI.Xaml;
using SkiaSharp.Views.Windows;

namespace CodeBrix.Android.SkiaSharp.Views.Handlers;

/// <summary>
/// The handler of SKXamlCanvas (a Canvas: it keeps the panel behaviour - its XAML children are shown
/// by the layout replay, its Background by the border drawable) whose native view,
/// <see cref="SKXamlCanvasViewGroup"/>, also paints the canvas's Skia buffer under the children.
/// </summary>
internal sealed class SKXamlCanvasHandler : BorderedViewGroupHandler<SKXamlCanvas>
{
    /// <summary>SKXamlCanvas's mapper (the panel's: Background through the border drawable).</summary>
    public static readonly PropertyMapper<SKXamlCanvas, SKXamlCanvasHandler> Mapper = new(ViewMappers.ViewMapper)
    {
        [FrameworkElement.BackgroundProperty] = MapBorder,
    };

    /// <summary>Creates the handler.</summary>
    public SKXamlCanvasHandler()
        : base(Mapper)
    {
    }

    /// <inheritdoc />
    protected override CodeBrixContentViewGroup CreatePlatformView() => new SKXamlCanvasViewGroup(Context);

    /// <inheritdoc />
    protected override void ConnectHandler(CodeBrixContentViewGroup platformView)
    {
        base.ConnectHandler(platformView);
        if (platformView is SKXamlCanvasViewGroup view && Element is SKXamlCanvas canvas)
        {
            SKXamlCanvasAndroidPlatform.For(canvas)?.Attach(view);
        }
    }

    /// <inheritdoc />
    protected override void DisconnectHandler(CodeBrixContentViewGroup platformView)
    {
        if (platformView is SKXamlCanvasViewGroup view && Element is SKXamlCanvas canvas)
        {
            SKXamlCanvasAndroidPlatform.For(canvas)?.Detach(view);
        }

        base.DisconnectHandler(platformView);
    }
}
