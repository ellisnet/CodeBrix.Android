// Derived from .NET MAUI, src/Core/src/CommandMapperExtensions.cs @ 828569a864. Copyright (c) .NET Foundation and Contributors.
// Licensed under the MIT License. See THIRD-PARTY-NOTICES.txt.

using System;
using Microsoft.UI.Xaml;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>
/// The customisation surface of a command mapper: modify, append to or prepend to the mapping of
/// a command (the previous action is kept and can be called).
/// </summary>
internal static class CommandMapperExtensions
{
    /// <summary>Modifies a command mapping in place; the new action receives the previous one.</summary>
    public static void ModifyMapping<TElement, THandler>(this ICommandMapper<TElement, THandler> commandMapper,
        string key, Func<THandler, TElement, object, Func<IAndroidElementHandler, UIElement, object, bool>, bool> method)
        where TElement : UIElement
        where THandler : IAndroidElementHandler
    {
        var previousMethod = commandMapper.GetCommand(key);

        bool NewMethod(THandler handler, TElement element, object args) => method(handler, element, args, previousMethod);

        commandMapper.Add(key, NewMethod);
    }

    /// <summary>Runs <paramref name="method"/> after the existing command mapping (the result is the previous one's).</summary>
    public static void AppendToMapping<TElement, THandler>(this ICommandMapper<TElement, THandler> commandMapper,
        string key, Action<THandler, TElement, object> method)
        where TElement : UIElement
        where THandler : IAndroidElementHandler
        => commandMapper.ModifyMapping(key, (handler, element, args, action) =>
        {
            var result = action?.Invoke(handler, element, args) ?? true;
            method(handler, element, args);
            return result;
        });

    /// <summary>Runs <paramref name="method"/> before the existing command mapping (the result is the previous one's).</summary>
    public static void PrependToMapping<TElement, THandler>(this ICommandMapper<TElement, THandler> commandMapper,
        string key, Action<THandler, TElement, object> method)
        where TElement : UIElement
        where THandler : IAndroidElementHandler
        => commandMapper.ModifyMapping(key, (handler, element, args, action) =>
        {
            method(handler, element, args);
            return action?.Invoke(handler, element, args) ?? true;
        });
}
