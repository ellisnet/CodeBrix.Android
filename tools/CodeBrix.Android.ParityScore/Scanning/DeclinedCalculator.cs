using System;
using System.Collections.Generic;
using System.Linq;
using CodeBrix.AssemblyTools;

namespace CodeBrix.Android.ParityScore.Scanning;

/// <summary>The status of one public DependencyProperty against a handler.</summary>
internal enum PropertyStatus
{
    /// <summary>The handler's mapper (through its chain) maps it.</summary>
    Mapped,

    /// <summary>Mapped by some native handlers only (the base row).</summary>
    Partial,

    /// <summary>Not mapped, and the explained list says why.</summary>
    Explained,

    /// <summary>Not mapped and not explained: the Android "declined" list.</summary>
    Declined,
}

/// <summary>One property row of the declined report.</summary>
/// <param name="Element">The registered element type (or <c>(base)</c> for the UIElement/FrameworkElement row).</param>
/// <param name="Handler">The handler type (or the number of native handlers, for the base row).</param>
/// <param name="Property">The property id (<c>Namespace.Type.NameProperty</c>).</param>
/// <param name="Status">The status.</param>
/// <param name="Note">The explanation category and reason, or the partial count.</param>
internal sealed record PropertyRow(string Element, string Handler, string Property, PropertyStatus Status, string Note);

/// <summary>The summary of one element (or the base row).</summary>
/// <param name="Element">The element type.</param>
/// <param name="Handler">The handler type.</param>
/// <param name="Mapper">The mapper id (or empty when none was found).</param>
/// <param name="Scope">Properties in scope.</param>
/// <param name="Mapped">Mapped (for the base row: by every native handler).</param>
/// <param name="Explained">Explained.</param>
/// <param name="Declined">Declined.</param>
internal sealed record ElementSummary(string Element, string Handler, string Mapper, int Scope, int Mapped, int Explained, int Declined);

/// <summary>The result of the declined calculation.</summary>
/// <param name="Summaries">One per native (element, handler) pair, sorted, then the base row last.</param>
/// <param name="Rows">Every property row.</param>
/// <param name="Templated">Registered element types that are served by the templated fallback or Core's path only.</param>
internal sealed record DeclinedResult(IReadOnlyList<ElementSummary> Summaries, IReadOnlyList<PropertyRow> Rows, IReadOnlyList<HandlerRegistration> Templated);

/// <summary>
/// Computes the Android "declined" list: for every element type with a native handler, the public DependencyProperties
/// declared on the element type and its bases below FrameworkElement that the handler's mapper does not map and the
/// explained list does not explain. The UIElement / FrameworkElement properties are evaluated once, in a base row,
/// against every native handler's mapper.
/// </summary>
internal sealed class DeclinedCalculator
{
    /// <summary>The element name of the base row.</summary>
    internal const string BaseRow = "(base: UIElement + FrameworkElement)";

    private readonly ParityConventions _conventions;
    private readonly AssemblySet _set;

    /// <summary>Creates the calculator.</summary>
    /// <param name="conventions">The names to recognise.</param>
    /// <param name="set">The assembly set that can resolve the Core element types.</param>
    internal DeclinedCalculator(ParityConventions conventions, AssemblySet set)
    {
        _conventions = conventions ?? throw new ArgumentNullException(nameof(conventions));
        _set = set ?? throw new ArgumentNullException(nameof(set));
    }

    /// <summary>Computes the result.</summary>
    /// <param name="model">The handler scan.</param>
    /// <param name="explained">The explained list.</param>
    /// <returns>The result.</returns>
    internal DeclinedResult Compute(HandlerModel model, ExplainedList explained)
    {
        var summaries = new List<ElementSummary>();
        var rows = new List<PropertyRow>();
        var handlerKeys = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        TypeDefinition anyElement = null;

        foreach (var registration in model.Registrations.Values.OrderBy(r => r.ElementType, StringComparer.Ordinal))
        {
            if (registration.Kind != RegistrationKind.Native)
            {
                continue;
            }

            var elementType = FindType(registration.ElementType);
            anyElement ??= elementType;
            var scope = elementType == null ? new List<string>() : ScopeOf(elementType);
            foreach (var handler in registration.HandlerTypes)
            {
                var mapper = MapperOf(model, handler, registration.ElementType);
                var keys = mapper == null ? new HashSet<string>() : model.EffectiveKeys(mapper);
                handlerKeys[handler] = keys;

                int mapped = 0, explainedCount = 0, declined = 0;
                foreach (var property in scope)
                {
                    var row = Classify(registration.ElementType, handler, property, keys.Contains(property), explained);
                    rows.Add(row);
                    switch (row.Status)
                    {
                        case PropertyStatus.Mapped: mapped++; break;
                        case PropertyStatus.Explained: explainedCount++; break;
                        default: declined++; break;
                    }
                }

                summaries.Add(new ElementSummary(registration.ElementType, handler, mapper ?? string.Empty, scope.Count, mapped, explainedCount, declined));
            }
        }

        // The base row: UIElement + FrameworkElement properties against every native handler's mapper.
        var baseScope = new List<string>();
        foreach (var baseName in _conventions.BaseElementTypes)
        {
            var baseType = FindType(baseName);
            if (baseType != null)
            {
                baseScope.AddRange(DeclaredProperties(baseType));
            }
        }

        int baseMapped = 0, baseExplained = 0, baseDeclined = 0;
        foreach (var property in baseScope)
        {
            var count = handlerKeys.Values.Count(k => k.Contains(property));
            PropertyRow row;
            if (handlerKeys.Count > 0 && count == handlerKeys.Count)
            {
                row = new PropertyRow(BaseRow, handlerKeys.Count + " native handlers", property, PropertyStatus.Mapped, string.Empty);
                baseMapped++;
            }
            else if (count > 0)
            {
                row = new PropertyRow(BaseRow, handlerKeys.Count + " native handlers", property, PropertyStatus.Partial, $"mapped by {count} of {handlerKeys.Count} native handlers");
                baseMapped++;
            }
            else
            {
                row = Classify(BaseRow, handlerKeys.Count + " native handlers", property, false, explained);
                if (row.Status == PropertyStatus.Explained)
                {
                    baseExplained++;
                }
                else
                {
                    baseDeclined++;
                }
            }

            rows.Add(row);
        }

        summaries.Add(new ElementSummary(BaseRow, handlerKeys.Count + " native handlers", string.Empty, baseScope.Count, baseMapped, baseExplained, baseDeclined));

        var templated = model.Registrations.Values.Where(r => r.Kind != RegistrationKind.Native).OrderBy(r => r.ElementType, StringComparer.Ordinal).ToList();
        return new DeclinedResult(summaries, rows, templated);
    }

    /// <summary>The public DependencyProperty ids declared on the type itself (static fields and static getters).</summary>
    /// <param name="type">The type.</param>
    /// <returns>The ids, sorted.</returns>
    internal IReadOnlyList<string> DeclaredProperties(TypeDefinition type)
    {
        var ids = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var field in type.Fields)
        {
            if (field.IsStatic && field.IsPublic && field.FieldType.FullName == _conventions.DependencyPropertyType)
            {
                ids.Add(HandlerScanner.PropertyId(type, field.Name));
            }
        }

        foreach (var property in type.Properties)
        {
            var getter = property.GetMethod;
            if (getter != null && getter.IsStatic && getter.IsPublic && !property.HasParameters && property.PropertyType.FullName == _conventions.DependencyPropertyType)
            {
                ids.Add(HandlerScanner.PropertyId(type, property.Name));
            }
        }

        return ids.ToList();
    }

    private List<string> ScopeOf(TypeDefinition elementType)
    {
        var scope = new List<string>();
        foreach (var type in _set.BaseChain(elementType))
        {
            if (_conventions.BaseElementTypes.Contains(type.FullName))
            {
                break;
            }

            scope.AddRange(DeclaredProperties(type));
        }

        return scope;
    }

    private static PropertyRow Classify(string element, string handler, string property, bool mapped, ExplainedList explained)
    {
        if (mapped)
        {
            return new PropertyRow(element, handler, property, PropertyStatus.Mapped, string.Empty);
        }

        var explanation = explained.Find(property);
        return explanation != null
            ? new PropertyRow(element, handler, property, PropertyStatus.Explained, explanation.Category + ": " + explanation.Reason)
            : new PropertyRow(element, handler, property, PropertyStatus.Declined, string.Empty);
    }

    private static string MapperOf(HandlerModel model, string handler, string element)
    {
        if (model.HandlerMappers.TryGetValue(handler, out var mapper))
        {
            return mapper;
        }

        var typed = model.Mappers.Values.Where(m => m.HandlerType == handler).ToList();
        return (typed.FirstOrDefault(m => m.ElementType == element) ?? typed.FirstOrDefault())?.Id;
    }

    private TypeDefinition FindType(string fullName)
    {
        foreach (var assembly in _set.Assemblies)
        {
            foreach (var module in assembly.Modules)
            {
                var type = module.GetType(fullName.Replace('+', '/'));
                if (type != null)
                {
                    return type;
                }

                foreach (var forwarded in module.ExportedTypes)
                {
                    if (forwarded.FullName == fullName)
                    {
                        TypeDefinition target;
                        try
                        {
                            target = forwarded.Resolve();
                        }
                        catch (AssemblyResolutionException)
                        {
                            target = null;
                        }

                        if (target != null)
                        {
                            return target;
                        }
                    }
                }
            }
        }

        return null;
    }
}
