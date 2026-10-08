using System;
using System.Linq;
using CodeBrix.Android.IntakeGate.Gates;

namespace CodeBrix.Android.IntakeGate.Packages;

/// <summary>
/// Package gate (c), the dependency policy: every declared dependency matches an allowed owner pattern
/// (build/nuget/package-dependency-owners.txt: Microsoft / Xamarin / dotnetframework packages,
/// self-produced CodeBrix.* packages, SQLitePCLRaw as the accepted exception). Anything else needs
/// Jeremy's approval first.
/// </summary>
internal sealed class DependencyOwnerGate : IPackageGate
{
    public GateResult Run(PackageSet packages)
    {
        var result = new GateResult(3, "CBAP0003", "dependency owners allowed");
        if (packages.OwnerPatterns.Count == 0)
        {
            result.Errors.Add("the list of allowed dependency owners is empty or missing");
        }

        foreach (var package in packages.Packages)
        {
            foreach (var dependency in package.Dependencies)
            {
                var pattern = packages.OwnerPatterns.FirstOrDefault(p => Matches(p, dependency.Id));
                if (pattern == null)
                {
                    result.Errors.Add($"{package.Id} depends on {dependency.Id}, whose owner is not allowed");
                }
            }
        }

        var distinct = packages.Packages.SelectMany(p => p.Dependencies).Select(d => d.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        result.Notes.Add($"{distinct} distinct dependency ids checked against {packages.OwnerPatterns.Count} owner patterns");
        return result;
    }

    /// <summary>Case-insensitive match; a trailing '*' matches any (possibly empty) rest of the id.</summary>
    internal static bool Matches(string pattern, string id)
    {
        if (pattern.EndsWith("*", StringComparison.Ordinal))
        {
            return id.StartsWith(pattern.Substring(0, pattern.Length - 1), StringComparison.OrdinalIgnoreCase);
        }

        return string.Equals(pattern, id, StringComparison.OrdinalIgnoreCase);
    }
}
