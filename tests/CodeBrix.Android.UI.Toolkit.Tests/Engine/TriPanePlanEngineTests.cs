using CodeBrix.Android.UI.Handlers;
using CodeBrix.Android.UI.Policy;
using CodeBrix.Platform.UI.Toolkit;
using CodeBrix.Platform.UI.Toolkit.Engine;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.Toolkit.Tests.Engine;

/// <summary>
/// The adaptive plan against the Toolkit Core's ENGINE (TriPaneLayoutState): the handler writes the plan's weights into the
/// control's percent properties, whose change runs the engine's state pass; here a host keeps the weights in fields and
/// the test runs that pass (OnWeightChanged) itself.
/// </summary>
public class TriPanePlanEngineTests
{
    private sealed class FieldHost : ITriPaneLayoutHost
    {
        public double SidePanePercent { get; set; } = 33.3;

        public double StackPercent { get; set; } = 66.7;

        public double UpperPanePercent { get; set; } = 50;

        public double LowerPanePercent { get; set; } = 50;

        public TriPaneViewSidePanePlacement SidePanePlacement { get; set; } = TriPaneViewSidePanePlacement.Left;

        public TriPaneViewRestoreGripMode RestoreGripMode { get; set; } = TriPaneViewRestoreGripMode.Auto;

        public bool IsDragToMinimizeEnabled { get; set; } = true;

        public double SidePaneMinLength => 0;

        public double StackMinLength => 0;

        public double UpperPaneMinLength => 0;

        public double LowerPaneMinLength => 0;

        public bool[] Minimized { get; private set; } = new bool[4];

        public TriPaneLayoutResult Layout { get; private set; }

        public void SyncMinimizedFlags(int version, bool isSideMinimized, bool isStackMinimized, bool isUpperMinimized, bool isLowerMinimized) =>
            Minimized = new[] { isSideMinimized, isStackMinimized, isUpperMinimized, isLowerMinimized };

        public void ApplyLayout(TriPaneLayoutResult layout) => Layout = layout;

        public TriPaneWeights Weights => new(SidePanePercent, StackPercent, UpperPanePercent, LowerPanePercent);
    }

    private sealed class Rig
    {
        internal FieldHost Host { get; } = new();

        internal TriPanePlan Plan { get; } = new();

        internal TriPaneLayoutState Engine { get; }

        internal Rig()
        {
            Engine = new TriPaneLayoutState(Host);
            Engine.UpdateState();
        }

        /// <summary>What TriPaneViewHandler.Apply does: plan, write (opened regions first), observe.</summary>
        internal void Apply(TriPaneForm form)
        {
            var target = Plan.Apply(form, Host.Weights);
            Write(target, open: true);
            Write(target, open: false);
            Plan.Observe(Host.Weights);
        }

        /// <summary>A tap on a restore grip, through the engine's drag entry points (no movement).</summary>
        internal void TapGrip(TriPaneViewDividerKind kind)
        {
            Engine.StartDividerDrag(kind, 0, 1000);
            Engine.CompleteDividerDrag(kind, 0, canceled: false);
        }

        private void Write(TriPaneWeights target, bool open)
        {
            void Set(double current, double wanted, System.Action<double> setter)
            {
                if (!current.Equals(wanted) && TriPaneWeights.IsClosed(wanted) != open)
                {
                    setter(wanted);
                    Engine.OnWeightChanged();
                }
            }

            Set(Host.SidePanePercent, target.Side, v => Host.SidePanePercent = v);
            Set(Host.StackPercent, target.Stack, v => Host.StackPercent = v);
            Set(Host.UpperPanePercent, target.Upper, v => Host.UpperPanePercent = v);
            Set(Host.LowerPanePercent, target.Lower, v => Host.LowerPanePercent = v);
        }
    }

    [Fact]
    public void One_pane_form_leaves_the_upper_pane_alone_on_screen_with_both_dividers_as_restore_grips()
    {
        //Arrange
        var rig = new Rig();

        //Act
        rig.Apply(TriPaneForm.OnePane);

        //Assert
        rig.Host.Minimized.Should().Equal(true, false, false, true);
        rig.Host.Layout.SideWeight.Should().Be(0);
        rig.Host.Layout.LowerWeight.Should().Be(0);
        rig.Host.Layout.IsSideGripVisible.Should().BeTrue();
        rig.Host.Layout.IsSideGripTowardStart.Should().BeTrue();
        rig.Host.Layout.IsStackGripVisible.Should().BeTrue();
        rig.Host.Layout.IsStackGripTowardStart.Should().BeFalse();
    }

    [Fact]
    public void Side_pane_and_one_stacked_form_lays_out_two_panes_with_the_stack_divider_as_a_grip()
    {
        //Arrange
        var rig = new Rig();

        //Act
        rig.Apply(TriPaneForm.SidePaneAndOneStacked);

        //Assert
        rig.Host.Minimized.Should().Equal(false, false, false, true);
        rig.Host.Layout.IsSideDividerVisible.Should().BeTrue();
        rig.Host.Layout.IsSideGripVisible.Should().BeFalse();
        rig.Host.Layout.IsStackGripVisible.Should().BeTrue();
    }

    [Fact]
    public void Tapping_the_side_grip_in_one_pane_form_switches_to_the_side_pane_and_back()
    {
        //Arrange
        var rig = new Rig();
        rig.Apply(TriPaneForm.OnePane);

        //Act
        rig.TapGrip(TriPaneViewDividerKind.Side);
        rig.Apply(TriPaneForm.OnePane);
        var sideShown = rig.Host.Minimized;
        var sideGrip = rig.Host.Layout;
        rig.TapGrip(TriPaneViewDividerKind.Side);
        rig.Apply(TriPaneForm.OnePane);

        //Assert
        sideShown.Should().Equal(false, true, true, true);
        sideGrip.IsSideGripVisible.Should().BeTrue();
        sideGrip.IsSideGripTowardStart.Should().BeFalse();
        rig.Host.Minimized.Should().Equal(true, false, false, true);
        rig.Host.Weights.Should().Be(new TriPaneWeights(0, 66.7, 50, 0));
    }

    [Fact]
    public void Tapping_the_stack_grip_in_one_pane_form_switches_to_the_lower_pane()
    {
        //Arrange
        var rig = new Rig();
        rig.Apply(TriPaneForm.OnePane);

        //Act
        rig.TapGrip(TriPaneViewDividerKind.Stack);
        rig.Apply(TriPaneForm.OnePane);

        //Assert
        rig.Host.Minimized.Should().Equal(true, false, true, false);
        rig.Host.Layout.IsStackGripVisible.Should().BeTrue();
        rig.Host.Layout.IsStackGripTowardStart.Should().BeTrue();
    }

    [Fact]
    public void The_docked_phone_round_trip_gives_the_app_its_exact_weights_back()
    {
        //Arrange
        var rig = new Rig();
        rig.Host.SidePanePercent = 27.5;
        rig.Host.StackPercent = 72.5;
        rig.Host.UpperPanePercent = 61;
        rig.Host.LowerPanePercent = 39;
        rig.Engine.OnWeightChanged();
        rig.Apply(TriPaneForm.ThreePanes);

        //Act
        rig.Apply(TriPaneForm.OnePane);
        rig.TapGrip(TriPaneViewDividerKind.Side);
        rig.Apply(TriPaneForm.OnePane);
        rig.TapGrip(TriPaneViewDividerKind.Side);
        rig.Apply(TriPaneForm.OnePane);
        rig.Apply(TriPaneForm.SidePaneAndOneStacked);
        rig.Apply(TriPaneForm.ThreePanes);

        //Assert
        rig.Host.Weights.Should().Be(new TriPaneWeights(27.5, 72.5, 61, 39));
        rig.Host.Minimized.Should().Equal(false, false, false, false);
        rig.Host.Layout.IsSideGripVisible.Should().BeFalse();
        rig.Host.Layout.IsStackGripVisible.Should().BeFalse();
    }

    [Fact]
    public void Under_restore_grip_mode_Never_the_closed_regions_have_no_grip_and_come_back_when_the_window_widens()
    {
        //Arrange
        var rig = new Rig();
        rig.Host.RestoreGripMode = TriPaneViewRestoreGripMode.Never;

        //Act
        rig.Apply(TriPaneForm.OnePane);
        var compact = rig.Host.Layout;
        rig.Apply(TriPaneForm.ThreePanes);

        //Assert
        compact.IsSideDividerVisible.Should().BeFalse();
        compact.IsStackDividerVisible.Should().BeFalse();
        rig.Host.Minimized.Should().Equal(false, false, false, false);
    }
}
