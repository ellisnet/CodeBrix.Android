using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CodeBrix.Android.ParityScore.Options;

/// <summary>The command line of the parity-score tool.</summary>
internal sealed class ParityOptions
{
    /// <summary>The usage text.</summary>
    internal const string Usage = """
        Usage:
          CodeBrix.Android.ParityScore --intake-lib <dir> --android <file-or-dir> [--android ...] --out <dir>
                                       [--explained <tsv>] [--label <text>]...

          --intake-lib <dir>   the intake's lib folder (artifacts/intake/<version>/lib); every *.Core.dll in it is scanned
          --android <path>     a CodeBrix.Android assembly, or a folder whose CodeBrix.Android*.dll are read (repeatable)
          --out <dir>          where the report files are written
          --explained <tsv>    the explained list (default: declined-explained.tsv beside the tool)
          --label <text>       a header line for the summary (repeatable), e.g. "pin 1.0.268.65"
        Exit code: 0 = report written, 1 = the inputs held nothing to score, 2 = usage error.
        """;

    /// <summary>The intake lib folder.</summary>
    internal string IntakeLibDirectory { get; private set; }

    /// <summary>The Android assembly files (resolved from files and folders, distinct, sorted).</summary>
    internal IReadOnlyList<string> AndroidAssemblies { get; private set; } = Array.Empty<string>();

    /// <summary>The output folder.</summary>
    internal string OutputDirectory { get; private set; }

    /// <summary>The explained list.</summary>
    internal string ExplainedFile { get; private set; }

    /// <summary>Header lines.</summary>
    internal IReadOnlyList<string> Labels { get; private set; } = Array.Empty<string>();

    /// <summary>Parses the arguments.</summary>
    /// <param name="args">The arguments.</param>
    /// <returns>The options.</returns>
    /// <exception cref="ArgumentException">An argument is unknown, missing its value, or a required one is absent.</exception>
    internal static ParityOptions Parse(IReadOnlyList<string> args)
    {
        var options = new ParityOptions();
        var android = new List<string>();
        var labels = new List<string>();
        for (var i = 0; i < args.Count; i++)
        {
            var name = args[i];
            if (i + 1 >= args.Count)
            {
                throw new ArgumentException($"{name} needs a value.");
            }

            var value = args[++i];
            switch (name)
            {
                case "--intake-lib": options.IntakeLibDirectory = value; break;
                case "--android": android.Add(value); break;
                case "--out": options.OutputDirectory = value; break;
                case "--explained": options.ExplainedFile = value; break;
                case "--label": labels.Add(value); break;
                default: throw new ArgumentException($"unknown option {name}.");
            }
        }

        if (string.IsNullOrEmpty(options.IntakeLibDirectory))
        {
            throw new ArgumentException("--intake-lib is required.");
        }

        if (string.IsNullOrEmpty(options.OutputDirectory))
        {
            throw new ArgumentException("--out is required.");
        }

        if (android.Count == 0)
        {
            throw new ArgumentException("at least one --android is required.");
        }

        options.AndroidAssemblies = ExpandAndroid(android);
        options.ExplainedFile ??= Path.Combine(AppContext.BaseDirectory, "declined-explained.tsv");
        options.Labels = labels;
        return options;
    }

    /// <summary>The Core assemblies of the intake folder (every <c>*.Core.dll</c>, sorted).</summary>
    /// <returns>The files.</returns>
    internal IReadOnlyList<string> CoreAssemblies() =>
        Directory.Exists(IntakeLibDirectory)
            ? Directory.GetFiles(IntakeLibDirectory, "*.Core.dll").OrderBy(f => f, StringComparer.Ordinal).ToList()
            : Array.Empty<string>();

    private static List<string> ExpandAndroid(IEnumerable<string> paths)
    {
        var files = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var path in paths)
        {
            if (Directory.Exists(path))
            {
                foreach (var file in Directory.GetFiles(path, "CodeBrix.Android*.dll"))
                {
                    files.Add(Path.GetFullPath(file));
                }
            }
            else if (File.Exists(path))
            {
                files.Add(Path.GetFullPath(path));
            }
        }

        // One file per assembly name (the same assembly may sit in several output folders).
        return files.GroupBy(Path.GetFileName, StringComparer.Ordinal).Select(g => g.First()).ToList();
    }
}
