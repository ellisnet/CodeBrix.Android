// Derived from .NET MAUI, src/Core/src/Handlers/ViewHandlerExtensions.Android.cs and src/Core/src/Platform/Android/ContextExtensions.cs (CreateMeasureSpec) @ 828569a864. Copyright (c) .NET Foundation and Contributors.
// Licensed under the MIT License. See THIRD-PARTY-NOTICES.txt.

using System;
using CodeBrix.Android.UI.Platform;
using CodeBrix.Android.UI.Portable.Layout;
using Windows.Foundation;
using AMeasureSpecMode = global::Android.Views.MeasureSpecMode;
using AView = global::Android.Views.View;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>
/// The leaf measure (plan 2.6): a natively measured element (MeasuresNatively) is measured
/// INSIDE Core's measure - one Android measure() with a spec built from Core's available size,
/// converted back to DIPs with the element's XamlRoot density.
/// </summary>
internal static class ViewHandlerExtensions
{
    /// <summary>
    /// Measures <paramref name="view"/> for Core. <paramref name="availableSize"/> is what
    /// MeasureOverride would get (margins removed, Width/Min/Max applied by Core), so an
    /// infinite dimension is Unspecified and a finite one AtMost.
    /// </summary>
    /// <param name="view">The native view.</param>
    /// <param name="availableSize">The available size in DIPs.</param>
    /// <param name="density">Pixels per DIP.</param>
    /// <returns>The desired size in DIPs.</returns>
    internal static Size GetDesiredSizeFromView(AView view, Size availableSize, double density)
    {
        if (view == null)
        {
            return new Size(0, 0);
        }

        var widthSpec = CreateMeasureSpec(availableSize.Width, density);
        var heightSpec = CreateMeasureSpec(availableSize.Height, density);
        view.Measure(widthSpec, heightSpec);
        return new Size(
            LayoutReplayMath.FromPixels(view.MeasuredWidth, density),
            LayoutReplayMath.FromPixels(view.MeasuredHeight, density));
    }

    /// <summary>The spec of one dimension: Unspecified for infinity, else AtMost.</summary>
    internal static int CreateMeasureSpec(double constraint, double density)
    {
        if (double.IsNaN(constraint) || double.IsInfinity(constraint))
        {
            return AMeasureSpecMode.Unspecified.MakeMeasureSpec(0);
        }

        var pixels = (int)Math.Floor(Math.Round(Math.Max(0, constraint) * density, 3));
        return AMeasureSpecMode.AtMost.MakeMeasureSpec(pixels);
    }
}
