// Derived from .NET MAUI, src/Core/src/Platform/Android/MauiWindowInsetListener.cs and
// src/Core/src/Platform/Android/SafeAreaPadding.cs (WindowInsetsExtensions) @ 828569a864.
// Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License. See THIRD-PARTY-NOTICES.txt.

using System;
using System.Collections.Generic;
using CodeBrix.Android.UI.Portable.Layout;
using AView = global::Android.Views.View;
using AViewCompat = global::AndroidX.Core.View.ViewCompat;
using AWindowInsetsAnimationCompat = global::AndroidX.Core.View.WindowInsetsAnimationCompat;
using AWindowInsetsCompat = global::AndroidX.Core.View.WindowInsetsCompat;
using IOnApplyWindowInsetsListener = global::AndroidX.Core.View.IOnApplyWindowInsetsListener;

namespace CodeBrix.Android.UI.Platform.Insets;

/// <summary>
/// The window-insets listener of a CodeBrix activity's root layout (one per activity). It reads the
/// safe-area insets (system bars and display cutout, the per-edge maximum) and the keyboard (IME) inset
/// from every inset dispatch and hands them to <see cref="InsetsChanged"/>; Core's layout then absorbs
/// them (Page padding, VisibleBounds, InputPane). MAUI's IME-animation gate is kept: while the soft
/// keyboard animates, dispatches are held back and the settled insets are re-applied once the animation
/// has ended (plus one looper turn), so the layout does not run once per animation frame.
/// </summary>
/// <remarks>
/// Adapted from MAUI's MauiWindowInsetListener, which applied the insets as Android padding view by view
/// (SafeAreaExtensions). Here Core owns layout, so the listener only reports the insets; the per-view
/// registry, the AppBarLayout/toolbar special cases and the per-view tracking are not needed.
/// </remarks>
internal sealed class WindowInsetsListener : AWindowInsetsAnimationCompat.Callback, IOnApplyWindowInsetsListener
{
    private AView _view;
    private bool _gateReleaseScheduled;
    private bool _reapplyInsetsWhenAnimationEnds;

    /// <summary>Creates the listener (dispatch mode STOP: the root's children do not need the callbacks).</summary>
    internal WindowInsetsListener()
        : base(DispatchModeStop)
    {
    }

    /// <summary>
    /// Raised with the settled insets in physical pixels: (safe area, keyboard bottom inset).
    /// </summary>
    internal event Action<SafeAreaPadding, double> InsetsChanged;

    /// <summary>True while an IME show/hide animation is running.</summary>
    internal bool IsImeAnimating { get; private set; }

    /// <summary>The last settled safe-area insets in physical pixels.</summary>
    internal SafeAreaPadding SafeAreaPx { get; private set; }

    /// <summary>The last settled keyboard (IME) bottom inset in physical pixels.</summary>
    internal double KeyboardPx { get; private set; }

    /// <summary>True once one inset dispatch has been read.</summary>
    internal bool HasInsets { get; private set; }

    /// <summary>Attaches the listener (insets and IME animation callbacks) to the root view.</summary>
    internal void Attach(AView view)
    {
        _view = view ?? throw new ArgumentNullException(nameof(view));
        AViewCompat.SetOnApplyWindowInsetsListener(view, this);
        AViewCompat.SetWindowInsetsAnimationCallback(view, this);
    }

    /// <summary>Detaches the listener from the root view.</summary>
    internal void Detach()
    {
        if (_view is { } view)
        {
            AViewCompat.SetOnApplyWindowInsetsListener(view, null);
            AViewCompat.SetWindowInsetsAnimationCallback(view, null);
            _view = null;
        }
    }

    /// <inheritdoc />
    public AWindowInsetsCompat OnApplyWindowInsets(AView v, AWindowInsetsCompat insets)
    {
        if (insets == null || v == null)
        {
            return insets;
        }

        if (IsImeAnimating)
        {
            // Animation-time values: re-applied once the animation has ended.
            _reapplyInsetsWhenAnimationEnds = true;
            return insets;
        }

        var safeArea = ToSafeAreaInsetsPx(insets);
        var keyboard = GetKeyboardInsetsPx(insets);
        var changed = !HasInsets || SafeAreaMath.Differs(safeArea, SafeAreaPx) || Math.Abs(keyboard - KeyboardPx) > 0.5;
        HasInsets = true;
        SafeAreaPx = safeArea;
        KeyboardPx = keyboard;
        if (changed)
        {
            InsetsChanged?.Invoke(safeArea, keyboard);
        }

        // Not consumed: nothing below the root reads insets natively.
        return insets;
    }

    /// <summary>The safe-area insets: per edge, the larger of the system bars and the display cutout (pixels).</summary>
    internal static SafeAreaPadding ToSafeAreaInsetsPx(AWindowInsetsCompat insets)
    {
        var systemBars = insets.GetInsets(AWindowInsetsCompat.Type.SystemBars());
        var displayCutout = insets.GetInsets(AWindowInsetsCompat.Type.DisplayCutout());
        return SafeAreaPadding.Max(
            new SafeAreaPadding(systemBars?.Left ?? 0, systemBars?.Top ?? 0, systemBars?.Right ?? 0, systemBars?.Bottom ?? 0),
            new SafeAreaPadding(displayCutout?.Left ?? 0, displayCutout?.Top ?? 0, displayCutout?.Right ?? 0, displayCutout?.Bottom ?? 0));
    }

    /// <summary>The keyboard's bottom inset: the distance from the window bottom to the top of the soft keyboard (pixels).</summary>
    internal static double GetKeyboardInsetsPx(AWindowInsetsCompat insets) =>
        insets.GetInsets(AWindowInsetsCompat.Type.Ime())?.Bottom ?? 0;

    /// <inheritdoc />
    public override void OnPrepare(AWindowInsetsAnimationCompat animation)
    {
        base.OnPrepare(animation);
        if (IsImeAnimation(animation))
        {
            StartImeAnimation();
        }
    }

    /// <inheritdoc />
    public override AWindowInsetsAnimationCompat.BoundsCompat OnStart(AWindowInsetsAnimationCompat animation, AWindowInsetsAnimationCompat.BoundsCompat bounds)
    {
        if (IsImeAnimation(animation))
        {
            StartImeAnimation();
        }

        return bounds;
    }

    /// <inheritdoc />
    public override AWindowInsetsCompat OnProgress(AWindowInsetsCompat insets, IList<AWindowInsetsAnimationCompat> runningAnimations) => insets;

    /// <inheritdoc />
    public override void OnEnd(AWindowInsetsAnimationCompat animation)
    {
        base.OnEnd(animation);
        if (!IsImeAnimation(animation))
        {
            return;
        }

        // Keep the gate up for one more main-looper turn: the system's deferred post-animation
        // inset dispatches can still carry animation-time IME insets.
        if (_view is { IsAttachedToWindow: true } poster)
        {
            _gateReleaseScheduled = true;
            poster.Post(() =>
            {
                if (_gateReleaseScheduled)
                {
                    EndImeAnimation(poster);
                }
            });
        }
        else
        {
            EndImeAnimation(null);
        }
    }

    private void StartImeAnimation()
    {
        _gateReleaseScheduled = false;
        IsImeAnimating = true;
    }

    private void EndImeAnimation(AView reapplyThrough)
    {
        IsImeAnimating = false;
        _gateReleaseScheduled = false;
        if (!_reapplyInsetsWhenAnimationEnds || reapplyThrough == null)
        {
            return;
        }

        _reapplyInsetsWhenAnimationEnds = false;

        // One call re-dispatches insets across the whole hierarchy with the settled values.
        AViewCompat.RequestApplyInsets(reapplyThrough);
    }

    private static bool IsImeAnimation(AWindowInsetsAnimationCompat animation) =>
        animation != null && (animation.TypeMask & AWindowInsetsCompat.Type.Ime()) != 0;
}
