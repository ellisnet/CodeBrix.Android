using System;
using System.Collections.Generic;
using CodeBrix.Android.UI.Portable.TextInput;

namespace CodeBrix.Android.UI.Input.TextInput;

/// <summary>
/// The soft-keyboard profiles of the custom text-entry controls (Core controls that report their focus through
/// CodeBrix.Platform's SoftwareKeyboardFocus seam: the TerminalView and AdvancedTextEdit add-ins). An add-in
/// registers its control's profile from its module initializer; <see cref="CoreTextInputController"/> reads it when
/// the control gains focus. Main thread (registration: any thread, under a lock).
/// </summary>
internal static class CoreTextInput
{
    private static readonly object _gate = new();
    private static readonly Dictionary<Type, CoreTextInputProfile> _profiles = new();

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
}
