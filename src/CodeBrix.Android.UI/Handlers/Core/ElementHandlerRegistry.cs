using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>
/// Which handler serves which Core element type: a registration per element type, resolved to
/// the most-derived registered type (<see cref="RegisteredHandlerTypeSet"/>), plus a fallback
/// for elements whose type (and every base type) has none. Registrations can be replaced at any
/// time before an element of that type enters a live tree (apps and add-ins customise here).
/// </summary>
internal sealed class ElementHandlerRegistry
{
    private readonly object _gate = new();
    private readonly Dictionary<Type, Func<UIElement, IAndroidElementHandler>> _factories = new();
    private readonly RegisteredHandlerTypeSet _types = new();
    private readonly Dictionary<Type, Type> _resolved = new();

    /// <summary>
    /// Creates the handler of an element no registration applies to (the templated fallback),
    /// or null to leave such elements to Core.
    /// </summary>
    public Func<UIElement, IAndroidElementHandler> Fallback { get; set; }

    /// <summary>Registers (or replaces) the handler of an element type and its derived types.</summary>
    /// <typeparam name="TElement">The element type (or an interface elements implement).</typeparam>
    /// <param name="factory">Creates a handler for an element (null result = Core's own path).</param>
    public void Register<TElement>(Func<UIElement, IAndroidElementHandler> factory)
        where TElement : class
        => Register(typeof(TElement), factory);

    /// <summary>Registers (or replaces) the handler of an element type and its derived types.</summary>
    /// <param name="elementType">The element type.</param>
    /// <param name="factory">Creates a handler for an element (null result = Core's own path).</param>
    public void Register(Type elementType, Func<UIElement, IAndroidElementHandler> factory)
    {
        ArgumentNullException.ThrowIfNull(elementType);
        ArgumentNullException.ThrowIfNull(factory);
        lock (_gate)
        {
            _factories[elementType] = factory;
            _types.Add(elementType);
            _resolved.Clear();
        }
    }

    /// <summary>
    /// The factory that serves <paramref name="elementType"/> right now - its most-derived registration, else the
    /// fallback - so that an add-in can register a more derived type that wraps it (adds behaviour, keeps the view).
    /// </summary>
    /// <param name="elementType">The element type.</param>
    /// <returns>The factory; it answers null (Core's own path) when neither a registration nor a fallback applies.</returns>
    public Func<UIElement, IAndroidElementHandler> FactoryFor(Type elementType)
    {
        ArgumentNullException.ThrowIfNull(elementType);
        var registered = ResolveRegisteredType(elementType);
        lock (_gate)
        {
            if (registered != null && _factories.TryGetValue(registered, out var factory))
            {
                return factory;
            }
        }

        return element => Fallback?.Invoke(element);
    }

    /// <summary>The registered type whose handler serves <paramref name="elementType"/>, or null.</summary>
    /// <param name="elementType">The element's runtime type.</param>
    /// <returns>The registered type, or null when only the fallback applies.</returns>
    public Type ResolveRegisteredType(Type elementType)
    {
        lock (_gate)
        {
            if (!_resolved.TryGetValue(elementType, out var registered))
            {
                registered = _types.Resolve(elementType);
                _resolved[elementType] = registered;
            }

            return registered;
        }
    }

    /// <summary>Creates the handler of an element: its registration, else the fallback, else null.</summary>
    /// <param name="element">The element entering a live tree.</param>
    /// <returns>The handler, or null (Core's own path).</returns>
    public IAndroidElementHandler CreateHandler(UIElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        Func<UIElement, IAndroidElementHandler> factory = null;
        var registered = ResolveRegisteredType(element.GetType());
        lock (_gate)
        {
            if (registered != null)
            {
                _factories.TryGetValue(registered, out factory);
            }
        }

        return factory != null ? factory(element) : Fallback?.Invoke(element);
    }
}
