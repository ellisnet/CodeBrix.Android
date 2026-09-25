// Derived from .NET MAUI, src/Core/src/PropertyMapperExtensions.cs @ 828569a864. Copyright (c) .NET Foundation and Contributors.
// Licensed under the MIT License. See THIRD-PARTY-NOTICES.txt.

using System;
using Microsoft.UI.Xaml;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>
/// The customisation surface of a property mapper: modify, replace, append to or prepend to
/// the mapping of a key (the previous action is kept and can be called).
/// </summary>
internal static class PropertyMapperExtensions
{
    /// <summary>Modifies a mapping in place; the new action receives the previous one.</summary>
    public static void ModifyMapping<TElement, THandler>(this IPropertyMapper<TElement, THandler> propertyMapper,
        DependencyProperty key, Action<THandler, TElement, Action<IAndroidElementHandler, UIElement>> method)
        where TElement : UIElement
        where THandler : IAndroidElementHandler
    {
        var previousMethod = propertyMapper.GetProperty(key);

        void NewMethod(THandler handler, TElement element)
        {
            method(handler, element, previousMethod);
        }

        propertyMapper.Add(key, NewMethod);
    }

    /// <summary>Replaces a mapping.</summary>
    public static void ReplaceMapping<TElement, THandler>(this IPropertyMapper<TElement, THandler> propertyMapper,
        DependencyProperty key, Action<THandler, TElement> method)
        where TElement : UIElement
        where THandler : IAndroidElementHandler
        => propertyMapper.ModifyMapping(key, (h, v, _) => method.Invoke(h, v));

    /// <summary>Runs <paramref name="method"/> after the existing mapping.</summary>
    public static void AppendToMapping<TElement, THandler>(this IPropertyMapper<TElement, THandler> propertyMapper,
        DependencyProperty key, Action<THandler, TElement> method)
        where TElement : UIElement
        where THandler : IAndroidElementHandler
        => propertyMapper.ModifyMapping(key, (handler, element, action) =>
        {
            action?.Invoke(handler, element);
            method(handler, element);
        });

    /// <summary>Runs <paramref name="method"/> before the existing mapping.</summary>
    public static void PrependToMapping<TElement, THandler>(this IPropertyMapper<TElement, THandler> propertyMapper,
        DependencyProperty key, Action<THandler, TElement> method)
        where TElement : UIElement
        where THandler : IAndroidElementHandler
        => propertyMapper.ModifyMapping(key, (handler, element, action) =>
        {
            method(handler, element);
            action?.Invoke(handler, element);
        });
}
