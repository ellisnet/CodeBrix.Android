using System;
using System.Collections.Generic;
using CodeBrix.Android.UI.Portable.TextInput;
using Microsoft.UI.Xaml.Controls;

namespace CodeBrix.Android.UI.Input.TextInput;

/// <summary>
/// The soft-keyboard profiles of the custom text-entry controls (Core controls that report their focus through
/// CodeBrix.Platform's SoftwareKeyboardFocus seam: the TerminalView and AdvancedTextEdit add-ins), and the text
/// TARGETS of the controls whose text a keyboard may see and edit (<see cref="RegisterTarget"/>). An add-in registers
/// its control's profile (and target) from its module initializer; <see cref="CoreTextInputController"/> reads them
/// when the control gains focus. Main thread (registration: any thread, under a lock).
/// </summary>
internal static class CoreTextInput
{
    private static readonly object _gate = new();
    private static readonly Dictionary<Type, CoreTextInputProfile> _profiles = new();
    private static readonly Dictionary<Type, Func<Control, ICoreTextInputTarget>> _targets = new();
    private static readonly Dictionary<Type, Func<Control, ICoreTextInputCaret>> _carets = new();

    /// <summary>Registers the profile of <paramref name="controlType"/> (and the types derived from it).</summary>
    /// <param name="controlType">The control type.</param>
    /// <param name="profile">The profile.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    internal static void RegisterProfile(Type controlType, CoreTextInputProfile profile)
    {
        ArgumentNullException.ThrowIfNull(controlType);
        ArgumentNullException.ThrowIfNull(profile);
        lock (_gate)
        {
            _profiles[controlType] = profile;
        }
    }

    /// <summary>The profile of <paramref name="control"/>: its type's, else the nearest base type's, else the default.</summary>
    /// <param name="control">The focused control.</param>
    /// <returns>The profile.</returns>
    internal static CoreTextInputProfile ProfileOf(object control)
    {
        lock (_gate)
        {
            for (var type = control?.GetType(); type != null; type = type.BaseType)
            {
                if (_profiles.TryGetValue(type, out var profile))
                {
                    return profile;
                }
            }
        }

        return CoreTextInputProfile.Default;
    }

    /// <summary>
    /// Registers how a soft-keyboard session on <paramref name="controlType"/> (and the types derived from it) reaches
    /// the control's TEXT: <paramref name="factory"/> makes the control's <see cref="ICoreTextInputTarget"/> when a
    /// session opens (it is disposed when the session ends). The input method then sees the text around the cursor,
    /// composes in place and edits the text itself (Portable/TextInput/TextInputTargetEditor). A control with no
    /// target keeps the key-press path (<see cref="CoreTextInputConnection"/> turns the keyboard's text into keys).
    /// </summary>
    /// <param name="controlType">The control type.</param>
    /// <param name="factory">Makes the target of one control (may return null: the key-press path).</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    internal static void RegisterTarget(Type controlType, Func<Control, ICoreTextInputTarget> factory)
    {
        ArgumentNullException.ThrowIfNull(controlType);
        ArgumentNullException.ThrowIfNull(factory);
        lock (_gate)
        {
            _targets[controlType] = factory;
        }
    }

    /// <summary>A new target for <paramref name="control"/> (its type's factory, else the nearest base type's), or null.</summary>
    /// <param name="control">The focused control.</param>
    /// <returns>The target, or null when the control types through key presses.</returns>
    internal static ICoreTextInputTarget CreateTarget(Control control)
    {
        Func<Control, ICoreTextInputTarget> factory = null;
        lock (_gate)
        {
            for (var type = control?.GetType(); type != null && factory == null; type = type.BaseType)
            {
                _targets.TryGetValue(type, out factory);
            }
        }

        return factory?.Invoke(control);
    }

    /// <summary>
    /// [AP8-S item L] Registers where the caret of <paramref name="controlType"/> (and the types derived from it) is:
    /// <paramref name="factory"/> makes the control's <see cref="ICoreTextInputCaret"/> when a session opens (disposed when
    /// it ends). The session's focus view is then laid out on the caret, so Android's pan brings the caret above the soft
    /// keyboard. A control with no caret keeps the parked 1x1 focus view (no pan for it).
    /// </summary>
    /// <param name="controlType">The control type.</param>
    /// <param name="factory">Makes the caret of one control (may return null: no caret).</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    internal static void RegisterCaret(Type controlType, Func<Control, ICoreTextInputCaret> factory)
    {
        ArgumentNullException.ThrowIfNull(controlType);
        ArgumentNullException.ThrowIfNull(factory);
        lock (_gate)
        {
            _carets[controlType] = factory;
        }
    }

    /// <summary>A new caret for <paramref name="control"/> (its type's factory, else the nearest base type's), or null.</summary>
    /// <param name="control">The focused control.</param>
    /// <returns>The caret, or null when the control's caret is not known.</returns>
    internal static ICoreTextInputCaret CreateCaret(Control control)
    {
        Func<Control, ICoreTextInputCaret> factory = null;
        lock (_gate)
        {
            for (var type = control?.GetType(); type != null && factory == null; type = type.BaseType)
            {
                _carets.TryGetValue(type, out factory);
            }
        }

        return factory?.Invoke(control);
    }
}
