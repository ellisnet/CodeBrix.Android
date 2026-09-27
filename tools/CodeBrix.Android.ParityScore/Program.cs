using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CodeBrix.Android.ParityScore.Options;
using CodeBrix.Android.ParityScore.Reporting;
using CodeBrix.Android.ParityScore.Scanning;

namespace CodeBrix.Android.ParityScore;

/// <summary>Entry point: scans the Core and Android assemblies and writes the parity report.</summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        ParityOptions options;
        try
        {
            options = ParityOptions.Parse(args);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine("CodeBrix.Android.ParityScore : error CBPS0001: " + ex.Message);
            Console.Error.WriteLine(ParityOptions.Usage);
            return 2;
        }

        return Run(options, ParityConventions.Product, Console.Out);
    }

    /// <summary>Runs the scan and writes the report.</summary>
    /// <param name="options">The options.</param>
    /// <param name="conventions">The names to recognise.</param>
    /// <param name="output">Where the summary is echoed.</param>
    /// <returns>The exit code.</returns>
    internal static int Run(ParityOptions options, ParityConventions conventions, TextWriter output)
    {
        var coreFiles = options.CoreAssemblies();
        if (coreFiles.Count == 0 || options.AndroidAssemblies.Count == 0)
        {
            output.WriteLine($"CodeBrix.Android.ParityScore : error CBPS0002: nothing to score ({coreFiles.Count} Core assemblies in {options.IntakeLibDirectory}, {options.AndroidAssemblies.Count} Android assemblies).");
            return 1;
        }

        using var set = new AssemblySet();
        set.AddSearchDirectory(options.IntakeLibDirectory);
        foreach (var directory in options.AndroidAssemblies.Select(Path.GetDirectoryName).Distinct())
        {
            set.AddSearchDirectory(directory);
        }

        var core = coreFiles.Select(set.Add).ToList();
        var android = options.AndroidAssemblies.Select(set.Add).ToList();

        var notImplemented = new NotImplementedScanner(conventions).Scan(core);
        var coreTypes = core.SelectMany(a => a.Modules).SelectMany(m => m.GetTypes()).Count(t => t.IsPublic || t.IsNestedPublic);

        var model = new HandlerScanner(conventions, set).Scan(android);
        var explained = ExplainedList.Load(options.ExplainedFile);
        var declined = new DeclinedCalculator(conventions, set).Compute(model, explained);

        var header = new List<string>(options.Labels)
        {
            $"Core assemblies: {core.Count} (*.Core.dll in {options.IntakeLibDirectory})",
            $"Android assemblies: {android.Count} ({string.Join(", ", android.Select(a => a.Name.Name))})",
            $"mappers found: {model.Mappers.Count}; handler types with a mapper: {model.HandlerMappers.Count}; registrations: {model.Registrations.Count}; explained list: {explained.Count} lines",
        };

        var summary = ParityReport.Write(options.OutputDirectory, header, notImplemented, coreTypes, declined);
        output.Write(summary);
        output.WriteLine($"report: {Path.GetFullPath(options.OutputDirectory)}");
        return model.Registrations.Count == 0 ? 1 : 0;
    }
}
