using System.Linq;
using CodeBrix.Android.Analyzers.Tests.Support;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.Analyzers.Tests.CSharp;

public class CbandCSharpAnalyzerTests
{
    private static string[] Ids(string body, string members = "") =>
        AnalyzerHarness.RunCSharp($$"""
            using Microsoft.UI.Xaml;
            using Microsoft.UI.Xaml.Controls;
            using Microsoft.UI.Xaml.Media;
            class Page
            {
                {{members}}
                void Run()
                {
                    {{body}}
                }
            }
            """).Select(d => d.Id).ToArray();

    [Fact]
    public void Template_assignment_on_a_button_reports_cband0001()
    {
        //Act
        var ids = Ids("var b = new Button(); b.Template = new ControlTemplate();");

        //Assert
        ids.Should().Equal("CBAND0001");
    }

    [Fact]
    public void Template_in_an_object_initializer_reports_cband0001()
    {
        //Act
        var ids = Ids("var b = new Button { Template = new ControlTemplate() };");

        //Assert
        ids.Should().Equal("CBAND0001");
    }

    [Fact]
    public void Template_assignment_on_a_user_control_is_silent()
    {
        //Act
        var ids = Ids("var u = new UserControl(); u.Template = new ControlTemplate();");

        //Assert
        ids.Should().BeEmpty();
    }

    [Fact]
    public void Template_cleared_with_null_is_silent()
    {
        //Act
        var ids = Ids("var b = new Button(); b.Template = null;");

        //Assert
        ids.Should().BeEmpty();
    }

    [Fact]
    public void Set_value_of_template_property_on_a_button_reports_cband0001()
    {
        //Act
        var ids = Ids("var b = new Button(); b.SetValue(Control.TemplateProperty, new ControlTemplate());");

        //Assert
        ids.Should().Equal("CBAND0001");
    }

    [Fact]
    public void Template_members_in_a_button_subclass_report_cband0002()
    {
        //Act
        var diagnostics = AnalyzerHarness.RunCSharp("""
            using Microsoft.UI.Xaml.Controls;
            class FancyButton : Button
            {
                protected override void OnApplyTemplate()
                {
                    var part = GetTemplateChild("Part");
                }
            }
            """);

        //Assert
        diagnostics.Select(d => d.Id).Should().Equal("CBAND0002", "CBAND0002");
    }

    [Fact]
    public void Template_members_in_a_user_control_subclass_are_silent()
    {
        //Act
        var diagnostics = AnalyzerHarness.RunCSharp("""
            using Microsoft.UI.Xaml.Controls;
            class Card : UserControl
            {
                protected override void OnApplyTemplate()
                {
                    var part = GetTemplateChild("Part");
                }
            }
            """);

        //Assert
        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void Go_to_state_on_a_button_reports_cband0002()
    {
        //Act
        var ids = Ids("VisualStateManager.GoToState(new Button(), \"Pressed\", true);");

        //Assert
        ids.Should().Equal("CBAND0002");
    }

    [Fact]
    public void Composition_apis_report_cband0003()
    {
        //Act
        var ids = Ids("var v = Microsoft.UI.Xaml.Hosting.ElementCompositionPreview.GetElementVisual(new Grid()); var c = new Microsoft.UI.Composition.Compositor();");

        //Assert
        ids.Should().Equal("CBAND0003", "CBAND0003");
    }

    [Fact]
    public void Theme_shadow_reports_cband0003_once()
    {
        //Act
        var ids = Ids("var g = new Grid(); g.Shadow = new ThemeShadow();");

        //Assert
        ids.Should().Equal("CBAND0003");
    }

    [Fact]
    public void Custom_system_backdrop_reports_cband0003_and_mica_cband0008()
    {
        //Act
        var ids = Ids("var w = new Window(); w.SystemBackdrop = new CustomBackdrop(); w.SystemBackdrop = new MicaBackdrop();");

        //Assert
        ids.Should().Equal("CBAND0003", "CBAND0008");
    }

    [Fact]
    public void Plane_projection_reports_cband0004_once()
    {
        //Act
        var ids = Ids("var g = new Grid(); g.Projection = new PlaneProjection();");

        //Assert
        ids.Should().Equal("CBAND0004");
    }

    [Fact]
    public void Zoom_enabled_reports_cband0005_and_disabled_is_silent()
    {
        //Act
        var ids = Ids("var s = new ScrollViewer(); s.ZoomMode = ZoomMode.Enabled; s.ZoomMode = ZoomMode.Disabled; ScrollViewer.SetZoomMode(s, ZoomMode.Enabled);");

        //Assert
        ids.Should().Equal("CBAND0005", "CBAND0005");
    }

    [Fact]
    public void Password_char_that_is_not_one_character_reports_cband0006()
    {
        //Act
        var ids = Ids("var p = new PasswordBox(); p.PasswordChar = \"**\"; p.PasswordChar = \"*\";");

        //Assert
        ids.Should().Equal("CBAND0006");
    }

    [Fact]
    public void Frame_buffer_option_reports_cband0007()
    {
        //Act
        var ids = Ids("var o = new CodeBrix.Platform.UI.Runtime.Skia.SoftwareKeyboardOptions();");

        //Assert
        ids.Should().Equal("CBAND0007");
    }

    [Fact]
    public void Acrylic_brush_reports_cband0008()
    {
        //Act
        var ids = Ids("var a = new AcrylicBrush();");

        //Assert
        ids.Should().Equal("CBAND0008");
    }

    [Fact]
    public void Every_cband_diagnostic_is_a_warning()
    {
        //Act
        var diagnostics = AnalyzerHarness.RunCSharp("""
            using Microsoft.UI.Xaml.Controls;
            class C { void M() { new Button().Template = new Microsoft.UI.Xaml.ControlTemplate(); } }
            """);

        //Assert
        diagnostics.Should().OnlyContain(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Warning);
    }

    [Fact]
    public void Generated_code_is_not_analysed()
    {
        //Act
        var diagnostics = AnalyzerHarness.RunCSharp("""
            // <auto-generated/>
            using Microsoft.UI.Xaml.Controls;
            class C { void M() { new Button().Template = new Microsoft.UI.Xaml.ControlTemplate(); } }
            """);

        //Assert
        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void Compilation_without_the_platform_is_silent()
    {
        //Act
        var diagnostics = AnalyzerHarness.Run(AnalyzerHarness.Compile(false, "class Template { public object Projection { get; set; } void M() { Projection = new object(); } }"));

        //Assert
        diagnostics.Should().BeEmpty();
    }
}
