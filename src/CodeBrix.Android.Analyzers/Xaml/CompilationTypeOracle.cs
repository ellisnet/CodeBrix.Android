using System.Collections.Concurrent;
using CodeBrix.Android.Analyzers.Surface;
using Microsoft.CodeAnalysis;

namespace CodeBrix.Android.Analyzers.Xaml;

/// <summary>Answers the XAML scan's type questions from a compilation (the app's code and every referenced assembly).</summary>
public sealed class CompilationTypeOracle : IXamlTypeOracle
{
    private readonly Compilation _compilation;
    private readonly ConcurrentDictionary<string, INamedTypeSymbol> _types = new();

    /// <summary>Creates the oracle.</summary>
    /// <param name="compilation">The compilation.</param>
    public CompilationTypeOracle(Compilation compilation)
    {
        _compilation = compilation;
    }

    /// <inheritdoc />
    public string Resolve(string clrNamespace, string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        if (clrNamespace != null)
        {
            if (Find(clrNamespace + "." + name) != null)
            {
                return clrNamespace + "." + name;
            }

            // The CodeBrix.Platform XAML maps a Microsoft.UI.Xaml* XML namespace onto the whole object model.
            if (!clrNamespace.StartsWith("Microsoft.UI.Xaml", System.StringComparison.Ordinal))
            {
                return null;
            }
        }

        foreach (var candidate in AndroidSurface.DefaultXamlNamespaces)
        {
            if (Find(candidate + "." + name) != null)
            {
                return candidate + "." + name;
            }
        }

        return null;
    }

    /// <inheritdoc />
    public bool DerivesFrom(string fullName, string baseFullName) => SymbolFacts.DerivesFrom(Find(fullName), baseFullName);

    /// <inheritdoc />
    public string NativeControl(string fullName) => SymbolFacts.NativeControl(Find(fullName));

    private INamedTypeSymbol Find(string fullName) => _types.GetOrAdd(fullName, n => _compilation.GetTypeByMetadataName(n));
}
