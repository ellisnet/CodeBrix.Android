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

    [Fact]
    public void The_divider_has_no_platform_drag_entry_points_of_its_own()
    {
        //Arrange
        //Act
        var names = typeof(TriPaneViewDivider).GetMethods(Internal | BindingFlags.Public).Select(m => m.Name).ToList();

        //Assert
        // FIXLIST [AP7-B TriPaneView] PLATFORM: when the Core gains RaiseDrag*FromPlatform (as Thumb has), the Android
        // handler should raise the divider's own events through them; this test then fails on purpose.
        names.Should().NotContain("RaiseDragStartedFromPlatform");
    }
}
