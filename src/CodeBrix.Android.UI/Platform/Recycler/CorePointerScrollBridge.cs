using System;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Windows.Foundation;

namespace CodeBrix.Android.UI.Platform.Recycler;

/// <summary>
/// Scrolls a native list for the pointer input only Core sees (plan 2.15, regime 2): pointers that never
/// went through Android's dispatch to the list - input injected into Core (UIReqs, automation) - and the
/// mouse wheel, which the input router gives to Core. Real touches are the list's own
/// (<see cref="CodeBrixRecyclerView.IsNativeTouchActive"/>): their Core pointer streams are ignored here.
/// A Core pointer that moves past the touch slop along the list's axis drags the list; when it lifts,
/// <see cref="DragEnded"/> tells the owner how the drag went (a pager turns its page from it).
/// </summary>
internal sealed class CorePointerScrollBridge : IDisposable
{
    /// <summary>The touch slop in DIPs (Android's default scaled touch slop is 8 dp).</summary>
    internal const double TouchSlopDips = 8;

    /// <summary>The distance one wheel notch (delta 120) scrolls, in DIPs.</summary>
    internal const double WheelNotchDips = 48;

    private readonly UIElement _element;
    private readonly CodeBrixRecyclerView _recycler;
    private readonly PointerEventHandler _pressed;
    private readonly PointerEventHandler _moved;
    private readonly PointerEventHandler _released;
    private readonly PointerEventHandler _cancelled;
    private readonly PointerEventHandler _wheel;
    private uint? _pointerId;
    private bool _dragging;
    private bool _scrolledList;
    private Point _start;
    private Point _last;
    private long _lastTicks;
    private double _velocityX;
    private double _velocityY;

    /// <summary>Starts following Core's pointer events on <paramref name="element"/>.</summary>
    /// <param name="element">The list element (its routed pointer events, handled ones included).</param>
    /// <param name="recycler">The native list.</param>
    internal CorePointerScrollBridge(UIElement element, CodeBrixRecyclerView recycler)
    {
        _element = element ?? throw new ArgumentNullException(nameof(element));
        _recycler = recycler ?? throw new ArgumentNullException(nameof(recycler));
        _pressed = OnPressed;
        _moved = OnMoved;
        _released = OnReleased;
        _cancelled = OnCancelled;
        _wheel = OnWheel;
        _element.AddHandler(UIElement.PointerPressedEvent, _pressed, true);
        _element.AddHandler(UIElement.PointerMovedEvent, _moved, true);
        _element.AddHandler(UIElement.PointerReleasedEvent, _released, true);
        _element.AddHandler(UIElement.PointerCanceledEvent, _cancelled, true);
        _element.AddHandler(UIElement.PointerCaptureLostEvent, _cancelled, true);
        _element.AddHandler(UIElement.PointerWheelChangedEvent, _wheel, true);
    }

    /// <summary>True when the list scrolls horizontally (a pager, a horizontal list).</summary>
    internal bool IsHorizontal { get; set; }

    /// <summary>
    /// Raised when a bridged drag ended: the total travel in DIPs along the list's axis (negative = the
    /// finger moved left/up) and the release velocity in DIPs per second.
    /// </summary>
    internal event Action<double, double> DragEnded;

    /// <inheritdoc />
    public void Dispose()
    {
        _element.RemoveHandler(UIElement.PointerPressedEvent, _pressed);
        _element.RemoveHandler(UIElement.PointerMovedEvent, _moved);
        _element.RemoveHandler(UIElement.PointerReleasedEvent, _released);
        _element.RemoveHandler(UIElement.PointerCanceledEvent, _cancelled);
        _element.RemoveHandler(UIElement.PointerCaptureLostEvent, _cancelled);
        _element.RemoveHandler(UIElement.PointerWheelChangedEvent, _wheel);
    }

    private double Density => HandlerContext.Density(_element);

    private void OnPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_recycler.IsNativeTouchActive || e.Pointer.PointerDeviceType == PointerDeviceType.Mouse)
        {
            // A real touch (the list handles it natively) or a mouse press (no drag-to-scroll for a mouse).
            _pointerId = null;
            return;
        }

        _pointerId = e.Pointer.PointerId;
        _dragging = false;
        _scrolledList = false;
        _start = _last = e.GetCurrentPoint(_element).Position;
        _lastTicks = Environment.TickCount64;
        _velocityX = _velocityY = 0;
    }

    private void OnMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_pointerId != e.Pointer.PointerId)
        {
            return;
        }

        var point = e.GetCurrentPoint(_element).Position;
        if (!_dragging)
        {
            var travel = IsHorizontal ? point.X - _start.X : point.Y - _start.Y;
            if (Math.Abs(travel) <= TouchSlopDips)
            {
                return;
            }

            _dragging = true;
            _last = point;
        }

        var dx = point.X - _last.X;
        var dy = point.Y - _last.Y;
        var now = Environment.TickCount64;
        var elapsed = Math.Max(1, now - _lastTicks) / 1000.0;
        _velocityX = dx / elapsed;
        _velocityY = dy / elapsed;
        _lastTicks = now;
        _last = point;
        var density = Density;
        var before = (_recycler.OffsetX, _recycler.OffsetY);
        _recycler.ScrollByPixels(
            IsHorizontal ? -(int)Math.Round(dx * density) : 0,
            IsHorizontal ? 0 : -(int)Math.Round(dy * density));
        if (before != (_recycler.OffsetX, _recycler.OffsetY))
        {
            // The list moved: the items under the pointer must not also treat it as a tap. A list that
            // cannot scroll (all items shown, inside an outer ScrollViewer) leaves the pointer to Core.
            _scrolledList = true;
            e.Handled = true;
        }
    }

    private void OnReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_pointerId != e.Pointer.PointerId)
        {
            return;
        }

        _pointerId = null;
        if (!_dragging)
        {
            return;
        }

        _dragging = false;
        if (!_scrolledList)
        {
            return;
        }

        e.Handled = true;
        var point = e.GetCurrentPoint(_element).Position;
        var travel = IsHorizontal ? point.X - _start.X : point.Y - _start.Y;
        DragEnded?.Invoke(travel, IsHorizontal ? _velocityX : _velocityY);
    }

    private void OnCancelled(object sender, PointerRoutedEventArgs e)
    {
        if (_pointerId != e.Pointer.PointerId)
        {
            return;
        }

        var wasDragging = _dragging && _scrolledList;
        _pointerId = null;
        _dragging = false;
        if (wasDragging)
        {
            var travel = IsHorizontal ? _last.X - _start.X : _last.Y - _start.Y;
            DragEnded?.Invoke(travel, 0);
        }
    }

    private void OnWheel(object sender, PointerRoutedEventArgs e)
    {
        if (e.Handled)
        {
            // An inner scrolling element took the wheel.
            return;
        }

        var properties = e.GetCurrentPoint(_element).Properties;
        var delta = properties.MouseWheelDelta;
        if (delta == 0)
        {
            return;
        }

        var notches = delta / 120.0;
        var pixels = (int)Math.Round(notches * WheelNotchDips * Density);
        // WinUI: a positive vertical delta (wheel up) scrolls towards the start, a positive horizontal
        // delta (tilt right) towards the end; a horizontal list scrolls a vertical wheel along its axis.
        var before = (_recycler.OffsetX, _recycler.OffsetY);
        if (properties.IsHorizontalMouseWheel)
        {
            _recycler.ScrollByPixels(pixels, 0);
        }
        else if (IsHorizontal)
        {
            _recycler.ScrollByPixels(-pixels, 0);
        }
        else
        {
            _recycler.ScrollByPixels(0, -pixels);
        }
        if (before != (_recycler.OffsetX, _recycler.OffsetY))
        {
            e.Handled = true;
        }
    }
}
