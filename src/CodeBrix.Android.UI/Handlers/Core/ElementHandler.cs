// Derived from .NET MAUI, src/Core/src/Handlers/Element/ElementHandler.cs @ 828569a864. Copyright (c) .NET Foundation and Contributors.
// Licensed under the MIT License. See THIRD-PARTY-NOTICES.txt.

using System;
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
/// before parent) unhooks the platform view. Core creates a new handler on the next Enter.
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

    /// <inheritdoc />
    public void Connect(UIElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
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
            OnConnectHandler(PlatformView);
        }

        _mapper.UpdateProperties(this, element);
        State = ElementHandlerState.Connected;
        OnConnected();
    }

    /// <inheritdoc />
    public void Disconnect()
    {
        var platformView = PlatformView;
        if (platformView != null && Element != null)
        {
            OnDisconnectHandler(platformView);
        }

        PlatformView = null;
        Element = null;
        HasArranged = false;
        State = ElementHandlerState.Disconnected;
    }

    /// <inheritdoc />
    public void UpdateValue(DependencyProperty property)
    {
        if (Element == null || property == null)
        {
            return;
        }

        _mapper.UpdateProperty(this, Element, property);
    }

    /// <inheritdoc />
    public virtual bool Invoke(string command, object args)
    {
        if (Element == null || _commandMapper == null)
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
