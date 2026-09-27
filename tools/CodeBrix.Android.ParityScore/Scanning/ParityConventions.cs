using System;
using System.Collections.Generic;
using System.Linq;

namespace CodeBrix.Android.ParityScore.Scanning;

/// <summary>
/// The names the scanners recognise (the object model's and the handler layer's types). <see cref="Product"/> holds the
/// CodeBrix.Platform / CodeBrix.Android names; tests supply their own fixture names.
/// </summary>
internal sealed class ParityConventions
{
    /// <summary>The conventions of the real product.</summary>
    internal static ParityConventions Product { get; } = new();

    /// <summary>Full name of the DependencyProperty type.</summary>
    internal string DependencyPropertyType { get; init; } = "Microsoft.UI.Xaml.DependencyProperty";

    /// <summary>Full names of the base element types whose properties the base view mapper answers for (the "base" row).</summary>
    internal IReadOnlyList<string> BaseElementTypes { get; init; } = new[] { "Microsoft.UI.Xaml.UIElement", "Microsoft.UI.Xaml.FrameworkElement" };

    /// <summary>Full name of the NotImplemented marker attribute.</summary>
    internal string NotImplementedAttribute { get; init; } = "CodeBrix.Platform.NotImplementedAttribute";

    /// <summary>
    /// A NotImplemented attribute with a platform list counts when the list is empty or names one of these (the symbols
    /// under which the Core assemblies are compiled); an attribute that names only other symbols (e.g. IS_UNIT_TESTS) does
    /// not count.
    /// </summary>
    internal IReadOnlyCollection<string> CorePlatformSymbols { get; init; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "__CODEBRIX_CORE__", "__SKIA__", "__NETSTD_REFERENCE__",
    };

    /// <summary>Full name of the exception whose construction marks a body as not implemented.</summary>
    internal string NotImplementedException { get; init; } = "System.NotImplementedException";

    /// <summary>Full name of the type whose <see cref="RaiseNotImplementedMethod"/> reports a not-implemented member at run time.</summary>
    internal string RaiseNotImplementedType { get; init; } = "Windows.Foundation.Metadata.ApiInformation";

    /// <summary>The method that reports a not-implemented member at run time.</summary>
    internal string RaiseNotImplementedMethod { get; init; } = "TryRaiseNotImplemented";

    /// <summary>Namespace of the property-mapper types.</summary>
    internal string MapperNamespace { get; init; } = "CodeBrix.Android.UI.Handlers";

    /// <summary>Names (with generic arity) of the property-mapper types and interfaces.</summary>
    internal IReadOnlyCollection<string> MapperTypeNames { get; init; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "PropertyMapper", "PropertyMapper`2", "IPropertyMapper", "IPropertyMapper`2",
    };

    /// <summary>Methods on a mapper type that add or replace a key.</summary>
    internal IReadOnlyCollection<string> MapperKeyMethods { get; init; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "set_Item", "Add",
    };

    /// <summary>Name of the static class holding the mapper customisation extensions.</summary>
    internal string MapperExtensionsType { get; init; } = "PropertyMapperExtensions";

    /// <summary>The customisation extensions (each adds or replaces a key).</summary>
    internal IReadOnlyCollection<string> MapperExtensionMethods { get; init; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "ModifyMapping", "ReplaceMapping", "AppendToMapping", "PrependToMapping",
    };

    /// <summary>Names of the types whose generic <c>Register</c> methods register an element handler.</summary>
    internal IReadOnlyCollection<string> RegistrationTypes { get; init; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "CodeBrix.Android.UI.Handlers.ElementHandlerRegistry", "CodeBrix.Android.UI.Handlers.CodeBrixHandlers",
    };

    /// <summary>The registration method name.</summary>
    internal string RegistrationMethod { get; init; } = "Register";

    /// <summary>Full name of the handler interface every Android element handler implements.</summary>
    internal string HandlerInterface { get; init; } = "CodeBrix.Android.UI.Handlers.IAndroidElementHandler";

    /// <summary>Full name of the templated-fallback handler (the element keeps Core's template: not a native handler).</summary>
    internal string FallbackHandler { get; init; } = "CodeBrix.Android.UI.Handlers.TemplatedFallbackHandler";

    /// <summary>True when <paramref name="ns"/> + <paramref name="name"/> is a mapper type.</summary>
    internal bool IsMapperTypeName(string ns, string name) => ns == MapperNamespace && MapperTypeNames.Contains(name);
}
