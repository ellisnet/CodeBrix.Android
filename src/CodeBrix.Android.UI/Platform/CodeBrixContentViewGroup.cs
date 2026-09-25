// Derived from .NET MAUI, src/Core/src/Platform/Android/ContentViewGroup.cs @ 828569a864. Copyright (c) .NET Foundation and Contributors.
// Licensed under the MIT License. See THIRD-PARTY-NOTICES.txt.

using AContext = global::Android.Content.Context;
using ACanvas = global::Android.Graphics.Canvas;
using APath = global::Android.Graphics.Path;
using ARectF = global::Android.Graphics.RectF;

namespace CodeBrix.Android.UI.Platform;

/// <summary>
/// A <see cref="CodeBrixViewGroup"/> that can clip its children to a rounded rectangle: the
/// view of a Border (WinUI clips a Border's child to the inner edge of its rounded corners) and
/// of the templated fallback. Adapted from MAUI's ContentViewGroup, whose clip path came from
/// its Java base PlatformContentViewGroup; here it is applied in <see cref="DispatchDraw"/>.
/// </summary>
internal class CodeBrixContentViewGroup : CodeBrixViewGroup
{
    private readonly APath _clipPath = new();
    private float[] _clipRadii;
    private float _clipLeft;
    private float _clipTop;
    private float _clipRight;
    private float _clipBottom;
    private bool _hasClip;

    /// <summary>Creates the view group.</summary>
    /// <param name="context">The context.</param>
    internal CodeBrixContentViewGroup(AContext context)
        : base(context)
    {
    }

    /// <summary>
    /// Clips children to the rectangle inset by the given pixels with the given per-corner radii
    /// (Path.addRoundRect order), or removes the clip when <paramref name="radii"/> is null.
    /// </summary>
    internal void SetChildClip(float left, float top, float right, float bottom, float[] radii)
    {
        _hasClip = radii != null;
        _clipRadii = radii;
        _clipLeft = left;
        _clipTop = top;
        _clipRight = right;
        _clipBottom = bottom;
        UpdateClipPath(Width, Height);
        Invalidate();
    }

    /// <inheritdoc />
    protected override void DispatchDraw(ACanvas canvas)
    {
        if (!_hasClip)
        {
            base.DispatchDraw(canvas);
            return;
        }

        var save = canvas.Save();
        canvas.ClipPath(_clipPath);
        base.DispatchDraw(canvas);
        canvas.RestoreToCount(save);
    }

    /// <inheritdoc />
    protected override void OnSizeChanged(int w, int h, int oldw, int oldh)
    {
        base.OnSizeChanged(w, h, oldw, oldh);
        UpdateClipPath(w, h);
    }

    private void UpdateClipPath(int width, int height)
    {
        _clipPath.Reset();
        if (!_hasClip || width <= 0 || height <= 0)
        {
            return;
        }

        using var rect = new ARectF(_clipLeft, _clipTop, width - _clipRight, height - _clipBottom);
        _clipPath.AddRoundRect(rect, _clipRadii, APath.Direction.Cw);
    }
}
