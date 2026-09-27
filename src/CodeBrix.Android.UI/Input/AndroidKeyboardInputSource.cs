// Derived from the upstream open-source XAML platform of CodeBrix.Platform (see THIRD-PARTY-NOTICES.txt, item 2),
// src/{U}.UI.Runtime.Skia.Android/Devices/Input/AndroidKeyboardInputSource.cs @ tag 6.6.166.
// Licensed under the Apache License, Version 2.0. See THIRD-PARTY-NOTICES.txt.

using System;
using CodeBrix.Android.UI.Hosting;
using Microsoft.Extensions.Logging;
using Windows.Foundation;
using Windows.System;
using Windows.UI.Core;
using AKeyEvent = global::Android.Views.KeyEvent;
using AKeyEventActions = global::Android.Views.KeyEventActions;

namespace CodeBrix.Android.UI.Input; //was previously: {U}.UI.Runtime.Skia.Android;

/// <summary>
/// The window's <see cref="ICodeBrixKeyboardInputSource"/> (plan 2.15): the activity's DispatchKeyEvent
/// hands every key event here BEFORE the focused widget sees it; Core routes KeyDown/KeyUp (PreviewKeyDown,
/// KeyboardAccelerators) to its focused element, and a key Core handled is not given to Android.
/// </summary>
internal sealed class AndroidKeyboardInputSource : ICodeBrixKeyboardInputSource
{
    private readonly ILogger _log = HostLog.For("CodeBrix.Android.UI.Input.Keyboard");

    /// <summary>Creates the source of a window.</summary>
    /// <param name="host">The window's XamlRoot host (null in host-free use).</param>
    internal AndroidKeyboardInputSource(AndroidXamlRootHost host)
    {
        if (host != null)
        {
            host.KeyboardSource = this;
        }
    }

    /// <inheritdoc />
    public event TypedEventHandler<object, KeyEventArgs> KeyDown;

    /// <inheritdoc />
    public event TypedEventHandler<object, KeyEventArgs> KeyUp;

    /// <summary>
    /// Raises one soft-keyboard key in Core (AP7-B, the text-input connection of custom text-entry controls:
    /// Input/TextInput): a KeyDown carrying the character the key types, or a KeyUp. The Platform software keyboard's
    /// injection shape (its ISoftwareKeyInjector): no modifiers, the character on the press only.
    /// </summary>
    /// <param name="pressed">True for the press, false for the release.</param>
    /// <param name="key">The virtual key (<see cref="VirtualKey.None"/> for a character no key names).</param>
    /// <param name="unicodeKey">The character the key types (press only), or null.</param>
    /// <returns>True when Core handled it.</returns>
    internal bool InjectSoftwareKey(bool pressed, VirtualKey key, char? unicodeKey)
    {
        try
        {
            var args = new KeyEventArgs("keyboard", key, VirtualKeyModifiers.None, default(CorePhysicalKeyStatus), unicodeKey: pressed ? unicodeKey : null);
            if (pressed)
            {
                KeyDown?.Invoke(this, args);
            }
            else
            {
                KeyUp?.Invoke(this, args);
            }

            return args.Handled;
        }
        catch (Exception ex)
        {
            Microsoft.UI.Xaml.Application.Current?.RaiseRecoverableUnhandledException(ex);
            return false;
        }
    }

    /// <summary>Raises one Android key event in Core; returns true when Core handled it.</summary>
    internal bool OnNativeKeyEvent(AKeyEvent e)
    {
        if (e == null || e.Action == AKeyEventActions.Multiple)
        {
            return false;
        }

        var virtualKey = VirtualKeyHelper.FromKeyCode(e.KeyCode, e.Source);
        var modifiers = VirtualKeyHelper.FromModifiers(e.MetaState);
        var unicode = e.GetUnicodeChar(e.MetaState);
        if (_log.IsEnabled(LogLevel.Trace))
        {
            _log.LogTrace("DispatchKeyEvent: {KeyCode} -> {VirtualKey} ({Modifiers}) {Action}", e.KeyCode, virtualKey, modifiers, e.Action);
        }

        try
        {
            var args = new KeyEventArgs("keyboard", virtualKey, modifiers, default(CorePhysicalKeyStatus), unicodeKey: unicode > 0 ? (char)unicode : null);
            if (e.Action == AKeyEventActions.Down)
            {
                KeyDown?.Invoke(this, args);
            }
            else
            {
                KeyUp?.Invoke(this, args);
            }

            return args.Handled;
        }
        catch (Exception ex)
        {
            Microsoft.UI.Xaml.Application.Current?.RaiseRecoverableUnhandledException(ex);
            return false;
        }
    }
}
