using System.Collections.Generic;

namespace CodeBrix.Android.ParityScore.Scanning;

/// <summary>A property mapper found in the Android assemblies (a static or instance field of a mapper type).</summary>
internal sealed class MapperDefinition
{
    /// <summary>Creates the definition.</summary>
    /// <param name="id">The field id (<c>Namespace.Type::Field</c>).</param>
    /// <param name="elementType">Full name of the mapper's TElement (or null for an untyped mapper).</param>
    /// <param name="handlerType">Full name of the mapper's THandler (or null).</param>
    internal MapperDefinition(string id, string elementType, string handlerType)
    {
        Id = id;
        ElementType = elementType;
        HandlerType = handlerType;
    }

    /// <summary>The field id.</summary>
    internal string Id { get; }

    /// <summary>The element type the mapper is typed for.</summary>
    internal string ElementType { get; }

    /// <summary>The handler type the mapper is typed for.</summary>
    internal string HandlerType { get; }

    /// <summary>Keys the mapper itself maps (DependencyProperty ids <c>Namespace.Type.NameProperty</c>).</summary>
    internal HashSet<string> Keys { get; } = new();

    /// <summary>Mappers this one is chained to (ids).</summary>
    internal List<string> Chained { get; } = new();

    /// <summary>Helper methods (mapper extension helpers of the handler layer) whose keys this mapper receives.</summary>
    internal List<string> Helpers { get; } = new();
}

/// <summary>How a registered element is served.</summary>
internal enum RegistrationKind
{
    /// <summary>A native handler (the factory can create a handler other than the templated fallback).</summary>
    Native,

    /// <summary>Only the templated fallback (Core's template).</summary>
    Fallback,

    /// <summary>No handler (Core's own path).</summary>
    CorePath,
}

/// <summary>One element-handler registration.</summary>
/// <param name="ElementType">Full name of the registered element type.</param>
/// <param name="Kind">How it is served.</param>
/// <param name="HandlerTypes">The native handler types the factory creates (empty unless <see cref="RegistrationKind.Native"/>).</param>
/// <param name="Assembly">The assembly holding the registration.</param>
internal sealed record HandlerRegistration(string ElementType, RegistrationKind Kind, IReadOnlyList<string> HandlerTypes, string Assembly);

/// <summary>Everything the handler scan found.</summary>
internal sealed class HandlerModel
{
    /// <summary>Mappers by id.</summary>
    internal Dictionary<string, MapperDefinition> Mappers { get; } = new();

    /// <summary>Keys added by mapper helper methods, by method id.</summary>
    internal Dictionary<string, HashSet<string>> HelperKeys { get; } = new();

    /// <summary>Registrations by element type (the last registration of an element wins, as in the registry).</summary>
    internal Dictionary<string, HandlerRegistration> Registrations { get; } = new();

    /// <summary>The mapper id each handler type passes to its base constructor.</summary>
    internal Dictionary<string, string> HandlerMappers { get; } = new();

    /// <summary>Every key the mapper maps, through its chain and helpers.</summary>
    /// <param name="mapperId">The mapper id.</param>
    /// <returns>The keys (empty when the mapper is unknown).</returns>
    internal HashSet<string> EffectiveKeys(string mapperId)
    {
        var keys = new HashSet<string>();
        Collect(mapperId, keys, new HashSet<string>());
        return keys;
    }

    private void Collect(string mapperId, HashSet<string> keys, HashSet<string> seen)
    {
        if (mapperId == null || !seen.Add(mapperId) || !Mappers.TryGetValue(mapperId, out var mapper))
        {
            return;
        }

        keys.UnionWith(mapper.Keys);
        foreach (var helper in mapper.Helpers)
        {
            if (HelperKeys.TryGetValue(helper, out var helperKeys))
            {
                keys.UnionWith(helperKeys);
            }
        }

        foreach (var chained in mapper.Chained)
        {
            Collect(chained, keys, seen);
        }
    }
}
