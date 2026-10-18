// Derived from .NET MAUI, src/Core/src/Handlers/Element/ElementHandler.cs @ 828569a864. Copyright (c) .NET Foundation and Contributors.
// Licensed under the MIT License. See THIRD-PARTY-NOTICES.txt.

using System;
using System.Threading;
using CodeBrix.Platform.UI.Contracts;
using Microsoft.UI.Xaml;
using Windows.Foundation;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>
/// The base of every CodeBrix.Android element handler (the MAUI ElementHandler lifecycle on the
/// Core seam): Core calls <see cref="Connect"/> when the element enters a live tree (parent
/// before children), the handler creates its platform view once, connects it, and runs EVERY
/// mapper in stable key order; afterwards each effective property change arrives as
/// <see cref="UpdateValue"/> and runs that key's mapper; <see cref="Disconnect"/> (children
/// before parent) unhooks the platform view. Core creates a new handler on the next Enter - unless the
/// handler declares <see cref="ElementHandlerCapabilities.RetainedAcrossLeave"/>: then Disconnect is a RETAINED
/// disconnect (the view leaves its parent and stops listening, but the handler keeps its element and its platform
/// view), Core connects the same handler on the next Enter and <see cref="Connect"/> shows the kept view again
/// (a recycled list row keeps its native views); <see cref="ReleaseRetained"/> lets the kept view go for good.
/// A handler that opts in must re-hook in ConnectHandler everything its DisconnectHandler unhooks.
/// </summary>
internal abstract class ElementHandler : IAndroidElementHandler
{
    /// <summary>The root of every mapper chain (no keys of its own).</summary>
    public static readonly IPropertyMapper<UIElement, IAndroidElementHandler> ElementMapper = new PropertyMapper<UIElement, IAndroidElementHandler>();

    /// <summary>The root of every command mapper chain (no commands of its own).</summary>
    public static readonly CommandMapper<UIElement, IAndroidElementHandler> ElementCommandMapper = new();

    private readonly IPropertyMapper _mapper;
    private readonly CommandMapper _commandMapper;

    /// <summary>Creates a handler that maps with <paramref name="mapper"/>.</summary>
    /// <param name="mapper">The property mapper (usually the handler type's static mapper).</param>
    /// <param name="commandMapper">The command mapper, or null for none.</param>
    protected ElementHandler(IPropertyMapper mapper, CommandMapper commandMapper = null)
    {
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        _commandMapper = commandMapper;
    }

    /// <inheritdoc />
    public UIElement Element { get; private set; }

    /// <inheritdoc />
    public object PlatformView { get; private set; }

    /// <inheritdoc />
    public ElementHandlerState State { get; private set; }

    /// <inheritdoc />
    public virtual ElementHandlerCapabilities Capabilities => ElementHandlerCapabilities.None;

    /// <inheritdoc />
    public Rect ArrangedRect { get; private set; }

    /// <inheritdoc />
    public bool HasArranged { get; private set; }

    /// <summary>The property mapper this handler maps with.</summary>
    internal IPropertyMapper HandlerMapper => _mapper;

    /// <summary>
    /// True while the handler is disconnected by a retained disconnect (<see cref="ElementHandlerCapabilities.RetainedAcrossLeave"/>):
    /// it keeps its element and platform view for the next <see cref="Connect"/> of the same element.
    /// </summary>
    internal bool IsRetainedDetached { get; private set; }

    /// <summary>How many platform views handlers created (diagnostics: what retained handlers save).</summary>
    internal static long PlatformViewsCreated => Interlocked.Read(ref _platformViewsCreated);

    /// <summary>How many connects showed a retained platform view again instead of creating one (diagnostics).</summary>
    internal static long RetainedReconnects => Interlocked.Read(ref _retainedReconnects);

    /// <summary>How many retained platform views were let go for good (<see cref="ReleaseRetained"/>; diagnostics).</summary>
    internal static long RetainedReleases => Interlocked.Read(ref _retainedReleases);

    private static long _platformViewsCreated;
    private static long _retainedReconnects;
    private static long _retainedReleases;

    /// <inheritdoc />
    public void Connect(UIElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        if (IsRetainedDetached)
        {
            if (ReferenceEquals(Element, element))
            {
                // A retained handler entering again (Core hook H16): the kept platform view listens again, every
                // mapper runs (nothing was mapped while it was detached) and the view goes back into its parent.
                IsRetainedDetached = false;
                State = ElementHandlerState.Reconnecting;
                HasArranged = false;
                OnConnectHandler(PlatformView);
                Interlocked.Increment(ref _retainedReconnects);
                _mapper.UpdateProperties(this, element);
                State = ElementHandlerState.Connected;
                OnConnected();
                return;
            }

            // Another element: the kept view is not reused for it.
            ReleaseRetained();
        }

        if (ReferenceEquals(Element, element))
        {
            return;
        }

        var reconnect = PlatformView != null;
        State = reconnect ? ElementHandlerState.Reconnecting : ElementHandlerState.Connecting;
        Element = element;
        HasArranged = false;
        if (!reconnect)
        {
            PlatformView = OnCreatePlatformElement();
            Interlocked.Increment(ref _platformViewsCreated);
            OnConnectHandler(PlatformView);
        }

        _mapper.UpdateProperties(this, element);
        State = ElementHandlerState.Connected;
        OnConnected();
    }

    /// <inheritdoc />
    public void Disconnect()
    {
        if (IsRetainedDetached)
        {
            // Already unhooked by the retained disconnect: a second Disconnect lets the kept view go.
            ReleaseRetained();
            return;
        }

        var platformView = PlatformView;
        if (platformView != null && Element != null)
        {
            OnDisconnectHandler(platformView);
            if ((Capabilities & ElementHandlerCapabilities.RetainedAcrossLeave) != 0)
            {
                // Retained disconnect (Core keeps this handler for the element's next Enter): the view left its parent
                // and stopped listening; the handler keeps the element and the view.
                IsRetainedDetached = true;
                HasArranged = false;
                State = ElementHandlerState.Disconnected;
                return;
            }
        }

        PlatformView = null;
        Element = null;
        HasArranged = false;
        State = ElementHandlerState.Disconnected;
    }

    /// <summary>
    /// Lets the platform view a retained disconnect kept go for good (its list row was dropped, or the element is
    /// connected to another handler): the handler forgets its element and view; the next <see cref="Connect"/> creates
    /// a new view. The view was already unhooked and taken out of its parent by the retained disconnect.
    /// </summary>
    /// <returns>True when a kept view was let go.</returns>
    internal bool ReleaseRetained()
    {
        if (!IsRetainedDetached)
        {
            return false;
        }

        IsRetainedDetached = false;
        PlatformView = null;
        Element = null;
        HasArranged = false;
        State = ElementHandlerState.Disconnected;
        Interlocked.Increment(ref _retainedReleases);
        return true;
    }

    /// <inheritdoc />
    public void UpdateValue(DependencyProperty property)
    {
        // A retained, detached handler maps nothing (no listener or brush watcher is taken while its element is out of
        // the tree): its next Connect runs every mapper.
        if (Element == null || property == null || IsRetainedDetached)
        {
            return;
        }

        _mapper.UpdateProperty(this, Element, property);
    }

    /// <inheritdoc />
    public virtual bool Invoke(string command, object args)
    {
        if (Element == null || _commandMapper == null || IsRetainedDetached)
        {
            return false;
        }

        return _commandMapper.Invoke(this, Element, command, args);
    }

    /// <inheritdoc />
    public virtual void OnChildAdded(UIElement child, int index)
    {
    }

    /// <inheritdoc />
    public virtual void OnChildRemoved(UIElement child)
    {
    }

    /// <inheritdoc />
    public virtual void OnChildMoved(int oldIndex, int newIndex)
    {
    }

    /// <summary>
    /// Measures the platform view (called by Core only for handlers with
    /// <see cref="ElementHandlerCapabilities.MeasuresNatively"/>, in place of MeasureOverride).
    /// </summary>
    /// <param name="availableSize">The available size in DIPs (margins removed, Min/Max applied).</param>
    /// <returns>The desired size in DIPs.</returns>
    public virtual Size Measure(Size availableSize) => new(0, 0);

    /// <inheritdoc />
    public void Arrange(Rect finalRect)
    {
        var changed = !HasArranged || ArrangedRect != finalRect;
        ArrangedRect = finalRect;
        HasArranged = true;
        OnArranged(finalRect, changed);
    }

    /// <summary>Hit test (called by Core only for handlers with <see cref="ElementHandlerCapabilities.OwnsVisuals"/>).</summary>
    /// <param name="relativeLocation">The point relative to the element, in DIPs.</param>
    /// <returns>True when the element is hit.</returns>
    public virtual bool HitTest(Point relativeLocation) =>
        relativeLocation.X >= 0 && relativeLocation.Y >= 0
        && relativeLocation.X < ArrangedRect.Width && relativeLocation.Y < ArrangedRect.Height;

    /// <inheritdoc />
    public virtual void OnTemplateSuppressed(FrameworkTemplate template)
    {
    }

    /// <inheritdoc />
    public virtual bool CanInvokeMappers() => true;

    /// <summary>Called after every Core arrange of the element.</summary>
    /// <param name="finalRect">The rectangle relative to the visual parent, in DIPs.</param>
    /// <param name="changed">True when it differs from the previous arrange.</param>
    protected virtual void OnArranged(Rect finalRect, bool changed)
    {
    }

    /// <summary>Called at the end of <see cref="Connect"/>, after every mapper ran.</summary>
    protected virtual void OnConnected()
    {
    }

    private protected abstract object OnCreatePlatformElement();

    private protected abstract void OnConnectHandler(object platformView);

    private protected abstract void OnDisconnectHandler(object platformView);
}
