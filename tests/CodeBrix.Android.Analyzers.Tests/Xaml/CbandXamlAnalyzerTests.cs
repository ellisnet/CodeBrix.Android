using System.Collections.Generic;
using System.Linq;
using CodeBrix.Android.Analyzers.Tests.Support;
using CodeBrix.Android.Analyzers.Xaml;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.Analyzers.Tests.Xaml;

public class CbandXamlAnalyzerTests
{
    private const string Page = """
        <Page xmlns="clr-namespace:Microsoft.UI.Xaml.Controls;assembly=CodeBrix.Platform.UI">
          <ScrollViewer ZoomMode="Enabled" />
        </Page>
        """;

    [Fact]
    public void Xaml_finding_is_reported_at_its_file_and_line()
    {
        //Act
        var diagnostics = AnalyzerHarness.Run(AnalyzerHarness.Compile(true), new[] { ("/app/Views/MainPage.xaml", Page) });

        //Assert
        diagnostics.Should().HaveCount(1);
        var span = diagnostics[0].Location.GetLineSpan();
        diagnostics[0].Id.Should().Be("CBAND0005");
        span.Path.Should().Be("/app/Views/MainPage.xaml");
        span.StartLinePosition.Line.Should().Be(1);
        span.StartLinePosition.Character.Should().Be(16);
    }

    [Fact]
    public void Non_xaml_additional_files_are_ignored()
    {
        //Act
        var diagnostics = AnalyzerHarness.Run(AnalyzerHarness.Compile(true), new[] { ("/app/notes.txt", Page) });

        //Assert
        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void Scan_property_false_turns_the_scan_off()
    {
        //Act
        var diagnostics = AnalyzerHarness.Run(
            AnalyzerHarness.Compile(true),
            new[] { ("/app/Views/MainPage.xaml", Page) },
            new Dictionary<string, string> { [CbandXamlAnalyzer.ScanProperty] = "false" });

        //Assert
        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void Compilation_without_the_platform_skips_the_scan()
    {
        //Act
        var diagnostics = AnalyzerHarness.Run(AnalyzerHarness.Compile(false, "class C { }"), new[] { ("/app/Views/MainPage.xaml", Page) });

        //Assert
        diagnostics.Select(d => d.Id).Should().BeEmpty();
    }
}
