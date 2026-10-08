using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CodeBrix.Android.IntakeGate.Gates;
using CodeBrix.AssemblyTools;

namespace CodeBrix.Android.IntakeGate.Packages;

/// <summary>
/// Package gate (b): no Skia twin of a Core assembly inside any package, and the package layout an
/// Android app can use.
/// <list type="bullet">
/// <item>No assembly anywhere in a package is the twin of a re-shipped Core (a Core's name without ".Core",
/// e.g. CodeBrix.Platform.UI for CodeBrix.Platform.UI.Core), and no CodeBrix.Platform.* assembly under lib/
/// is anything but a *.Core assembly or the XAML parser.</item>
/// <item>No package carries the Platform's runtime-replace folder (codebrix-platform-runtime/).</item>
/// <item>No CodeBrix assembly under lib/ references a twin (a CodeBrix.Platform.* assembly that is not a *.Core
/// assembly, the XAML parser, or an assembly of a package the Platform repository does not produce).</item>
/// <item>Every lib/ file is in the Android target framework folder (NuGet selects one folder only).</item>
/// <item>Every lib/ assembly ships in exactly one package.</item>
/// </list>
/// </summary>
internal sealed class NoSkiaTwinGate : IPackageGate
{
    private const string XamlParser = "CodeBrix.Platform.Xaml";

    public GateResult Run(PackageSet packages)
    {
        var result = new GateResult(2, "CBAP0002", "no Skia twin, Android layout");
        var libFolder = "lib/" + packages.AndroidTargetFramework + "/";

        var coreNames = packages.Packages
            .SelectMany(p => p.Entries)
            .Select(AssemblyName)
            .Where(n => n != null && n.StartsWith("CodeBrix.Platform.", StringComparison.Ordinal) && n.EndsWith(".Core", StringComparison.Ordinal))
            .ToHashSet(StringComparer.Ordinal);
        var twinNames = coreNames.Select(n => n.Substring(0, n.Length - ".Core".Length)).ToHashSet(StringComparer.Ordinal);

        var owners = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var assembliesChecked = 0;
        foreach (var package in packages.Packages)
        {
            foreach (var entry in package.Entries)
            {
                if (entry.StartsWith("codebrix-platform-runtime/", StringComparison.OrdinalIgnoreCase))
                {
                    result.Errors.Add($"{package.Id}: {entry} (the Platform runtime-replace folder)");
                    continue;
                }

                var name = AssemblyName(entry);
                if (name != null && twinNames.Contains(name))
                {
                    result.Errors.Add($"{package.Id}: {entry} is the Skia twin of {name}.Core");
                }

                if (!entry.StartsWith("lib/", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!entry.StartsWith(libFolder, StringComparison.Ordinal) || entry.IndexOf('/', libFolder.Length) >= 0)
                {
                    result.Errors.Add($"{package.Id}: {entry} is not in {libFolder}");
                }

                if (name == null)
                {
                    continue;
                }

                if (name.StartsWith("CodeBrix.Platform.", StringComparison.Ordinal)
                    && !name.EndsWith(".Core", StringComparison.Ordinal) && name != XamlParser && !packages.ExternalAssemblies.Contains(name))
                {
                    result.Errors.Add($"{package.Id}: {entry} is a CodeBrix.Platform assembly that is not a Core assembly");
                }

                var fileName = Path.GetFileName(entry);
                if (owners.TryGetValue(fileName, out var owner) && owner != package.Id)
                {
                    result.Errors.Add($"{fileName} ships in both {owner} and {package.Id}");
                }
                else
                {
                    owners[fileName] = package.Id;
                }

                if (name.StartsWith("CodeBrix.", StringComparison.Ordinal) && package.Assemblies.TryGetValue(entry, out var bytes))
                {
                    assembliesChecked++;
                    CheckReferences(package.Id, entry, bytes, packages, result);
                }
            }
        }

        result.Notes.Add($"{packages.Packages.Count} packages, {coreNames.Count} Core assemblies, {assembliesChecked} lib/ assemblies' references checked");
        return result;
    }

    private static void CheckReferences(string packageId, string entry, byte[] bytes, PackageSet packages, GateResult result)
    {
        AssemblyDefinition definition;
        try
        {
            definition = AssemblyDefinition.ReadAssembly(new MemoryStream(bytes), new ReaderParameters { InMemory = true, ReadSymbols = false });
        }
        catch (Exception ex) when (ex is BadImageFormatException || ex is InvalidOperationException || ex is IOException)
        {
            result.Errors.Add($"{packageId}: {entry} cannot be read as an assembly ({ex.Message})");
            return;
        }

        foreach (var reference in definition.MainModule.AssemblyReferences)
        {
            var referenceName = reference.Name;
            var isTwin = referenceName.StartsWith("CodeBrix.Platform", StringComparison.Ordinal)
                && !referenceName.EndsWith(".Core", StringComparison.Ordinal)
                && referenceName != XamlParser
                && !packages.ExternalAssemblies.Contains(referenceName);
            if (isTwin)
            {
                result.Errors.Add($"{packageId}: {entry} references the non-Core assembly {referenceName}");
            }
        }
    }

    /// <summary>The assembly name of a .dll entry (file name without extension); null for other files.</summary>
    internal static string AssemblyName(string entry) =>
        entry.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ? Path.GetFileNameWithoutExtension(entry) : null;
}
