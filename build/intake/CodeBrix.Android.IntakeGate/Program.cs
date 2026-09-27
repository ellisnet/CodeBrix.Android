using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CodeBrix.Android.IntakeGate.Gates;
using CodeBrix.Android.IntakeGate.Manifest;
using CodeBrix.Android.IntakeGate.Packages;

namespace CodeBrix.Android.IntakeGate;

/// <summary>
/// Entry point: writes the intake manifest, then runs the five intake gates over the
/// extracted CodeBrix.Platform assemblies. Exit code 0 = every enabled gate passed.
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        // "packages" mode: the package gates over produced .nupkg files (build/nuget/CodeBrix.Android.Pack.proj).
        if (args.Length > 0 && args[0] == "packages")
        {
            return PackageGateRunner.Run(args.Skip(1).ToArray());
        }

        IntakeOptions options;
        try
        {
            options = IntakeOptions.Parse(args);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine("CodeBrix.Android.IntakeGate : error CBAI0000: " + ex.Message);
            Console.Error.WriteLine(IntakeOptions.Usage);
            return 2;
        }

        var context = IntakeContext.Load(options);

        var manifestPath = Path.Combine(options.IntakeDirectory, IntakeContext.ManifestFileName);
        var manifestSummary = IntakeManifestWriter.Write(context, manifestPath);

        var gates = new List<IIntakeGate>
        {
            new ExpectedFileSetGate(),
            new NoSkiaReferenceGate(),
            new InternalsVisibleToGate(),
            new ReferenceResolutionGate(),
            new SeamFingerprintGate(),
        };

        var results = new List<GateResult>();
        foreach (var gate in gates)
        {
            results.Add(gate.Run(context));
        }

        var reportPath = Path.Combine(options.IntakeDirectory, IntakeContext.GateReportFileName);
        var report = GateReport.Format(context, manifestSummary, results);
        File.WriteAllText(reportPath, report);
        Console.WriteLine(report);

        var failed = false;
        foreach (var result in results)
        {
            foreach (var error in result.Errors)
            {
                // Canonical MSBuild error format, so the Exec task in the intake project reports each one.
                Console.Error.WriteLine($"CodeBrix.Android.IntakeGate : error {result.Code}: {result.Name}: {error}");
                failed = true;
            }
        }

        return failed ? 1 : 0;
    }
}
