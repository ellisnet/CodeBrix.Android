// Derived from .NET MAUI, src/Core/src/Handlers/Element/ElementHandlerOfT.cs @ 828569a864. Copyright (c) .NET Foundation and Contributors.
// Licensed under the MIT License. See THIRD-PARTY-NOTICES.txt.

using System;
using Microsoft.UI.Xaml;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>
/// A typed element handler: <typeparamref name="TElement"/> is the Core element type it serves
/// and <typeparamref name="TPlatformView"/> the platform view it creates.
/// </summary>
/// <typeparam name="TElement">The Core element type.</typeparam>
/// <typeparam name="TPlatformView">The platform view type.</typeparam>
internal abstract class ElementHandler<TElement, TPlatformView> : ElementHandler
    where TElement : UIElement
    where TPlatformView : class
{
    /// <summary>Creates a handler that maps with <paramref name="mapper"/>.</summary>
    /// <param name="mapper">The property mapper.</param>
    /// <param name="commandMapper">The command mapper, or null.</param>
    protected ElementHandler(IPropertyMapper mapper, CommandMapper commandMapper = null)
        : base(mapper, commandMapper)
    {
    }

    /// <summary>The typed platform view (throws while disconnected).</summary>
    public new TPlatformView PlatformView =>
        (TPlatformView)base.PlatformView ?? throw new InvalidOperationException("PlatformView cannot be null here");

    /// <summary>The typed element (throws while disconnected).</summary>
    public TElement VirtualElement =>
        (TElement)Element ?? throw new InvalidOperationException("Element cannot be null here");

    /// <summary>Creates the platform view (once per handler).</summary>
    /// <returns>The platform view.</returns>
    protected abstract TPlatformView CreatePlatformElement();

    /// <summary>Hooks the platform view up (listeners); called once, right after it is created.</summary>
    /// <param name="platformView">The platform view.</param>
    protected virtual void ConnectHandler(TPlatformView platformView)
    {
    }

    /// <summary>Unhooks the platform view (listeners) when the element leaves the tree.</summary>
    /// <param name="platformView">The platform view.</param>
    protected virtual void DisconnectHandler(TPlatformView platformView)
    {
    }

    private protected override object OnCreatePlatformElement() => CreatePlatformElement();

    private protected override void OnConnectHandler(object platformView) => ConnectHandler((TPlatformView)platformView);

    private protected override void OnDisconnectHandler(object platformView) => DisconnectHandler((TPlatformView)platformView);
}
