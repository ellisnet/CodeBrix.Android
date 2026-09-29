using System;
using System.Linq;
using System.Reflection;
using CodeBrix.Android.Tests.Shared;
using CodeBrix.Platform.UI.Toolkit;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.Toolkit.Tests.Core;

public class CoreContractTests
{
    private const string AndroidName = "CodeBrix.Android.UI.Toolkit";
    private const string ToolkitCore = "CodeBrix.Platform.UI.Toolkit.Core";
    private const BindingFlags Internal = BindingFlags.Instance | BindingFlags.NonPublic;

    [Fact]
    public void The_Toolkit_Core_grants_its_internals_to_the_Android_assembly_name()
    {
        //Arrange
        //Act
        var grants = CoreMetadata.InternalsVisibleTo(ToolkitCore);

        //Assert
        grants.Should().Contain(AndroidName);
        grants.Should().Contain(AndroidName + ".Tests");
    }

    [Fact]
    public void The_Toolkit_Core_ships_the_TriPaneView_engine_types()
    {
        //Arrange
        //Act
        var types = CoreMetadata.TypeNames(ToolkitCore);

        //Assert
        types.Should().Contain("CodeBrix.Platform.UI.Toolkit.Engine.TriPaneLayoutState");
        types.Should().Contain("CodeBrix.Platform.UI.Toolkit.Engine.ITriPaneLayoutHost");
        types.Should().Contain("CodeBrix.Platform.UI.Toolkit.Engine.TriPaneLayoutResult");
        types.Should().Contain("CodeBrix.Platform.UI.Toolkit.TriPaneView");
        types.Should().Contain("CodeBrix.Platform.UI.Toolkit.TriPaneViewDivider");
    }

    [Theory]
    [InlineData("StartDividerDrag", typeof(TriPaneViewDividerKind), typeof(double), typeof(double))]
    [InlineData("UpdateDividerDrag", typeof(TriPaneViewDividerKind), typeof(double))]
    [InlineData("CompleteDividerDrag", typeof(TriPaneViewDividerKind), typeof(double), typeof(bool))]
    public void TriPaneView_has_the_drag_entry_points_the_Android_handler_drives(string name, params Type[] parameters)
    {
        //Arrange
        //Act
        var method = typeof(TriPaneView).GetMethod(name, Internal, parameters);

        //Assert
        method.Should().NotBeNull();
        method.IsAssembly.Should().BeTrue();
    }

    [Theory]
    [InlineData("SidePaneEffectiveWeight")]
    [InlineData("IsSideDividerVisible")]
    [InlineData("IsSideRestoreGripVisible")]
    [InlineData("IsStackDividerVisible")]
    [InlineData("IsStackRestoreGripVisible")]
    public void TriPaneView_has_the_layout_outputs_the_Android_diagnostics_read(string name)
    {
        //Arrange
        //Act
        var property = typeof(TriPaneView).GetProperty(name, Internal);

        //Assert
        property.Should().NotBeNull();
    }

    [Fact]
    public void The_divider_exposes_its_dragging_state_as_a_dependency_property()
    {
        //Arrange
        //Act
        var property = typeof(TriPaneViewDivider).GetProperty(nameof(TriPaneViewDivider.IsDraggingProperty), BindingFlags.Static | BindingFlags.Public);

        //Assert
        property.Should().NotBeNull();
    }

    [Theory]
    [InlineData("RaiseDragStartedFromPlatform")]
    [InlineData("RaiseDragDeltaFromPlatform", typeof(double), typeof(double))]
    [InlineData("RaiseDragCompletedFromPlatform", typeof(bool))]
    public void The_divider_has_the_platform_drag_entry_points_the_Android_handler_raises(string name, params Type[] parameters)
    {
        //Arrange
        //Act
        var method = typeof(TriPaneViewDivider).GetMethod(name, Internal, parameters);

        //Assert
        // AP1.12 (WPE1-13 item e): the inverted canary of AP7-B TriPaneView (The_divider_has_no_platform_drag_entry_points_of_its_own):
        // the Core gained the entry points, and the Android handler raises the divider's own drag events through them.
        method.Should().NotBeNull();
        method.IsAssembly.Should().BeTrue();
    }

    [Fact]
    public void TriPaneView_gives_the_platform_its_dividers_and_its_display_override()
    {
        //Arrange
        //Act
        var getDivider = typeof(TriPaneView).GetMethod("GetDivider", Internal, new[] { typeof(TriPaneViewDividerKind) });
        var displayOverride = typeof(TriPaneView).GetProperty("DisplayOverride", Internal);
        var refresh = typeof(TriPaneView).GetMethod("RefreshDisplayOverride", Internal, Type.EmptyTypes);

        //Assert
        getDivider.Should().NotBeNull();
        displayOverride.Should().NotBeNull();
        displayOverride.PropertyType.FullName.Should().Be("CodeBrix.Platform.UI.Toolkit.Engine.ITriPaneDisplayOverride");
        refresh.Should().NotBeNull();
    }

    [Fact]
    public void The_display_override_contract_has_the_two_members_the_Android_side_implements()
    {
        //Arrange
        var contract = typeof(TriPaneView).Assembly.GetType("CodeBrix.Platform.UI.Toolkit.Engine.ITriPaneDisplayOverride");

        //Act
        var members = contract?.GetMethods().Select(m => m.Name).OrderBy(n => n).ToArray();

        //Assert
        contract.Should().NotBeNull();
        members.Should().BeEquivalentTo(new[] { "GetDisplayWeights", "RestoreRequested" });
    }
}
