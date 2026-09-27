using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace CodeBrix.Android.Analyzers.Surface;

/// <summary>Symbol questions the CBAND rules ask (by full metadata name, so no Platform assembly is referenced).</summary>
internal static class SymbolFacts
{
    /// <summary>The full name of a type (<c>Namespace.Type</c>, nested types with a dot).</summary>
    /// <param name="type">The type.</param>
    /// <returns>The name, or null.</returns>
    internal static string FullName(ITypeSymbol type)
    {
        if (type == null)
        {
            return null;
        }

        var name = type.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        if (name.StartsWith("global::", System.StringComparison.Ordinal))
        {
            name = name.Substring("global::".Length);
        }

        var generic = name.IndexOf('<');
        return generic >= 0 ? name.Substring(0, generic) : name;
    }

    /// <summary>The first type in the base chain of <paramref name="type"/> whose full name is in <paramref name="names"/>, or null.</summary>
    /// <param name="type">The type.</param>
    /// <param name="names">The names.</param>
    /// <returns>The matching full name, or null.</returns>
    internal static string FirstInChain(ITypeSymbol type, IReadOnlyCollection<string> names)
    {
        for (var current = type; current != null; current = current.BaseType)
        {
            var name = FullName(current);
            if (name != null && Contains(names, name))
            {
                return name;
            }
        }

        return null;
    }

    /// <summary>True when <paramref name="type"/> is or derives from <paramref name="fullName"/>.</summary>
    /// <param name="type">The type.</param>
    /// <param name="fullName">The base type's full name.</param>
    /// <returns>True when it derives.</returns>
    internal static bool DerivesFrom(ITypeSymbol type, string fullName)
    {
        for (var current = type; current != null; current = current.BaseType)
        {
            if (FullName(current) == fullName)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The native control a type is (or derives from), or null.</summary>
    /// <param name="type">The type.</param>
    /// <returns>The native control's full name, or null.</returns>
    internal static string NativeControl(ITypeSymbol type) => FirstInChain(type, AndroidSurface.NativeControls);

    private static bool Contains(IReadOnlyCollection<string> names, string name)
    {
        if (names is ICollection<string> collection)
        {
            return collection.Contains(name);
        }

        foreach (var candidate in names)
        {
            if (candidate == name)
            {
                return true;
            }
        }

        return false;
    }
}
