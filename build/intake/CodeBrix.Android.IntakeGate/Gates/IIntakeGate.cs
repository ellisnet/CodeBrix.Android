namespace CodeBrix.Android.IntakeGate.Gates;

/// <summary>One intake check. A gate never throws for a failed check; it reports errors.</summary>
internal interface IIntakeGate
{
    GateResult Run(IntakeContext context);
}
