using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace CodeBrix.Android.UIReqs.Device.Runtime;

/// <summary>
/// The add-in groups' binding scope (AP7). On CodeBrix.Platform every add-in's UIReqs pair is its own test assembly:
/// its steps and scenario hooks bind only its own features (plus the core harness it references). Here the copied
/// add-in scenario code (Scenarios/AddIns/&lt;Group&gt;/, namespace CodeBrix.Platform.UI.AddIn.&lt;Group&gt;.UIReqs) is
/// compiled into the one scenario app, so a step definition or a Before/AfterScenario hook declared in an add-in
/// group's namespace applies only to the features of that group's folder (Scenarios/Features/&lt;Group&gt;/); the
/// core harness's bindings apply everywhere, as before.
/// </summary>
internal static class AddInScope
{
    private const string AddInNamespacePrefix = "CodeBrix.Platform.UI.AddIn.";

    /// <summary>The add-in group a binding type belongs to, or null for the core harness.</summary>
    internal static string? GroupOf(Type type)
    {
        var ns = type.Namespace ?? string.Empty;
        if (!ns.StartsWith(AddInNamespacePrefix, StringComparison.Ordinal))
        {
            return null;
        }

        var rest = ns.Substring(AddInNamespacePrefix.Length);
        var dot = rest.IndexOf('.');
        return dot < 0 ? rest : rest.Substring(0, dot);
    }

    /// <summary>The group of a feature: the last segment of its folder path (Features/&lt;Group&gt;).</summary>
    internal static string GroupOfFolder(string? folder)
    {
        var parts = (folder ?? string.Empty).Split('/', '\\', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 0 ? string.Empty : parts[^1];
    }

    /// <summary>True when a binding method applies to a feature of <paramref name="featureGroup"/>.</summary>
    internal static bool Applies(MethodInfo method, string featureGroup)
    {
        var group = GroupOf(method.DeclaringType!);
        return group == null || string.Equals(group, featureGroup, StringComparison.Ordinal);
    }

    /// <summary>The methods that apply to a feature of <paramref name="featureGroup"/>.</summary>
    internal static List<MethodInfo> Filter(IEnumerable<MethodInfo> methods, string featureGroup) =>
        methods.Where(m => Applies(m, featureGroup)).ToList();
}
