using System;
using System.Linq;
using System.Reflection;
using CodeBrix.Android.Tests.Shared;
using CodeBrix.Platform.UI.TerminalView;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.TerminalView.Tests.Core;

public class CoreContractTests
{
    private const string AndroidName = "CodeBrix.Android.UI.TerminalView";
    private const string TerminalCore = "CodeBrix.Platform.UI.TerminalView.Core";
    private const string CanvasContract = "CodeBrix.Platform.UI.TerminalView.Contracts.IRenderCanvasPlatform";

    [Fact]
    public void The_TerminalView_Core_grants_its_internals_to_the_Android_assembly_name()
    {
        //Arrange
        //Act
        var grants = CoreMetadata.InternalsVisibleTo(TerminalCore);

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
    public void The_TerminalView_Core_loads_the_Android_assembly_by_name_for_its_canvas()
    {
        //Arrange
        //Act
        var names = CoreMetadata.UserStrings(TerminalCore);

        //Assert
        names.Should().Contain(AndroidName);
    }

    [Fact]
    public void The_TerminalView_Core_ships_the_canvas_contract_and_the_engine_entry_types()
    {
        //Arrange
        //Act
        var types = CoreMetadata.TypeNames(TerminalCore);

        //Assert
        types.Should().Contain(CanvasContract);
        types.Should().Contain("CodeBrix.Platform.UI.TerminalView.TerminalControl");
        types.Should().Contain("CodeBrix.Platform.UI.TerminalView.Engine.TerminalRenderer");
        types.Should().Contain("CodeBrix.Platform.UI.TerminalView.Engine.TerminalInputEncoder");
        types.Should().Contain("CodeBrix.Platform.UI.TerminalView.Engine.TerminalModifierKey");
        types.Should().Contain("CodeBrix.Platform.UI.TerminalView.Engine.TerminalKeyCommand");
        types.Should().Contain("CodeBrix.Platform.UI.TerminalView.Input.VirtualKeyMapper");
    }

    [Fact]
    public void The_canvas_contract_is_the_four_members_the_Android_canvas_supply_implements()
    {
        //Arrange
        var contract = typeof(TerminalControl).Assembly.GetType(CanvasContract, throwOnError: true);

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
    public void The_TerminalView_Core_is_built_on_the_VT_engine_the_text_engine_and_Skia()
    {
        //Arrange
        //Act
        var references = CoreMetadata.References(TerminalCore);

        //Assert
        references.Should().Contain("CodeBrix.Terminal");
        references.Should().Contain("CodeBrix.Platform.UI.TextLayout.Core");
        references.Should().Contain("SkiaSharp");
        references.Should().NotContain("CodeBrix.Platform.UI.TerminalView");
    }

    [Fact]
    public void The_UI_Core_has_the_software_keyboard_seam_the_terminal_reports_its_focus_through()
    {
        //Arrange
        //Act
        var types = CoreMetadata.TypeNames("CodeBrix.Platform.UI.Core");

        //Assert
        types.Should().Contain("CodeBrix.Platform.UI.Xaml.Controls.Extensions.SoftwareKeyboardFocus");
        types.Should().Contain("CodeBrix.Platform.UI.Xaml.Controls.Extensions.ITextInputFocusNotificationsSingleton");
    }

    [Fact]
    public void The_TerminalControl_reads_keys_through_its_OnKeyDown_and_OnKeyUp_overrides()
    {
        //Arrange
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        //Act
        var overrides = typeof(TerminalControl).GetMethods(flags).Select(m => m.Name).ToList();

        //Assert
        overrides.Should().Contain("OnKeyDown");
        overrides.Should().Contain("OnKeyUp");
        overrides.Should().Contain("OnGotFocus");
        overrides.Should().Contain("OnLostFocus");
    }

    [Fact]
    public void The_TerminalControl_exposes_its_caret_cell_and_its_changes_to_the_platform()
    {
        //Arrange
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        //Act
        var caret = typeof(TerminalControl).GetMethod("GetCaretRectForPlatform", flags, Type.EmptyTypes);
        var changed = typeof(TerminalControl).GetEvent("CaretRectChangedForPlatform", flags);

        //Assert
        caret.Should().NotBeNull("the Android TerminalCaret reads the cursor cell through it (WPE1-18 seam)");
        caret.ReturnType.Should().Be(typeof(Windows.Foundation.Rect));
        changed.Should().NotBeNull("the Android TerminalCaret follows the cursor through it (WPE1-18 seam)");
        changed.EventHandlerType.Should().Be(typeof(EventHandler));
    }
}
