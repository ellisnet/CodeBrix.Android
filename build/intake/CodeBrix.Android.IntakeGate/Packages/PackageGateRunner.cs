using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CodeBrix.Android.IntakeGate.Gates;

namespace CodeBrix.Android.IntakeGate.Packages;

/// <summary>
/// The "packages" mode of the tool: runs the three package gates over a folder of produced .nupkg files,
/// prints (and optionally writes) a report that lists every package's dependencies and lib/ files for review,
/// and reports each error in MSBuild's canonical format. Exit code 0 = every gate passed.
/// </summary>
internal static class PackageGateRunner
{
    internal static int Run(string[] args)
    {
        PackageGateOptions options;
        try
        {
            options = PackageGateOptions.Parse(args);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine("CodeBrix.Android.IntakeGate : error CBAP0000: " + ex.Message);
            Console.Error.WriteLine(PackageGateOptions.Usage);
            return 2;
        }

        PackageSet packages;
        try
        {
            packages = PackageSet.Load(options);
        }
        catch (Exception ex) when (ex is IOException || ex is InvalidDataException || ex is UnauthorizedAccessException || ex is System.Xml.XmlException)
        {
            Console.Error.WriteLine("CodeBrix.Android.IntakeGate : error CBAP0000: " + ex.Message);
            return 2;
        }

        var results = RunGates(packages);
        var report = Format(packages, results);
        if (options.ReportFile != null)
        {
            File.WriteAllText(options.ReportFile, report);
        }

        Console.WriteLine(report);
        var failed = false;
        foreach (var result in results)
        {
            foreach (var error in result.Errors)
            {
                Console.Error.WriteLine($"CodeBrix.Android.IntakeGate : error {result.Code}: {result.Name}: {error}");
                failed = true;
            }
        }

        return failed ? 1 : 0;
    }

    internal static IReadOnlyList<GateResult> RunGates(PackageSet packages)
    {
        var results = new List<GateResult>
        {
            new PlatformDependencyGate().Run(packages),
            new NoSkiaTwinGate().Run(packages),
            new DependencyOwnerGate().Run(packages),
        };
        if (packages.Packages.Count == 0)
        {
            results[0].Errors.Add("no .nupkg files found");
        }

        return results;
    }

    internal static string Format(PackageSet packages, IReadOnlyList<GateResult> results)
    {
        var text = new StringBuilder();
        text.AppendLine("CodeBrix.Android package gates");
        text.AppendLine();
        foreach (var result in results)
        {
            text.AppendLine($"({(char)('a' + result.Number - 1)}) {result.Code} {result.Name}: {result.Status}");
            foreach (var error in result.Errors)
            {
                text.AppendLine("      ERROR " + error);
            }

            foreach (var note in result.Notes)
            {
                text.AppendLine("      " + note);
            }
        }

        text.AppendLine();
        text.AppendLine("PACKAGES (dependencies and lib/ files, for the dependency review)");
        foreach (var package in packages.Packages)
        {
            text.AppendLine();
            text.AppendLine($"{package.Id} {package.Version}  ({Path.GetFileName(package.Path)})");
            foreach (var dependency in package.Dependencies.OrderBy(d => d.Id, StringComparer.Ordinal))
            {
                text.AppendLine($"    dependency  {dependency.Id} {dependency.Version}  [{dependency.TargetFramework}]");
            }

            foreach (var entry in package.Entries.Where(e => e.StartsWith("lib/", StringComparison.OrdinalIgnoreCase)))
            {
                text.AppendLine("    " + entry);
            }

            var others = package.Entries.Count(e => !e.StartsWith("lib/", StringComparison.OrdinalIgnoreCase));
            text.AppendLine($"    (+ {others} other files: docs, notices, analyzers, build logic)");
        }

        return text.ToString();
    }
}
