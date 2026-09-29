using System;
using System.Linq;
using System.Reflection;
using CodeBrix.Android.Tests.Shared;
using CodeBrix.Platform.UI.PlotterView;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.PlotterView.Tests.Core;

public class CoreContractTests
{
    private const string AndroidName = "CodeBrix.Android.UI.PlotterView";
    private const string PlotterCore = "CodeBrix.Platform.UI.PlotterView.Core";
    private const string CanvasContract = "CodeBrix.Platform.UI.PlotterView.Contracts.IRenderCanvasPlatform";
    private const string ContractLoader = "CodeBrix.Platform.UI.PlotterView.Contracts.PlatformContract";

    [Fact]
    public void The_PlotterView_Core_grants_its_internals_to_the_Android_assembly_name()
    {
        //Arrange
        //Act
        var grants = CoreMetadata.InternalsVisibleTo(PlotterCore);

        //Assert
        grants.Should().Contain(AndroidName);
        grants.Should().Contain(AndroidName + ".Tests");
    }

    [Fact]
    public void The_UI_Core_grants_its_internals_to_the_Android_assembly_name()
    {
        //Arrange
        //Act
        var grants = CoreMetadata.InternalsVisibleTo("CodeBrix.Platform.UI.Core");

        //Assert
        grants.Should().Contain(AndroidName);
    }

    [Fact]
    public void The_PlotterView_Core_loads_the_Android_assembly_by_name_for_its_canvas()
    {
        //Arrange
        var loader = typeof(PlotterControl).Assembly.GetType(ContractLoader, throwOnError: true);

        //Act
        var names = (string[])loader.GetField("PlatformAssemblyNames", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);

        //Assert
        names.Should().Contain(AndroidName);
    }

    [Fact]
    public void The_PlotterView_Core_finds_the_font_source_in_the_TextLayout_add_in_the_Android_twin_depends_on()
    {
        //Arrange
        var loader = typeof(PlotterControl).Assembly.GetType(ContractLoader, throwOnError: true);

        //Act
        var names = (string[])loader.GetField("FontSourceAssemblyNames", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);

        //Assert
        names.Should().Contain("CodeBrix.Android.UI.TextLayout");
        names.Should().Contain(AndroidName);
    }

    [Fact]
    public void The_PlotterView_Core_ships_the_canvas_contract_and_the_engine_entry_type()
    {
        //Arrange
        //Act
        var types = CoreMetadata.TypeNames(PlotterCore);

        //Assert
        types.Should().Contain(CanvasContract);
        types.Should().Contain("CodeBrix.Platform.UI.PlotterView.PlotterControl");
        types.Should().Contain("CodeBrix.Platform.UI.PlotterView.Engine.PlotHost");
        types.Should().Contain("CodeBrix.Platform.UI.PlotterView.Input.TouchGestureTracker");
    }

    [Fact]
    public void The_canvas_contract_is_the_four_members_the_Android_canvas_supply_implements()
    {
        //Arrange
        var contract = typeof(PlotterControl).Assembly.GetType(CanvasContract, throwOnError: true);

        //Act
        var members = contract.GetMethods().Select(m => $"{m.Name}({string.Join(",", m.GetParameters().Select(p => p.ParameterType.Name))}):{m.ReturnType.Name}").OrderBy(s => s, StringComparer.Ordinal).ToList();

        //Assert
        members.Should().Equal(
            "AddPaintHandler(FrameworkElement,Action`2):Void",
            "CreateRenderCanvas():FrameworkElement",
            "GetScale(FrameworkElement):Double",
            "Invalidate(FrameworkElement):Void");
    }

    [Fact]
    public void The_PlotterView_Core_is_built_on_the_chart_engine_and_Skia_and_not_on_the_text_engine()
    {
        //Arrange
        //Act
        var references = CoreMetadata.References(PlotterCore);

        //Assert
        references.Should().Contain("CodeBrix.Plotter");
        references.Should().Contain("SkiaSharp");
        references.Should().NotContain("CodeBrix.Platform.UI.TextLayout.Core");
        references.Should().NotContain("CodeBrix.Platform.UI.PlotterView");
    }

    [Fact]
    public void The_PlotterView_Core_is_built_against_the_chart_engine_with_the_touch_pan_and_pinch()
    {
        //Arrange
        //Act
        var plotter = typeof(PlotterControl).Assembly.GetReferencedAssemblies().Single(a => a.Name == "CodeBrix.Plotter");

        //Assert
        plotter.Version.Should().BeGreaterThanOrEqualTo(new Version(1, 0, 269, 1424), "D7 (touch pan/pinch) is in CodeBrix.Plotter 1.0.269.1424");
    }

    [Fact]
    public void The_PlotterControl_reads_pointer_and_touch_input_through_its_canvas_handlers()
    {
        //Arrange
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        //Act
        var handlers = typeof(PlotterControl).GetMethods(flags).Select(m => m.Name).ToList();

        //Assert
        handlers.Should().Contain("OnCanvasPointerPressed");
        handlers.Should().Contain("OnCanvasPointerMoved");
        handlers.Should().Contain("OnCanvasPointerReleased");
        handlers.Should().Contain("OnCanvasPointerWheelChanged");
        handlers.Should().Contain("OnCanvasPointerLost");
    }
}
