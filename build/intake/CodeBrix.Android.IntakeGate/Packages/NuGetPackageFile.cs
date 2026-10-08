using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;

namespace CodeBrix.Android.IntakeGate.Packages;

/// <summary>
/// One produced .nupkg as the package gates see it: its id, version, declared dependencies (from the
/// .nuspec inside it) and every file entry, with the bytes of each managed assembly entry kept for
/// inspection.
/// </summary>
internal sealed class NuGetPackageFile
{
    private NuGetPackageFile(string path, string id, string version, IReadOnlyList<PackageDependency> dependencies,
        IReadOnlyList<string> entries, IReadOnlyDictionary<string, byte[]> assemblies)
    {
        Path = path;
        Id = id;
        Version = version;
        Dependencies = dependencies;
        Entries = entries;
        Assemblies = assemblies;
    }

    /// <summary>The .nupkg file.</summary>
    internal string Path { get; }

    /// <summary>The package id declared by the nuspec.</summary>
    internal string Id { get; }

    /// <summary>The package version declared by the nuspec.</summary>
    internal string Version { get; }

    /// <summary>Every dependency of every dependency group.</summary>
    internal IReadOnlyList<PackageDependency> Dependencies { get; }

    /// <summary>Every file entry (forward slashes, unescaped), excluding the OPC packaging parts.</summary>
    internal IReadOnlyList<string> Entries { get; }

    /// <summary>Entry path -> bytes, for every entry ending in .dll.</summary>
    internal IReadOnlyDictionary<string, byte[]> Assemblies { get; }

    internal static NuGetPackageFile Read(string path)
    {
        using var archive = ZipFile.OpenRead(path);
        XDocument nuspec = null;
        var entries = new List<string>();
        var assemblies = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var entry in archive.Entries)
        {
            var name = Uri.UnescapeDataString(entry.FullName.Replace('\\', '/'));
            if (name.EndsWith("/", StringComparison.Ordinal) || IsPackagingPart(name))
            {
                continue;
            }

            if (!name.Contains('/') && name.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase))
            {
                using var stream = entry.Open();
                nuspec = XDocument.Load(stream);
                continue;
            }

            entries.Add(name);
            if (name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                using var stream = entry.Open();
                using var memory = new MemoryStream();
                stream.CopyTo(memory);
                assemblies[name] = memory.ToArray();
            }
        }

        if (nuspec == null)
        {
            throw new InvalidDataException(path + " has no .nuspec");
        }

        var metadata = nuspec.Root?.Elements().FirstOrDefault(e => e.Name.LocalName == "metadata");
        var id = metadata?.Elements().FirstOrDefault(e => e.Name.LocalName == "id")?.Value.Trim();
        var version = metadata?.Elements().FirstOrDefault(e => e.Name.LocalName == "version")?.Value.Trim();
        var dependencies = new List<PackageDependency>();
        var dependenciesElement = metadata?.Elements().FirstOrDefault(e => e.Name.LocalName == "dependencies");
        if (dependenciesElement != null)
        {
            foreach (var dependency in dependenciesElement.Descendants().Where(e => e.Name.LocalName == "dependency"))
            {
                var group = dependency.Parent?.Name.LocalName == "group" ? (string)dependency.Parent.Attribute("targetFramework") ?? string.Empty : string.Empty;
                dependencies.Add(new PackageDependency((string)dependency.Attribute("id") ?? string.Empty, (string)dependency.Attribute("version") ?? string.Empty, group));
            }
        }

        entries.Sort(StringComparer.Ordinal);
        return new NuGetPackageFile(path, id ?? string.Empty, version ?? string.Empty, dependencies, entries, assemblies);
    }

    private static bool IsPackagingPart(string name) =>
        name == "[Content_Types].xml"
        || name.StartsWith("_rels/", StringComparison.Ordinal)
        || name.StartsWith("package/services/", StringComparison.Ordinal)
        || name == ".signature.p7s";
}

/// <summary>A dependency declared by a package's nuspec.</summary>
/// <param name="Id">The dependency's package id.</param>
/// <param name="Version">The version range as written.</param>
/// <param name="TargetFramework">The dependency group's target framework ("" outside a group).</param>
internal sealed record PackageDependency(string Id, string Version, string TargetFramework);
