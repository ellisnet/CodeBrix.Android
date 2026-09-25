// Derived from .NET MAUI, src/Core/src/CommandMapper.cs @ 828569a864. Copyright (c) .NET Foundation and Contributors.
// Licensed under the MIT License. See THIRD-PARTY-NOTICES.txt.

using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>
/// Maps one-shot commands (the seam's <c>IElementHandler.Invoke(command, args)</c>, e.g.
/// ChangeView) to handler actions. Keys are the command names of
/// <c>ElementHandlerCommands</c> (ordinal); a lookup falls through to the single chained mapper.
/// </summary>
internal abstract class CommandMapper : ICommandMapper
{
    private readonly Dictionary<string, Func<IAndroidElementHandler, UIElement, object, bool>> _mapper = new(StringComparer.Ordinal);

    /// <summary>Creates an unchained mapper.</summary>
    protected CommandMapper()
    {
    }

    /// <summary>Creates a mapper chained to a base mapper.</summary>
    /// <param name="chained">The base mapper.</param>
    protected CommandMapper(CommandMapper chained)
    {
        Chained = chained;
    }

    /// <summary>The base mapper.</summary>
    public CommandMapper Chained { get; set; }

    /// <inheritdoc />
    public virtual Func<IAndroidElementHandler, UIElement, object, bool> GetCommand(string key)
    {
        if (key != null && _mapper.TryGetValue(key, out var action))
        {
            return action;
        }

        return Chained?.GetCommand(key);
    }

    /// <inheritdoc />
    public bool Invoke(IAndroidElementHandler handler, UIElement element, string command, object args)
    {
        if (element == null)
        {
            return false;
        }

        return InvokeCore(command, handler, element, args);
    }

    /// <summary>Adds or replaces the action of a command.</summary>
    private protected virtual void SetPropertyCore(string key, Func<IAndroidElementHandler, UIElement, object, bool> action)
    {
        ArgumentNullException.ThrowIfNull(key);
        _mapper[key] = action;
    }

    private protected virtual bool InvokeCore(string key, IAndroidElementHandler handler, UIElement element, object args)
    {
        if (!handler.CanInvokeMappers())
        {
            return false;
        }

        var action = GetCommand(key);
        return action != null && action(handler, element, args);
    }
}

/// <summary>A command mapper (see <see cref="CommandMapper"/>).</summary>
internal interface ICommandMapper
{
    /// <summary>The action of a command (own first, then the chained mapper), or null.</summary>
    Func<IAndroidElementHandler, UIElement, object, bool> GetCommand(string key);

    /// <summary>Runs a command; the result is the seam's Invoke result (false when unmapped).</summary>
    bool Invoke(IAndroidElementHandler handler, UIElement element, string command, object args);
}

/// <summary>A typed command mapper.</summary>
/// <typeparam name="TElement">The element type.</typeparam>
/// <typeparam name="THandler">The handler type.</typeparam>
internal interface ICommandMapper<out TElement, out THandler> : ICommandMapper
    where TElement : UIElement
    where THandler : IAndroidElementHandler
{
    /// <summary>Adds a command whose result is always true (handled).</summary>
    void Add(string key, Action<THandler, TElement, object> action);

    /// <summary>Adds a command with an explicit result.</summary>
    void Add(string key, Func<THandler, TElement, object, bool> action);
}

/// <summary>A typed command mapper.</summary>
/// <typeparam name="TElement">The element type.</typeparam>
/// <typeparam name="THandler">The handler type.</typeparam>
internal class CommandMapper<TElement, THandler> : CommandMapper, ICommandMapper<TElement, THandler>
    where TElement : UIElement
    where THandler : IAndroidElementHandler
{
    /// <summary>Creates an unchained mapper.</summary>
    public CommandMapper()
    {
    }

    /// <summary>Creates a mapper chained to a base mapper.</summary>
    /// <param name="chained">The base mapper.</param>
    public CommandMapper(CommandMapper chained)
        : base(chained)
    {
    }

    /// <summary>Gets or sets the action of a command.</summary>
    /// <param name="key">The command name.</param>
    public Func<THandler, TElement, object, bool> this[string key]
    {
        get
        {
            var action = GetCommand(key) ?? throw new IndexOutOfRangeException($"Unable to find mapping for '{nameof(key)}'.");
            return (h, v, o) => action.Invoke(h, v, o);
        }

        set => Add(key, value);
    }

    /// <inheritdoc />
    public void Add(string key, Action<THandler, TElement, object> action) =>
        SetPropertyCore(key, (h, v, o) =>
        {
            action?.Invoke((THandler)h, (TElement)v, o);
            return true;
        });

    /// <inheritdoc />
    public void Add(string key, Func<THandler, TElement, object, bool> action) =>
        SetPropertyCore(key, (h, v, o) => action != null && action((THandler)h, (TElement)v, o));
}
