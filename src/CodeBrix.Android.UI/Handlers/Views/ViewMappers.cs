// Derived from .NET MAUI, src/Core/src/Handlers/View/ViewHandler.cs (the ViewMapper) @ 828569a864. Copyright (c) .NET Foundation and Contributors.
// Licensed under the MIT License. See THIRD-PARTY-NOTICES.txt.

using System;
using CodeBrix.Android.UI.Platform;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using ALayoutDirection = global::Android.Views.LayoutDirection;
using AMatrix = global::Android.Graphics.Matrix;
using AViewStates = global::Android.Views.ViewStates;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>
/// The UIElement / FrameworkElement mapper every view handler chains to (plan 2.5):
/// Visibility, Opacity, RenderTransform (+origin), Clip, Canvas.ZIndex and FlowDirection.
/// </summary>
internal static class ViewMappers
{
    /// <summary>The UIElement + FrameworkElement mapper.</summary>
    public static readonly PropertyMapper<UIElement, IViewHandler> ViewMapper = new(ElementHandler.ElementMapper)
    {
        [UIElement.VisibilityProperty] = MapVisibility,
        [UIElement.OpacityProperty] = MapOpacity,
        [UIElement.RenderTransformProperty] = MapRenderTransform,
        [UIElement.RenderTransformOriginProperty] = MapRenderTransform,
        [UIElement.ClipProperty] = MapClip,
        [Canvas.ZIndexProperty] = MapZIndex,
        [FrameworkElement.FlowDirectionProperty] = MapFlowDirection,
        [ToolTipService.ToolTipProperty] = ToolTipMapping.Map,
    };

    /// <summary>Maps UIElement.Visibility (Collapsed = Gone: Core arranges nothing for it).</summary>
    public static void MapVisibility(IViewHandler handler, UIElement element)
    {
        if (handler.NativeView is { } view)
        {
            var state = element.Visibility == Visibility.Visible ? AViewStates.Visible : AViewStates.Gone;
            if (view.Visibility != state)
            {
                view.Visibility = state;
                handler.InvalidateNativeLayout();
            }
        }
    }

    /// <summary>Maps UIElement.Opacity to the view's alpha.</summary>
    public static void MapOpacity(IViewHandler handler, UIElement element)
    {
        if (handler.NativeView is { } view)
        {
            view.Alpha = (float)Math.Clamp(double.IsNaN(element.Opacity) ? 1 : element.Opacity, 0, 1);
        }
    }

    /// <summary>
    /// Maps RenderTransform + RenderTransformOrigin to the view's animation matrix (any affine
    /// transform, in pixels, about the element's top-left - as WinUI applies it).
    /// </summary>
    public static void MapRenderTransform(IViewHandler handler, UIElement element)
    {
        handler.WatchRenderTransform(element.RenderTransform);

        ApplyRenderTransform(handler, element);
    }

    /// <summary>
    /// Maps UIElement.Clip (a RectangleGeometry, in element coordinates) to the view's clip bounds, intersected
    /// with Core's layout clip (<see cref="ClipReplay"/>; the parent view group replays that one after each layout).
    /// </summary>
    public static void MapClip(IViewHandler handler, UIElement element)
    {
        if (handler.NativeView is { } view)
        {
            ClipReplay.Apply(view, element, handler.Density);
        }
    }

    /// <summary>Maps Canvas.ZIndex: the parent view re-orders its drawing.</summary>
    public static void MapZIndex(IViewHandler handler, UIElement element)
    {
        if (handler.NativeView?.Parent is CodeBrixViewGroup parent)
        {
            parent.InvalidateDrawingOrder();
        }
    }

    /// <summary>Maps FrameworkElement.FlowDirection to the view's layout direction.</summary>
    public static void MapFlowDirection(IViewHandler handler, UIElement element)
    {
        if (handler.NativeView is { } view && element is FrameworkElement fe)
        {
            view.LayoutDirection = fe.FlowDirection == FlowDirection.RightToLeft ? ALayoutDirection.Rtl : ALayoutDirection.Ltr;
        }
    }

    /// <summary>Applies the element's render transform to the view (also after a size change).</summary>
    internal static void ApplyRenderTransform(IViewHandler handler, UIElement element)
    {
        if (handler.NativeView is not { } view)
        {
            return;
        }

        var transform = element.RenderTransform;
        if (transform == null)
        {
            view.AnimationMatrix = null;
            return;
        }

        var size = element.RenderSize;
        var matrix = transform.ToMatrix(element.RenderTransformOrigin, size);
        if (matrix.IsIdentity)
        {
            view.AnimationMatrix = null;
            return;
        }

        var density = (float)handler.Density;
        var android = new AMatrix();
        android.SetValues(new[]
        {
            matrix.M11, matrix.M21, matrix.M31 * density,
            matrix.M12, matrix.M22, matrix.M32 * density,
            0f, 0f, 1f,
        });
        view.AnimationMatrix = android;
    }

}
