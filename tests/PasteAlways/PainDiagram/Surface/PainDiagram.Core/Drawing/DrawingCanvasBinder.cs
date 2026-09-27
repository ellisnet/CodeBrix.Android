using System;

namespace CodeBrix.Imaging.Drawing;

/// <summary>
/// Wires a <see cref="DrawingCanvas"/> to the <see cref="DrawingSession"/> that the hosting
/// page's view model owns, so every head spells the wiring the same way and none of them keeps
/// a copy of it in code-behind. The paint handler renders the session; press, move, release and
/// capture-lost are forwarded to it and the pointer is captured while a stroke is in progress.
/// The bodies are forwarding-only, so no drawing state lives in the view: the session decides
/// whether a press starts a stroke and tracks whether one is in flight.
///
/// The events differ per UI stack, so the two branches are chosen the same way
/// <see cref="DrawingCanvas"/> chooses its base class: pointer events on the CodeBrix.Platform
/// Skia heads and on native WinUI 3, mouse events on native WPF.
/// </summary>
public static class DrawingCanvasBinder
{
    /// <summary>
    /// Subscribes the canvas's paint and pointer (or mouse) events to the drawing session the
    /// getter returns. The getter is called on every event rather than captured once, so the
    /// canvas can be wired before the page's <c>DataContext</c> has arrived and keeps working
    /// after the view model is disposed.
    /// </summary>
    /// <param name="canvas">The canvas to wire up; nothing happens when it is <c>null</c>.</param>
    /// <param name="sessionGetter">
    /// Returns the current drawing session, or <c>null</c> when there is not one yet.
    /// </param>
    public static void BindToSession(this DrawingCanvas canvas, Func<DrawingSession> sessionGetter)
    {
        if (canvas == null || sessionGetter == null) { return; }

        canvas.PaintSurface += (_, e) => sessionGetter()?.Render(e.Surface, e.Info);

#if (HAS_CODEBRIXPLATFORM || HAS_WINUI)
        canvas.PointerPressed += (_, e) =>
        {
            var session = sessionGetter();
            if (session == null) { return; }

            var pointerPoint = e.GetCurrentPoint(canvas);
            if (!pointerPoint.Properties.IsLeftButtonPressed) { return; }

            if (session.PointerPressed(DrawCanvasHelper.GetPointFromPosition(pointerPoint.Position), canvas.GetViewSize()))
            {
                canvas.CapturePointer(e.Pointer);
                e.Handled = true;
            }
        };

        canvas.PointerMoved += (_, e) =>
        {
            var session = sessionGetter();
            if (session is not { IsPointerActive: true }) { return; }

            session.PointerMoved(DrawCanvasHelper.GetPointFromPosition(e.GetCurrentPoint(canvas).Position), canvas.GetViewSize());
            e.Handled = true;
        };

        canvas.PointerReleased += (_, e) =>
        {
            var session = sessionGetter();
            if (session is not { IsPointerActive: true }) { return; }

            session.PointerReleased();
            canvas.ReleasePointerCapture(e.Pointer);
            e.Handled = true;
        };

        //If capture is lost mid-stroke (e.g. the window deactivates), discard the stroke
        canvas.PointerCaptureLost += (_, _) => sessionGetter()?.PointerCanceled();

        //SKXamlCanvas does not repaint itself when it is resized
        canvas.SizeChanged += (_, _) => canvas.Invalidate();
#else
        canvas.MouseDown += (_, e) =>
        {
            var session = sessionGetter();
            if (session == null || e.ChangedButton != System.Windows.Input.MouseButton.Left) { return; }

            if (session.PointerPressed(DrawCanvasHelper.GetPointFromPosition(e.GetPosition(canvas)), canvas.GetViewSize()))
            {
                canvas.CaptureMouse();
                e.Handled = true;
            }
        };

        canvas.MouseMove += (_, e) =>
        {
            var session = sessionGetter();
            if (session is not { IsPointerActive: true }) { return; }

            session.PointerMoved(DrawCanvasHelper.GetPointFromPosition(e.GetPosition(canvas)), canvas.GetViewSize());
            e.Handled = true;
        };

        canvas.MouseUp += (_, e) =>
        {
            var session = sessionGetter();
            if (e.ChangedButton != System.Windows.Input.MouseButton.Left || session is not { IsPointerActive: true }) { return; }

            session.PointerReleased();
            canvas.ReleaseMouseCapture();
            e.Handled = true;
        };

        //If capture is lost mid-stroke (e.g. the window deactivates), discard the stroke
        canvas.LostMouseCapture += (_, _) => sessionGetter()?.PointerCanceled();
#endif
    }
}
