using System;
using System.IO;

namespace CodeBrix.Android.IntakeGate;

/// <summary>
/// Command-line options of the intake gate tool.
/// </summary>
internal sealed class IntakeOptions
{
    internal const string Usage =
        "usage: CodeBrix.Android.IntakeGate --intake-dir <dir> --gates-dir <dir> --bcl-dir <dir> --src-dir <dir>\n" +
        "       --platform-version <version> --sources <file> --packages <file> [--seam-gate true|false]";

    /// <summary>The extraction folder, artifacts/intake/&lt;version&gt;/.</summary>
    internal string IntakeDirectory { get; private set; }

    /// <summary>The folder holding the gate input lists (build/intake/gates/).</summary>
    internal string GatesDirectory { get; private set; }

    /// <summary>The Microsoft.NETCore.App reference-assembly folder (defines "the BCL" for gate 4).</summary>
    internal string BclDirectory { get; private set; }

    /// <summary>The repository src/ folder (scanned for CodeBrix.Android.* projects by gate 3).</summary>
    internal string SourceDirectory { get; private set; }

    /// <summary>The pinned CodeBrix.Platform version.</summary>
    internal string PlatformVersion { get; private set; }

    /// <summary>File written by the intake project: one line per extracted file, "relative-path|package-id|package-version".</summary>
    internal string SourcesFile { get; private set; }

    /// <summary>File written by the intake project: one line per downloaded package, "package-id|package-version|nupkg-path".</summary>
    internal string PackagesFile { get; private set; }

    /// <summary>Whether gate 5 (the handler-seam fingerprint) is enforced.</summary>
    internal bool SeamGateEnabled { get; private set; }

    internal static IntakeOptions Parse(string[] args)
    {
        var options = new IntakeOptions();
        for (var i = 0; i < args.Length; i++)
        {
            var name = args[i];
            if (i + 1 >= args.Length)
            {
                throw new ArgumentException("missing value for " + name);
            }

            var value = args[++i];
            switch (name)
            {
                case "--intake-dir": options.IntakeDirectory = Path.GetFullPath(value); break;
                case "--gates-dir": options.GatesDirectory = Path.GetFullPath(value); break;
                case "--bcl-dir": options.BclDirectory = Path.GetFullPath(value); break;
                case "--src-dir": options.SourceDirectory = Path.GetFullPath(value); break;
                case "--platform-version": options.PlatformVersion = value; break;
                case "--sources": options.SourcesFile = Path.GetFullPath(value); break;
                case "--packages": options.PackagesFile = Path.GetFullPath(value); break;
                case "--seam-gate": options.SeamGateEnabled = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase); break;
                default: throw new ArgumentException("unknown option " + name);
            }
        }

        Require(options.IntakeDirectory, "--intake-dir");
        Require(options.GatesDirectory, "--gates-dir");
        Require(options.BclDirectory, "--bcl-dir");
        Require(options.SourceDirectory, "--src-dir");
        Require(options.PlatformVersion, "--platform-version");
        Require(options.SourcesFile, "--sources");
        Require(options.PackagesFile, "--packages");
        return options;
    }

    private static void Require(string value, string name)
    {
        if (string.IsNullOrEmpty(value))
        {
            throw new ArgumentException(name + " is required");
        }
    }
}
