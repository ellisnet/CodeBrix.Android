using System;
using System.Collections.Generic;
using System.Linq;
using CodeBrix.AssemblyTools;
using CodeBrix.AssemblyTools.Cil;

namespace CodeBrix.Android.ParityScore.Scanning;

/// <summary>How a member was found not implemented (a member can have several reasons).</summary>
[Flags]
internal enum NotImplementedReason
{
    /// <summary>Implemented.</summary>
    None = 0,

    /// <summary>Carries the NotImplemented attribute for the Core (or its type does).</summary>
    Marked = 1,

    /// <summary>Its body constructs a NotImplementedException (throws it).</summary>
    Throws = 2,

    /// <summary>Its body reports itself through ApiInformation.TryRaiseNotImplemented.</summary>
    Raises = 4,
}

/// <summary>One not-implemented public member of a Core type.</summary>
/// <param name="Member">The member as written in the report (e.g. <c>Text { get; set; }</c>, <c>Focus(FocusState)</c>).</param>
/// <param name="Reason">Why it counts.</param>
internal sealed record NotImplementedMember(string Member, NotImplementedReason Reason);

/// <summary>The not-implemented members of one public Core type.</summary>
/// <param name="Assembly">The assembly name.</param>
/// <param name="Type">The type's full name.</param>
/// <param name="TypeMarked">True when the type itself carries the NotImplemented attribute.</param>
/// <param name="PublicMembers">The number of public members the type declares.</param>
/// <param name="Members">The not-implemented members, sorted.</param>
internal sealed record NotImplementedType(string Assembly, string Type, bool TypeMarked, int PublicMembers, IReadOnlyList<NotImplementedMember> Members)
{
    /// <summary>Members marked NotImplemented.</summary>
    internal int Marked => Members.Count(m => (m.Reason & NotImplementedReason.Marked) != 0);

    /// <summary>Members that throw NotImplementedException.</summary>
    internal int Throws => Members.Count(m => (m.Reason & NotImplementedReason.Throws) != 0);

    /// <summary>Members that raise TryRaiseNotImplemented.</summary>
    internal int Raises => Members.Count(m => (m.Reason & NotImplementedReason.Raises) != 0);
}

/// <summary>
/// Counts the NotImplemented members of every public type of the Core assemblies: members that carry the NotImplemented
/// attribute for the Core (a marked type marks all its members), and members whose body throws NotImplementedException or
/// calls ApiInformation.TryRaiseNotImplemented. Property and event accessors count once, as their property or event.
/// </summary>
internal sealed class NotImplementedScanner
{
    private readonly ParityConventions _conventions;

    /// <summary>Creates the scanner.</summary>
    /// <param name="conventions">The names to recognise.</param>
    internal NotImplementedScanner(ParityConventions conventions)
    {
        _conventions = conventions ?? throw new ArgumentNullException(nameof(conventions));
    }

    /// <summary>Scans the public types of <paramref name="assemblies"/>.</summary>
    /// <param name="assemblies">The Core assemblies.</param>
    /// <returns>One entry per public type that has at least one not-implemented member or is itself marked, sorted by type name.</returns>
    internal IReadOnlyList<NotImplementedType> Scan(IEnumerable<AssemblyDefinition> assemblies)
    {
        var result = new List<NotImplementedType>();
        foreach (var assembly in assemblies)
        {
            foreach (var type in assembly.Modules.SelectMany(m => m.GetTypes()))
            {
                if (!IsVisible(type))
                {
                    continue;
                }

                var entry = ScanType(assembly.Name.Name, type);
                if (entry != null)
                {
                    result.Add(entry);
                }
            }
        }

        return result.OrderBy(t => t.Type, StringComparer.Ordinal).ThenBy(t => t.Assembly, StringComparer.Ordinal).ToList();
    }

    /// <summary>Scans one type.</summary>
    /// <param name="assemblyName">The assembly name (for the report).</param>
    /// <param name="type">The type.</param>
    /// <returns>The entry, or null when everything is implemented.</returns>
    internal NotImplementedType ScanType(string assemblyName, TypeDefinition type)
    {
        var typeMarked = IsMarked(type);
        var members = new Dictionary<string, NotImplementedReason>(StringComparer.Ordinal);
        var publicMembers = 0;

        var accessors = new HashSet<MethodDefinition>();
        foreach (var property in type.Properties)
        {
            var methods = new[] { property.GetMethod, property.SetMethod }.Where(m => m != null && IsVisible(m)).ToList();
            if (methods.Count == 0)
            {
                continue;
            }

            publicMembers++;
            accessors.UnionWith(methods);
            var reason = typeMarked || IsMarked(property) || methods.Any(IsMarked) ? NotImplementedReason.Marked : NotImplementedReason.None;
            reason |= methods.Aggregate(NotImplementedReason.None, (r, m) => r | BodyReason(m));
            Record(members, PropertyName(property), reason);
        }

        foreach (var evt in type.Events)
        {
            var methods = new[] { evt.AddMethod, evt.RemoveMethod }.Where(m => m != null && IsVisible(m)).ToList();
            if (methods.Count == 0)
            {
                continue;
            }

            publicMembers++;
            accessors.UnionWith(methods);
            var reason = typeMarked || IsMarked(evt) || methods.Any(IsMarked) ? NotImplementedReason.Marked : NotImplementedReason.None;
            reason |= methods.Aggregate(NotImplementedReason.None, (r, m) => r | BodyReason(m));
            Record(members, "event " + evt.Name, reason);
        }

        foreach (var method in type.Methods)
        {
            if (accessors.Contains(method) || !IsVisible(method) || method.IsStatic && method.IsConstructor)
            {
                continue;
            }

            publicMembers++;
            var reason = typeMarked || IsMarked(method) ? NotImplementedReason.Marked : NotImplementedReason.None;
            reason |= BodyReason(method);
            Record(members, MethodName(method), reason);
        }

        foreach (var field in type.Fields)
        {
            if (!(field.IsPublic || field.IsFamily || field.IsFamilyOrAssembly) || field.IsSpecialName)
            {
                continue;
            }

            publicMembers++;
            Record(members, field.Name, typeMarked || IsMarked(field) ? NotImplementedReason.Marked : NotImplementedReason.None);
        }

        if (!typeMarked && members.Count == 0)
        {
            return null;
        }

        var list = members.Select(kv => new NotImplementedMember(kv.Key, kv.Value)).OrderBy(m => m.Member, StringComparer.Ordinal).ToList();
        return new NotImplementedType(assemblyName, type.FullName.Replace('/', '+'), typeMarked, publicMembers, list);
    }

    /// <summary>True when the attribute list carries the NotImplemented attribute for the Core.</summary>
    /// <param name="provider">The member or type.</param>
    /// <returns>True when marked.</returns>
    internal bool IsMarked(ICustomAttributeProvider provider)
    {
        if (provider == null || !provider.HasCustomAttributes)
        {
            return false;
        }

        foreach (var attribute in provider.CustomAttributes)
        {
            if (attribute.AttributeType.FullName != _conventions.NotImplementedAttribute)
            {
                continue;
            }

            var platforms = Platforms(attribute);
            if (platforms.Count == 0 || platforms.Any(p => _conventions.CorePlatformSymbols.Contains(p)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>What the body of <paramref name="method"/> says about it (throws / raises).</summary>
    /// <param name="method">The method.</param>
    /// <returns>The reasons found in its body.</returns>
    internal NotImplementedReason BodyReason(MethodDefinition method)
    {
        if (method == null || !method.HasBody)
        {
            return NotImplementedReason.None;
        }

        var reason = NotImplementedReason.None;
        foreach (var instruction in method.Body.Instructions)
        {
            if (instruction.OpCode.Code == Code.Newobj && instruction.Operand is MethodReference ctor
                && ctor.DeclaringType.FullName == _conventions.NotImplementedException)
            {
                reason |= NotImplementedReason.Throws;
            }
            else if ((instruction.OpCode.Code == Code.Call || instruction.OpCode.Code == Code.Callvirt) && instruction.Operand is MethodReference called
                && called.Name == _conventions.RaiseNotImplementedMethod && called.DeclaringType.FullName == _conventions.RaiseNotImplementedType)
            {
                reason |= NotImplementedReason.Raises;
            }
        }

        return reason;
    }

    private static void Record(Dictionary<string, NotImplementedReason> members, string name, NotImplementedReason reason)
    {
        if (reason == NotImplementedReason.None)
        {
            return;
        }

        members[name] = members.TryGetValue(name, out var existing) ? existing | reason : reason;
    }

    private static List<string> Platforms(CustomAttribute attribute)
    {
        var platforms = new List<string>();
        foreach (var argument in attribute.ConstructorArguments)
        {
            if (argument.Value is CustomAttributeArgument[] array)
            {
                platforms.AddRange(array.Select(a => a.Value as string).Where(s => s != null));
            }
            else if (argument.Value is string single)
            {
                platforms.Add(single);
            }
        }

        return platforms;
    }

    private static bool IsVisible(TypeDefinition type)
    {
        for (var current = type; current != null; current = current.DeclaringType)
        {
            var visible = current.IsNested
                ? current.IsNestedPublic || current.IsNestedFamily || current.IsNestedFamilyOrAssembly
                : current.IsPublic;
            if (!visible)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsVisible(MethodDefinition method) => method.IsPublic || method.IsFamily || method.IsFamilyOrAssembly;

    private static string PropertyName(PropertyDefinition property)
    {
        var accessors = (property.GetMethod != null ? "get; " : string.Empty) + (property.SetMethod != null ? "set; " : string.Empty);
        var name = property.HasParameters
            ? "this[" + string.Join(", ", property.Parameters.Select(p => p.ParameterType.Name)) + "]"
            : property.Name;
        return name + " { " + accessors + "}";
    }

    private static string MethodName(MethodDefinition method)
    {
        var name = method.IsConstructor ? ".ctor" : method.Name;
        if (method.HasGenericParameters)
        {
            name += "<" + string.Join(", ", method.GenericParameters.Select(p => p.Name)) + ">";
        }

        return name + "(" + string.Join(", ", method.Parameters.Select(p => p.ParameterType.Name)) + ")";
    }
}
