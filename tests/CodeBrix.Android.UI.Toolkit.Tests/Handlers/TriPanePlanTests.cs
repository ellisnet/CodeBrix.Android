using CodeBrix.Android.UI.Handlers;
using CodeBrix.Android.UI.Policy;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.Toolkit.Tests.Handlers;

public class TriPanePlanTests
{
    private static readonly TriPaneWeights Default = new(33.3, 66.7, 50, 50);

    private static TriPaneWeights Step(TriPanePlan plan, TriPaneForm form, TriPaneWeights current)
    {
        var target = plan.Apply(form, current);
        plan.Observe(target);
        return target;
    }

    [Fact]
    public void Three_panes_leave_the_weights_alone()
    {
        //Arrange
        var plan = new TriPanePlan();

        //Act
        var target = Step(plan, TriPaneForm.ThreePanes, Default);

        //Assert
        target.Should().Be(Default);
        plan.ClosedByPlan.Should().BeEmpty();
    }

    [Fact]
    public void One_pane_closes_the_side_pane_and_the_lower_pane()
    {
        //Arrange
        var plan = new TriPanePlan();

        //Act
        var target = Step(plan, TriPaneForm.OnePane, Default);

        //Assert
        target.Should().Be(new TriPaneWeights(0, 66.7, 50, 0));
        plan.ClosedByPlan[TriPaneRegion.Side].Should().Be(33.3);
        plan.ClosedByPlan[TriPaneRegion.Lower].Should().Be(50);
    }

    [Fact]
    public void Side_pane_and_one_stacked_closes_only_the_lower_pane()
    {
        //Arrange
        var plan = new TriPanePlan();

        //Act
        var target = Step(plan, TriPaneForm.SidePaneAndOneStacked, Default);

        //Assert
        target.Should().Be(new TriPaneWeights(33.3, 66.7, 50, 0));
    }

    [Fact]
    public void Widening_again_gives_every_closed_region_its_weight_back()
    {
        //Arrange
        var plan = new TriPanePlan();
        var compact = Step(plan, TriPaneForm.OnePane, new TriPaneWeights(25, 75, 60, 40));

        //Act
        var medium = Step(plan, TriPaneForm.SidePaneAndOneStacked, compact);
        var expanded = Step(plan, TriPaneForm.ThreePanes, medium);

        //Assert
        medium.Should().Be(new TriPaneWeights(25, 75, 60, 0));
        expanded.Should().Be(new TriPaneWeights(25, 75, 60, 40));
        plan.ClosedByPlan.Should().BeEmpty();
    }

    [Fact]
    public void Reopening_the_side_pane_in_one_pane_form_closes_the_stack_and_gives_the_side_its_weight()
    {
        //Arrange
        var plan = new TriPanePlan();
        var compact = Step(plan, TriPaneForm.OnePane, Default);

        //Act
        // A restore grip reopens the side pane at the engine's default weight.
        var target = Step(plan, TriPaneForm.OnePane, compact with { Side = 33.333 });

        //Assert
        target.Should().Be(new TriPaneWeights(33.3, 0, 50, 0));
        plan.ClosedByPlan[TriPaneRegion.Stack].Should().Be(66.7);
    }

    [Fact]
    public void Reopening_the_stack_again_closes_the_side_pane()
    {
        //Arrange
        var plan = new TriPanePlan();
        var side = Step(plan, TriPaneForm.OnePane, Step(plan, TriPaneForm.OnePane, Default) with { Side = 33.333 });

        //Act
        var target = Step(plan, TriPaneForm.OnePane, side with { Stack = 66.667 });

        //Assert
        target.Should().Be(new TriPaneWeights(0, 66.7, 50, 0));
    }

    [Fact]
    public void Reopening_the_lower_pane_closes_the_upper_pane()
    {
        //Arrange
        var plan = new TriPanePlan();
        var compact = Step(plan, TriPaneForm.OnePane, Default);

        //Act
        var target = Step(plan, TriPaneForm.OnePane, compact with { Lower = 50 });

        //Assert
        target.Should().Be(new TriPaneWeights(0, 66.7, 0, 50));
    }

    [Fact]
    public void A_region_the_app_closed_itself_is_never_reopened()
    {
        //Arrange
        var plan = new TriPanePlan();
        var appClosedSide = Default with { Side = 0 };

        //Act
        var compact = Step(plan, TriPaneForm.OnePane, appClosedSide);
        var expanded = Step(plan, TriPaneForm.ThreePanes, compact);

        //Assert
        compact.Should().Be(new TriPaneWeights(0, 66.7, 50, 0));
        expanded.Should().Be(new TriPaneWeights(0, 66.7, 50, 50));
    }

    [Fact]
    public void An_app_that_shows_only_the_lower_pane_keeps_it_in_one_pane_form()
    {
        //Arrange
        var plan = new TriPanePlan();

        //Act
        var target = Step(plan, TriPaneForm.OnePane, Default with { Upper = 0 });

        //Assert
        target.Should().Be(new TriPaneWeights(0, 66.7, 0, 50));
    }

    [Fact]
    public void The_plan_never_closes_both_regions_of_a_pair()
    {
        //Arrange
        var plan = new TriPanePlan();

        //Act
        var target = Step(plan, TriPaneForm.OnePane, Default with { Stack = 0 });

        //Assert
        target.Should().Be(new TriPaneWeights(33.3, 0, 50, 0));
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
