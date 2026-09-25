using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace CodeBrix.Android.Tests.Shared;

/// <summary>
/// Reads the metadata of a re-shipped Core assembly of the pinned Platform build (never loads it): the
/// InternalsVisibleTo grants, the referenced assemblies and the user strings (the names a Core loads BY NAME).
/// Linked into the add-in test projects; the test project passes the intake lib folder as the assembly metadata
/// "CodeBrix.Android.Tests.CoreLib".
/// </summary>
internal static class CoreMetadata
{
    /// <summary>The InternalsVisibleTo grants of a Core assembly.</summary>
    internal static List<string> InternalsVisibleTo(string assembly)
    {
        using var stream = Open(assembly);
        using var pe = new PEReader(stream);
        var md = pe.GetMetadataReader();
        var result = new List<string>();
        foreach (var handle in md.GetAssemblyDefinition().GetCustomAttributes())
        {
            var attribute = md.GetCustomAttribute(handle);
            if (attribute.Constructor.Kind != HandleKind.MemberReference)
            {
                continue;
            }

            var ctor = md.GetMemberReference((MemberReferenceHandle)attribute.Constructor);
            if (ctor.Parent.Kind != HandleKind.TypeReference
                || md.GetString(md.GetTypeReference((TypeReferenceHandle)ctor.Parent).Name) != "InternalsVisibleToAttribute")
            {
                continue;
            }

            var reader = md.GetBlobReader(attribute.Value);
            reader.ReadUInt16();
            result.Add(reader.ReadSerializedString());
        }

        return result;
    }

    /// <summary>The names of the assemblies a Core assembly references.</summary>
    internal static List<string> References(string assembly)
    {
        using var stream = Open(assembly);
        using var pe = new PEReader(stream);
        var md = pe.GetMetadataReader();
        return md.AssemblyReferences.Select(h => md.GetString(md.GetAssemblyReference(h).Name)).ToList();
    }

    /// <summary>The user strings (string literals) of a Core assembly.</summary>
    internal static List<string> UserStrings(string assembly)
    {
        using var stream = Open(assembly);
        using var pe = new PEReader(stream);
        var md = pe.GetMetadataReader();
        var result = new List<string>();
        var handle = MetadataTokens.UserStringHandle(1);
        while (!handle.IsNil)
        {
            result.Add(md.GetUserString(handle));
            handle = md.GetNextHandle(handle);
        }

        return result;
    }

    /// <summary>The full names of the types a Core assembly defines.</summary>
    internal static List<string> TypeNames(string assembly)
    {
        using var stream = Open(assembly);
        using var pe = new PEReader(stream);
        var md = pe.GetMetadataReader();
        return md.TypeDefinitions.Select(h => md.GetTypeDefinition(h))
            .Select(t => (md.GetString(t.Namespace) is { Length: > 0 } ns ? ns + "." : string.Empty) + md.GetString(t.Name))
            .ToList();
    }

    private static FileStream Open(string assembly)
    {
        var lib = Assembly.GetExecutingAssembly().GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(a => a.Key == "CodeBrix.Android.Tests.CoreLib").Value;
        return File.OpenRead(Path.Combine(lib, assembly + ".dll"));
    }
}
