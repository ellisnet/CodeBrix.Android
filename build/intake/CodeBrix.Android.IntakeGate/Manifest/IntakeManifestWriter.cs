using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CodeBrix.Android.IntakeGate.Internal;
using CodeBrix.AssemblyTools;

namespace CodeBrix.Android.IntakeGate.Manifest;

/// <summary>
/// Writes intake-manifest.tsv: one row per extracted file (path, source package, SHA-256,
/// size, assembly version, trimmability), preceded by one row per downloaded package.
/// </summary>
internal static class IntakeManifestWriter
{
    internal static ManifestSummary Write(IntakeContext context, string path)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# CodeBrix.Android intake manifest");
        builder.AppendLine("# CodeBrixPlatformVersion\t" + context.Options.PlatformVersion);
        builder.AppendLine("#");
        builder.AppendLine("# packages: package-id\tversion\tnupkg-sha256");
        foreach (var package in context.Packages)
        {
            var hash = File.Exists(package.NupkgPath) ? Hashing.Sha256(package.NupkgPath) : "(nupkg not found)";
            builder.AppendLine("#package\t" + package.PackageId + "\t" + package.PackageVersion + "\t" + hash);
        }

        builder.AppendLine("#");
        builder.AppendLine("path\tpackage\tsha256\tbytes\tassembly-version\tis-trimmable");

        var summary = new ManifestSummary();
        foreach (var relativePath in context.Files)
        {
            var fullPath = Path.Combine(context.Options.IntakeDirectory, relativePath);
            context.Sources.TryGetValue(relativePath, out var source);
            var assemblyVersion = "-";
            var trimmable = "-";
            if (relativePath.EndsWith(".dll", StringComparison.Ordinal))
            {
                var definition = TryRead(fullPath);
                if (definition != null)
                {
                    assemblyVersion = definition.Name.Version.ToString();
                    trimmable = IsTrimmable(definition) ? "yes" : "no";
                    summary.ManagedAssemblies++;
                }
            }

            var bytes = new FileInfo(fullPath).Length;
            summary.Files++;
            summary.Bytes += bytes;
            summary.CountByFolder[TopFolder(relativePath)] = summary.CountByFolder.GetValueOrDefault(TopFolder(relativePath)) + 1;

            builder.Append(relativePath).Append('\t')
                .Append(source?.PackageId ?? "-").Append('\t')
                .Append(Hashing.Sha256(fullPath)).Append('\t')
                .Append(bytes).Append('\t')
                .Append(assemblyVersion).Append('\t')
                .Append(trimmable).AppendLine();
        }

        summary.Packages = context.Packages.Count;
        File.WriteAllText(path, builder.ToString());
        return summary;
    }

    private static AssemblyDefinition TryRead(string path)
    {
        try
        {
            return AssemblyDefinition.ReadAssembly(path, new ReaderParameters { InMemory = true, ReadSymbols = false });
        }
        catch (BadImageFormatException)
        {
            return null;
        }
    }

    private static bool IsTrimmable(AssemblyDefinition definition)
        => definition.CustomAttributes.Any(a =>
            a.AttributeType.FullName == "System.Reflection.AssemblyMetadataAttribute"
            && a.ConstructorArguments.Count == 2
            && (a.ConstructorArguments[0].Value as string) == "IsTrimmable"
            && string.Equals(a.ConstructorArguments[1].Value as string, "True", StringComparison.OrdinalIgnoreCase));

    private static string TopFolder(string relativePath)
    {
        var parts = relativePath.Split('/');
        return parts.Length > 2 && parts[0] == "buildTransitive" ? parts[0] + "/" + parts[1] : parts.Length > 1 ? parts[0] : ".";
    }
}

/// <summary>Counts reported in the gate report header.</summary>
internal sealed class ManifestSummary
{
    internal int Packages { get; set; }

    internal int Files { get; set; }

    internal int ManagedAssemblies { get; set; }

    internal long Bytes { get; set; }

    internal SortedDictionary<string, int> CountByFolder { get; } = new SortedDictionary<string, int>(StringComparer.Ordinal);
}
