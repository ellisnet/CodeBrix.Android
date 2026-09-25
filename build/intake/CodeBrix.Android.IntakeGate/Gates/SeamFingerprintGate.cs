using System;
using System.Collections.Generic;
using System.Linq;
using CodeBrix.AssemblyTools;

namespace CodeBrix.Android.IntakeGate.Gates;

/// <summary>
/// Gate 5: the per-control handler seam exists in CodeBrix.Platform.UI.Core with the
/// expected shape. gates/seam-fingerprint.txt lists "TypeName" and "TypeName::MemberName"
/// lines. A line may start with "[AssemblyName] " to look in another intake assembly (the
/// platform contracts of the Foundation and WinRT Cores; AP1.9); without it the assembly is
/// CodeBrix.Platform.UI.Core. A generic type is written without its arity ("IFontSourcePlatform").
/// The gate is OFF (reported, not enforced) until the pin moves to a Platform build
/// that carries the seam; the intake project turns it on with CodeBrixIntakeSeamGate=true
/// (on by default since AP1.8).
/// </summary>
internal sealed class SeamFingerprintGate : IIntakeGate
{
    internal const string ListFileName = "seam-fingerprint.txt";
    private const string SeamAssembly = "CodeBrix.Platform.UI.Core";

    public GateResult Run(IntakeContext context)
    {
        var result = new GateResult(5, "CBAI0005", "handler-seam fingerprint");
        var findings = new List<string>();
        var entries = context.ReadGateList(ListFileName);

        var typesByAssembly = new Dictionary<string, List<TypeDefinition>>(StringComparer.Ordinal);
        foreach (var line in entries)
        {
            var (assembly, entry) = SplitAssembly(line);
            if (!typesByAssembly.TryGetValue(assembly, out var types))
            {
                types = context.LibAssemblies.TryGetValue(assembly, out var loaded)
                    ? loaded.Definition.MainModule.GetTypes().ToList()
                    : null;
                typesByAssembly[assembly] = types;
                if (types == null)
                {
                    findings.Add(assembly + " is not in the intake");
                }
            }

            if (types == null)
            {
                continue;
            }

            var parts = entry.Split("::", 2, StringSplitOptions.TrimEntries);
            // Several types can share a simple name (a control and a same-named helper);
            // the entry is present when ANY of them has the member.
            var candidates = types.Where(t => SimpleName(t) == parts[0]).ToList();
            if (candidates.Count == 0)
            {
                findings.Add("type not found: " + Describe(assembly, parts[0]));
                continue;
            }

            if (parts.Length == 2 && !candidates.Any(t => HasMember(t, parts[1])))
            {
                findings.Add($"member not found: {Describe(assembly, candidates[0].FullName)}::{parts[1]}");
            }
        }

        var present = entries.Count - findings.Count(f => f.StartsWith("type not found", StringComparison.Ordinal) || f.StartsWith("member not found", StringComparison.Ordinal));
        if (!context.Options.SeamGateEnabled)
        {
            result.Skipped = true;
            result.Notes.Add($"OFF until the pin moves to the seam build (CodeBrixIntakeSeamGate=true); {present} of {entries.Count} fingerprint entries present in this build");
            return result;
        }

        result.Errors.AddRange(findings);
        result.Notes.Add($"{present} of {entries.Count} fingerprint entries present");
        return result;
    }

    private static (string Assembly, string Entry) SplitAssembly(string line)
    {
        if (line.StartsWith('['))
        {
            var close = line.IndexOf(']');
            if (close > 1)
            {
                return (line.Substring(1, close - 1).Trim(), line.Substring(close + 1).Trim());
            }
        }

        return (SeamAssembly, line);
    }

    private static string SimpleName(TypeDefinition type)
    {
        var tick = type.Name.IndexOf('`');
        return tick < 0 ? type.Name : type.Name.Substring(0, tick);
    }

    private static string Describe(string assembly, string typeName)
        => assembly == SeamAssembly ? typeName : "[" + assembly + "] " + typeName;

    private static bool HasMember(TypeDefinition type, string member)
        => type.Methods.Any(m => m.Name == member)
            || type.Properties.Any(p => p.Name == member)
            || type.Fields.Any(f => f.Name == member)
            || type.Events.Any(e => e.Name == member);
}
