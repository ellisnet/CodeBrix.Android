// Derived from .NET MAUI, src/Core/src/Handlers/View/ViewHandler.cs, ViewHandlerOfT.cs and ViewHandlerOfT.Android.cs @ 828569a864. Copyright (c) .NET Foundation and Contributors.
// Licensed under the MIT License. See THIRD-PARTY-NOTICES.txt.

using System;
using CodeBrix.Android.UI.Platform;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using AContext = global::Android.Content.Context;
using AView = global::Android.Views.View;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>
/// The base of every handler whose platform view is an Android <see cref="AView"/>: the MAUI
/// ViewHandler on the Core seam. Its mapper chains to <see cref="ViewMappers.ViewMapper"/>
/// (what every UIElement / FrameworkElement shows natively). Width/Height/Min/Max/Margin/
/// alignment are NEVER mapped - Core layout owns them and the parent view replays the result.
/// </summary>
/// <typeparam name="TElement">The Core element type.</typeparam>
/// <typeparam name="TView">The Android view type.</typeparam>
internal abstract class ViewHandler<TElement, TView> : ElementHandler<TElement, TView>, IViewHandler
    where TElement : UIElement
    where TView : AView
{
    private Transform _watchedTransform;
    private Size _lastArrangedSize;

    /// <summary>Creates a view handler that maps with <paramref name="mapper"/>.</summary>
    /// <param name="mapper">The property mapper (chain it to <see cref="ViewMappers.ViewMapper"/>).</param>
    /// <param name="commandMapper">The command mapper, or null.</param>
    protected ViewHandler(IPropertyMapper mapper, CommandMapper commandMapper = null)
        : base(mapper, commandMapper)
    {
    }

    /// <inheritdoc />
    public AView NativeView => base.PlatformView as AView;

    /// <inheritdoc />
    public AContext Context { get; private set; }

    /// <inheritdoc />
    public double Density => HandlerContext.Density(Element);

    /// <summary>
    /// A MAUI-style hook to replace the platform view an instance creates (e.g. a subclass of
    /// the default widget); null uses <see cref="CreatePlatformView"/>.
    /// </summary>
    public static Func<ViewHandler<TElement, TView>, TView> PlatformViewFactory { get; set; }

    /// <inheritdoc />
    public override bool CanInvokeMappers() => NativeView is not { Handle: var handle } || handle != IntPtr.Zero;

    /// <inheritdoc />
    public void InvalidateNativeLayout()
    {
        var view = NativeView;
        if (view == null)
        {
            return;
        }

        view.RequestLayout();
        (view.Parent as AView)?.RequestLayout();
    }

    /// <summary>Creates the platform view (through <see cref="PlatformViewFactory"/> when set).</summary>
    protected sealed override TView CreatePlatformElement()
    {
        Context = HandlerContext.For(Element);
        return PlatformViewFactory?.Invoke(this) ?? CreatePlatformView();
    }

    /// <summary>Creates the Android view of the element.</summary>
    /// <returns>The view.</returns>
    protected abstract TView CreatePlatformView();

    /// <inheritdoc />
    protected override void DisconnectHandler(TView platformView)
    {
        WatchRenderTransform(null);
        ViewTreeSync.DetachFromParent(Element, platformView);
        base.DisconnectHandler(platformView);
    }

    /// <summary>Shows the new view inside its visual parent's view (see <see cref="ViewTreeSync"/>).</summary>
    protected override void OnConnected()
    {
        base.OnConnected();
        ViewTreeSync.AttachToParent(Element, NativeView);
    }

    /// <inheritdoc />
    protected override void OnArranged(Rect finalRect, bool changed)
    {
        if (!changed)
        {
            return;
        }

        InvalidateNativeLayout();
        var size = new Size(finalRect.Width, finalRect.Height);
        if (size != _lastArrangedSize)
        {
            _lastArrangedSize = size;
            OnArrangedSizeChanged(size);
        }
    }

    /// <summary>
    /// Called when Core arranged the element at a new size: size-dependent native state
    /// (the render-transform origin, drawables) is refreshed.
    /// </summary>
    /// <param name="size">The new size in DIPs.</param>
    protected virtual void OnArrangedSizeChanged(Size size)
    {
        if (Element is { } element)
        {
            ViewMappers.ApplyRenderTransform(this, element);
        }
    }

    /// <inheritdoc />
    public void WatchRenderTransform(Transform transform)
    {
        if (ReferenceEquals(_watchedTransform, transform))
        {
            return;
        }

        if (_watchedTransform != null)
        {
            _watchedTransform.Changed -= OnTransformChanged;
        }

        _watchedTransform = transform;
        if (transform != null)
        {
            transform.Changed += OnTransformChanged;
        }
    }

    private void OnTransformChanged(object sender, EventArgs e)
    {
        if (Element is { } element)
        {
            ViewMappers.ApplyRenderTransform(this, element);
        }
    }
}
