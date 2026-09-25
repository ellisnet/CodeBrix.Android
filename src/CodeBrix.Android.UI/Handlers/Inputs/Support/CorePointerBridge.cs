using System;
using CodeBrix.Android.UI.Input;
using CodeBrix.Android.UI.Platform;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.Foundation;
using AInputSourceType = global::Android.Views.InputSourceType;
using AMetaKeyStates = global::Android.Views.MetaKeyStates;
using AMotionEvent = global::Android.Views.MotionEvent;
using AMotionEventActions = global::Android.Views.MotionEventActions;
using ASystemClock = global::Android.OS.SystemClock;
using AView = global::Android.Views.View;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>
/// The input model of the native widgets whose behaviour lives in the WIDGET (ToggleSwitch, Slider,
/// TextBox, PasswordBox, ComboBox, NumberBox, AutoSuggestBox): a real Android touch reaches the widget
/// first (the activity dispatches to native views before Core) and the widget acts on it - the handler
/// marks it handled (<see cref="NativeInput.MarkHandled"/>) so Core only routes it; a pointer that exists
/// only in CORE (injected input: UI automation, the UIReqs scenarios, accessibility tools driving Core)
/// is FORWARDED to the widget as the equivalent MotionEvent sequence, so the widget behaves the same
/// whichever way the finger arrived. Controls whose behaviour is Core's own (the ButtonBase family) do
/// not use this bridge.
/// </summary>
internal sealed class CorePointerBridge
{
    private readonly Func<AView> _target;
    private UIElement _element;
    private PointerEventHandler _pressed;
    private PointerEventHandler _moved;
    private PointerEventHandler _released;
    private PointerEventHandler _canceled;
    private uint? _pointerId;
    private Pointer _pointer;
    private long _downTime;
    private bool _forwarding;
    private bool _realTouch;

    /// <summary>Creates a bridge that forwards to the view <paramref name="target"/> returns (the handler's native view).</summary>
    /// <param name="target">The native view the element is shown by (laid out at the element's rectangle).</param>
    internal CorePointerBridge(Func<AView> target) => _target = target ?? throw new ArgumentNullException(nameof(target));

    /// <summary>True while a forwarded (Core-only) pointer is down.</summary>
    internal bool IsForwardingGesture => _pointerId != null;

    /// <summary>True while a finger (real or forwarded) is acting on the widget.</summary>
    internal bool IsTouchActive => _realTouch || _forwarding || _pointerId != null;

    /// <summary>True while a synthesized event is being dispatched to the widget.</summary>
    internal bool IsDispatchingForwardedEvent => _forwarding;

    /// <summary>Starts listening to the element's Core pointer events.</summary>
    /// <param name="element">The element.</param>
    internal void Attach(UIElement element)
    {
        Detach();
        _element = element;
        _pressed = OnPressed;
        _moved = OnMoved;
        _released = OnReleased;
        _canceled = OnCanceled;
        element.AddHandler(UIElement.PointerPressedEvent, _pressed, true);
        element.AddHandler(UIElement.PointerMovedEvent, _moved, true);
        element.AddHandler(UIElement.PointerReleasedEvent, _released, true);
        element.AddHandler(UIElement.PointerCanceledEvent, _canceled, true);
        element.AddHandler(UIElement.PointerCaptureLostEvent, _canceled, true);
    }

    /// <summary>Stops listening.</summary>
    internal void Detach()
    {
        if (_element is { } element)
        {
            element.RemoveHandler(UIElement.PointerPressedEvent, _pressed);
            element.RemoveHandler(UIElement.PointerMovedEvent, _moved);
            element.RemoveHandler(UIElement.PointerReleasedEvent, _released);
            element.RemoveHandler(UIElement.PointerCanceledEvent, _canceled);
            element.RemoveHandler(UIElement.PointerCaptureLostEvent, _canceled);
        }

        _element = null;
        _pointerId = null;
        _pointer = null;
        _realTouch = false;
    }

    /// <summary>
    /// Called from the widget's touch listener for every MotionEvent it receives: a real touch is marked
    /// handled for Core (the widget acts on it) and suppresses forwarding for the same gesture.
    /// </summary>
    /// <param name="view">The view that received the event.</param>
    /// <param name="e">The event.</param>
    internal void OnNativeTouch(AView view, AMotionEvent e)
    {
        if (_forwarding || e == null)
        {
            return;
        }

        NativeInput.MarkHandled();
        switch (e.ActionMasked)
        {
            case AMotionEventActions.Down:
                _realTouch = true;
                break;
            case AMotionEventActions.Up:
            case AMotionEventActions.Cancel:
                // Core routes this same event right after the native dispatch: forget the gesture after that.
                view?.Post(() => _realTouch = false);
                break;
        }
    }

    private void OnPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_realTouch || _forwarding || _element is not { } element || _pointerId != null)
        {
            return;
        }

        if (element is Control { IsEnabled: false })
        {
            return;
        }

        _pointerId = e.Pointer.PointerId;
        _pointer = e.Pointer;
        element.CapturePointer(e.Pointer);
        _downTime = ASystemClock.UptimeMillis();
        Dispatch(AMotionEventActions.Down, e.GetCurrentPoint(element).Position);
        e.Handled = true;
    }

    private void OnMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_pointerId != e.Pointer.PointerId || _element is not { } element)
        {
            return;
        }

        Dispatch(AMotionEventActions.Move, e.GetCurrentPoint(element).Position);
        e.Handled = true;
    }

    private void OnReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_pointerId != e.Pointer.PointerId || _element is not { } element)
        {
            return;
        }

        _pointerId = null;
        Dispatch(AMotionEventActions.Up, e.GetCurrentPoint(element).Position);
        if (_pointer != null)
        {
            element.ReleasePointerCapture(_pointer);
            _pointer = null;
        }

        e.Handled = true;
    }

    private void OnCanceled(object sender, PointerRoutedEventArgs e)
    {
        if (_pointerId != e.Pointer.PointerId || _element is not { } element)
        {
            return;
        }

        _pointerId = null;
        _pointer = null;
        Dispatch(AMotionEventActions.Cancel, e.GetCurrentPoint(element).Position);
    }

    private void Dispatch(AMotionEventActions action, Point position)
    {
        if (_target() is not { } view || _element is not { } element)
        {
            return;
        }

        var density = HandlerContext.Density(element);
        var now = ASystemClock.UptimeMillis();
        var ev = AMotionEvent.Obtain(_downTime, Math.Max(now, _downTime), action, (float)(position.X * density), (float)(position.Y * density), AMetaKeyStates.None);
        ev.SetSource(AInputSourceType.Touchscreen);
        _forwarding = true;
        try
        {
            view.DispatchTouchEvent(ev);
        }
        finally
        {
            _forwarding = false;
            ev.Recycle();
        }
    }
}
