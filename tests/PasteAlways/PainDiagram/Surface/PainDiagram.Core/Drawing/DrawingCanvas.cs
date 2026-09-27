namespace CodeBrix.Imaging.Drawing;

/// <summary>
/// SkiaSharp-based drawing surface, abstracted so a single control name -
/// <c>&lt;drawing:DrawingCanvas /&gt;</c> - can be used in the XAML of every head. This one
/// linked source file is compiled into each head's assembly and resolves to the correct
/// base control for that head via conditional compilation:
/// <list type="bullet">
///   <item>CodeBrix.Platform Skia heads (which should have HAS_CODEBRIXPLATFORM defined on
///   their shared assembly); and native WinUI 3 (which should have HAS_WINUI defined):
///   SkiaSharp.Views.Windows.SKXamlCanvas.</item>
///   <item>native WPF (neither symbol): SkiaSharp.Views.WPF.SKElement.</item>
/// </list>
/// It is a plain subclass that carries no extra behavior - the hosting page's code-behind
/// wires PaintSurface and the pointer/mouse events to the DrawingSession exactly as before.
/// </summary>
#if (HAS_CODEBRIXPLATFORM || HAS_WINUI)
public class DrawingCanvas : SkiaSharp.Views.Windows.SKXamlCanvas { }
#else
public class DrawingCanvas : SkiaSharp.Views.WPF.SKElement { }
#endif

public static class DrawCanvasHelper
{
    public static SkiaSharp.SKSize GetViewSize(this DrawingCanvas canvas) =>
        (canvas == null)
        ? default
        : new SkiaSharp.SKSize((float)canvas.ActualWidth, (float)canvas.ActualHeight);

#if (HAS_CODEBRIXPLATFORM || HAS_WINUI)
    public static SkiaSharp.SKPoint GetPointFromPosition(Windows.Foundation.Point point) =>
        new ((float)point.X, (float)point.Y);
#else
    public static SkiaSharp.SKPoint GetPointFromPosition(System.Windows.Point point) =>
        new ((float)point.X, (float)point.Y);
#endif
}
