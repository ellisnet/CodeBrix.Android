using System;
using System.Collections.Generic;
using System.Linq;

namespace CodeBrix.Android.IntakeGate.Gates;

/// <summary>
/// Gate 2: no framework Core assembly references SkiaSharp* or HarfBuzzSharp*; an add-in
/// Core may, only when its package is a Skia-canvas add-in (the R7 rule) listed in
/// gates/skia-canvas-addins.txt. No Core assembly references a Skia twin (a
/// CodeBrix.Platform.* assembly that is not itself a *.Core assembly or the XAML parser);
/// a CodeBrix.Platform.* assembly listed in gates/allowed-references.txt comes from a
/// package the Platform repository does not produce (e.g. CodeBrix.Platform.OpenGL from
/// its own repository) and is not a twin.
/// </summary>
internal sealed class NoSkiaReferenceGate : IIntakeGate
{
    internal const string ListFileName = "skia-canvas-addins.txt";

    public GateResult Run(IntakeContext context)
    {
        var result = new GateResult(2, "CBAI0002", "no SkiaSharp/HarfBuzzSharp in Core");
        var skiaAllowedPackages = new HashSet<string>(context.ReadGateList(ListFileName), StringComparer.Ordinal);
        var externalAssemblies = new HashSet<string>(
            context.ReadGateList(ReferenceResolutionGate.ListFileName).Select(l => l.Split((char[])null, 2, StringSplitOptions.RemoveEmptyEntries)[0]),
            StringComparer.Ordinal);

        var checkedCount = 0;
        foreach (var assembly in context.LibAssemblies.Values.Where(a => a.IsCore).OrderBy(a => a.Definition.Name.Name, StringComparer.Ordinal))
        {
            checkedCount++;
            var name = assembly.Definition.Name.Name;
            foreach (var reference in assembly.Definition.MainModule.AssemblyReferences)
            {
                var referenceName = reference.Name;
                var isSkia = referenceName.StartsWith("SkiaSharp", StringComparison.Ordinal)
                    || referenceName.StartsWith("HarfBuzzSharp", StringComparison.Ordinal);
                if (isSkia)
                {
                    var allowed = !assembly.IsFramework && assembly.Source != null && skiaAllowedPackages.Contains(assembly.Source.PackageId);
                    if (allowed)
                    {
                        result.Notes.Add($"{name} -> {referenceName} (allowed: Skia-canvas add-in)");
                    }
                    else
                    {
                        result.Errors.Add($"{name} references {referenceName}");
                    }
                }

                var isPlatformTwin = referenceName.StartsWith("CodeBrix.Platform", StringComparison.Ordinal)
                    && !referenceName.EndsWith(".Core", StringComparison.Ordinal)
                    && referenceName != "CodeBrix.Platform.Xaml"
                    && !externalAssemblies.Contains(referenceName);
                if (isPlatformTwin)
                {
                    result.Errors.Add($"{name} references the non-Core assembly {referenceName}");
                }
            }
        }

        result.Notes.Add($"{checkedCount} Core assemblies checked");
        return result;
    }
}
