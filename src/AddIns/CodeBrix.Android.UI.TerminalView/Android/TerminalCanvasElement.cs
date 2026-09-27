using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace CodeBrix.Android.UI.TerminalView.Android;

/// <summary>
/// The drawing surface a TerminalControl puts into its template on Android (the element IRenderCanvasPlatform creates):
/// a childless Canvas - the Skia heads' surface (RenderCanvas) is a Canvas too, and the shared UIReqs steps find the
/// surface of a terminal by that type - shown by <see cref="TerminalCanvasHandler"/> as a native Skia view that runs
/// the paint handlers on each draw (one canvas unit = one DIP, clipped to the element) and redraws on
/// <see cref="Invalidate"/>. The whole box is hit-testable: the control's pointer handlers are on this element (the
/// finger/mouse selection gestures, the wheel, the context menu).
/// </summary>
/// <remarks>
/// The contract is the Skia heads' RenderCanvas contract: paint once loaded and sized, and again whenever invalidated;
/// Invalidate from any thread.
/// </remarks>
internal sealed partial class TerminalCanvasElement : Canvas
{
    private readonly List<Action<object, Size>> _paintHandlers = new();
    private int _paintCount;
    private int _invalidateCount;
    private Action _invalidated;

    /// <summary>How many times the element has painted (diagnostics and device fences).</summary>
    internal int PaintCount => Volatile.Read(ref _paintCount);

    /// <summary>How many repaints were requested (diagnostics and device fences).</summary>
    internal int InvalidateCount => Volatile.Read(ref _invalidateCount);

    /// <summary>The display scale factor of the element's window (1.0 = 160 dpi on Android, 96 dpi on the Skia heads).</summary>
    internal double DisplayScale => XamlRoot?.RasterizationScale is > 0 and var scale ? scale : 1.0;

    /// <summary>What the handler does when a repaint is requested (any thread; null while no handler is connected).</summary>
    internal Action Invalidated
    {
        get => Volatile.Read(ref _invalidated);
        set => Volatile.Write(ref _invalidated, value);
    }

    /// <summary>Adds a paint handler (the control's engine paint: the canvas as an object, the size in DIPs).</summary>
    /// <param name="paint">The handler.</param>
    /// <exception cref="ArgumentNullException"><paramref name="paint"/> is null.</exception>
    internal void AddPaintHandler(Action<object, Size> paint)
    {
        ArgumentNullException.ThrowIfNull(paint);
        lock (_paintHandlers)
        {
            _paintHandlers.Add(paint);
        }

        Invalidate();
    }

    /// <summary>Requests a repaint (any thread: the native view is posted an invalidation).</summary>
    internal void Invalidate()
    {
        Interlocked.Increment(ref _invalidateCount);
        Invalidated?.Invoke();
    }

    /// <summary>Runs the paint handlers (the native view's draw pass, UI thread).</summary>
    /// <param name="canvas">The canvas (an SKCanvas), scaled so one unit is one DIP and clipped to the element.</param>
    /// <param name="area">The element's size in DIPs.</param>
    internal void Paint(object canvas, Size area)
    {
        Action<object, Size>[] handlers;
        lock (_paintHandlers)
        {
            if (_paintHandlers.Count == 0)
            {
                return;
            }

            handlers = _paintHandlers.ToArray();
        }

        Interlocked.Increment(ref _paintCount);
        foreach (var handler in handlers)
        {
            handler(canvas, area);
        }
    }

    /// <inheritdoc />
    internal override bool IsViewHit() => true;
}
