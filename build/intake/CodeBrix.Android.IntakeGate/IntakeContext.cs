using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CodeBrix.Android.IntakeGate.Internal;
using CodeBrix.AssemblyTools;

namespace CodeBrix.Android.IntakeGate;

/// <summary>
/// Everything the manifest writer and the gates read: the extracted files, where each
/// came from, the managed assemblies among them, and the gate input lists.
/// </summary>
internal sealed class IntakeContext
{
    internal const string ManifestFileName = "intake-manifest.tsv";
    internal const string GateReportFileName = "intake-gates.txt";

    /// <summary>Files the tool itself writes into the intake folder (not part of the extracted set).</summary>
    internal static readonly string[] ToolOutputFiles = { ManifestFileName, GateReportFileName, "intake-sources.txt", "intake-packages.txt" };

    private IntakeContext(IntakeOptions options)
    {
        Options = options;
    }

    internal IntakeOptions Options { get; }

    /// <summary>Relative path (forward slashes) of every extracted file, sorted ordinally.</summary>
    internal IReadOnlyList<string> Files { get; private set; }

    /// <summary>Relative path -> the package the file was extracted from.</summary>
    internal IReadOnlyDictionary<string, IntakeSource> Sources { get; private set; }

    /// <summary>The downloaded packages, in the order the intake project listed them.</summary>
    internal IReadOnlyList<IntakePackage> Packages { get; private set; }

    /// <summary>Every managed assembly under lib/, keyed by assembly name.</summary>
    internal IReadOnlyDictionary<string, IntakeAssembly> LibAssemblies { get; private set; }

    /// <summary>Assembly names of the .NET reference pack (System.*, netstandard, ...).</summary>
    internal ISet<string> BclAssemblyNames { get; private set; }

    internal static IntakeContext Load(IntakeOptions options)
    {
        var context = new IntakeContext(options);

        var toolOutputs = new HashSet<string>(ToolOutputFiles, StringComparer.Ordinal);
        context.Files = Directory.EnumerateFiles(options.IntakeDirectory, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(options.IntakeDirectory, path).Replace('\\', '/'))
            .Where(path => !toolOutputs.Contains(path))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

        var sources = new Dictionary<string, IntakeSource>(StringComparer.Ordinal);
        foreach (var line in ListFile.ReadLines(options.SourcesFile))
        {
            var parts = line.Split('|');
            if (parts.Length == 3)
            {
                sources[parts[0].Replace('\\', '/')] = new IntakeSource(parts[1], parts[2]);
            }
        }

        context.Sources = sources;

        var packages = new List<IntakePackage>();
        foreach (var line in ListFile.ReadLines(options.PackagesFile))
        {
            var parts = line.Split('|');
            if (parts.Length == 3)
            {
                packages.Add(new IntakePackage(parts[0], parts[1], parts[2]));
            }
        }

        context.Packages = packages;

        var assemblies = new Dictionary<string, IntakeAssembly>(StringComparer.Ordinal);
        foreach (var relativePath in context.Files.Where(f => f.StartsWith("lib/", StringComparison.Ordinal) && f.EndsWith(".dll", StringComparison.Ordinal)))
        {
            var fullPath = Path.Combine(options.IntakeDirectory, relativePath);
            var definition = AssemblyDefinition.ReadAssembly(fullPath, new ReaderParameters { InMemory = true, ReadSymbols = false });
            sources.TryGetValue(relativePath, out var source);
            assemblies[definition.Name.Name] = new IntakeAssembly(relativePath, definition, source);
        }

        context.LibAssemblies = assemblies;

        context.BclAssemblyNames = new HashSet<string>(
            Directory.EnumerateFiles(options.BclDirectory, "*.dll").Select(Path.GetFileNameWithoutExtension),
            StringComparer.Ordinal);

        return context;
    }

    /// <summary>Reads a gate input list from build/intake/gates/.</summary>
    internal IReadOnlyList<string> ReadGateList(string fileName)
        => ListFile.ReadLines(Path.Combine(Options.GatesDirectory, fileName));
}

/// <summary>The package an extracted file came from.</summary>
internal sealed record IntakeSource(string PackageId, string PackageVersion);

/// <summary>A downloaded package of the pinned build.</summary>
internal sealed record IntakePackage(string PackageId, string PackageVersion, string NupkgPath);

/// <summary>A managed assembly under lib/ of the intake folder.</summary>
internal sealed record IntakeAssembly(string RelativePath, AssemblyDefinition Definition, IntakeSource Source)
{
    /// <summary>True for the framework package's assemblies (as opposed to an add-in's).</summary>
    internal bool IsFramework => Source != null && Source.PackageId == "CodeBrix.Platform.ApacheLicenseForever";

    /// <summary>True for a *.Core.dll.</summary>
    internal bool IsCore => Definition.Name.Name.EndsWith(".Core", StringComparison.Ordinal);
}
