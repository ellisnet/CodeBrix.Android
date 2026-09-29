using CodeBrix.Android.UI.Handlers;
using CodeBrix.Android.UI.Policy;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.Toolkit.Tests.Handlers;

/// <summary>AP1.12: the adaptive form as DISPLAY weights (the answer of the Core's display override; nothing is written).</summary>
public class TriPanePlanTests
{
    private static readonly TriPaneWeights Default = new(33.3, 66.7, 50, 50);

    private static TriPanePlan Plan(TriPaneForm? form) => new() { Form = form };

    [Fact]
    public void Without_a_form_the_application_weights_are_displayed()
    {
        //Arrange
        var plan = Plan(null);

        //Act
        var display = plan.Display(Default);

        //Assert
        display.Should().Be(Default);
    }

    [Fact]
    public void Three_panes_display_the_application_weights()
    {
        //Arrange
        var plan = Plan(TriPaneForm.ThreePanes);

        //Act
        var display = plan.Display(Default);

        //Assert
        display.Should().Be(Default);
    }

    [Fact]
    public void One_pane_hides_the_side_pane_and_the_lower_pane()
    {
        //Arrange
        var plan = Plan(TriPaneForm.OnePane);

        //Act
        var display = plan.Display(Default);

        //Assert
        display.Should().Be(new TriPaneWeights(0, 66.7, 50, 0));
    }

    [Fact]
    public void Side_pane_and_one_stacked_hides_only_the_lower_pane()
    {
        //Arrange
        var plan = Plan(TriPaneForm.SidePaneAndOneStacked);

        //Act
        var display = plan.Display(Default);

        //Assert
        display.Should().Be(new TriPaneWeights(33.3, 66.7, 50, 0));
    }

    [Fact]
    public void Widening_again_displays_the_application_weights_unchanged()
    {
        //Arrange
        var plan = Plan(TriPaneForm.OnePane);
        plan.Display(Default);
        plan.Restore(TriPaneRegion.Side);
        plan.Display(Default);

        //Act
        plan.Form = TriPaneForm.ThreePanes;
        var display = plan.Display(Default);

        //Assert
        display.Should().Be(Default);
    }

    [Fact]
    public void Restoring_the_side_pane_in_one_pane_form_hides_the_stack()
    {
        //Arrange
        var plan = Plan(TriPaneForm.OnePane);
        plan.Display(Default);

        //Act
        var changed = plan.Restore(TriPaneRegion.Side);
        var display = plan.Display(Default);

        //Assert
        changed.Should().BeTrue();
        display.Should().Be(new TriPaneWeights(33.3, 0, 50, 0));
    }

    [Fact]
    public void Restoring_the_stack_again_hides_the_side_pane_and_shows_the_stacked_pane_it_had()
    {
        //Arrange
        var plan = Plan(TriPaneForm.OnePane);
        plan.Restore(TriPaneRegion.Lower);
        plan.Restore(TriPaneRegion.Side);

        //Act
        plan.Restore(TriPaneRegion.Stack);
        var display = plan.Display(Default);

        //Assert
        display.Should().Be(new TriPaneWeights(0, 66.7, 0, 50));
    }

    [Fact]
    public void Restoring_the_lower_pane_hides_the_upper_pane()
    {
        //Arrange
        var plan = Plan(TriPaneForm.SidePaneAndOneStacked);

        //Act
        var changed = plan.Restore(TriPaneRegion.Lower);
        var display = plan.Display(Default);

        //Assert
        changed.Should().BeTrue();
        display.Should().Be(new TriPaneWeights(33.3, 66.7, 0, 50));
    }

    [Fact]
    public void A_restore_in_three_panes_form_changes_nothing()
    {
        //Arrange
        var plan = Plan(TriPaneForm.ThreePanes);

        //Act
        var changed = plan.Restore(TriPaneRegion.Side);

        //Assert
        changed.Should().BeFalse();
        plan.Display(Default).Should().Be(Default);
    }

    [Fact]
    public void A_region_the_app_closed_itself_stays_closed_in_every_form()
    {
        //Arrange
        var closedSide = Default with { Side = 0 };

        //Act
        var one = Plan(TriPaneForm.OnePane).Display(closedSide);
        var medium = Plan(TriPaneForm.SidePaneAndOneStacked).Display(closedSide);
        var three = Plan(TriPaneForm.ThreePanes).Display(closedSide);

        //Assert
        one.Side.Should().Be(0);
        medium.Side.Should().Be(0);
        three.Side.Should().Be(0);
    }

    [Fact]
    public void An_app_that_shows_only_the_lower_pane_keeps_it_in_one_pane_form()
    {
        //Arrange
        var plan = Plan(TriPaneForm.OnePane);

        //Act
        var display = plan.Display(new TriPaneWeights(0, 100, 0, 100));

        //Assert
        display.Should().Be(new TriPaneWeights(0, 100, 0, 100));
    }

    [Fact]
    public void The_region_the_app_opened_last_is_the_one_shown()
    {
        //Arrange
        var plan = Plan(TriPaneForm.SidePaneAndOneStacked);
        plan.Display(Default with { Lower = 0 });

        //Act
        var display = plan.Display(Default);

        //Assert
        display.Should().Be(new TriPaneWeights(33.3, 66.7, 0, 50));
        plan.StackAxisChoice.Should().Be(TriPaneRegion.Lower);
    }

    [Fact]
    public void The_plan_never_hides_both_regions_of_a_pair()
    {
        //Arrange
        var plan = Plan(TriPaneForm.OnePane);

        //Act
        var display = plan.Display(Default with { Stack = 0 });

        //Assert
        display.Should().Be(new TriPaneWeights(33.3, 0, 50, 0));
    }

    [Theory]
    [InlineData(0d, true)]
    [InlineData(-1d, true)]
    [InlineData(double.NaN, true)]
    [InlineData(0.01d, false)]
    public void A_weight_is_closed_the_way_the_engine_reads_it(double weight, bool closed)
    {
        //Arrange
        //Act
        var result = TriPaneWeights.IsClosed(weight);

        //Assert
        result.Should().Be(closed);
    }
}
