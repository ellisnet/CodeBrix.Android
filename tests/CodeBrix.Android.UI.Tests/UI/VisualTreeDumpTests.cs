using CodeBrix.Android.UI.Diagnostics;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.Tests.UI;

public class VisualTreeDumpTests
{
    [Fact]
    public void Dump_of_no_root_is_empty()
    {
        //Act
        var lines = VisualTreeDump.Dump(null);

        //Assert
        lines.Should().BeEmpty();
    }
}
