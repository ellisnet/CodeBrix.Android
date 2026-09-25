// Derived from the upstream open-source XAML platform of CodeBrix.Platform (see THIRD-PARTY-NOTICES.txt, item 2),
// src/{U}.UI.Runtime.Skia.Android/Devices/Input/AndroidCorePointerInputSource.cs @ tag 6.6.166.
// Licensed under the Apache License, Version 2.0. See THIRD-PARTY-NOTICES.txt.

using System;
using System.Collections.Generic;
using CodeBrix.Android.UI.Hosting;
using Microsoft.Extensions.Logging;
using Windows.Devices.Input;
using Windows.Foundation;
using Windows.UI.Core;
using Windows.UI.Input;
using AAxis = global::Android.Views.Axis;
using AMotionEvent = global::Android.Views.MotionEvent;
using AMotionEventActions = global::Android.Views.MotionEventActions;
using PointerEventArgs = Windows.UI.Core.PointerEventArgs;

namespace CodeBrix.Android.UI.Input; //was previously: {U}.UI.Runtime.Skia.Android;

/// <summary>
/// The window's <see cref="ICodeBrixCorePointerInputSource"/> (plan 2.15, regime 2): every touch, mouse,
/// stylus, hover and wheel MotionEvent of the activity reaches Core's managed pointer pipeline (hit test
/// H8, routed Pointer* events, the gesture recognizer, capture), in DIPs relative to the XamlRoot. A
/// native view that takes a gesture over (a scroll view that starts dragging) CANCELS that pointer in
/// Core; an event a native widget handled is delivered with Handled set. One source per XamlRoot host.
/// </summary>
/// <remarks>
/// Adapted from the upstream Skia head's source, which was a singleton fed with events the Skia canvas
/// did not consume. Here it is per window, wheel events are converted (the upstream did not), capture
/// keeps native scroll views from intercepting, and PointerCursor sets the root view's pointer icon.
/// </remarks>
internal sealed class AndroidCorePointerInputSource : ICodeBrixCorePointerInputSource
{
    private readonly ILogger _log = HostLog.For("CodeBrix.Android.UI.Input.Pointers");
    private readonly HashSet<uint> _captured = new();
    private CoreCursor _cursor = new(CoreCursorType.Arrow, 0);
    private Point _position;

    /// <summary>Creates the source of a window.</summary>
    /// <param name="host">The window's XamlRoot host (null in host-free use).</param>
    internal AndroidCorePointerInputSource(AndroidXamlRootHost host)
    {
        Host = host;
        if (host != null)
        {
            host.PointerSource = this;
        }
    }

#pragma warning disable CS0067 // PointerCaptureLost is raised by Core's own capture logic, not by the source.
    /// <inheritdoc />
    public event TypedEventHandler<object, PointerEventArgs> PointerCaptureLost;
#pragma warning restore CS0067

    /// <inheritdoc />
    public event TypedEventHandler<object, PointerEventArgs> PointerEntered;

    /// <inheritdoc />
    public event TypedEventHandler<object, PointerEventArgs> PointerExited;

    /// <inheritdoc />
    public event TypedEventHandler<object, PointerEventArgs> PointerMoved;

    /// <inheritdoc />
    public event TypedEventHandler<object, PointerEventArgs> PointerPressed;

    /// <inheritdoc />
    public event TypedEventHandler<object, PointerEventArgs> PointerReleased;

    /// <inheritdoc />
    public event TypedEventHandler<object, PointerEventArgs> PointerWheelChanged;

    /// <inheritdoc />
    public event TypedEventHandler<object, PointerEventArgs> PointerCancelled;

    /// <summary>The window host this source feeds.</summary>
    internal AndroidXamlRootHost Host { get; }

    /// <inheritdoc />
    public bool HasCapture => _captured.Count > 0;

    /// <inheritdoc />
    public Point PointerPosition => _position;

    /// <inheritdoc />
    public CoreCursor PointerCursor
    {
        get => _cursor;
        set
        {
            _cursor = value;
            PointerIcons.Apply(Host?.Wrapper.Activity?.RootLayout, value);
        }
    }

    /// <summary>True while Core has captured the pointer (a native scroll view must not intercept it).</summary>
    internal bool IsCaptured(uint pointerId) => _captured.Contains(pointerId);

    /// <inheritdoc />
    public void SetPointerCapture()
    {
    }

    /// <inheritdoc />
    public void ReleasePointerCapture()
    {
    }

    /// <inheritdoc />
    void ICodeBrixCorePointerInputSource.SetPointerCapture(PointerIdentifier pointer)
    {
        _captured.Add(pointer.Id);

        // A captured stream belongs to Core: no ancestor may intercept it (a native scroll view).
        Host?.Wrapper.Activity?.RootLayout?.ContentLayer?.RequestDisallowInterceptTouchEvent(true);
    }

    /// <inheritdoc />
    void ICodeBrixCorePointerInputSource.ReleasePointerCapture(PointerIdentifier pointer) => _captured.Remove(pointer.Id);

    /// <summary>
    /// Feeds one MotionEvent to Core.
    /// </summary>
    /// <param name="nativeArgs">The event (window coordinates).</param>
    /// <param name="originX">The XamlRoot content's left in the window, in pixels.</param>
    /// <param name="originY">The XamlRoot content's top in the window, in pixels.</param>
    /// <param name="density">Pixels per DIP.</param>
    /// <param name="nativelyHandled">True when a native widget handled the event (delivered with Handled set).</param>
    /// <param name="cancelled">Pointers a native view took over: their events are dropped (the take-over sent a cancel).</param>
    internal void OnNativeMotionEvent(AMotionEvent nativeArgs, float originX, float originY, double density, bool nativelyHandled, ISet<uint> cancelled)
    {
        try
        {
            var pointerCount = nativeArgs.PointerCount;
            var action = nativeArgs.Action & AMotionEventActions.Mask;
            if (action == AMotionEventActions.Scroll)
            {
                RaiseWheel(nativeArgs, originX, originY, density);
                return;
            }

            if (pointerCount > 1 && action == AMotionEventActions.Move)
            {
                // A move is raised for all pointers (multi-touch fingers).
                for (var pointerIndex = 0; pointerIndex < pointerCount; pointerIndex++)
                {
                    if (!IsCancelled(nativeArgs, pointerIndex, cancelled))
                    {
                        Dispatch(AMotionEventActions.Move, ToManaged(nativeArgs, originX, originY, density, pointerIndex, nativelyHandled));
                    }
                }
            }
            else if (!IsCancelled(nativeArgs, nativeArgs.ActionIndex, cancelled))
            {
                Dispatch(action, ToManaged(nativeArgs, originX, originY, density, nativeArgs.ActionIndex, nativelyHandled));
            }

            if (action is AMotionEventActions.Up or AMotionEventActions.Cancel)
            {
                // The gesture is over: every pointer of it is free again.
                cancelled?.Clear();
                _captured.Clear();
            }
            else if (action == AMotionEventActions.PointerUp)
            {
                cancelled?.Remove(PointerHelpers.GetPointerId(nativeArgs, nativeArgs.ActionIndex));
            }
        }
        catch (Exception error)
        {
            if (_log.IsEnabled(LogLevel.Error))
            {
                _log.LogError(error, "Failed to dispatch a native pointer event.");
            }
        }
    }

    /// <summary>
    /// A native view took the pointer's gesture over (it scrolls natively): Core gets a cancel for it and
    /// drops the rest of its stream.
    /// </summary>
    internal void CancelPointer(AMotionEvent nativeArgs, int pointerIndex, float originX, float originY, double density)
    {
        var args = ToManaged(nativeArgs, originX, originY, density, pointerIndex, nativelyHandled: true);
        _captured.Remove(args.CurrentPoint.PointerId);
        PointerCancelled?.Invoke(this, args);
    }

    private static bool IsCancelled(AMotionEvent nativeArgs, int pointerIndex, ISet<uint> cancelled) =>
        cancelled != null && cancelled.Count > 0 && cancelled.Contains(PointerHelpers.GetPointerId(nativeArgs, pointerIndex));

    private void Dispatch(AMotionEventActions action, PointerEventArgs args)
    {
        _position = args.CurrentPoint.Position;
        switch (action)
        {
            case AMotionEventActions.HoverEnter when args.CurrentPoint.PointerDeviceType is PointerDeviceType.Touch:
            case AMotionEventActions.HoverExit when args.CurrentPoint.PointerDeviceType is PointerDeviceType.Touch:
                // Touch hover only happens with TalkBack: ignored.
                break;

            case AMotionEventActions.HoverEnter:
                PointerEntered?.Invoke(this, args);
                break;

            case AMotionEventActions.HoverExit when !args.CurrentPoint.IsInContact:
                // A mouse button press / pen touch sends a HoverExit before the Down: only a real exit counts.
                PointerExited?.Invoke(this, args);
                break;

            case AMotionEventActions.HoverExit:
                break;

            case PointerHelpers.StylusWithBarrelDown:
            case AMotionEventActions.Down:
            case AMotionEventActions.PointerDown:
                PointerPressed?.Invoke(this, args);
                break;

            case PointerHelpers.StylusWithBarrelUp:
            case AMotionEventActions.Up:
            case AMotionEventActions.PointerUp:
                PointerReleased?.Invoke(this, args);
                break;

            case PointerHelpers.StylusWithBarrelMove:
            case AMotionEventActions.Move:
            case AMotionEventActions.HoverMove:
                PointerMoved?.Invoke(this, args);
                break;

            case AMotionEventActions.Cancel:
                PointerCancelled?.Invoke(this, args);
                break;

            default:
                // ButtonPress/ButtonRelease duplicate Down/Up for mice; others are not pointer input.
                break;
        }
    }

    private void RaiseWheel(AMotionEvent nativeArgs, float originX, float originY, double density)
    {
        var vertical = nativeArgs.GetAxisValue(AAxis.Vscroll);
        var horizontal = nativeArgs.GetAxisValue(AAxis.Hscroll);
        if (vertical != 0)
        {
            var args = ToManaged(nativeArgs, originX, originY, density, 0, false);
            args.CurrentPoint.Properties.MouseWheelDelta = (int)Math.Round(vertical * 120);
            PointerWheelChanged?.Invoke(this, args);
        }

        if (horizontal != 0)
        {
            var args = ToManaged(nativeArgs, originX, originY, density, 0, false);
            args.CurrentPoint.Properties.IsHorizontalMouseWheel = true;
            args.CurrentPoint.Properties.MouseWheelDelta = (int)Math.Round(horizontal * 120);
            PointerWheelChanged?.Invoke(this, args);
        }
    }

    private static PointerEventArgs ToManaged(AMotionEvent nativeArgs, float originX, float originY, double density, int pointerIndex, bool nativelyHandled)
    {
        var nativePointerType = nativeArgs.GetToolType(pointerIndex);
        var pointerType = nativePointerType.ToPointerDeviceType();
        var pointerDevice = PointerDevice.For(pointerType);
        var pointerId = PointerHelpers.GetPointerId(nativeArgs, pointerIndex);
        var nativePointerAction = nativeArgs.Action;
        var nativePointerButtons = nativeArgs.ButtonState;

        uint frameId;
        ulong timestamp;
        if (OperatingSystem.IsAndroidVersionAtLeast(34))
        {
            var nativeTimestamp = nativeArgs.EventTimeNanos;
            frameId = (uint)nativeTimestamp;
            timestamp = (ulong)nativeTimestamp / 1000; // ns to microseconds
        }
        else
        {
            var nativeTimestamp = nativeArgs.EventTime;
            frameId = (uint)nativeTimestamp;
            timestamp = (ulong)nativeTimestamp * 1000; // ms to microseconds
        }

        var isInContact = PointerHelpers.IsInContact(nativeArgs, pointerType, nativePointerAction, nativePointerButtons);
        var keyModifiers = nativeArgs.MetaState.ToVirtualKeyModifiers();
        var x = (nativeArgs.GetX(pointerIndex) - originX) / density;
        var y = (nativeArgs.GetY(pointerIndex) - originY) / density;

        var position = new Point(x, y);
        var properties = PointerHelpers.GetProperties(nativeArgs, pointerIndex, nativePointerType, nativePointerAction, nativePointerButtons, isInRange: true, isInContact);
        var point = new PointerPoint(frameId, timestamp, pointerDevice, pointerId, position, position, isInContact, properties);
        return new PointerEventArgs(point, keyModifiers)
        {
            Handled = nativelyHandled,
        };
    }
}
