using System;
using System.Linq;
using System.Reflection;
using CodeBrix.Android.Tests.Shared;
using CodeBrix.Platform.UI.AdvancedTextEdit.Editing;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.AdvancedTextEdit.Tests.Core;

public class CoreContractTests
{
    private const string AndroidName = "CodeBrix.Android.UI.AdvancedTextEdit";
    private const string EditorCore = "CodeBrix.Platform.UI.AdvancedTextEdit.Core";
    private const string CanvasContract = "CodeBrix.Platform.UI.AdvancedTextEdit.Contracts.IRenderCanvasPlatform";

    [Fact]
    public void The_AdvancedTextEdit_Core_grants_its_internals_to_the_Android_assembly_name()
    {
        //Arrange
        //Act
        var grants = CoreMetadata.InternalsVisibleTo(EditorCore);

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
    public void The_AdvancedTextEdit_Core_loads_the_Android_assembly_by_name_for_its_canvas()
    {
        //Arrange
        //Act
        var names = CoreMetadata.UserStrings(EditorCore);

        //Assert
        names.Should().Contain(AndroidName);
    }

    [Fact]
    public void The_AdvancedTextEdit_Core_ships_the_canvas_contract_the_surfaces_and_the_engine_types()
    {
        //Arrange
        //Act
        var types = CoreMetadata.TypeNames(EditorCore);

        //Assert
        types.Should().Contain(CanvasContract);
        types.Should().Contain("CodeBrix.Platform.UI.AdvancedTextEdit.Rendering.Internal.RenderCanvasSupply");
        types.Should().Contain("CodeBrix.Platform.UI.AdvancedTextEdit.AdvancedTextEdit");
        types.Should().Contain("CodeBrix.Platform.UI.AdvancedTextEdit.Editing.TextArea");
        types.Should().Contain("CodeBrix.Platform.UI.AdvancedTextEdit.Rendering.TextView");
        types.Should().Contain("CodeBrix.Platform.UI.AdvancedTextEdit.Editing.LineNumberMargin");
        types.Should().Contain("CodeBrix.Platform.UI.AdvancedTextEdit.Folding.FoldingMargin");
        types.Should().Contain("CodeBrix.Platform.UI.AdvancedTextEdit.Engine.HighlightingValues");
        types.Should().Contain("CodeBrix.Platform.UI.AdvancedTextEdit.Engine.FontFamilyValue");
        types.Should().Contain("CodeBrix.Platform.UI.AdvancedTextEdit.Engine.IFoldingHost");
        types.Should().Contain("CodeBrix.Platform.UI.AdvancedTextEdit.Engine.CompletionFilter");
    }

    [Fact]
    public void The_canvas_contract_is_the_four_members_the_Android_canvas_supply_implements()
    {
        //Arrange
        var contract = typeof(TextArea).Assembly.GetType(CanvasContract, throwOnError: true);

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
    public void The_AdvancedTextEdit_Core_is_built_on_the_text_engine_and_Skia_and_not_on_its_Skia_twin()
    {
        //Arrange
        //Act
        var references = CoreMetadata.References(EditorCore);

        //Assert
        references.Should().Contain("CodeBrix.Platform.UI.TextLayout.Core");
        references.Should().Contain("SkiaSharp");
        references.Should().NotContain("CodeBrix.Platform.UI.AdvancedTextEdit");
    }

    [Fact]
    public void The_UI_Core_has_the_software_keyboard_seam_the_text_area_reports_its_focus_through()
    {
        //Arrange
        //Act
        var types = CoreMetadata.TypeNames("CodeBrix.Platform.UI.Core");
        var references = CoreMetadata.References(EditorCore);

        //Assert
        types.Should().Contain("CodeBrix.Platform.UI.Xaml.Controls.Extensions.SoftwareKeyboardFocus");
        types.Should().Contain("CodeBrix.Platform.UI.Xaml.Controls.Extensions.ITextInputFocusNotificationsSingleton");
        references.Should().Contain("CodeBrix.Platform.UI.Core");
    }

    [Fact]
    public void The_TextArea_reads_keys_and_focus_through_its_overrides_and_types_through_PerformTextInput()
    {
        //Arrange
        const BindingFlags declared = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        //Act
        var overrides = typeof(TextArea).GetMethods(declared).Select(m => m.Name).ToList();
        var typing = typeof(TextArea).GetMethod(nameof(TextArea.PerformTextInput), new[] { typeof(string) });

        //Assert
        overrides.Should().Contain("OnKeyDown");
        overrides.Should().Contain("OnGotFocus");
        overrides.Should().Contain("OnLostFocus");
        overrides.Should().Contain("OnPointerPressed");
        typing.Should().NotBeNull();
        typeof(TextArea).GetEvent(nameof(TextArea.TextEntered)).Should().NotBeNull();
        typeof(TextArea).GetEvent(nameof(TextArea.SelectionChanged)).Should().NotBeNull();
    }

    [Fact]
    public void The_Core_members_the_soft_keyboards_text_target_uses_are_there()
    {
        //Arrange
        var core = typeof(TextArea).Assembly;
        const BindingFlags any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        //Act
        var caret = core.GetType("CodeBrix.Platform.UI.AdvancedTextEdit.Editing.Caret", throwOnError: true);
        var document = core.GetType("CodeBrix.Platform.UI.AdvancedTextEdit.Document.TextDocument", throwOnError: true);
        var handler = core.GetType("CodeBrix.Platform.UI.AdvancedTextEdit.Editing.TextAreaInputHandler", throwOnError: true);
        var commands = core.GetType("CodeBrix.Platform.UI.AdvancedTextEdit.Editing.EditorCommands", throwOnError: true);
        var geometry = core.GetType("CodeBrix.Platform.UI.AdvancedTextEdit.Rendering.BackgroundGeometryBuilder", throwOnError: true);
        var runProperties = core.GetType("CodeBrix.Platform.UI.AdvancedTextEdit.Rendering.VisualLineElementTextRunProperties", throwOnError: true);

        //Assert
        caret.GetEvent("PositionChanged").Should().NotBeNull();
        caret.GetProperty("Offset").Should().NotBeNull();
        typeof(TextArea).GetProperty("ReadOnlySectionProvider").Should().NotBeNull();
        typeof(TextArea).GetProperty("ActiveInputHandler").Should().NotBeNull();
        typeof(TextArea).GetEvent("DocumentChanged").Should().NotBeNull();
        typeof(TextArea).GetMethod("ClearSelection").Should().NotBeNull();
        document.GetEvent("Changed").Should().NotBeNull();
        document.GetMethod("BeginUpdate").Should().NotBeNull();
        document.GetMethod("EndUpdate").Should().NotBeNull();
        document.GetMethod("Replace", new[] { typeof(int), typeof(int), typeof(string) }).Should().NotBeNull();
        handler.GetMethod("ExecuteCommand").Should().NotBeNull();
        new[] { "SelectAll", "Cut", "Copy", "Paste" }.Select(n => commands.GetField(n, any)).Should().NotContainNulls();
        geometry.GetMethod("GetRectsForSegment").Should().NotBeNull();
        runProperties.GetMethod("GetSolidColor", any).Should().NotBeNull();
    }
}
