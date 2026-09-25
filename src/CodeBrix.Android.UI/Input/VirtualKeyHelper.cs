// Derived from the upstream open-source XAML platform of CodeBrix.Platform (see THIRD-PARTY-NOTICES.txt, item 2),
// src/{U}.UWP/System/VirtualKeyHelper.Android.cs @ tag 6.6.166.
// Licensed under the Apache License, Version 2.0. See THIRD-PARTY-NOTICES.txt.

using System.Collections.Generic;
using Windows.System;
using AInputSourceType = global::Android.Views.InputSourceType;
using AKeycode = global::Android.Views.Keycode;
using AMetaKeyStates = global::Android.Views.MetaKeyStates;

namespace CodeBrix.Android.UI.Input; //was previously: Windows.System;

/// <summary>
/// Android key codes and meta states to WinUI virtual keys and modifiers (and back, for injected
/// input). The upstream table (116 codes) with these fixes: Alt maps to <see cref="VirtualKeyModifiers.Menu"/>
/// (the upstream dropped it) and the Alt keys to LeftMenu/RightMenu; the arrow keys of a KEYBOARD are
/// Up/Down/Left/Right (the upstream always reported the gamepad D-pad keys); the OEM punctuation keys
/// (minus, slash, grave, brackets, backslash, apostrophe) and the right Windows key are mapped.
/// </summary>
internal static class VirtualKeyHelper //was previously: internal static partial class VirtualKeyHelper
{
    private static readonly (AKeycode Code, VirtualKey Key)[] _table =
    {
        (AKeycode.Num0, VirtualKey.Number0), (AKeycode.Num1, VirtualKey.Number1), (AKeycode.Num2, VirtualKey.Number2),
        (AKeycode.Num3, VirtualKey.Number3), (AKeycode.Num4, VirtualKey.Number4), (AKeycode.Num5, VirtualKey.Number5),
        (AKeycode.Num6, VirtualKey.Number6), (AKeycode.Num7, VirtualKey.Number7), (AKeycode.Num8, VirtualKey.Number8),
        (AKeycode.Num9, VirtualKey.Number9),
        (AKeycode.Numpad0, VirtualKey.NumberPad0), (AKeycode.Numpad1, VirtualKey.NumberPad1), (AKeycode.Numpad2, VirtualKey.NumberPad2),
        (AKeycode.Numpad3, VirtualKey.NumberPad3), (AKeycode.Numpad4, VirtualKey.NumberPad4), (AKeycode.Numpad5, VirtualKey.NumberPad5),
        (AKeycode.Numpad6, VirtualKey.NumberPad6), (AKeycode.Numpad7, VirtualKey.NumberPad7), (AKeycode.Numpad8, VirtualKey.NumberPad8),
        (AKeycode.Numpad9, VirtualKey.NumberPad9),
        (AKeycode.A, VirtualKey.A), (AKeycode.B, VirtualKey.B), (AKeycode.C, VirtualKey.C), (AKeycode.D, VirtualKey.D),
        (AKeycode.E, VirtualKey.E), (AKeycode.F, VirtualKey.F), (AKeycode.G, VirtualKey.G), (AKeycode.H, VirtualKey.H),
        (AKeycode.I, VirtualKey.I), (AKeycode.J, VirtualKey.J), (AKeycode.K, VirtualKey.K), (AKeycode.L, VirtualKey.L),
        (AKeycode.M, VirtualKey.M), (AKeycode.N, VirtualKey.N), (AKeycode.O, VirtualKey.O), (AKeycode.P, VirtualKey.P),
        (AKeycode.Q, VirtualKey.Q), (AKeycode.R, VirtualKey.R), (AKeycode.S, VirtualKey.S), (AKeycode.T, VirtualKey.T),
        (AKeycode.U, VirtualKey.U), (AKeycode.V, VirtualKey.V), (AKeycode.W, VirtualKey.W), (AKeycode.X, VirtualKey.X),
        (AKeycode.Y, VirtualKey.Y), (AKeycode.Z, VirtualKey.Z),

        (AKeycode.Comma, (VirtualKey)188), (AKeycode.Period, (VirtualKey)190),
        (AKeycode.Equals, (VirtualKey)187), (AKeycode.NumpadEquals, (VirtualKey)187),
        (AKeycode.NumpadDot, VirtualKey.Decimal), (AKeycode.NumpadComma, VirtualKey.Decimal),
        (AKeycode.NumpadDivide, VirtualKey.Divide), (AKeycode.NumpadSubtract, VirtualKey.Subtract),
        (AKeycode.NumpadMultiply, VirtualKey.Multiply), (AKeycode.NumpadAdd, VirtualKey.Add),
        (AKeycode.Enter, VirtualKey.Enter), (AKeycode.NumpadEnter, VirtualKey.Enter), (AKeycode.Clear, VirtualKey.Clear),
        (AKeycode.Space, VirtualKey.Space), (AKeycode.Tab, VirtualKey.Tab), (AKeycode.Del, VirtualKey.Back),
        (AKeycode.ForwardDel, VirtualKey.Delete), (AKeycode.Escape, VirtualKey.Escape),
        (AKeycode.MetaLeft, VirtualKey.LeftWindows),
        (AKeycode.F1, VirtualKey.F1), (AKeycode.F2, VirtualKey.F2), (AKeycode.F3, VirtualKey.F3), (AKeycode.F4, VirtualKey.F4),
        (AKeycode.F5, VirtualKey.F5), (AKeycode.F6, VirtualKey.F6), (AKeycode.F7, VirtualKey.F7), (AKeycode.F8, VirtualKey.F8),
        (AKeycode.F9, VirtualKey.F9), (AKeycode.F10, VirtualKey.F10), (AKeycode.F11, VirtualKey.F11), (AKeycode.F12, VirtualKey.F12),
        (AKeycode.Semicolon, (VirtualKey)186),
        (AKeycode.Help, VirtualKey.Help), (AKeycode.NumLock, VirtualKey.NumberKeyLock), (AKeycode.ScrollLock, VirtualKey.Scroll),
        (AKeycode.Search, VirtualKey.Search), (AKeycode.Insert, VirtualKey.Insert), (AKeycode.MoveHome, VirtualKey.Home),
        (AKeycode.MoveEnd, VirtualKey.End), (AKeycode.PageUp, VirtualKey.PageUp), (AKeycode.PageDown, VirtualKey.PageDown),
        (AKeycode.AppSwitch, VirtualKey.Application),
        (AKeycode.CtrlLeft, VirtualKey.LeftControl), (AKeycode.CtrlRight, VirtualKey.RightControl),
        (AKeycode.ShiftLeft, VirtualKey.LeftShift), (AKeycode.ShiftRight, VirtualKey.RightShift),
        (AKeycode.SystemNavigationUp, VirtualKey.Up), (AKeycode.SystemNavigationDown, VirtualKey.Down),
        (AKeycode.SystemNavigationLeft, VirtualKey.Left), (AKeycode.SystemNavigationRight, VirtualKey.Right),
        (AKeycode.DpadUp, VirtualKey.GamepadDPadUp), (AKeycode.DpadDown, VirtualKey.GamepadDPadDown),
        (AKeycode.DpadLeft, VirtualKey.GamepadDPadLeft), (AKeycode.DpadRight, VirtualKey.GamepadDPadRight),
        (AKeycode.DpadCenter, VirtualKey.GamepadA),
        (AKeycode.Bookmark, VirtualKey.Favorites), (AKeycode.Menu, VirtualKey.Menu), (AKeycode.Back, VirtualKey.GoBack),
        (AKeycode.Home, VirtualKey.GoHome), (AKeycode.Forward, VirtualKey.GoForward),
        (AKeycode.VolumeMute, (VirtualKey)173), (AKeycode.VolumeDown, (VirtualKey)174), (AKeycode.VolumeUp, (VirtualKey)175),
        (AKeycode.MediaNext, (VirtualKey)176), (AKeycode.MediaPrevious, (VirtualKey)177), (AKeycode.MediaStop, (VirtualKey)178),
        (AKeycode.MediaPlayPause, (VirtualKey)179), (AKeycode.MediaPause, (VirtualKey)179), (AKeycode.MediaPlay, (VirtualKey)250),
        (AKeycode.CapsLock, VirtualKey.CapitalLock), (AKeycode.Kana, VirtualKey.Kana), (AKeycode.Sleep, VirtualKey.Sleep),

        // CodeBrix additions (not in the upstream table).
        (AKeycode.AltLeft, VirtualKey.LeftMenu), (AKeycode.AltRight, VirtualKey.RightMenu),
        (AKeycode.MetaRight, VirtualKey.RightWindows),
        (AKeycode.Minus, (VirtualKey)189), (AKeycode.Slash, (VirtualKey)191), (AKeycode.Grave, (VirtualKey)192),
        (AKeycode.LeftBracket, (VirtualKey)219), (AKeycode.Backslash, (VirtualKey)220),
        (AKeycode.RightBracket, (VirtualKey)221), (AKeycode.Apostrophe, (VirtualKey)222),
        (AKeycode.Plus, VirtualKey.Add), (AKeycode.Break, VirtualKey.Pause), (AKeycode.Sysrq, VirtualKey.Snapshot),
    };

    private static readonly Dictionary<AKeycode, VirtualKey> _toVirtual = BuildToVirtual();
    private static readonly Dictionary<VirtualKey, AKeycode> _toKeycode = BuildToKeycode();

    /// <summary>The WinUI virtual key of an Android key code (None when unmapped).</summary>
    /// <param name="key">The key code.</param>
    /// <param name="source">The event's input source: the arrow keys of a keyboard are Up/Down/Left/Right, a gamepad's D-pad stays GamepadDPad*.</param>
    public static VirtualKey FromKeyCode(AKeycode key, AInputSourceType source = AInputSourceType.Keyboard)
    {
        var fromKeyboard = (source & AInputSourceType.Gamepad) != AInputSourceType.Gamepad && (source & AInputSourceType.Joystick) != AInputSourceType.Joystick;
        if (fromKeyboard)
        {
            switch (key)
            {
                case AKeycode.DpadUp: return VirtualKey.Up;
                case AKeycode.DpadDown: return VirtualKey.Down;
                case AKeycode.DpadLeft: return VirtualKey.Left;
                case AKeycode.DpadRight: return VirtualKey.Right;
                case AKeycode.DpadCenter: return VirtualKey.Enter;
            }
        }

        return _toVirtual.TryGetValue(key, out var virtualKey) ? virtualKey : VirtualKey.None;
    }

    /// <summary>The WinUI modifiers of an Android meta state (Shift, Control, Windows and - fixed - Alt as Menu).</summary>
    public static VirtualKeyModifiers FromModifiers(AMetaKeyStates flags)
    {
        var modifiers = VirtualKeyModifiers.None;
        if ((flags & AMetaKeyStates.ShiftMask) != 0)
        {
            modifiers |= VirtualKeyModifiers.Shift;
        }

        if ((flags & AMetaKeyStates.CtrlMask) != 0)
        {
            modifiers |= VirtualKeyModifiers.Control;
        }

        if ((flags & AMetaKeyStates.MetaMask) != 0)
        {
            modifiers |= VirtualKeyModifiers.Windows;
        }

        if ((flags & AMetaKeyStates.AltMask) != 0)
        {
            modifiers |= VirtualKeyModifiers.Menu;
        }

        return modifiers;
    }

    /// <summary>The Android meta state of WinUI modifiers (injected key events).</summary>
    public static AMetaKeyStates ToMetaState(VirtualKeyModifiers modifiers)
    {
        var meta = (AMetaKeyStates)0;
        if ((modifiers & VirtualKeyModifiers.Shift) != 0)
        {
            meta |= AMetaKeyStates.ShiftOn | AMetaKeyStates.ShiftLeftOn;
        }

        if ((modifiers & VirtualKeyModifiers.Control) != 0)
        {
            meta |= AMetaKeyStates.CtrlOn | AMetaKeyStates.CtrlLeftOn;
        }

        if ((modifiers & VirtualKeyModifiers.Windows) != 0)
        {
            meta |= AMetaKeyStates.MetaOn | AMetaKeyStates.MetaLeftOn;
        }

        if ((modifiers & VirtualKeyModifiers.Menu) != 0)
        {
            meta |= AMetaKeyStates.AltOn | AMetaKeyStates.AltLeftOn;
        }

        return meta;
    }

    /// <summary>The Android key code of a WinUI virtual key from a keyboard (Unknown when there is none).</summary>
    public static AKeycode ToKeyCode(VirtualKey key)
    {
        switch (key)
        {
            case VirtualKey.Up: return AKeycode.DpadUp;
            case VirtualKey.Down: return AKeycode.DpadDown;
            case VirtualKey.Left: return AKeycode.DpadLeft;
            case VirtualKey.Right: return AKeycode.DpadRight;
            case VirtualKey.Shift: return AKeycode.ShiftLeft;
            case VirtualKey.Control: return AKeycode.CtrlLeft;
            case VirtualKey.Menu: return AKeycode.AltLeft;
        }

        return _toKeycode.TryGetValue(key, out var code) ? code : AKeycode.Unknown;
    }

    private static Dictionary<AKeycode, VirtualKey> BuildToVirtual()
    {
        var map = new Dictionary<AKeycode, VirtualKey>();
        foreach (var (code, key) in _table)
        {
            map[code] = key;
        }

        return map;
    }

    private static Dictionary<VirtualKey, AKeycode> BuildToKeycode()
    {
        var map = new Dictionary<VirtualKey, AKeycode>();
        foreach (var (code, key) in _table)
        {
            // First entry wins (Enter, not NumpadEnter; Number0, not a numpad twin).
            map.TryAdd(key, code);
        }

        return map;
    }
}
