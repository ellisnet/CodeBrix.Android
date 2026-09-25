using System.Collections.Generic;

namespace CodeBrix.Android.IntakeGate.Gates;

/// <summary>The outcome of one gate: errors fail the intake, notes are informational.</summary>
internal sealed class GateResult
{
    internal GateResult(int number, string code, string name)
    {
        Number = number;
        Code = code;
        Name = name;
    }

    internal int Number { get; }

    /// <summary>The MSBuild error code used for this gate's errors (CBAI000n).</summary>
    internal string Code { get; }

    internal string Name { get; }

    internal bool Skipped { get; set; }

    internal List<string> Errors { get; } = new List<string>();

    internal List<string> Notes { get; } = new List<string>();

    internal string Status => Skipped ? "SKIPPED" : Errors.Count == 0 ? "PASS" : "FAIL";
}
