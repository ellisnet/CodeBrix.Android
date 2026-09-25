// Derived from .NET MAUI, src/Core/src/PropertyMapper.cs @ 828569a864. Copyright (c) .NET Foundation and Contributors.
// Licensed under the MIT License. See THIRD-PARTY-NOTICES.txt.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>
/// Maps dependency-property changes of an element to handler actions (the MAUI property mapper,
/// keyed by <see cref="DependencyProperty"/> instead of a property name). Mappers chain to
/// reproduce the WinUI class hierarchy: a lookup falls through to the chained (base) mappers,
/// and <see cref="UpdateProperties"/> runs the base keys first, in registration order.
/// </summary>
/// <remarks>
/// Mappers always read the EFFECTIVE value from the element (never the change arguments), so
/// local, style, binding, inherited and theme values all map the same way.
/// </remarks>
internal abstract class PropertyMapper : IPropertyMapper
{
    private protected readonly Dictionary<DependencyProperty, Action<IAndroidElementHandler, UIElement>> _mapper = new();
    private IPropertyMapper[] _chained;

    private List<DependencyProperty> _updatePropertiesKeys;
    private List<Action<IAndroidElementHandler, UIElement>> _updatePropertiesMappers;
    private Dictionary<DependencyProperty, Action<IAndroidElementHandler, UIElement>> _cachedMappers;

    /// <summary>Creates an unchained mapper.</summary>
    protected PropertyMapper()
    {
    }

    /// <summary>Creates a mapper chained to base mappers.</summary>
    /// <param name="chained">The base mappers (looked up after this one's own keys).</param>
    protected PropertyMapper(params IPropertyMapper[] chained)
    {
        Chained = chained;
    }

    private List<Action<IAndroidElementHandler, UIElement>> UpdatePropertiesMappers => _updatePropertiesMappers ?? SnapshotMappers().UpdatePropertiesMappers;

    private Dictionary<DependencyProperty, Action<IAndroidElementHandler, UIElement>> CachedMappers => _cachedMappers ?? SnapshotMappers().CachedMappers;

    /// <summary>The base mappers.</summary>
    public IPropertyMapper[] Chained
    {
        get => _chained;
        set
        {
            _chained = value;
            ClearMergedMappers();
        }
    }

    /// <inheritdoc />
    public virtual Action<IAndroidElementHandler, UIElement> GetProperty(DependencyProperty key)
    {
        if (key == null)
        {
            return null;
        }

        if (_mapper.TryGetValue(key, out var action))
        {
            return action;
        }

        var chainedPropertyMappers = Chained;
        if (chainedPropertyMappers is not null)
        {
            foreach (var chained in chainedPropertyMappers)
            {
                var returnValue = chained.GetProperty(key);
                if (returnValue != null)
                {
                    return returnValue;
                }
            }
        }

        return null;
    }

    /// <inheritdoc />
    public void UpdateProperty(IAndroidElementHandler handler, UIElement element, DependencyProperty property)
    {
        if (element == null || property == null || !handler.CanInvokeMappers())
        {
            return;
        }

        TryUpdatePropertyCore(property, handler, element);
    }

    /// <inheritdoc />
    public void UpdateProperties(IAndroidElementHandler handler, UIElement element)
    {
        if (element == null || !handler.CanInvokeMappers())
        {
            return;
        }

        foreach (var mapper in UpdatePropertiesMappers)
        {
            mapper(handler, element);
        }
    }

    /// <inheritdoc />
    public virtual IEnumerable<DependencyProperty> GetKeys()
    {
        // The initial order of the keys is kept (base mappers first, last chained first) so a
        // mapping overridden by a derived mapper still runs at its original position.
        var chainedPropertyMappers = Chained;
        if (chainedPropertyMappers is not null)
        {
            for (var i = chainedPropertyMappers.Length - 1; i >= 0; i--)
            {
                foreach (var key in chainedPropertyMappers[i].GetKeys())
                {
                    yield return key;
                }
            }
        }

        foreach (var mapper in _mapper)
        {
            yield return mapper.Key;
        }
    }

    /// <summary>Adds or replaces the action of a key.</summary>
    /// <param name="key">The dependency property.</param>
    /// <param name="action">The action.</param>
    protected virtual void SetPropertyCore(DependencyProperty key, Action<IAndroidElementHandler, UIElement> action)
    {
        ArgumentNullException.ThrowIfNull(key);
        _mapper[key] = action;
        ClearMergedMappers();
    }

    /// <summary>Runs the action of a key when the handler can run mappers.</summary>
    /// <param name="key">The dependency property.</param>
    /// <param name="handler">The handler.</param>
    /// <param name="element">The element.</param>
    protected virtual void UpdatePropertyCore(DependencyProperty key, IAndroidElementHandler handler, UIElement element)
    {
        if (!handler.CanInvokeMappers())
        {
            return;
        }

        TryUpdatePropertyCore(key, handler, element);
    }

    internal bool TryUpdatePropertyCore(DependencyProperty key, IAndroidElementHandler handler, UIElement element)
    {
        var cachedMappers = CachedMappers;
        if (cachedMappers.TryGetValue(key, out var action))
        {
            if (action is not null)
            {
                action(handler, element);
                return true;
            }

            return false;
        }

        // The cache starts with the UpdateProperties keys only; a key that is not mapped is
        // remembered as unmapped so the lookup runs once.
        var mapper = GetProperty(key);
        cachedMappers[key] = mapper;

        if (mapper is not null)
        {
            mapper(handler, element);
            return true;
        }

        return false;
    }

    private void ClearMergedMappers()
    {
        _updatePropertiesMappers = null;
        _updatePropertiesKeys = null;
        _cachedMappers = null;
    }

    private (List<DependencyProperty> UpdatePropertiesKeys, List<Action<IAndroidElementHandler, UIElement>> UpdatePropertiesMappers, Dictionary<DependencyProperty, Action<IAndroidElementHandler, UIElement>> CachedMappers) SnapshotMappers()
    {
        var updatePropertiesKeys = GetKeys().Distinct().ToList();
        var updatePropertiesMappers = new List<Action<IAndroidElementHandler, UIElement>>(updatePropertiesKeys.Count);
        var cachedMappers = new Dictionary<DependencyProperty, Action<IAndroidElementHandler, UIElement>>(updatePropertiesKeys.Count);

        foreach (var key in updatePropertiesKeys)
        {
            var mapper = GetProperty(key);
            updatePropertiesMappers.Add(mapper);
            cachedMappers[key] = mapper;
        }

        _updatePropertiesKeys = updatePropertiesKeys;
        _updatePropertiesMappers = updatePropertiesMappers;
        _cachedMappers = cachedMappers;

        return (updatePropertiesKeys, updatePropertiesMappers, cachedMappers);
    }
}

/// <summary>A property mapper (see <see cref="PropertyMapper"/>).</summary>
internal interface IPropertyMapper
{
    /// <summary>The action of a key (own keys first, then the chained mappers), or null.</summary>
    Action<IAndroidElementHandler, UIElement> GetProperty(DependencyProperty key);

    /// <summary>Every key, base mappers first, in registration order (may repeat a key).</summary>
    IEnumerable<DependencyProperty> GetKeys();

    /// <summary>Runs every mapped action once, in key order (used on connect).</summary>
    void UpdateProperties(IAndroidElementHandler handler, UIElement element);

    /// <summary>Runs the action of one key, if mapped.</summary>
    void UpdateProperty(IAndroidElementHandler handler, UIElement element, DependencyProperty property);
}

/// <summary>A typed property mapper.</summary>
/// <typeparam name="TElement">The element type.</typeparam>
/// <typeparam name="THandler">The handler type.</typeparam>
internal interface IPropertyMapper<out TElement, out THandler> : IPropertyMapper
    where TElement : UIElement
    where THandler : IAndroidElementHandler
{
    /// <summary>Adds or replaces the action of a key.</summary>
    void Add(DependencyProperty key, Action<THandler, TElement> action);
}

/// <summary>
/// A typed property mapper. An action registered for <typeparamref name="TElement"/> falls
/// back to the chained mappers when the element is of another type (the MAUI rule).
/// </summary>
/// <typeparam name="TElement">The element type.</typeparam>
/// <typeparam name="THandler">The handler type.</typeparam>
internal class PropertyMapper<TElement, THandler> : PropertyMapper, IPropertyMapper<TElement, THandler>
    where TElement : UIElement
    where THandler : IAndroidElementHandler
{
    /// <summary>Creates an unchained mapper.</summary>
    public PropertyMapper()
    {
    }

    /// <summary>Creates a mapper chained to base mappers.</summary>
    /// <param name="chained">The base mappers.</param>
    public PropertyMapper(params IPropertyMapper[] chained)
        : base(chained)
    {
    }

    /// <summary>Gets or sets the action of a key.</summary>
    /// <param name="key">The dependency property.</param>
    public Action<THandler, TElement> this[DependencyProperty key]
    {
        get
        {
            var action = GetProperty(key) ?? throw new IndexOutOfRangeException($"Unable to find mapping for '{nameof(key)}'.");
            return (h, v) => action.Invoke(h, v);
        }

        set => Add(key, value);
    }

    /// <inheritdoc />
    public void Add(DependencyProperty key, Action<THandler, TElement> action) =>
        SetPropertyCore(key, (h, v) =>
        {
            if (v is TElement typed && h is THandler typedHandler)
            {
                action?.Invoke(typedHandler, typed);
            }
            else if (Chained != null)
            {
                foreach (var chain in Chained)
                {
                    if (chain is PropertyMapper propertyMapper)
                    {
                        if (propertyMapper.TryUpdatePropertyCore(key, h, v))
                        {
                            break;
                        }
                    }
                    else if (chain.GetProperty(key) != null)
                    {
                        chain.UpdateProperty(h, v, key);
                        break;
                    }
                }
            }
        });
}
