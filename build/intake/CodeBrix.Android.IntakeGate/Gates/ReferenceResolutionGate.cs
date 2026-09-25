using System;
using System.Collections.Generic;
using System.Linq;

namespace CodeBrix.Android.IntakeGate.Gates;

/// <summary>
/// Gate 4: every assembly reference of every extracted lib/ assembly resolves inside the
/// intake set, the .NET reference pack (the BCL), or gates/allowed-references.txt
/// (assemblies that come from allowed, non-Platform-repo packages).
/// </summary>
internal sealed class ReferenceResolutionGate : IIntakeGate
{
    internal const string ListFileName = "allowed-references.txt";

    public GateResult Run(IntakeContext context)
    {
        var result = new GateResult(4, "CBAI0004", "references resolve (intake/BCL/allowed)");
        var allowed = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in context.ReadGateList(ListFileName))
        {
            var parts = line.Split((char[])null, 2, StringSplitOptions.RemoveEmptyEntries);
            allowed[parts[0]] = parts.Length > 1 ? parts[1].Trim() : "(no package named)";
        }

        var used = new SortedSet<string>(StringComparer.Ordinal);
        var count = 0;
        foreach (var assembly in context.LibAssemblies.Values.OrderBy(a => a.Definition.Name.Name, StringComparer.Ordinal))
        {
            foreach (var reference in assembly.Definition.MainModule.AssemblyReferences)
            {
                count++;
                var name = reference.Name;
                if (context.LibAssemblies.ContainsKey(name) || context.BclAssemblyNames.Contains(name) || name == "netstandard" || name == "mscorlib")
                {
                    continue;
                }

                if (allowed.TryGetValue(name, out var package))
                {
                    used.Add($"{name} ({package})");
                    continue;
                }

                result.Errors.Add($"{assembly.Definition.Name.Name} references {name}, which is not in the intake, the BCL or gates/{ListFileName}");
            }
        }

        result.Notes.Add($"{count} references checked in {context.LibAssemblies.Count} assemblies");
        foreach (var entry in used)
        {
            result.Notes.Add("allowed package reference: " + entry);
        }

        return result;
    }
}
