using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CodeBrix.AssemblyTools;

namespace CodeBrix.Android.ParityScore.Scanning;

/// <summary>
/// A set of assemblies read for inspection only (nothing is loaded into the process), with a resolver that finds
/// references in every folder the set was read from, plus any extra search folders.
/// </summary>
internal sealed class AssemblySet : IDisposable
{
    private readonly DefaultAssemblyResolver _resolver = new();
    private readonly List<AssemblyDefinition> _assemblies = new();
    private readonly HashSet<string> _directories = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TypeDefinition> _resolved = new(StringComparer.Ordinal);

    /// <summary>The assemblies read, in the order they were added.</summary>
    internal IReadOnlyList<AssemblyDefinition> Assemblies => _assemblies;

    /// <summary>Adds a folder the resolver searches.</summary>
    /// <param name="directory">The folder.</param>
    internal void AddSearchDirectory(string directory)
    {
        if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory) && _directories.Add(Path.GetFullPath(directory)))
        {
            _resolver.AddSearchDirectory(Path.GetFullPath(directory));
        }
    }

    /// <summary>Reads an assembly and adds its folder to the resolver.</summary>
    /// <param name="path">The assembly file.</param>
    /// <returns>The assembly.</returns>
    internal AssemblyDefinition Add(string path)
    {
        AddSearchDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
        var assembly = AssemblyDefinition.ReadAssembly(path, new ReaderParameters { AssemblyResolver = _resolver, InMemory = true, ReadSymbols = false });
        _assemblies.Add(assembly);
        return assembly;
    }

    /// <summary>Every type of every assembly in the set, nested types included.</summary>
    /// <returns>The types.</returns>
    internal IEnumerable<TypeDefinition> AllTypes() => _assemblies.SelectMany(a => a.Modules).SelectMany(m => AllTypes(m.Types));

    /// <summary>Resolves a type reference (null when it cannot be found).</summary>
    /// <param name="reference">The reference.</param>
    /// <returns>The definition, or null.</returns>
    internal TypeDefinition Resolve(TypeReference reference)
    {
        if (reference == null)
        {
            return null;
        }

        if (reference is TypeDefinition definition)
        {
            return definition;
        }

        var element = reference.GetElementType();
        var key = element.Scope?.Name + "|" + element.FullName;
        if (_resolved.TryGetValue(key, out var cached))
        {
            return cached;
        }

        TypeDefinition result;
        try
        {
            result = element.Resolve();
        }
        catch (AssemblyResolutionException)
        {
            result = null;
        }

        _resolved[key] = result;
        return result;
    }

    /// <summary>Resolves a method reference (null when it cannot be found).</summary>
    /// <param name="reference">The reference.</param>
    /// <returns>The definition, or null.</returns>
    internal MethodDefinition Resolve(MethodReference reference)
    {
        if (reference == null)
        {
            return null;
        }

        try
        {
            return reference.Resolve();
        }
        catch (AssemblyResolutionException)
        {
            return null;
        }
    }

    /// <summary>The type and its base types, most derived first (stops where a base cannot be resolved).</summary>
    /// <param name="type">The type.</param>
    /// <returns>The chain.</returns>
    internal IEnumerable<TypeDefinition> BaseChain(TypeDefinition type)
    {
        for (var current = type; current != null; current = Resolve(current.BaseType))
        {
            yield return current;
        }
    }

    /// <summary>True when <paramref name="type"/> or a base type implements the interface named <paramref name="interfaceFullName"/>.</summary>
    /// <param name="type">The type.</param>
    /// <param name="interfaceFullName">The interface's full name.</param>
    /// <returns>True when implemented.</returns>
    internal bool Implements(TypeDefinition type, string interfaceFullName)
    {
        foreach (var current in BaseChain(type))
        {
            foreach (var implemented in current.Interfaces)
            {
                if (implemented.InterfaceType.GetElementType().FullName == interfaceFullName)
                {
                    return true;
                }

                var resolved = Resolve(implemented.InterfaceType);
                if (resolved != null && resolved.FullName != current.FullName && Implements(resolved, interfaceFullName))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var assembly in _assemblies)
        {
            assembly.Dispose();
        }

        _resolver.Dispose();
    }

    private static IEnumerable<TypeDefinition> AllTypes(IEnumerable<TypeDefinition> types)
    {
        foreach (var type in types)
        {
            yield return type;
            foreach (var nested in AllTypes(type.NestedTypes))
            {
                yield return nested;
            }
        }
    }
}
