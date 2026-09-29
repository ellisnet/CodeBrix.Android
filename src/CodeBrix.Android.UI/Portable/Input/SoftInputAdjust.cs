// Derived from .NET MAUI, src/Controls/src/Core/PlatformConfiguration/AndroidSpecific/Application.cs
// (WindowSoftInputModeAdjust) @ 828569a864.
// Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License. See THIRD-PARTY-NOTICES.txt.

namespace CodeBrix.Android.UI.Hosting;

/// <summary>
/// How a CodeBrix app's window makes room for the soft keyboard (the IME) on Android - MAUI's
/// WindowSoftInputModeAdjust. Set it app-wide with <c>CodeBrixApplication.SoftInputAdjust</c> (the Android host).
/// </summary>
public enum SoftInputAdjust
{
    /// <summary>
    /// The default: the window PANS (moves up, possibly off the top of the screen) so the focused text
    /// field is visible above the keyboard; the page keeps its full size (Android's adjustPan).
    /// </summary>
    Pan,

    /// <summary>
    /// The page is laid out again in the space ABOVE the keyboard: Core withholds the keyboard's height from
    /// the bottom of the window (the root's content bottom occlusion inset, as the Platform's own on-screen
    /// keyboard does), so the page's bottom edge sits on the keyboard's top edge (Android's adjustResize).
    /// Popups keep the full window.
    /// </summary>
    Resize,

    /// <summary>
    /// Neither: the window's adjust mode is left unspecified (Android's adjustUnspecified; the system decides)
    /// and the page keeps its full size.
    /// </summary>
    Unspecified,
}
