using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace CodeBrix.Android.IntakeGate.Gates;

/// <summary>
/// Gate 3: every CodeBrix.Android.* assembly is granted InternalsVisibleTo by the Core
/// assemblies it needs. gates/ivt-grants.txt lists, per Android assembly, the Core
/// assemblies that must grant it ("Android.Assembly &lt;- Core.A, Core.B", or
/// "&lt;- (none)"). Every row is checked against the Core assemblies' attributes, and every
/// CodeBrix.Android.* project under src/ must have a row (a new project states what it needs).
/// </summary>
internal sealed class InternalsVisibleToGate : IIntakeGate
{
    internal const string ListFileName = "ivt-grants.txt";
    private const string IvtAttribute = "System.Runtime.CompilerServices.InternalsVisibleToAttribute";

    public GateResult Run(IntakeContext context)
    {
        var result = new GateResult(3, "CBAI0003", "InternalsVisibleTo grants");
        var rows = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var line in context.ReadGateList(ListFileName))
        {
            var parts = line.Split("<-", 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2 || parts[0].Length == 0)
            {
                result.Errors.Add("malformed row in gates/" + ListFileName + ": " + line);
                continue;
            }

            var grantors = parts[1] == "(none)"
                ? new List<string>()
                : parts[1].Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList();
            rows[parts[0]] = grantors;
        }

        var granted = 0;
        foreach (var row in rows.OrderBy(r => r.Key, StringComparer.Ordinal))
        {
            foreach (var grantor in row.Value)
            {
                if (!context.LibAssemblies.TryGetValue(grantor, out var core))
                {
                    result.Errors.Add($"{row.Key}: the grantor {grantor} is not in the intake");
                    continue;
                }

                var grants = core.Definition.CustomAttributes
                    .Where(a => a.AttributeType.FullName == IvtAttribute && a.ConstructorArguments.Count == 1)
                    .Select(a => ((a.ConstructorArguments[0].Value as string) ?? string.Empty).Split(',')[0].Trim());
                if (grants.Contains(row.Key, StringComparer.Ordinal))
                {
                    granted++;
                }
                else
                {
                    result.Errors.Add($"{grantor} does not grant InternalsVisibleTo(\"{row.Key}\")");
                }
            }
        }

        var projects = FindAndroidProjects(context.Options.SourceDirectory);
        foreach (var project in projects.Where(p => !rows.ContainsKey(p)))
        {
            result.Errors.Add($"src/ builds {project} but gates/{ListFileName} has no row for it");
        }

        result.Notes.Add($"{rows.Count} Android assemblies listed, {granted} grants verified, {projects.Count} CodeBrix.Android.* projects under src/");
        return result;
    }

    private static List<string> FindAndroidProjects(string sourceDirectory)
    {
        var names = new List<string>();
        if (!Directory.Exists(sourceDirectory))
        {
            return names;
        }

        foreach (var csproj in Directory.EnumerateFiles(sourceDirectory, "*.csproj", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(csproj);
            var match = Regex.Match(text, "<AssemblyName>([^<]+)</AssemblyName>");
            var name = match.Success ? match.Groups[1].Value.Trim() : Path.GetFileNameWithoutExtension(csproj);
            if (name.StartsWith("CodeBrix.Android", StringComparison.Ordinal))
            {
                names.Add(name);
            }
        }

        names.Sort(StringComparer.Ordinal);
        return names;
    }
}
