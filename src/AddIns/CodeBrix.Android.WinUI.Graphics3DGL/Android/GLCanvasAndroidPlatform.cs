using CodeBrix.Platform.WinUI.Graphics3DGL;
using CodeBrix.Platform.WinUI.Graphics3DGL.Contracts;
using Microsoft.UI.Composition;

namespace CodeBrix.Android.WinUI.Graphics3DGL.Android;

/// <summary>
/// IGLCanvasPlatform on Android: the element's visual is a BorderVisual that remembers its element. The Skia heads
/// paint the picture from that visual; on Android the native view does (see Handlers/GLCanvasHandler.cs). The visual
/// only reports that the element asked for a new frame: GLCanvasElement.Invalidate() marks the element and has the
/// compositor invalidate its visual, which marks the visual dirty (SetMatrixDirty) - the visual passes that on to
/// the view, whose next drawing pass then runs the element's OnVisualPainting.
/// </summary>
internal sealed class GLCanvasAndroidPlatform : IGLCanvasPlatform
{
    /// <inheritdoc />
    public BorderVisual CreateVisual(GLCanvasElement owner, Compositor compositor) => new GLCanvasVisual(owner, compositor);

    /// <summary>The visual of a GLCanvasElement.</summary>
    internal sealed class GLCanvasVisual : BorderVisual
    {
        internal GLCanvasVisual(GLCanvasElement owner, Compositor compositor)
            : base(compositor)
        {
            CanvasElement = owner;
        }

        /// <summary>The element.</summary>
        internal GLCanvasElement CanvasElement { get; }

        /// <summary>Called whenever the visual is invalidated (set by the element's handler while it is connected).</summary>
        internal System.Action Invalidated { get; set; }

        /// <inheritdoc />
        internal override bool SetMatrixDirty()
        {
            var dirty = base.SetMatrixDirty();
            Invalidated?.Invoke();
            return dirty;
        }
    }
}
