using System.Linq;
using CodeBrix.Android.Analyzers.Tests.Support;
using CodeBrix.Android.Analyzers.Xaml;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.Analyzers.Tests.Xaml;

public class XamlScannerTests
{
    private const string Head = """<Page xmlns="clr-namespace:Microsoft.UI.Xaml.Controls;assembly=CodeBrix.Platform.UI" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" xmlns:m="clr-namespace:Microsoft.UI.Xaml.Media;assembly=CodeBrix.Platform.UI" xmlns:local="using:App.Controls">""";

    private static string[] Ids(string body, string extraSource = "") =>
        AnalyzerHarness.ScanXaml(Head + "\n" + body + "\n</Page>", extraSource).Select(f => f.Descriptor.Id).ToArray();

    [Fact]
    public void Style_template_setter_for_a_button_reports_cband0001()
    {
        //Arrange
        var xaml = Head + """

              <Page.Resources>
                <Style x:Key="TempleButtonStyle" TargetType="Button">
                  <Setter Property="Template">
                    <Setter.Value><ControlTemplate /></Setter.Value>
                  </Setter>
                </Style>
              </Page.Resources>
            </Page>
            """;
        var setterLine = xaml.Split('\n').ToList().FindIndex(l => l.Contains("<Setter Property")) + 1;

        //Act
        var findings = AnalyzerHarness.ScanXaml(xaml);

        //Assert
        findings.Should().HaveCount(1);
        findings[0].Descriptor.Id.Should().Be("CBAND0001");
        findings[0].Line.Should().Be(setterLine);
        findings[0].Arguments[1].Should().Be("Button");
    }

    [Fact]
    public void Prefixed_target_type_resolves_through_its_namespace()
    {
        //Act
        var ids = Ids("""<Style xmlns:c="clr-namespace:Microsoft.UI.Xaml.Controls;assembly=CodeBrix.Platform.UI.FluentTheme" TargetType="c:ScrollViewer"><Setter Property="Template" Value="{StaticResource T}" /></Style>""");

        //Assert
        ids.Should().Equal("CBAND0001");
    }

    [Fact]
    public void Template_setter_for_a_user_control_is_silent()
    {
        //Act
        var ids = Ids("""<Style TargetType="UserControl"><Setter Property="Template" Value="{StaticResource T}" /></Style>""");

        //Assert
        ids.Should().BeEmpty();
    }

    [Fact]
    public void Template_attribute_and_property_element_on_a_button_report_cband0001()
    {
        //Act
        var ids = Ids("""<Grid><Button Template="{StaticResource T}" /><Button><Button.Template><ControlTemplate /></Button.Template></Button></Grid>""");

        //Assert
        ids.Should().Equal("CBAND0001", "CBAND0001");
    }

    [Fact]
    public void App_subclass_of_button_reports_cband0001()
    {
        //Act
        var ids = Ids("""<local:FancyButton Template="{StaticResource T}" />""", "namespace App.Controls { public class FancyButton : Microsoft.UI.Xaml.Controls.Button { } }");

        //Assert
        ids.Should().Equal("CBAND0001");
    }

    [Fact]
    public void Zoom_enabled_reports_cband0005_and_disabled_is_silent()
    {
        //Act
        var ids = Ids("""<Grid><ScrollViewer ZoomMode="Enabled" /><ScrollViewer ZoomMode="Disabled" /><Grid ScrollViewer.ZoomMode="Enabled" /></Grid>""");

        //Assert
        ids.Should().Equal("CBAND0005", "CBAND0005");
    }

    [Fact]
    public void Zoom_style_setter_reports_cband0005()
    {
        //Act
        var ids = Ids("""<Style TargetType="ScrollViewer"><Setter Property="ZoomMode" Value="Enabled" /></Style>""");

        //Assert
        ids.Should().Equal("CBAND0005");
    }

    [Fact]
    public void Password_char_rules_follow_the_native_mask()
    {
        //Act
        var ids = Ids("""<Grid><PasswordBox PasswordChar="ab" /><PasswordBox PasswordChar="*" /><PasswordBox PasswordChar="{StaticResource Mask}" /></Grid>""");

        //Assert
        ids.Should().Equal("CBAND0006");
    }

    [Fact]
    public void Composition_projection_and_material_elements_report_their_ids()
    {
        //Act
        var ids = Ids("""<Grid><Grid.Shadow><m:ThemeShadow /></Grid.Shadow><Grid.Projection><m:PlaneProjection /></Grid.Projection><Grid.Background><m:AcrylicBrush /></Grid.Background></Grid>""");

        //Assert
        ids.Should().Equal("CBAND0003", "CBAND0004", "CBAND0008");
    }

    [Fact]
    public void Ordinary_page_is_silent()
    {
        //Act
        var ids = Ids("""<Grid><Button Content="OK" /><ScrollViewer ZoomMode="Disabled"><TextBox /></ScrollViewer><m:SolidColorBrush /></Grid>""");

        //Assert
        ids.Should().BeEmpty();
    }

    [Fact]
    public void Malformed_xml_yields_no_findings()
    {
        //Act
        var findings = AnalyzerHarness.ScanXaml("<Page><Grid></Page>");

        //Assert
        findings.Should().BeEmpty();
    }

    [Theory]
    [InlineData("using:App.Views", "App.Views")]
    [InlineData("clr-namespace:Microsoft.UI.Xaml.Controls;assembly=CodeBrix.Platform.UI", "Microsoft.UI.Xaml.Controls")]
    [InlineData("http://schemas.microsoft.com/winfx/2006/xaml/presentation", null)]
    [InlineData("http://example.com/other", null)]
    public void Clr_namespace_is_read_from_the_xml_namespace(string xmlNamespace, string expected)
    {
        //Act
        var clr = XamlScanner.ClrNamespace(xmlNamespace);

        //Assert
        clr.Should().Be(expected);
    }
}
