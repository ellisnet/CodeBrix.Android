using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using CodeBrix.Android.IntakeGate.Packages;
using CodeBrix.AssemblyTools;

namespace CodeBrix.Android.IntakeGate.Tests.Support;

/// <summary>
/// Builds a throw-away folder of .nupkg files (a nuspec plus entries; assemblies written with
/// CodeBrix.AssemblyTools) and the package gates' input lists, for the package gate tests.
/// </summary>
internal sealed class FakePackages : IDisposable
{
    internal const string AndroidTfm = "net10.0-android36.1";

    private readonly List<string> _platformIds = new List<string> { "CodeBrix.Platform.ApacheLicenseForever" };
    private readonly List<string> _owners = new List<string> { "Xamarin.*", "Microsoft.*", "CodeBrix.*", "SQLitePCLRaw.*" };
    private readonly List<string> _external = new List<string> { "CodeBrix.Platform.OpenGL" };

    internal FakePackages()
    {
        Root = Path.Combine(Path.GetTempPath(), "cba-package-gates-" + Guid.NewGuid().ToString("N"));
        PackageDirectory = Path.Combine(Root, "packages");
        Directory.CreateDirectory(PackageDirectory);
    }

    internal string Root { get; }

    internal string PackageDirectory { get; }

    /// <summary>Writes &lt;id&gt;.1.0.0.nupkg with the given dependencies ("id" or "id version") and entries.</summary>
    internal FakePackages AddPackage(string id, IEnumerable<string> dependencies = null, IEnumerable<PackageEntry> entries = null)
    {
        var nuspec = new StringBuilder();
        nuspec.Append("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        nuspec.Append("<package xmlns=\"http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd\"><metadata>");
        nuspec.Append("<id>").Append(id).Append("</id><version>1.0.0</version><authors>test</authors><description>test</description>");
        nuspec.Append("<dependencies><group targetFramework=\"").Append(AndroidTfm).Append("\">");
        foreach (var dependency in dependencies ?? Enumerable.Empty<string>())
        {
            var parts = dependency.Split(' ');
            nuspec.Append("<dependency id=\"").Append(parts[0]).Append("\" version=\"").Append(parts.Length > 1 ? parts[1] : "1.0.0").Append("\" />");
        }

        nuspec.Append("</group></dependencies></metadata></package>");

        using var archive = ZipFile.Open(Path.Combine(PackageDirectory, id + ".1.0.0.nupkg"), ZipArchiveMode.Create);
        WriteEntry(archive, id + ".nuspec", Encoding.UTF8.GetBytes(nuspec.ToString()));
        WriteEntry(archive, "[Content_Types].xml", Encoding.UTF8.GetBytes("<Types />"));
        foreach (var entry in entries ?? Enumerable.Empty<PackageEntry>())
        {
            WriteEntry(archive, entry.Path, entry.Bytes);
        }

        return this;
    }

    internal FakePackages WithPlatformIds(params string[] ids)
    {
        _platformIds.Clear();
        _platformIds.AddRange(ids);
        return this;
    }

    internal FakePackages WithOwners(params string[] patterns)
    {
        _owners.Clear();
        _owners.AddRange(patterns);
        return this;
    }

    /// <summary>A lib/&lt;android tfm&gt;/ assembly entry with the given references.</summary>
    internal static PackageEntry LibAssembly(string name, params string[] references) =>
        new PackageEntry("lib/" + AndroidTfm + "/" + name + ".dll", AssemblyBytes(name, references));

    /// <summary>An assembly entry at an arbitrary path.</summary>
    internal static PackageEntry AssemblyAt(string path, string name) => new PackageEntry(path, AssemblyBytes(name));

    /// <summary>A plain file entry.</summary>
    internal static PackageEntry File(string path) => new PackageEntry(path, Encoding.UTF8.GetBytes("x"));

    internal PackageSet Load()
    {
        var idsFile = Path.Combine(Root, "platform-repo-package-ids.txt");
        var ownersFile = Path.Combine(Root, "owners.txt");
        var externalFile = Path.Combine(Root, "allowed-references.txt");
        System.IO.File.WriteAllLines(idsFile, _platformIds);
        System.IO.File.WriteAllLines(ownersFile, _owners.Select(o => o + "    owner"));
        System.IO.File.WriteAllLines(externalFile, _external.Select(e => e + "    package"));
        var options = PackageGateOptions.Parse(new[]
        {
            "--package-dir", PackageDirectory, "--platform-ids", idsFile, "--owners", ownersFile, "--external-assemblies", externalFile,
        });
        return PackageSet.Load(options);
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

    private static byte[] AssemblyBytes(string name, params string[] references)
    {
        using var assembly = AssemblyDefinition.CreateAssembly(new AssemblyNameDefinition(name, new Version(1, 0, 0, 0)), name, ModuleKind.Dll);
        foreach (var reference in references)
        {
            assembly.MainModule.AssemblyReferences.Add(new AssemblyNameReference(reference, new Version(1, 0, 0, 0)));
        }

        using var memory = new MemoryStream();
        assembly.Write(memory);
        return memory.ToArray();
    }

    private static void WriteEntry(ZipArchive archive, string path, byte[] bytes)
    {
        using var stream = archive.CreateEntry(path).Open();
        stream.Write(bytes, 0, bytes.Length);
    }
}

/// <summary>One file entry of a fake package.</summary>
/// <param name="Path">The entry path.</param>
/// <param name="Bytes">The entry content.</param>
internal sealed record PackageEntry(string Path, byte[] Bytes);
