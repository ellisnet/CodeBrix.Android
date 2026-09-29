using CodeBrix.Android.UI.Handlers;
using CodeBrix.Android.UI.Policy;
using CodeBrix.Platform.UI.Toolkit;
using CodeBrix.Platform.UI.Toolkit.Engine;
using CodeBrix.Platform.UI.Toolkit.Internal;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.Toolkit.Tests.Engine;

/// <summary>
/// The adaptive plan against the Toolkit Core's ENGINE (TriPaneLayoutState) through the engine's DISPLAY OVERRIDE (AP1.12,
/// WPE1-13 ITriPaneDisplayOverride), wired exactly as TriPaneViewEntryPoints wires it for the handler: the engine asks the
/// plan for the weights to lay out on every state pass and hands it the taps on a hidden region's grip. The host keeps the
/// application's four weights in fields and COUNTS every write: the adaptive form must never write them.
/// </summary>
public class TriPanePlanEngineTests
{
    private sealed class FieldHost : ITriPaneLayoutHost
    {
        private double _side = 33.3;
        private double _stack = 66.7;
        private double _upper = 50;
        private double _lower = 50;

        public int Writes { get; private set; }

        public double SidePanePercent { get => _side; set { _side = value; Writes++; } }

        public double StackPercent { get => _stack; set { _stack = value; Writes++; } }

        public double UpperPanePercent { get => _upper; set { _upper = value; Writes++; } }

        public double LowerPanePercent { get => _lower; set { _lower = value; Writes++; } }

        /// <summary>Sets the application's weights (the app itself, not the adaptive form; not counted).</summary>
        public void SetApplicationWeights(double side, double stack, double upper, double lower)
        {
            (_side, _stack, _upper, _lower) = (side, stack, upper, lower);
        }

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

    /// <summary>The Core display override over the plan (what TriPaneViewEntryPoints.SetDisplayOverride installs).</summary>
    private sealed class PlanOverride : ITriPaneDisplayOverride
    {
        private readonly TriPanePlan _plan;

        internal PlanOverride(TriPanePlan plan) => _plan = plan;

        public TriPaneDisplayWeights GetDisplayWeights(TriPaneDisplayWeights applicationWeights)
        {
            var display = _plan.Display(new TriPaneWeights(applicationWeights.Side, applicationWeights.Stack, applicationWeights.Upper, applicationWeights.Lower));
            return new TriPaneDisplayWeights(display.Side, display.Stack, display.Upper, display.Lower);
        }

        public bool RestoreRequested(TriPaneViewRegion region) => _plan.Restore((TriPaneRegion)(int)region);
    }

    private sealed class Rig
    {
        internal FieldHost Host { get; } = new();

        internal TriPanePlan Plan { get; } = new();

        internal TriPaneLayoutState Engine { get; }

        internal Rig()
        {
            Engine = new TriPaneLayoutState(Host);
            Engine.DisplayOverride = new PlanOverride(Plan);
            Engine.UpdateState();
        }

        /// <summary>What TriPaneViewHandler.Apply does on a new size class: set the form, refresh the override.</summary>
        internal void Apply(TriPaneForm form)
        {
            Plan.Form = form;
            Engine.UpdateState();
        }

        /// <summary>A tap on a restore grip, through the engine's drag entry points (no movement).</summary>
        internal void TapGrip(TriPaneViewDividerKind kind)
        {
            Engine.StartDividerDrag(kind, 0, 1000);
            Engine.CompleteDividerDrag(kind, 0, canceled: false);
        }
    }

    private static readonly TriPaneWeights Default = new(33.3, 66.7, 50, 50);

    [Fact]
    public void One_pane_form_lays_out_the_upper_pane_alone_with_both_dividers_as_restore_grips_and_writes_nothing()
    {
        //Arrange
        var rig = new Rig();

        //Act
        rig.Apply(TriPaneForm.OnePane);

        //Assert
        rig.Host.Minimized.Should().Equal(false, false, false, false);
        rig.Host.Layout.SideWeight.Should().Be(0);
        rig.Host.Layout.LowerWeight.Should().Be(0);
        rig.Host.Layout.IsSideGripVisible.Should().BeTrue();
        rig.Host.Layout.IsSideGripTowardStart.Should().BeTrue();
        rig.Host.Layout.IsStackGripVisible.Should().BeTrue();
        rig.Host.Layout.IsStackGripTowardStart.Should().BeFalse();
        rig.Host.Weights.Should().Be(Default);
        rig.Host.Writes.Should().Be(0);
    }

    [Fact]
    public void Side_pane_and_one_stacked_form_lays_out_two_panes_with_the_stack_divider_as_a_grip()
    {
        //Arrange
        var rig = new Rig();

        //Act
        rig.Apply(TriPaneForm.SidePaneAndOneStacked);

        //Assert
        rig.Host.Minimized.Should().Equal(false, false, false, false);
        rig.Host.Layout.IsSideDividerVisible.Should().BeTrue();
        rig.Host.Layout.IsSideGripVisible.Should().BeFalse();
        rig.Host.Layout.IsStackGripVisible.Should().BeTrue();
        rig.Host.Layout.LowerWeight.Should().Be(0);
        rig.Host.Writes.Should().Be(0);
    }

    [Fact]
    public void Tapping_the_side_grip_in_one_pane_form_switches_to_the_side_pane_and_back_without_writing()
    {
        //Arrange
        var rig = new Rig();
        rig.Apply(TriPaneForm.OnePane);

        //Act
        rig.TapGrip(TriPaneViewDividerKind.Side);
        var sideShown = rig.Host.Layout;
        rig.TapGrip(TriPaneViewDividerKind.Side);

        //Assert
        sideShown.SideWeight.Should().BeGreaterThan(0);
        sideShown.StackWeight.Should().Be(0);
        sideShown.IsSideGripVisible.Should().BeTrue();
        sideShown.IsSideGripTowardStart.Should().BeFalse();
        rig.Host.Layout.SideWeight.Should().Be(0);
        rig.Host.Layout.UpperWeight.Should().BeGreaterThan(0);
        rig.Host.Minimized.Should().Equal(false, false, false, false);
        rig.Host.Weights.Should().Be(Default);
        rig.Host.Writes.Should().Be(0);
    }

    [Fact]
    public void Tapping_the_stack_grip_in_one_pane_form_switches_to_the_lower_pane()
    {
        //Arrange
        var rig = new Rig();
        rig.Apply(TriPaneForm.OnePane);

        //Act
        rig.TapGrip(TriPaneViewDividerKind.Stack);

        //Assert
        rig.Host.Layout.UpperWeight.Should().Be(0);
        rig.Host.Layout.LowerWeight.Should().BeGreaterThan(0);
        rig.Host.Layout.IsStackGripVisible.Should().BeTrue();
        rig.Host.Layout.IsStackGripTowardStart.Should().BeTrue();
        rig.Host.Writes.Should().Be(0);
    }

    [Fact]
    public void The_docked_phone_round_trip_never_touches_the_app_weights()
    {
        //Arrange
        var rig = new Rig();
        rig.Host.SetApplicationWeights(27.5, 72.5, 61, 39);
        rig.Engine.OnWeightChanged();
        rig.Apply(TriPaneForm.ThreePanes);

        //Act
        rig.Apply(TriPaneForm.OnePane);
        rig.TapGrip(TriPaneViewDividerKind.Side);
        rig.TapGrip(TriPaneViewDividerKind.Side);
        rig.Apply(TriPaneForm.SidePaneAndOneStacked);
        rig.Apply(TriPaneForm.ThreePanes);

        //Assert
        rig.Host.Weights.Should().Be(new TriPaneWeights(27.5, 72.5, 61, 39));
        rig.Host.Writes.Should().Be(0);
        rig.Host.Minimized.Should().Equal(false, false, false, false);
        rig.Host.Layout.IsSideGripVisible.Should().BeFalse();
        rig.Host.Layout.IsStackGripVisible.Should().BeFalse();
        rig.Host.Layout.LowerWeight.Should().BeGreaterThan(0);
        rig.Host.Layout.SideWeight.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Under_restore_grip_mode_Never_the_hidden_regions_have_no_grip_and_come_back_when_the_window_widens()
    {
        //Arrange
        var rig = new Rig();
        rig.Host.RestoreGripMode = TriPaneViewRestoreGripMode.Never;

        //Act
        rig.Apply(TriPaneForm.OnePane);
        var compact = rig.Host.Layout;
        rig.Apply(TriPaneForm.ThreePanes);

        //Assert
        compact.IsSideGripVisible.Should().BeFalse();
        compact.IsStackGripVisible.Should().BeFalse();
        rig.Host.Minimized.Should().Equal(false, false, false, false);
        rig.Host.Layout.SideWeight.Should().BeGreaterThan(0);
        rig.Host.Layout.LowerWeight.Should().BeGreaterThan(0);
    }

    [Fact]
    public void A_region_the_app_minimized_itself_stays_minimized_and_keeps_the_app_flags()
    {
        //Arrange
        var rig = new Rig();
        rig.Host.SetApplicationWeights(0, 100, 50, 50);
        rig.Engine.OnWeightChanged();

        //Act
        rig.Apply(TriPaneForm.SidePaneAndOneStacked);

        //Assert
        rig.Host.Minimized[0].Should().BeTrue();
        rig.Host.Layout.SideWeight.Should().Be(0);
        rig.Host.Writes.Should().Be(0);
    }
}
