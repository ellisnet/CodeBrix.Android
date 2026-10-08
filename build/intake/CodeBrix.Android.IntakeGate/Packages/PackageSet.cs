using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CodeBrix.Android.IntakeGate.Internal;

namespace CodeBrix.Android.IntakeGate.Packages;

/// <summary>
/// Everything the package gates read: the produced packages and the gate input lists.
/// </summary>
internal sealed class PackageSet
{
    internal PackageSet(IReadOnlyList<NuGetPackageFile> packages, IEnumerable<string> platformIds,
        IEnumerable<string> ownerPatterns, IEnumerable<string> externalAssemblies, string androidTargetFramework)
    {
        Packages = packages;
        PlatformIds = new HashSet<string>(platformIds, StringComparer.OrdinalIgnoreCase);
        OwnerPatterns = ownerPatterns.ToList();
        ExternalAssemblies = new HashSet<string>(externalAssemblies, StringComparer.Ordinal);
        AndroidTargetFramework = androidTargetFramework;
    }

    /// <summary>The packages, ordered by id.</summary>
    internal IReadOnlyList<NuGetPackageFile> Packages { get; }

    /// <summary>The package ids the CodeBrix.Platform repository produces (gate a).</summary>
    internal ISet<string> PlatformIds { get; }

    /// <summary>The allowed dependency id patterns (gate c); a trailing '*' matches any rest.</summary>
    internal IReadOnlyList<string> OwnerPatterns { get; }

    /// <summary>Assembly names that come from packages the Platform repository does not produce (gate b).</summary>
    internal ISet<string> ExternalAssemblies { get; }

    /// <summary>The lib/ folder every packaged assembly must be in (gate b).</summary>
    internal string AndroidTargetFramework { get; }

    internal static PackageSet Load(PackageGateOptions options)
    {
        var packages = Directory.EnumerateFiles(options.PackageDirectory, "*.nupkg", SearchOption.TopDirectoryOnly)
            .Where(path => !path.EndsWith(".symbols.nupkg", StringComparison.OrdinalIgnoreCase))
            .Select(NuGetPackageFile.Read)
            .OrderBy(p => p.Id, StringComparer.Ordinal)
            .ToList();

        // The first column of each list line is the entry (the owner / package columns are documentation).
        static IEnumerable<string> FirstColumn(string path) =>
            path == null
                ? Enumerable.Empty<string>()
                : ListFile.ReadLines(path).Select(l => l.Split((char[])null, 2, StringSplitOptions.RemoveEmptyEntries)[0]);

        return new PackageSet(packages, FirstColumn(options.PlatformIdsFile), FirstColumn(options.OwnersFile),
            FirstColumn(options.ExternalAssembliesFile), options.AndroidTargetFramework);
    }
}
