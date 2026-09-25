using System.Collections.Generic;
using CodeBrix.Android.UI.Handlers;
using CodeBrix.Android.UI.Tests.HostFree;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SilverAssertions;
using Windows.Foundation;
using Xunit;

namespace CodeBrix.Android.UI.Tests.Handlers;

[Collection(HostFreeCoreCollection.Name)]
public class ElementHandlerTests
{
    public ElementHandlerTests() => HostFreeCore.EnsureInitialized();

    [Fact]
    public void Connect_creates_the_view_once_then_maps_every_key_then_reports_connected()
    {
        //Arrange
        RecordingHandler handler = null;
        var mapper = new PropertyMapper<UIElement, IAndroidElementHandler>
        {
            [UIElement.OpacityProperty] = (_, _) => handler.Log.Add("map-opacity"),
            [UIElement.VisibilityProperty] = (_, _) => handler.Log.Add("map-visibility"),
        };
        handler = new RecordingHandler(mapper);

        //Act
        handler.Connect(new Border());

        //Assert
        handler.Log.Should().Equal("create", "connect-view", "map-opacity", "map-visibility", "connected");
        handler.State.Should().Be(ElementHandlerState.Connected);
        handler.PlatformView.Should().NotBeNull();
    }

    [Fact]
    public void UpdateValue_runs_only_the_mapping_of_that_property()
    {
        //Arrange
        var calls = new List<string>();
        var mapper = new PropertyMapper<UIElement, IAndroidElementHandler>
        {
            [UIElement.OpacityProperty] = (_, e) => calls.Add("opacity=" + e.Opacity),
            [UIElement.VisibilityProperty] = (_, _) => calls.Add("visibility"),
        };
        var handler = new RecordingHandler(mapper);
        var border = new Border();
        handler.Connect(border);
        calls.Clear();

        //Act
        border.Opacity = 0.5;
        handler.UpdateValue(UIElement.OpacityProperty);

        //Assert
        calls.Should().Equal("opacity=0.5");
    }

    [Fact]
    public void Disconnect_unhooks_the_view_and_forgets_the_element()
    {
        //Arrange
        var handler = new RecordingHandler(new PropertyMapper<UIElement, IAndroidElementHandler>());
        handler.Connect(new Border());

        //Act
        handler.Disconnect();

        //Assert
        handler.Log.Should().Contain("disconnect-view");
        handler.Element.Should().BeNull();
        ((IAndroidElementHandler)handler).PlatformView.Should().BeNull();
        handler.State.Should().Be(ElementHandlerState.Disconnected);
    }

    [Fact]
    public void UpdateValue_after_disconnect_does_nothing()
    {
        //Arrange
        var calls = 0;
        var mapper = new PropertyMapper<UIElement, IAndroidElementHandler> { [UIElement.OpacityProperty] = (_, _) => calls++ };
        var handler = new RecordingHandler(mapper);
        handler.Connect(new Border());
        handler.Disconnect();
        calls = 0;

        //Act
        handler.UpdateValue(UIElement.OpacityProperty);

        //Assert
        calls.Should().Be(0);
    }

    [Fact]
    public void Arrange_records_the_rectangle_and_reports_changes()
    {
        //Arrange
        var handler = new RecordingHandler(new PropertyMapper<UIElement, IAndroidElementHandler>());
        handler.Connect(new Border());

        //Act
        handler.Arrange(new Rect(10, 20, 30, 40));
        handler.Arrange(new Rect(10, 20, 30, 40));
        handler.Arrange(new Rect(11, 20, 30, 40));

        //Assert
        handler.HasArranged.Should().BeTrue();
        handler.ArrangedRect.Should().Be(new Rect(11, 20, 30, 40));
        handler.Log.Should().ContainInOrder("arranged-changed", "arranged-same", "arranged-changed");
    }

    [Fact]
    public void Invoke_without_a_command_mapper_is_not_handled()
    {
        //Arrange
        var handler = new RecordingHandler(new PropertyMapper<UIElement, IAndroidElementHandler>());
        handler.Connect(new Border());

        //Act
        var handled = handler.Invoke("ChangeView", null);

        //Assert
        handled.Should().BeFalse();
    }
}
