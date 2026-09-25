using System.Collections.Generic;
using System.Text;
using CodeBrix.Android.IntakeGate.Manifest;

namespace CodeBrix.Android.IntakeGate.Gates;

/// <summary>Formats intake-gates.txt (also printed to the build log).</summary>
internal static class GateReport
{
    internal static string Format(IntakeContext context, ManifestSummary summary, IReadOnlyList<GateResult> results)
    {
        var builder = new StringBuilder();
        builder.AppendLine("CodeBrix.Android intake - CodeBrixPlatformVersion " + context.Options.PlatformVersion);
        builder.AppendLine($"  manifest: {summary.Packages} packages, {summary.Files} files ({summary.ManagedAssemblies} managed assemblies), {summary.Bytes} bytes");
        foreach (var pair in summary.CountByFolder)
        {
            builder.AppendLine($"    {pair.Key,-48} {pair.Value,4} files");
        }

        foreach (var result in results)
        {
            builder.AppendLine($"  gate {result.Number} {result.Name,-44} {result.Status}");
            foreach (var note in result.Notes)
            {
                builder.AppendLine("      " + note);
            }

            foreach (var error in result.Errors)
            {
                builder.AppendLine("      ERROR " + error);
            }
        }

        return builder.ToString();
    }
}
