using System.Linq;
using CodeBrix.Android.ParityScore.Scanning;
using CodeBrix.Android.ParityScore.Tests.Fixture;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.ParityScore.Tests.Scanning;

public class DeclinedCalculatorTests
{
    private static DeclinedResult Compute(params string[] explainedLines)
    {
        using var set = FixtureSupport.OpenSet();
        var model = new HandlerScanner(FixtureSupport.Conventions, set).Scan(set.Assemblies);
        return new DeclinedCalculator(FixtureSupport.Conventions, set).Compute(model, ExplainedList.Parse(explainedLines));
    }

    [Fact]
    public void Unmapped_property_of_a_native_element_is_declined()
    {
        //Act
        var result = Compute();

        //Assert
        result.Rows.Single(r => r.Element == "ParityFixture.Xaml.Button" && r.Property == "ParityFixture.Xaml.Button.FlyoutProperty")
            .Status.Should().Be(PropertyStatus.Declined);
    }

    [Fact]
    public void Mapped_properties_come_from_the_whole_chain()
    {
        //Act
        var result = Compute();

        //Assert
        var mapped = result.Rows.Where(r => r.Element == "ParityFixture.Xaml.Button" && r.Status == PropertyStatus.Mapped).Select(r => r.Property);
        mapped.Should().Contain(new[] { "ParityFixture.Xaml.Button.ContentProperty", "ParityFixture.Xaml.Control.ForegroundProperty" });
    }

    [Fact]
    public void Explained_property_is_not_declined()
    {
        //Act
        var result = Compute("ParityFixture.Xaml.Control\tTemplateProperty\tcore-template\tNot used by native handlers.");

        //Assert
        var row = result.Rows.Single(r => r.Element == "ParityFixture.Xaml.Button" && r.Property == "ParityFixture.Xaml.Control.TemplateProperty");
        row.Status.Should().Be(PropertyStatus.Explained);
        row.Note.Should().StartWith("core-template");
    }

    [Fact]
    public void Scope_stops_below_the_base_element_types()
    {
        //Act
        var result = Compute();

        //Assert
        var button = result.Summaries.Single(s => s.Element == "ParityFixture.Xaml.Button");
        button.Scope.Should().Be(4);
        result.Rows.Where(r => r.Element == "ParityFixture.Xaml.Button").Select(r => r.Property)
            .Should().NotContain("ParityFixture.Xaml.FrameworkElement.WidthProperty");
    }

    [Fact]
    public void Base_row_counts_properties_every_native_handler_maps()
    {
        //Act
        var result = Compute("*\tWidthProperty\tcore-layout\tCore measures.");

        //Assert
        var baseRows = result.Rows.Where(r => r.Element == DeclinedCalculator.BaseRow).ToDictionary(r => r.Property, r => r.Status);
        baseRows["ParityFixture.Xaml.UIElement.OpacityProperty"].Should().Be(PropertyStatus.Mapped);
        baseRows["ParityFixture.Xaml.FrameworkElement.WidthProperty"].Should().Be(PropertyStatus.Explained);
        baseRows["ParityFixture.Xaml.UIElement.IsHitTestVisibleProperty"].Should().Be(PropertyStatus.Declined);
    }

    [Fact]
    public void Templated_registrations_are_listed_apart()
    {
        //Act
        var result = Compute();

        //Assert
        result.Templated.Select(r => r.ElementType).Should().Contain(new[] { "ParityFixture.Xaml.Control", "ParityFixture.Xaml.FrameworkElement" });
        result.Summaries.Select(s => s.Element).Should().NotContain("ParityFixture.Xaml.Control");
    }
}
