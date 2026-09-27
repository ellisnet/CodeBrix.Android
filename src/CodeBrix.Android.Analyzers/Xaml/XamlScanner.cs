using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using CodeBrix.Android.Analyzers.Diagnostics;
using CodeBrix.Android.Analyzers.Surface;
using Microsoft.CodeAnalysis;

namespace CodeBrix.Android.Analyzers.Xaml;

/// <summary>Answers type questions for the XAML scan (the analyzer answers them from the compilation).</summary>
public interface IXamlTypeOracle
{
    /// <summary>The full name of the type a XAML element names, or null when it cannot be resolved.</summary>
    /// <param name="clrNamespace">The CLR namespace of the element's XML namespace (null for the default presentation namespace).</param>
    /// <param name="name">The element's local name.</param>
    /// <returns>The full name, or null.</returns>
    string Resolve(string clrNamespace, string name);

    /// <summary>True when the type named <paramref name="fullName"/> is or derives from <paramref name="baseFullName"/>.</summary>
    /// <param name="fullName">The type.</param>
    /// <param name="baseFullName">The base type.</param>
    /// <returns>True when it derives.</returns>
    bool DerivesFrom(string fullName, string baseFullName);

    /// <summary>The native control the type is or derives from (full name), or null.</summary>
    /// <param name="fullName">The type.</param>
    /// <returns>The native control, or null.</returns>
    string NativeControl(string fullName);
}

/// <summary>One CBAND finding in a XAML file (1-based line and column of the element or attribute name).</summary>
public sealed class XamlFinding
{
    /// <summary>Creates the finding.</summary>
    /// <param name="descriptor">The diagnostic.</param>
    /// <param name="line">The line (1-based).</param>
    /// <param name="column">The column (1-based).</param>
    /// <param name="length">The length of the name the finding points at.</param>
    /// <param name="arguments">The message arguments.</param>
    public XamlFinding(DiagnosticDescriptor descriptor, int line, int column, int length, IReadOnlyList<string> arguments)
    {
        Descriptor = descriptor;
        Line = line;
        Column = column;
        Length = length;
        Arguments = arguments;
    }

    /// <summary>The diagnostic.</summary>
    public DiagnosticDescriptor Descriptor { get; }

    /// <summary>The line (1-based).</summary>
    public int Line { get; }

    /// <summary>The column (1-based).</summary>
    public int Column { get; }

    /// <summary>The length of the name the finding points at.</summary>
    public int Length { get; }

    /// <summary>The message arguments.</summary>
    public IReadOnlyList<string> Arguments { get; }
}

/// <summary>
/// The XAML half of the CBAND diagnostics: scans one XAML file for the constructs CodeBrix.Android accepts and ignores
/// (templates on native controls, composition, 3-D projection, zoom, PasswordChar, acrylic/Mica). A file that is not
/// well-formed XML is skipped (the XAML generator reports it).
/// </summary>
public static class XamlScanner
{
    private const string PresentationNamespace = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private const string XamlLanguageNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";

    /// <summary>Scans XAML text.</summary>
    /// <param name="text">The file's text.</param>
    /// <param name="oracle">The type oracle.</param>
    /// <returns>The findings, in document order.</returns>
    public static IReadOnlyList<XamlFinding> Scan(string text, IXamlTypeOracle oracle)
    {
        XDocument document;
        try
        {
            document = XDocument.Parse(text, LoadOptions.SetLineInfo);
        }
        catch (XmlException)
        {
            return Array.Empty<XamlFinding>();
        }

        var findings = new List<XamlFinding>();
        foreach (var element in document.Descendants())
        {
            ScanElement(element, oracle, findings);
        }

        return findings;
    }

    private static void ScanElement(XElement element, IXamlTypeOracle oracle, List<XamlFinding> findings)
    {
        var local = element.Name.LocalName;
        var dot = local.IndexOf('.');
        if (dot > 0)
        {
            ScanPropertyElement(element, local.Substring(0, dot), local.Substring(dot + 1), oracle, findings);
            return;
        }

        var type = ResolveElement(element.Name, oracle);
        var shown = type != null ? AndroidSurface.SimpleName(type) : local;

        if (type != null && (AndroidSurface.CompositionTypes.Contains(type) || type.StartsWith(AndroidSurface.CompositionNamespace + ".", StringComparison.Ordinal)))
        {
            Add(findings, CbandDescriptors.CompositionIgnored, element, shown);
        }
        else if (type != null && AndroidSurface.ProjectionTypes.Contains(type))
        {
            Add(findings, CbandDescriptors.ProjectionIgnored, element, shown);
        }
        else if (type != null && AndroidSurface.MaterialTypes.Contains(type))
        {
            Add(findings, CbandDescriptors.MaterialFallback, element, shown);
        }
        else if (type != null && oracle.DerivesFrom(type, AndroidSurface.SystemBackdrop))
        {
            Add(findings, CbandDescriptors.CompositionIgnored, element, shown);
        }
        else if (AndroidSurface.FrameBufferOptionNames.Contains(local))
        {
            Add(findings, CbandDescriptors.FrameBufferOptionIgnored, element, local);
        }

        if (local == "Setter" && IsXamlObjectNamespace(element.Name.NamespaceName))
        {
            ScanSetter(element, oracle, findings);
        }

        foreach (var attribute in element.Attributes())
        {
            if (attribute.IsNamespaceDeclaration || attribute.Name.Namespace != XNamespace.None)
            {
                continue;
            }

            ScanAttribute(element, type, attribute, oracle, findings);
        }
    }

    private static void ScanAttribute(XElement element, string type, XAttribute attribute, IXamlTypeOracle oracle, List<XamlFinding> findings)
    {
        var name = attribute.Name.LocalName;
        var value = attribute.Value.Trim();
        switch (name)
        {
            case "Template" when type != null && oracle.NativeControl(type) is { } native:
                Add(findings, CbandDescriptors.ControlTemplateOnNativeControl, attribute, "Template=\"" + Shorten(value) + "\"", AndroidSurface.SimpleName(native));
                break;
            case "ZoomMode" when type != null && oracle.DerivesFrom(type, AndroidSurface.ScrollViewer) && value == "Enabled":
            case "ScrollViewer.ZoomMode" when value == "Enabled":
                Add(findings, CbandDescriptors.ZoomIgnored, attribute, name + "=\"Enabled\"");
                break;
            case "PasswordChar" when type != null && oracle.DerivesFrom(type, AndroidSurface.PasswordBox) && !IsMarkupExtension(value) && attribute.Value.Length != 1:
                Add(findings, CbandDescriptors.PasswordCharIgnored, attribute, attribute.Value);
                break;
            case "Shadow" when IsSet(value):
                Add(findings, CbandDescriptors.CompositionIgnored, attribute, "Shadow");
                break;
            case "Projection" when IsSet(value):
            case "Transform3D" when IsSet(value):
                Add(findings, CbandDescriptors.ProjectionIgnored, attribute, name);
                break;
        }
    }

    private static void ScanPropertyElement(XElement element, string owner, string property, IXamlTypeOracle oracle, List<XamlFinding> findings)
    {
        switch (property)
        {
            case "Template":
            {
                var parentType = element.Parent != null ? ResolveElement(element.Parent.Name, oracle) : null;
                var native = parentType != null ? oracle.NativeControl(parentType) : null;
                if (native != null)
                {
                    Add(findings, CbandDescriptors.ControlTemplateOnNativeControl, element, "<" + owner + ".Template>", AndroidSurface.SimpleName(native));
                }

                break;
            }

            case "Shadow":
                // The shadow object inside is reported by its own element; the property is reported only without one.
                if (!element.Elements().Any())
                {
                    Add(findings, CbandDescriptors.CompositionIgnored, element, "<" + owner + ".Shadow>");
                }

                break;
            case "Projection":
            case "Transform3D":
                // The projection object inside is reported by its own element; the property is reported only without one.
                if (!element.Elements().Any())
                {
                    Add(findings, CbandDescriptors.ProjectionIgnored, element, "<" + owner + "." + property + ">");
                }

                break;
        }
    }

    private static void ScanSetter(XElement setter, IXamlTypeOracle oracle, List<XamlFinding> findings)
    {
        var property = (string)setter.Attribute("Property");
        if (string.IsNullOrEmpty(property))
        {
            return;
        }

        property = property.Trim();
        var style = setter.Ancestors().FirstOrDefault(a => a.Name.LocalName == "Style");
        var target = style != null ? ResolveTypeReference(style, (string)style.Attribute("TargetType"), oracle) : null;
        var value = (string)setter.Attribute("Value") ?? setter.Elements().FirstOrDefault(e => e.Name.LocalName == "Setter.Value")?.Value;
        value = value?.Trim();

        switch (property)
        {
            case "Template" when target != null && oracle.NativeControl(target) is { } native:
                Add(findings, CbandDescriptors.ControlTemplateOnNativeControl, setter, "Style setter Template (Style TargetType " + AndroidSurface.SimpleName(target) + ")", AndroidSurface.SimpleName(native));
                break;
            case "ZoomMode" when target != null && oracle.DerivesFrom(target, AndroidSurface.ScrollViewer) && value == "Enabled":
            case "ScrollViewer.ZoomMode" when value == "Enabled":
                Add(findings, CbandDescriptors.ZoomIgnored, setter, "Style setter " + property + "=Enabled");
                break;
            case "PasswordChar" when target != null && oracle.DerivesFrom(target, AndroidSurface.PasswordBox) && value != null && !IsMarkupExtension(value) && value.Length != 1:
                Add(findings, CbandDescriptors.PasswordCharIgnored, setter, value);
                break;
            case "Shadow":
                Add(findings, CbandDescriptors.CompositionIgnored, setter, "Style setter Shadow");
                break;
            case "Projection":
            case "Transform3D":
                Add(findings, CbandDescriptors.ProjectionIgnored, setter, "Style setter " + property);
                break;
        }
    }

    private static string ResolveElement(XName name, IXamlTypeOracle oracle) => oracle.Resolve(ClrNamespace(name.NamespaceName), name.LocalName);

    /// <summary>Resolves a type reference written in an attribute (<c>Button</c>, <c>c:Button</c>, <c>{x:Type c:Button}</c>).</summary>
    private static string ResolveTypeReference(XElement scope, string reference, IXamlTypeOracle oracle)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            return null;
        }

        reference = reference.Trim();
        if (reference.StartsWith("{", StringComparison.Ordinal))
        {
            reference = reference.Trim('{', '}').Trim();
            var space = reference.IndexOf(' ');
            if (space < 0)
            {
                return null;
            }

            reference = reference.Substring(space + 1).Trim();
        }

        var colon = reference.IndexOf(':');
        XNamespace ns;
        string name;
        if (colon > 0)
        {
            ns = scope.GetNamespaceOfPrefix(reference.Substring(0, colon));
            name = reference.Substring(colon + 1);
        }
        else
        {
            ns = scope.GetDefaultNamespace();
            name = reference;
        }

        return ns == null ? null : oracle.Resolve(ClrNamespace(ns.NamespaceName), name);
    }

    /// <summary>The CLR namespace an XML namespace names (null for the default presentation namespace or an unknown URI).</summary>
    internal static string ClrNamespace(string xmlNamespace)
    {
        if (string.IsNullOrEmpty(xmlNamespace) || xmlNamespace == PresentationNamespace)
        {
            return null;
        }

        if (xmlNamespace.StartsWith("using:", StringComparison.Ordinal))
        {
            return xmlNamespace.Substring("using:".Length).Trim();
        }

        if (xmlNamespace.StartsWith("clr-namespace:", StringComparison.Ordinal))
        {
            var rest = xmlNamespace.Substring("clr-namespace:".Length);
            var semicolon = rest.IndexOf(';');
            return (semicolon >= 0 ? rest.Substring(0, semicolon) : rest).Trim();
        }

        return null;
    }

    private static bool IsXamlObjectNamespace(string xmlNamespace) => xmlNamespace != XamlLanguageNamespace;

    private static bool IsMarkupExtension(string value) => value.StartsWith("{", StringComparison.Ordinal) && !value.StartsWith("{}", StringComparison.Ordinal);

    private static bool IsSet(string value) => value.Length > 0 && value != "{x:Null}";

    private static string Shorten(string value) => value.Length <= 40 ? value : value.Substring(0, 37) + "...";

    private static void Add(List<XamlFinding> findings, DiagnosticDescriptor descriptor, XObject node, params string[] arguments)
    {
        var info = (IXmlLineInfo)node;
        var length = node is XElement element ? QualifiedName(element).Length : ((XAttribute)node).Name.LocalName.Length;
        findings.Add(new XamlFinding(descriptor, info.HasLineInfo() ? info.LineNumber : 1, info.HasLineInfo() ? info.LinePosition : 1, length, arguments));
    }

    private static string QualifiedName(XElement element)
    {
        var prefix = element.GetPrefixOfNamespace(element.Name.Namespace);
        return string.IsNullOrEmpty(prefix) ? element.Name.LocalName : prefix + ":" + element.Name.LocalName;
    }
}
