// Derived from .NET MAUI, src/Core/src/Handlers/View/ViewHandler.cs, ViewHandlerOfT.cs and ViewHandlerOfT.Android.cs @ 828569a864. Copyright (c) .NET Foundation and Contributors.
// Licensed under the MIT License. See THIRD-PARTY-NOTICES.txt.

using System;
using CodeBrix.Android.UI.Platform;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
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
    private AView _namedView;

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

    /// <summary>
    /// AP9-3: the view TalkBack and UI Automator read for this element - the one that carries the element's
    /// AutomationProperties.Name as its content description (and its AutomationId as its tag). The native view
    /// itself by default; a handler whose root view only hosts the interactive widget (a text field inside its
    /// layout, a Material button inside its host, an overlaid widget) returns that widget.
    /// </summary>
    public virtual AView AccessibilityView => NativeView;

    /// <inheritdoc />
    public void ApplyAutomationName()
    {
        var target = AccessibilityView;
        var description = Element is { } element ? AutomationText.ContentDescriptionOf(AutomationProperties.GetName(element)) : null;
        if (_namedView != null && !ReferenceEquals(_namedView, target))
        {
            // The accessibility view was replaced (e.g. a Button switched between text and element content).
            ClearDescription(_namedView);
            _namedView = null;
        }

        if (target == null)
        {
            return;
        }

        if (description != null)
        {
            target.ContentDescription = description;
            _namedView = target;
        }
        else if (_namedView != null)
        {
            ClearDescription(_namedView);
            _namedView = null;
            OnAutomationNameCleared();
        }
    }

    /// <inheritdoc />
    public void ApplyAutomationId()
    {
        // One statement: a string tag (or none) only, so a tag the view's own code set is never replaced.
        if (AccessibilityView is { Tag: null or global::Java.Lang.String } target)
        {
            target.Tag = AutomationText.TagOf(Element is { } element ? AutomationProperties.GetAutomationId(element) : null) is { } id ? new global::Java.Lang.String(id) : null;
        }
    }

    /// <summary>
    /// Called when the element's AutomationProperties.Name was cleared after it had named the accessibility view
    /// (whose content description is now null): a handler that labels its widget itself puts its own label back.
    /// </summary>
    protected virtual void OnAutomationNameCleared()
    {
    }

    private static void ClearDescription(AView view)
    {
        if (view.Handle != IntPtr.Zero)
        {
            view.ContentDescription = null;
        }
    }

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
        _namedView = null;
        WatchRenderTransform(null);
        ViewTreeSync.DetachFromParent(Element, platformView);
        base.DisconnectHandler(platformView);
    }

    /// <summary>Shows the new view inside its visual parent's view (see <see cref="ViewTreeSync"/>).</summary>
    protected override void OnConnected()
    {
        base.OnConnected();

        // AP9-3: after every mapper ran, so the app's automation name wins over a label a handler gave its widget.
        ApplyAutomationName();
        ApplyAutomationId();
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
