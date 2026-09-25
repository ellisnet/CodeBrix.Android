using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using CodeBrix.AssemblyTools;

namespace CodeBrix.Android.IntakeGate.Tests.Support;

/// <summary>
/// Builds a throw-away intake folder (lib/ assemblies written with CodeBrix.AssemblyTools,
/// gate lists, source map) under the system temp folder, and loads an IntakeContext over it.
/// </summary>
internal sealed class FakeIntake : IDisposable
{
    internal const string FrameworkPackage = "CodeBrix.Platform.ApacheLicenseForever";

    private readonly List<string> _sources = new List<string>();

    internal FakeIntake()
    {
        Root = Path.Combine(Path.GetTempPath(), "codebrix-android-intake-tests", Guid.NewGuid().ToString("N"));
        IntakeDirectory = Path.Combine(Root, "intake");
        GatesDirectory = Path.Combine(Root, "gates");
        BclDirectory = Path.Combine(Root, "bcl");
        SourceDirectory = Path.Combine(Root, "src");
        Directory.CreateDirectory(Path.Combine(IntakeDirectory, "lib"));
        Directory.CreateDirectory(GatesDirectory);
        Directory.CreateDirectory(BclDirectory);
        Directory.CreateDirectory(SourceDirectory);
        File.WriteAllText(Path.Combine(BclDirectory, "System.Runtime.dll"), string.Empty);

        // Assemblies written by CodeBrix.AssemblyTools reference the running corelib.
        File.WriteAllText(Path.Combine(BclDirectory, "System.Private.CoreLib.dll"), string.Empty);
    }

    internal string Root { get; }

    internal string IntakeDirectory { get; }

    internal string GatesDirectory { get; }

    internal string BclDirectory { get; }

    internal string SourceDirectory { get; }

    /// <summary>Writes lib/&lt;name&gt;.dll referencing the given assemblies and granting IVT to the given names.</summary>
    internal FakeIntake AddAssembly(string name, string packageId, IEnumerable<string> references = null, IEnumerable<string> grants = null, IEnumerable<string> typeNames = null)
    {
        var assemblyName = new AssemblyNameDefinition(name, new Version(1, 0, 0, 0));
        using var assembly = AssemblyDefinition.CreateAssembly(assemblyName, name, ModuleKind.Dll);
        var module = assembly.MainModule;
        foreach (var reference in references ?? Enumerable.Empty<string>())
        {
            module.AssemblyReferences.Add(new AssemblyNameReference(reference, new Version(1, 0, 0, 0)));
        }

        var ivtConstructor = module.ImportReference(typeof(InternalsVisibleToAttribute).GetConstructor(new[] { typeof(string) }));
        foreach (var grant in grants ?? Enumerable.Empty<string>())
        {
            var attribute = new CustomAttribute(ivtConstructor);
            attribute.ConstructorArguments.Add(new CustomAttributeArgument(module.TypeSystem.String, grant));
            assembly.CustomAttributes.Add(attribute);
        }

        foreach (var typeName in typeNames ?? Enumerable.Empty<string>())
        {
            module.Types.Add(new TypeDefinition("Fake", typeName, TypeAttributes.NotPublic | TypeAttributes.Interface | TypeAttributes.Abstract));
        }

        var relativePath = "lib/" + name + ".dll";
        assembly.Write(Path.Combine(IntakeDirectory, relativePath));
        _sources.Add(relativePath + "|" + packageId + "|1.0.0");
        return this;
    }

    /// <summary>Writes a plain (non-assembly) file into the intake folder.</summary>
    internal FakeIntake AddFile(string relativePath)
    {
        var fullPath = Path.Combine(IntakeDirectory, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
        File.WriteAllText(fullPath, "x");
        return this;
    }

    /// <summary>Writes gates/&lt;fileName&gt;.</summary>
    internal FakeIntake WithGateList(string fileName, params string[] lines)
    {
        File.WriteAllLines(Path.Combine(GatesDirectory, fileName), lines);
        return this;
    }

    /// <summary>Writes src/&lt;folder&gt;/&lt;folder&gt;.csproj with the given AssemblyName.</summary>
    internal FakeIntake WithSourceProject(string assemblyName)
    {
        var folder = Path.Combine(SourceDirectory, assemblyName);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, assemblyName + ".csproj"), "<Project><PropertyGroup><AssemblyName>" + assemblyName + "</AssemblyName></PropertyGroup></Project>");
        return this;
    }

    internal IntakeContext Load(bool seamGate = false)
    {
        var sourcesFile = Path.Combine(IntakeDirectory, "intake-sources.txt");
        File.WriteAllLines(sourcesFile, _sources);
        var packagesFile = Path.Combine(IntakeDirectory, "intake-packages.txt");
        File.WriteAllText(packagesFile, string.Empty);

        var options = IntakeOptions.Parse(new[]
        {
            "--intake-dir", IntakeDirectory,
            "--gates-dir", GatesDirectory,
            "--bcl-dir", BclDirectory,
            "--src-dir", SourceDirectory,
            "--platform-version", "1.0.0",
            "--sources", sourcesFile,
            "--packages", packagesFile,
            "--seam-gate", seamGate ? "true" : "false",
        });
        return IntakeContext.Load(options);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Root, true);
        }
        catch (IOException)
        {
        }
    }
}
