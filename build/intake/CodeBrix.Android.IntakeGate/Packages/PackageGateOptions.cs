using System;
using System.IO;

namespace CodeBrix.Android.IntakeGate.Packages;

/// <summary>
/// Command-line options of the package gates (the tool's "packages" mode, run by
/// build/nuget/CodeBrix.Android.Pack.proj over the packages it produced).
/// </summary>
internal sealed class PackageGateOptions
{
    internal const string Usage =
        "usage: CodeBrix.Android.IntakeGate packages --package-dir <dir> --platform-ids <file> --owners <file>\n" +
        "       [--external-assemblies <file>] [--tfm <android tfm>] [--report <file>]";

    /// <summary>The target framework folder an Android app selects (every lib/ file must be in it).</summary>
    internal const string DefaultAndroidTargetFramework = "net10.0-android36.1";

    /// <summary>The folder holding the produced .nupkg files.</summary>
    internal string PackageDirectory { get; private set; }

    /// <summary>build/platform-repo-package-ids.txt: the ids the CodeBrix.Platform repository produces.</summary>
    internal string PlatformIdsFile { get; private set; }

    /// <summary>build/nuget/package-dependency-owners.txt: the allowed dependency id patterns.</summary>
    internal string OwnersFile { get; private set; }

    /// <summary>
    /// build/intake/gates/allowed-references.txt: assemblies from packages the Platform repository does not
    /// produce (a CodeBrix.Platform.* name listed there is not a Skia twin).
    /// </summary>
    internal string ExternalAssembliesFile { get; private set; }

    /// <summary>The Android target framework folder name under lib/.</summary>
    internal string AndroidTargetFramework { get; private set; } = DefaultAndroidTargetFramework;

    /// <summary>Where the report is written (optional).</summary>
    internal string ReportFile { get; private set; }

    internal static PackageGateOptions Parse(string[] args)
    {
        var options = new PackageGateOptions();
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
                case "--package-dir": options.PackageDirectory = Path.GetFullPath(value); break;
                case "--platform-ids": options.PlatformIdsFile = Path.GetFullPath(value); break;
                case "--owners": options.OwnersFile = Path.GetFullPath(value); break;
                case "--external-assemblies": options.ExternalAssembliesFile = Path.GetFullPath(value); break;
                case "--tfm": options.AndroidTargetFramework = value; break;
                case "--report": options.ReportFile = Path.GetFullPath(value); break;
                default: throw new ArgumentException("unknown option " + name);
            }
        }

        Require(options.PackageDirectory, "--package-dir");
        Require(options.PlatformIdsFile, "--platform-ids");
        Require(options.OwnersFile, "--owners");
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
