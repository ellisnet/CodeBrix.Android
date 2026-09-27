using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using CodeBrix.Android.UI.TextLayout.Portable;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.TextLayout.Tests.Portable;

public class TextEngineIcuTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(49, false)]
    [InlineData(50, true)]
    [InlineData(76, true)]
    [InlineData(100, true)]
    [InlineData(101, false)]
    public void Only_a_version_in_the_engines_range_counts_as_bound(int version, bool bound)
    {
        //Act
        var result = TextEngineIcu.IsBound(version);

        //Assert
        result.Should().Be(bound);
    }

    [Fact]
    public void The_pinned_TextLayout_Core_keeps_its_ICU_state_where_the_probe_reads_it()
    {
        //Arrange
        var fields = NestedTypeFields("CodeBrix.Platform.UI.TextLayout.Core", TextEngineIcu.EngineTypeName, TextEngineIcu.IcuTypeName);

        //Assert
        fields.Should().NotBeNull($"{TextEngineIcu.EngineTypeName}+{TextEngineIcu.IcuTypeName} must exist in the Core");
        fields.Should().Contain(TextEngineIcu.VersionFieldName);
        fields.Should().Contain(TextEngineIcu.LibraryFieldName);
    }

    // The field names of a nested type of a Core assembly, read from its metadata (never loaded); null when absent.
    private static List<string> NestedTypeFields(string assembly, string outerFullName, string nestedName)
    {
        var lib = Assembly.GetExecutingAssembly().GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(a => a.Key == "CodeBrix.Android.Tests.CoreLib").Value;
        using var stream = File.OpenRead(Path.Combine(lib, assembly + ".dll"));
        using var pe = new PEReader(stream);
        var md = pe.GetMetadataReader();
        foreach (var handle in md.TypeDefinitions)
        {
            var type = md.GetTypeDefinition(handle);
            if (md.GetString(type.Namespace) + "." + md.GetString(type.Name) != outerFullName)
            {
                continue;
            }

            foreach (var nestedHandle in type.GetNestedTypes())
            {
                var nested = md.GetTypeDefinition(nestedHandle);
                if (md.GetString(nested.Name) == nestedName)
                {
                    return nested.GetFields().Select(f => md.GetString(md.GetFieldDefinition(f).Name)).ToList();
                }
            }
        }

        return null;
    }
}
