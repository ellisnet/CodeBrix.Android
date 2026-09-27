using System.IO;
using System.Linq;
using CodeBrix.Android.ParityScore.Reporting;
using CodeBrix.Android.ParityScore.Scanning;
using CodeBrix.Android.ParityScore.Tests.Fixture;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.ParityScore.Tests.Reporting;

public class ParityReportTests
{
    private static (string Folder, string Summary) WriteFixtureReport()
    {
        using var set = FixtureSupport.OpenSet();
        var notImplemented = new NotImplementedScanner(FixtureSupport.Conventions).Scan(set.Assemblies);
        var model = new HandlerScanner(FixtureSupport.Conventions, set).Scan(set.Assemblies);
        var declined = new DeclinedCalculator(FixtureSupport.Conventions, set).Compute(model, ExplainedList.Parse(new string[0]));
        var folder = FixtureSupport.TempDirectory();
        var summary = ParityReport.Write(folder, new[] { "pin test" }, notImplemented, 10, declined);
        return (folder, summary);
    }

    [Fact]
    public void Write_creates_every_report_file_with_a_header_line()
    {
        //Act
        var (folder, _) = WriteFixtureReport();

        //Assert
        foreach (var file in new[] { ParityReport.NotImplementedFile, ParityReport.NotImplementedMembersFile, ParityReport.DeclinedFile, ParityReport.DeclinedPropertiesFile, ParityReport.TemplatedFile, ParityReport.SummaryFile })
        {
            File.Exists(Path.Combine(folder, file)).Should().BeTrue(file);
        }

        File.ReadLines(Path.Combine(folder, ParityReport.DeclinedFile)).First().Should().Be("element\thandler\tmapper\tscope\tmapped\texplained\tdeclined");
    }

    [Fact]
    public void Summary_carries_the_labels_and_both_scores()
    {
        //Act
        var (_, summary) = WriteFixtureReport();

        //Assert
        summary.Should().Contain("pin test");
        summary.Should().Contain("(a) Core NotImplemented");
        summary.Should().Contain("(b) Android declined list");
    }

    [Fact]
    public void Declined_properties_file_lists_the_declined_status()
    {
        //Act
        var (folder, _) = WriteFixtureReport();

        //Assert
        File.ReadAllLines(Path.Combine(folder, ParityReport.DeclinedPropertiesFile))
            .Should().Contain(l => l.Contains("ParityFixture.Xaml.Button.FlyoutProperty\tdeclined"));
    }
}
