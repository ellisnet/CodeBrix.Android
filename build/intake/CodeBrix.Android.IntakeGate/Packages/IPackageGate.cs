using CodeBrix.Android.IntakeGate.Gates;

namespace CodeBrix.Android.IntakeGate.Packages;

/// <summary>One package check. A gate never throws for a failed check; it reports errors.</summary>
internal interface IPackageGate
{
    GateResult Run(PackageSet packages);
}
