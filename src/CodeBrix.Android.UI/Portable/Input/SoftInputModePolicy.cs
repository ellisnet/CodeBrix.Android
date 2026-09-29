// Derived from .NET MAUI, src/Controls/src/Core/Platform/Android/Extensions/ApplicationExtensions.cs (ToPlatform),
// src/Core/src/Platform/Android/WindowExtensions.cs (UpdateWindowSoftInputModeAdjust) and
// src/Core/src/Platform/Android/SafeAreaExtensions.cs (the keyboard is a bottom inset unless the window pans) @ 828569a864.
// Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License. See THIRD-PARTY-NOTICES.txt.

using System;
using CodeBrix.Android.UI.Hosting;

namespace CodeBrix.Android.UI.Portable.Input;

/// <summary>
/// The soft-input (IME) adjust policy of a CodeBrix activity, in plain integers so it is testable without Android.
/// The values are Android's WindowManager.LayoutParams SOFT_INPUT_* bits.
/// <list type="bullet">
/// <item>The adjust bits the window gets (<see cref="Resolve"/>): an app-level <see cref="SoftInputAdjust"/> the app
/// SET wins; otherwise the activity's own declared adjust mode ([Activity(WindowSoftInputMode = ...)]) is kept;
/// otherwise Pan (MAUI's default). The visibility (state) bits are never touched.</item>
/// <item>Whether the keyboard is withheld from the page (<see cref="WithholdsKeyboard"/>): only in adjustResize - Pan
/// moves the window instead, Unspecified and adjustNothing leave the page alone.</item>
/// </list>
/// </summary>
internal static class SoftInputModePolicy
{
    /// <summary>SOFT_INPUT_ADJUST_UNSPECIFIED.</summary>
    internal const int AdjustUnspecified = 0x00;

    /// <summary>SOFT_INPUT_ADJUST_RESIZE.</summary>
    internal const int AdjustResize = 0x10;

    /// <summary>SOFT_INPUT_ADJUST_PAN.</summary>
    internal const int AdjustPan = 0x20;

    /// <summary>SOFT_INPUT_ADJUST_NOTHING.</summary>
    internal const int AdjustNothing = 0x30;

    /// <summary>SOFT_INPUT_MASK_ADJUST.</summary>
    internal const int MaskAdjust = 0xF0;

    /// <summary>The adjust bits of an app-level setting (MAUI's ToPlatform; anything unknown is Pan).</summary>
    /// <param name="adjust">The app-level setting.</param>
    /// <returns>The SOFT_INPUT_ADJUST_* value.</returns>
    internal static int ToAdjustBits(SoftInputAdjust adjust) => adjust switch
    {
        SoftInputAdjust.Resize => AdjustResize,
        SoftInputAdjust.Unspecified => AdjustUnspecified,
        _ => AdjustPan,
    };

    /// <summary>
    /// The window's soft-input mode after the policy: <paramref name="currentMode"/> with its adjust bits replaced by
    /// the app-level setting when the app set one, else by <paramref name="declaredMode"/>'s adjust bits when the
    /// activity declared any, else by AdjustPan. The state (visibility) bits of <paramref name="currentMode"/> stay.
    /// </summary>
    /// <param name="currentMode">The window's current SOFT_INPUT_* mode (state + adjust bits).</param>
    /// <param name="declaredMode">The mode the activity declared in its manifest (read before the policy ran).</param>
    /// <param name="appSetting">The app-level setting, or null when the app never set one.</param>
    /// <returns>The mode to set on the window.</returns>
    internal static int Resolve(int currentMode, int declaredMode, SoftInputAdjust? appSetting)
    {
        int adjust;
        if (appSetting is { } setting)
        {
            adjust = ToAdjustBits(setting);
        }
        else if ((declaredMode & MaskAdjust) != AdjustUnspecified)
        {
            adjust = declaredMode & MaskAdjust;
        }
        else
        {
            adjust = AdjustPan;
        }

        return (currentMode & ~MaskAdjust) | adjust;
    }

    /// <summary>True when the window's mode withholds the keyboard from the page (adjustResize only).</summary>
    /// <param name="mode">The window's SOFT_INPUT_* mode.</param>
    /// <returns>True for adjustResize.</returns>
    internal static bool WithholdsKeyboard(int mode) => (mode & MaskAdjust) == AdjustResize;

    /// <summary>
    /// The height Core withholds from the bottom of the window (the root's content bottom occlusion inset, DIPs):
    /// the keyboard's bottom inset in DIPs when the mode withholds it, else 0. The IME inset is measured from the
    /// window's bottom edge (it includes the navigation bar under the keyboard), so the page's bottom edge lands on
    /// the keyboard's top edge.
    /// </summary>
    /// <param name="keyboardPx">The settled IME bottom inset in physical pixels.</param>
    /// <param name="density">The display density (pixels per DIP).</param>
    /// <param name="withholds">The result of <see cref="WithholdsKeyboard"/>.</param>
    /// <returns>The inset in DIPs (never negative).</returns>
    internal static double OcclusionInsetDips(double keyboardPx, double density, bool withholds) =>
        withholds && keyboardPx > 0 && density > 0 ? Math.Max(0, keyboardPx / density) : 0;
}
