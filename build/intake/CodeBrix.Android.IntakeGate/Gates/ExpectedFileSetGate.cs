using System;
using System.Collections.Generic;
using System.Linq;

namespace CodeBrix.Android.IntakeGate.Gates;

/// <summary>
/// Gate 1: the extracted file set equals gates/expected-files.txt exactly (nothing missing,
/// nothing extra). The list is part of the pin: update it deliberately on every pin bump.
/// </summary>
internal sealed class ExpectedFileSetGate : IIntakeGate
{
    internal const string ListFileName = "expected-files.txt";

    public GateResult Run(IntakeContext context)
    {
        var result = new GateResult(1, "CBAI0001", "expected file set");
        var expected = new HashSet<string>(context.ReadGateList(ListFileName), StringComparer.Ordinal);
        if (expected.Count == 0)
        {
            result.Errors.Add("gates/" + ListFileName + " is missing or empty");
            return result;
        }

        var actual = new HashSet<string>(context.Files, StringComparer.Ordinal);
        foreach (var missing in expected.Where(f => !actual.Contains(f)).OrderBy(f => f, StringComparer.Ordinal))
        {
            result.Errors.Add("missing: " + missing);
        }

        foreach (var extra in actual.Where(f => !expected.Contains(f)).OrderBy(f => f, StringComparer.Ordinal))
        {
            result.Errors.Add("unexpected: " + extra);
        }

        result.Notes.Add($"{actual.Count} files extracted, {expected.Count} expected");
        return result;
    }
}
