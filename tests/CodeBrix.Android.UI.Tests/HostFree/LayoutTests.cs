using System.Linq;
using CodeBrix.Android.UI.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.Tests.HostFree;

[Collection(HostFreeCoreCollection.Name)]
public class LayoutTests
{
    public LayoutTests() => HostFreeCore.EnsureInitialized();

    [Fact]
    public void StackPanel_stacks_text_blocks_measured_by_the_text_platform()
    {
        //Arrange
        var first = new TextBlock { Text = "abcd", FontSize = 20 };
        var second = new TextBlock { Text = "abcdefgh", FontSize = 20 };
        var panel = new StackPanel { Children = { first, second } };

        //Act
        HostFreeCore.Layout(panel, 400, 300);

        //Assert (test metrics: a character is 0.5 em wide, a line 1.25 em high; since pin 1.0.268.12 (WPE1-5 B4) a
        //TextBlock's ActualWidth is its arranged width - the stretched 400 slot - as in WinUI, not its DesiredSize)
        first.DesiredSize.Width.Should().Be(40);
        first.ActualWidth.Should().Be(400);
        first.ActualHeight.Should().Be(25);
        second.DesiredSize.Width.Should().Be(80);
        LayoutInformation.GetLayoutSlot(second).Y.Should().Be(25);
        second.ActualOffset.Y.Should().Be(25);
    }

    [Fact]
    public void Grid_places_children_in_their_rows_and_columns()
    {
        //Arrange
        var cell = new Border { Width = 50, Height = 20 };
        Grid.SetRow(cell, 1);
        Grid.SetColumn(cell, 1);
        var grid = new Grid
        {
            RowDefinitions = { new RowDefinition { Height = new GridLength(100) }, new RowDefinition() },
            ColumnDefinitions = { new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }, new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) } },
            Children = { cell },
        };

        //Act
        HostFreeCore.Layout(grid, 400, 300);

        //Assert (the cell is 200x200 at 200,100; the 50x20 border is centred in it)
        LayoutInformation.GetLayoutSlot(cell).X.Should().Be(200);
        LayoutInformation.GetLayoutSlot(cell).Y.Should().Be(100);
        cell.ActualOffset.X.Should().Be(275);
        cell.ActualOffset.Y.Should().Be(190);
    }

    [Fact]
    public void VisualTreeDump_describes_a_laid_out_tree()
    {
        //Arrange
        var panel = new StackPanel { Name = "Root", Children = { new TextBlock { Text = "Hello \"dump\"", FontSize = 10 } } };
        HostFreeCore.Layout(panel, 200, 100);

        //Act
        var lines = VisualTreeDump.Dump(panel);

        //Assert
        lines.Count.Should().Be(2);
        lines[0].Should().Be("StackPanel #Root slot=0,0,200x100 actual=200x100");
        lines[1].Should().Be("  TextBlock text=\"Hello \\\"dump\\\"\" slot=0,0,200x13 actual=200x13"); //12.5 rounded by layout rounding; ActualWidth = the arranged width (WPE1-5 B4)
    }

    [Fact]
    public void VisualTreeDump_marks_collapsed_elements()
    {
        //Arrange
        var hidden = new TextBlock { Text = "x", Visibility = Visibility.Collapsed };
        var panel = new StackPanel { Children = { hidden } };
        HostFreeCore.Layout(panel, 100, 100);

        //Act
        var lines = VisualTreeDump.Dump(panel);

        //Assert
        lines.Last().Should().EndWith(" collapsed");
    }
}
