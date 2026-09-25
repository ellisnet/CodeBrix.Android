// Derived from the upstream open-source XAML platform of CodeBrix.Platform (see THIRD-PARTY-NOTICES.txt, item 2),
// src/{U}.UWP/Extensions/PointerHelpers.Android.cs and src/{U}.UWP/Extensions/MotionEventExtensions.Android.cs @ tag 6.6.166.
// Licensed under the Apache License, Version 2.0. See THIRD-PARTY-NOTICES.txt.

using System;
using System.Collections.Generic;
using Windows.Devices.Input;
using Windows.System;
using Windows.UI.Input;
using AAxis = global::Android.Views.Axis;
using AMotionEvent = global::Android.Views.MotionEvent;
using AMotionEventActions = global::Android.Views.MotionEventActions;
using AMotionEventButtonState = global::Android.Views.MotionEventButtonState;
using AMotionEventToolType = global::Android.Views.MotionEventToolType;

namespace CodeBrix.Android.UI.Input; //was previously: {U}.UI.Xaml.Extensions;

/// <summary>
/// MotionEvent to WinUI pointer conversions: pointer ids, device types, the in-contact state and the
/// pointer point properties (buttons, pressure, update kind, stylus barrel and eraser).
/// </summary>
internal static class PointerHelpers
{
    /// <summary>The stylus is pressed while holding the barrel button.</summary>
    internal const AMotionEventActions StylusWithBarrelDown = (AMotionEventActions)211;

    /// <summary>The stylus is moved after having been pressed while holding the barrel button.</summary>
    internal const AMotionEventActions StylusWithBarrelMove = (AMotionEventActions)213;

    /// <summary>The stylus is released after having been pressed while holding the barrel button.</summary>
    internal const AMotionEventActions StylusWithBarrelUp = (AMotionEventActions)212;

    private const int PointerIdsCount = (int)AMotionEventActions.PointerIndexMask >> (int)AMotionEventActions.PointerIndexShift; // 0xff
    private const int PointerIdsShift = 31 - (int)AMotionEventActions.PointerIndexShift; // 23

    private static readonly Dictionary<AMotionEventButtonState, PointerUpdateKind> _none = new(0);

    private static readonly Dictionary<AMotionEventButtonState, PointerUpdateKind> _fingerDownUpdates = new()
    {
        { 0, PointerUpdateKind.LeftButtonPressed },
        { AMotionEventButtonState.Primary, PointerUpdateKind.LeftButtonPressed },
    };

    private static readonly Dictionary<AMotionEventButtonState, PointerUpdateKind> _fingerUpUpdates = new()
    {
        { 0, PointerUpdateKind.LeftButtonReleased },
        { AMotionEventButtonState.Primary, PointerUpdateKind.LeftButtonReleased },
    };

    private static readonly Dictionary<AMotionEventButtonState, PointerUpdateKind> _mouseDownUpdates = new()
    {
        { AMotionEventButtonState.Primary, PointerUpdateKind.LeftButtonPressed },
        { AMotionEventButtonState.Tertiary, PointerUpdateKind.MiddleButtonPressed },
        { AMotionEventButtonState.Secondary, PointerUpdateKind.RightButtonPressed },
    };

    private static readonly Dictionary<AMotionEventButtonState, PointerUpdateKind> _mouseUpUpdates = new()
    {
        { AMotionEventButtonState.Primary, PointerUpdateKind.LeftButtonReleased },
        { AMotionEventButtonState.Tertiary, PointerUpdateKind.MiddleButtonReleased },
        { AMotionEventButtonState.Secondary, PointerUpdateKind.RightButtonReleased },
    };

    /// <summary>
    /// A pointer id stable across down_1 / down_2 / up_1 / up_2: the MotionEvent pointer id in the top
    /// bits, the device id in the low bits.
    /// </summary>
    internal static uint GetPointerId(AMotionEvent nativeEvent, int pointerIndex) =>
        ((uint)nativeEvent.GetPointerId(pointerIndex) & PointerIdsCount) << PointerIdsShift | (uint)nativeEvent.DeviceId;

    /// <summary>The WinUI device type of an Android tool type (MotionEventExtensions).</summary>
    internal static PointerDeviceType ToPointerDeviceType(this AMotionEventToolType type) => type switch
    {
        AMotionEventToolType.Mouse => PointerDeviceType.Mouse,
        AMotionEventToolType.Stylus or AMotionEventToolType.Eraser => PointerDeviceType.Pen,
        _ => PointerDeviceType.Touch,
    };

    /// <summary>The WinUI modifiers of the event's meta state.</summary>
    internal static VirtualKeyModifiers ToVirtualKeyModifiers(this global::Android.Views.MetaKeyStates metaState) =>
        VirtualKeyHelper.FromModifiers(metaState);

    /// <summary>Whether the pointer is in contact (pressed) for this event.</summary>
    internal static bool IsInContact(AMotionEvent nativeEvent, PointerDeviceType pointerType, AMotionEventActions action, AMotionEventButtonState buttons)
    {
        switch (pointerType)
        {
            case PointerDeviceType.Mouse:
                // For mouse, we cannot only rely on action: We will get a "HoverExit" when we press the left button.
                return buttons != 0;

            case PointerDeviceType.Pen when nativeEvent.GetAxisValue(AAxis.Distance, nativeEvent.ActionIndex) is not 0:
                return false;

            case PointerDeviceType.Pen when nativeEvent.GetAxisValue(AAxis.Pressure, nativeEvent.ActionIndex) is not 0:
                return true;

            case PointerDeviceType.Pen:
                // Neither a distance nor a pressure (seen on some devices): the pen is NOT in contact, except
                // for the actions that imply it.
                switch (action)
                {
                    case AMotionEventActions.HoverMove:
                        return false;
                    case AMotionEventActions.HoverEnter:
                    case AMotionEventActions.HoverExit:
                    case AMotionEventActions.Move:
                    case StylusWithBarrelDown:
                    case AMotionEventActions.Down:
                    case AMotionEventActions.PointerDown:
                        return true;
                    case StylusWithBarrelUp:
                    case AMotionEventActions.Up:
                    case AMotionEventActions.PointerUp:
                        return false;
                    default:
                        return true;
                }

            default:
                // WARNING: MotionEventActions.Down == 0, so action.HasFlag(MotionEventActions.Up) is always true!
                return !action.HasFlag(AMotionEventActions.Up)
                    && !action.HasFlag(AMotionEventActions.PointerUp)
                    && !action.HasFlag(AMotionEventActions.Cancel);
        }
    }

    /// <summary>The pointer point properties of one pointer of the event.</summary>
    internal static PointerPointProperties GetProperties(AMotionEvent nativeEvent, int pointerIndex, AMotionEventToolType type, AMotionEventActions action, AMotionEventButtonState buttons, bool isInRange, bool isInContact)
    {
        var props = new PointerPointProperties
        {
            IsPrimary = true,
            IsInRange = isInRange,
        };

        var isDown = action == AMotionEventActions.Down || action.HasFlag(AMotionEventActions.PointerDown);
        var isUp = action.HasFlag(AMotionEventActions.Up) || action.HasFlag(AMotionEventActions.PointerUp);
        var updates = _none;
        switch (type)
        {
            case AMotionEventToolType.Finger:
            case AMotionEventToolType.Unknown:
                props.IsLeftButtonPressed = isInContact;
                updates = isDown ? _fingerDownUpdates : isUp ? _fingerUpUpdates : _none;
                break;

            case AMotionEventToolType.Mouse:
                props.IsLeftButtonPressed = buttons.HasFlag(AMotionEventButtonState.Primary);
                props.IsMiddleButtonPressed = buttons.HasFlag(AMotionEventButtonState.Tertiary);
                props.IsRightButtonPressed = buttons.HasFlag(AMotionEventButtonState.Secondary);
                updates = isDown ? _mouseDownUpdates : isUp ? _mouseUpUpdates : _none;
                break;

            case AMotionEventToolType.Stylus when action == StylusWithBarrelDown:
            case AMotionEventToolType.Stylus when action == StylusWithBarrelMove:
            case AMotionEventToolType.Stylus when action == StylusWithBarrelUp:
                props.IsBarrelButtonPressed = buttons.HasFlag(AMotionEventButtonState.StylusPrimary);
                props.IsRightButtonPressed = isInContact;
                props.Pressure = Math.Min(1f, nativeEvent.GetPressure(pointerIndex));
                break;

            case AMotionEventToolType.Stylus:
                props.IsBarrelButtonPressed = buttons.HasFlag(AMotionEventButtonState.StylusPrimary);
                props.IsLeftButtonPressed = isInContact;
                props.Pressure = Math.Min(1f, nativeEvent.GetPressure(pointerIndex));
                break;

            case AMotionEventToolType.Eraser:
                props.IsEraser = true;
                props.Pressure = Math.Min(1f, nativeEvent.GetPressure(pointerIndex));
                break;
        }

        if (updates.TryGetValue(nativeEvent.ActionButton, out var update))
        {
            props.PointerUpdateKind = update;
        }

        return props;
    }
}
