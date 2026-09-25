using CodeBrix.Android.UI.Handlers;
using CodeBrix.Android.UI.Tests.HostFree;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SilverAssertions;
using Windows.Foundation;
using Xunit;

namespace CodeBrix.Android.UI.Tests.Composed;

/// <summary>AP6: when an Expander is native, and the native Expander's layout.</summary>
[Collection(HostFreeCoreCollection.Name)]
public class ExpanderLayoutTests
{
    public ExpanderLayoutTests() => HostFreeCore.EnsureInitialized();

    [Fact]
    public void A_text_header_opening_down_is_native()
    {
        //Assert
        ExpanderLayout.CanMapNatively("Details", null, null, ExpandDirection.Down).Should().BeTrue();
        ExpanderLayout.CanMapNatively(42, null, null, ExpandDirection.Down).Should().BeTrue();
    }

    [Fact]
    public void An_element_header_a_header_template_or_opening_up_keeps_the_template()
    {
        //Assert
        ExpanderLayout.CanMapNatively(new TextBlock(), null, null, ExpandDirection.Down).Should().BeFalse();
        ExpanderLayout.CanMapNatively("Details", new DataTemplate(), null, ExpandDirection.Down).Should().BeFalse();
        ExpanderLayout.CanMapNatively("Details", null, null, ExpandDirection.Up).Should().BeFalse();
    }

    [Fact]
    public void The_header_is_at_least_48_tall()
    {
        //Assert
        ExpanderLayout.HeaderHeight(20).Should().Be(48);
        ExpanderLayout.HeaderHeight(60).Should().Be(60);
        ExpanderLayout.HeaderText(null).Should().Be(string.Empty);
    }

    [Fact]
    public void A_collapsed_Expander_is_as_tall_as_its_header()
    {
        //Act
        var size = ExpanderLayout.Desired(new Size(500, double.PositiveInfinity), 500, 48, expanded: false, new Size(200, 160), new Thickness(17));

        //Assert
        size.Should().Be(new Size(500, 48));
    }

    [Fact]
    public void An_expanded_Expander_adds_its_content_box()
    {
        //Act
        var size = ExpanderLayout.Desired(new Size(500, double.PositiveInfinity), 500, 48, expanded: true, new Size(200, 160), new Thickness(17));

        //Assert
        size.Should().Be(new Size(500, 48 + 160 + 34));
    }

    [Fact]
    public void The_content_sits_under_the_header_inside_border_and_padding()
    {
        //Act
        var rect = ExpanderLayout.ContentRect(new Size(500, 242), 48, expanded: true, new Size(200, 160), new Thickness(17),
            HorizontalAlignment.Stretch, VerticalAlignment.Stretch);

        //Assert
        rect.Should().Be(new Rect(17, 65, 466, 160));
    }

    [Fact]
    public void A_collapsed_Expanders_content_has_no_height_and_left_alignment_uses_its_width()
    {
        //Act
        var rect = ExpanderLayout.ContentRect(new Size(500, 48), 48, expanded: false, new Size(200, 160), new Thickness(17),
            HorizontalAlignment.Left, VerticalAlignment.Top);

        //Assert
        rect.Height.Should().Be(0);
        rect.Width.Should().Be(200);
        ExpanderLayout.IsOnHeader(new Point(10, 47), 500, 48).Should().BeTrue();
        ExpanderLayout.IsOnHeader(new Point(10, 48), 500, 48).Should().BeFalse();
    }
}
