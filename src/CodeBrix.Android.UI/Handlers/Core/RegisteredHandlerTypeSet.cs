// Derived from .NET MAUI, src/Core/src/Hosting/Internal/RegisteredHandlerServiceTypeSet.cs @ 828569a864. Copyright (c) .NET Foundation and Contributors.
// Licensed under the MIT License. See THIRD-PARTY-NOTICES.txt.

using System;
using System.Collections.Generic;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>
/// The set of element types a handler is registered for, resolving an element type to the
/// MOST-DERIVED registered type it is assignable to (an app's <c>EmbeddedImageButton : Button</c>
/// resolves to <c>Button</c>): exact match first, then concrete types, then interfaces. Two
/// unrelated interface matches are ambiguous and throw.
/// </summary>
internal sealed class RegisteredHandlerTypeSet
{
    private readonly HashSet<Type> _concreteHandlerServiceTypeSet = new();
    private readonly HashSet<Type> _interfaceHandlerServiceTypeSet = new();

    /// <summary>Registers an element type (a class or an interface).</summary>
    /// <param name="elementType">The element type.</param>
    public void Add(Type elementType)
    {
        ArgumentNullException.ThrowIfNull(elementType);
        if (elementType.IsInterface)
        {
            _interfaceHandlerServiceTypeSet.Add(elementType);
        }
        else
        {
            _concreteHandlerServiceTypeSet.Add(elementType);
        }
    }

    /// <summary>Resolves an element type to the registered type whose handler serves it, or null.</summary>
    /// <param name="type">The element's runtime type.</param>
    /// <returns>The registered type, or null when none applies.</returns>
    public Type Resolve(Type type)
    {
        if (_concreteHandlerServiceTypeSet.Contains(type)
            || _interfaceHandlerServiceTypeSet.Contains(type))
        {
            return type;
        }

        return ResolveFromTypeSet(type, _concreteHandlerServiceTypeSet)
            ?? ResolveFromTypeSet(type, _interfaceHandlerServiceTypeSet);
    }

    private static Type ResolveFromTypeSet(Type type, HashSet<Type> set)
    {
        Type best = null;

        foreach (var registered in set)
        {
            if (registered.IsAssignableFrom(type))
            {
                if (best is null || best.IsAssignableFrom(registered))
                {
                    best = registered;
                }
                else if (!registered.IsAssignableFrom(best))
                {
                    // Two registered types that are not derived from each other both apply
                    // (two interfaces of the element): the match is ambiguous.
                    throw new InvalidOperationException($"Unable to find a single element handler corresponding to {type}. There is an ambiguous match between {best} and {registered}.");
                }
            }
        }

        return best;
    }
}
