using System.IO;
using System.Linq;
using CodeBrix.Android.ParityScore.Scanning;
using CodeBrix.Android.ParityScore.Tests.Fixture;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.ParityScore.Tests.Scanning;

public class NotImplementedScannerTests
{
    private static NotImplementedType ScanFixture(string typeName)
    {
        using var set = FixtureSupport.OpenSet();
        var scanner = new NotImplementedScanner(FixtureSupport.Conventions);
        return scanner.Scan(set.Assemblies).SingleOrDefault(t => t.Type == typeName);
    }

    [Fact]
    public void Marked_member_counts_and_unit_test_only_mark_does_not()
    {
        //Act
        var type = ScanFixture("ParityFixture.Xaml.Partly");

        //Assert
        type.Should().NotBeNull();
        type.Members.Select(m => m.Member).Should().Contain("Marked()");
        type.Members.Select(m => m.Member).Should().NotContain("MarkedForUnitTestsOnly()");
    }

    [Fact]
    public void Body_that_throws_not_implemented_counts_as_throws()
    {
        //Act
        var type = ScanFixture("ParityFixture.Xaml.Partly");

        //Assert
        type.Members.Single(m => m.Member == "Throws()").Reason.Should().Be(NotImplementedReason.Throws);
        type.Throws.Should().Be(1);
    }

    [Fact]
    public void Event_marked_for_the_core_counts_once()
    {
        //Act
        var type = ScanFixture("ParityFixture.Xaml.Partly");

        //Assert
        type.Members.Count(m => m.Member == "event MarkedEvent").Should().Be(1);
        type.Marked.Should().Be(2);
    }

    [Fact]
    public void Implemented_property_is_not_listed()
    {
        //Act
        var type = ScanFixture("ParityFixture.Xaml.Partly");

        //Assert
        type.Members.Select(m => m.Member).Should().NotContain("Implemented { get; set; }");
        type.PublicMembers.Should().Be(6);
    }

    [Fact]
    public void Marked_type_marks_every_member()
    {
        //Act
        var type = ScanFixture("ParityFixture.Xaml.Missing");

        //Assert
        type.TypeMarked.Should().BeTrue();
        type.Members.Select(m => m.Member).Should().Contain(new[] { "First()", "Second { get; set; }", ".ctor()" });
    }

    [Fact]
    public void Raise_not_implemented_call_counts_as_raises()
    {
        //Act
        var type = ScanFixture("ParityFixture.Xaml.Raising");

        //Assert
        type.Raises.Should().Be(1);
        type.Members.Single().Member.Should().Be("Reports()");
    }

    [Fact]
    public void Complete_type_is_not_reported()
    {
        //Act
        var type = ScanFixture("ParityFixture.Xaml.Complete");

        //Assert
        type.Should().BeNull();
    }

    [Fact]
    public void Real_platform_ui_core_has_not_implemented_members()
    {
        //Arrange
        var root = FixtureSupport.RepositoryRoot();
        root.Should().NotBeNull();
        var pin = System.Xml.Linq.XDocument.Load(Path.Combine(root, "build", "PlatformPin.props"))
            .Descendants("CodeBrixPlatformVersion").First().Value.Trim();
        var core = Path.Combine(root, "artifacts", "intake", pin, "lib", "CodeBrix.Platform.UI.Core.dll");
        File.Exists(core).Should().BeTrue("the intake runs before the tests in a solution build");
        using var set = new AssemblySet();

        //Act
        var assembly = set.Add(core);
        var result = new NotImplementedScanner(ParityConventions.Product).Scan(new[] { assembly });

        //Assert
        result.Sum(t => t.Members.Count).Should().BeGreaterThan(100);
        result.Should().Contain(t => t.Type == "Microsoft.UI.Xaml.Controls.RichTextBlock");
    }
}
