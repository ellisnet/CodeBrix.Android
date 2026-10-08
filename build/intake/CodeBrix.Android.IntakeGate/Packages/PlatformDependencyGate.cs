using CodeBrix.Android.IntakeGate.Gates;

namespace CodeBrix.Android.IntakeGate.Packages;

/// <summary>
/// Package gate (a), Constraint 1: no CodeBrix.Android package declares a dependency on a package the
/// CodeBrix.Platform repository produces (build/platform-repo-package-ids.txt). The Core assemblies are
/// re-shipped instead.
/// </summary>
internal sealed class PlatformDependencyGate : IPackageGate
{
    public GateResult Run(PackageSet packages)
    {
        var result = new GateResult(1, "CBAP0001", "no dependency on a CodeBrix.Platform repository package");
        if (packages.PlatformIds.Count == 0)
        {
            result.Errors.Add("the list of CodeBrix.Platform repository package ids is empty or missing");
        }

        var checkedCount = 0;
        foreach (var package in packages.Packages)
        {
            foreach (var dependency in package.Dependencies)
            {
                checkedCount++;
                if (packages.PlatformIds.Contains(dependency.Id))
                {
                    result.Errors.Add($"{package.Id} depends on {dependency.Id} {dependency.Version}");
                }
            }
        }

        result.Notes.Add($"{checkedCount} dependencies of {packages.Packages.Count} packages checked against {packages.PlatformIds.Count} ids");
        return result;
    }
}
