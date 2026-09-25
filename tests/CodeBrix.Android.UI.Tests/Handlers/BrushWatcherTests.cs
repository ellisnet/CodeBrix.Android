using CodeBrix.Android.UI.Handlers;
using CodeBrix.Android.UI.Tests.HostFree;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.Tests.Handlers;

[Collection(HostFreeCoreCollection.Name)]
public class BrushWatcherTests
{
    public BrushWatcherTests() => HostFreeCore.EnsureInitialized();

    [Fact]
    public void A_solid_colour_change_is_reported()
    {
        //Arrange
        var changes = 0;
        var brush = new SolidColorBrush(Colors.Red);
        var watcher = new BrushWatcher(() => changes++);
        watcher.Watch(brush);

        //Act
        brush.Color = Colors.Blue;

        //Assert
        changes.Should().Be(1);
    }

    [Fact]
    public void A_gradient_stop_colour_change_is_reported()
    {
        //Arrange
        var changes = 0;
        var stop = new GradientStop { Color = Colors.Red, Offset = 0 };
        var brush = new LinearGradientBrush { GradientStops = { stop, new GradientStop { Color = Colors.Blue, Offset = 1 } } };
        var watcher = new BrushWatcher(() => changes++);
        watcher.Watch(brush);

        //Act
        stop.Color = Colors.Green;

        //Assert
        changes.Should().Be(1);
    }

    [Fact]
    public void A_replaced_brush_is_no_longer_followed()
    {
        //Arrange
        var changes = 0;
        var first = new SolidColorBrush(Colors.Red);
        var watcher = new BrushWatcher(() => changes++);
        watcher.Watch(first);
        watcher.Watch(new SolidColorBrush(Colors.Green));

        //Act
        first.Color = Colors.Blue;

        //Assert
        changes.Should().Be(0);
    }

    [Fact]
    public void Clear_stops_following()
    {
        //Arrange
        var changes = 0;
        var brush = new SolidColorBrush(Colors.Red);
        var watcher = new BrushWatcher(() => changes++);
        watcher.Watch(brush);
        watcher.Clear();

        //Act
        brush.Opacity = 0.5;

        //Assert
        changes.Should().Be(0);
    }
}
