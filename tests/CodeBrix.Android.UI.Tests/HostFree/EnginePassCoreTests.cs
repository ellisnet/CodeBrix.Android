using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Shapes;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.Tests.HostFree;

/// <summary>
/// AP1.9 (pin 1.0.268.12): the Core fixes of the engine pass that CodeBrix.Android now relies on instead of its own
/// workarounds, run on the extracted Core. A Core that regresses fails here, not only on the device.
/// </summary>
[Collection(HostFreeCoreCollection.Name)]
public class EnginePassCoreTests
{
    public EnginePassCoreTests() => HostFreeCore.EnsureInitialized();

    [Fact]
    public void A_shape_strokes_with_the_WinUI_default_miter_limit_of_ten()
    {
        //Arrange (WPE1-5 B5: the default was 0 - the AP6 ShapeHandler read it as 10)
        var rectangle = new Rectangle();

        //Assert
        rectangle.StrokeMiterLimit.Should().Be(10);
    }

    [Fact]
    public void A_TextBlock_with_a_margin_reports_its_arranged_width_as_ActualWidth()
    {
        //Arrange (WPE1-5 B4: ActualWidth was DesiredSize, margin included)
        var text = new TextBlock { Text = "abcd", FontSize = 20, Margin = new Thickness(20, 0, 20, 0), HorizontalAlignment = HorizontalAlignment.Left };
        var panel = new StackPanel { Children = { text } };

        //Act
        HostFreeCore.Layout(panel, 400, 300);

        //Assert (test metrics: a character is 0.5 em wide)
        text.ActualWidth.Should().Be(40);
        text.DesiredSize.Width.Should().Be(80);
    }

    [Fact]
    public void A_text_submitted_to_an_editable_ComboBox_without_a_template_becomes_its_selected_item()
    {
        //Arrange (WPE1-5 B8: a custom value did not stick until the template's TextBox existed)
        var combo = new ComboBox { IsEditable = true, Items = { "alpha", "beta" } };
        var panel = new StackPanel { Children = { combo } };
        HostFreeCore.Layout(panel, 400, 300);

        //Act
        combo.RaiseTextSubmittedFromPlatform("gamma");

        //Assert
        combo.SelectedItem.Should().Be("gamma");
    }

    [Fact]
    public void A_text_submitted_to_an_editable_ComboBox_that_names_an_item_selects_that_item()
    {
        //Arrange
        var combo = new ComboBox { IsEditable = true, Items = { "alpha", "beta" } };
        var panel = new StackPanel { Children = { combo } };
        HostFreeCore.Layout(panel, 400, 300);

        //Act
        combo.RaiseTextSubmittedFromPlatform("beta");

        //Assert
        combo.SelectedIndex.Should().Be(1);
    }
}
