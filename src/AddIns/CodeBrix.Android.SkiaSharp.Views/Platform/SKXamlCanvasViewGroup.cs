using CodeBrix.Android.SkiaSharp.Views.Android;
using CodeBrix.Android.UI.Platform;
using ACanvas = global::Android.Graphics.Canvas;
using AContext = global::Android.Content.Context;

namespace CodeBrix.Android.SkiaSharp.Views.Platform;

/// <summary>
/// The native view of an SKXamlCanvas: a CodeBrix view group (the layout replay of the canvas's XAML
/// children) that paints the canvas's Skia buffer in its own draw pass, UNDER its children - an
/// SKXamlCanvas's children draw above its painting, as on every CodeBrix.Platform head.
/// </summary>
internal sealed class SKXamlCanvasViewGroup : CodeBrixContentViewGroup
{
    /// <summary>Creates the view group.</summary>
    /// <param name="context">The activity context.</param>
    internal SKXamlCanvasViewGroup(AContext context)
        : base(context)
    {
        // A view group skips its own OnDraw by default.
        SetWillNotDraw(false);
    }

    /// <summary>The element's canvas platform (set while the handler is connected).</summary>
    internal SKXamlCanvasAndroidPlatform CanvasPlatform { get; set; }

    /// <inheritdoc />
    protected override void OnDraw(ACanvas canvas)
    {
        base.OnDraw(canvas);
        CanvasPlatform?.Draw(this, canvas);
    }
}
