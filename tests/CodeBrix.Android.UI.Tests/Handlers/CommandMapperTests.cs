using System.Collections.Generic;
using CodeBrix.Android.UI.Handlers;
using CodeBrix.Android.UI.Tests.HostFree;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.Tests.Handlers;

[Collection(HostFreeCoreCollection.Name)]
public class CommandMapperTests
{
    public CommandMapperTests() => HostFreeCore.EnsureInitialized();

    [Fact]
    public void Invoke_returns_the_command_result_and_passes_the_arguments()
    {
        //Arrange
        object received = null;
        var mapper = new CommandMapper<UIElement, IAndroidElementHandler>();
        mapper.Add("ChangeView", (_, _, args) =>
        {
            received = args;
            return false;
        });

        //Act
        var result = mapper.Invoke(new RecordingHandler(new PropertyMapper<UIElement, IAndroidElementHandler>()), new Border(), "ChangeView", 42);

        //Assert
        result.Should().BeFalse();
        received.Should().Be(42);
    }

    [Fact]
    public void An_unmapped_command_is_not_handled()
    {
        //Arrange
        var mapper = new CommandMapper<UIElement, IAndroidElementHandler>();

        //Act
        var result = mapper.Invoke(new RecordingHandler(new PropertyMapper<UIElement, IAndroidElementHandler>()), new Border(), "Focus", null);

        //Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void A_command_of_the_chained_mapper_is_found()
    {
        //Arrange
        var baseMapper = new CommandMapper<UIElement, IAndroidElementHandler>();
        baseMapper.Add("Focus", (_, _, _) => { });
        var mapper = new CommandMapper<UIElement, IAndroidElementHandler>(baseMapper);

        //Act
        var result = mapper.Invoke(new RecordingHandler(new PropertyMapper<UIElement, IAndroidElementHandler>()), new Border(), "Focus", null);

        //Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void AppendToMapping_keeps_the_original_result_and_runs_afterwards()
    {
        //Arrange
        var calls = new List<string>();
        var mapper = new CommandMapper<UIElement, IAndroidElementHandler>();
        mapper.Add("ChangeView", (_, _, _) =>
        {
            calls.Add("original");
            return false;
        });
        mapper.AppendToMapping("ChangeView", (_, _, _) => calls.Add("appended"));

        //Act
        var result = mapper.Invoke(new RecordingHandler(new PropertyMapper<UIElement, IAndroidElementHandler>()), new Border(), "ChangeView", null);

        //Assert
        result.Should().BeFalse();
        calls.Should().Equal("original", "appended");
    }
}
