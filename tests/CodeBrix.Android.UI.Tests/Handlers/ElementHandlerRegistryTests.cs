using System;
using CodeBrix.Android.UI.Handlers;
using CodeBrix.Android.UI.Tests.HostFree;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.Tests.Handlers;

[Collection(HostFreeCoreCollection.Name)]
public class ElementHandlerRegistryTests
{
    public ElementHandlerRegistryTests() => HostFreeCore.EnsureInitialized();

    [Fact]
    public void CreateHandler_uses_the_most_derived_registration()
    {
        //Arrange
        var registry = new ElementHandlerRegistry();
        registry.Register<Control>(_ => Tagged("control"));
        registry.Register<Button>(_ => Tagged("button"));

        //Act
        var handler = (RecordingHandler)registry.CreateHandler(new AppButton());

        //Assert
        handler.Log.Should().Contain("button");
    }

    [Fact]
    public void CreateHandler_falls_back_when_no_registration_applies()
    {
        //Arrange
        var registry = new ElementHandlerRegistry { Fallback = _ => Tagged("fallback") };
        registry.Register<TextBlock>(_ => Tagged("text"));

        //Act
        var handler = (RecordingHandler)registry.CreateHandler(new Border());

        //Assert
        handler.Log.Should().Contain("fallback");
    }

    [Fact]
    public void CreateHandler_returns_null_without_a_registration_or_fallback()
    {
        //Arrange
        var registry = new ElementHandlerRegistry();

        //Act
        var handler = registry.CreateHandler(new Border());

        //Assert
        handler.Should().BeNull();
    }

    [Fact]
    public void A_later_registration_replaces_the_earlier_one()
    {
        //Arrange
        var registry = new ElementHandlerRegistry();
        registry.Register<Border>(_ => Tagged("first"));
        registry.CreateHandler(new Border());
        registry.Register<Border>(_ => Tagged("second"));

        //Act
        var handler = (RecordingHandler)registry.CreateHandler(new Border());

        //Assert
        handler.Log.Should().Contain("second");
    }

    [Fact]
    public void Factory_reports_a_throwing_handler_as_no_handler()
    {
        //Arrange
        var registry = new ElementHandlerRegistry();
        registry.Register<Border>(_ => throw new InvalidOperationException("broken handler"));
        var factory = new ElementHandlerFactory(registry);

        //Act
        var handler = factory.CreateHandler(new Border());

        //Assert
        handler.Should().BeNull();
        factory.CreatedCount.Should().Be(0);
    }

    [Fact]
    public void Factory_counts_the_handlers_it_created()
    {
        //Arrange
        var registry = new ElementHandlerRegistry { Fallback = _ => Tagged("fallback") };
        var factory = new ElementHandlerFactory(registry);

        //Act
        factory.CreateHandler(new Border());
        factory.CreateHandler(new Grid());

        //Assert
        factory.CreatedCount.Should().Be(2);
    }

    private static RecordingHandler Tagged(string tag)
    {
        var handler = new RecordingHandler(new PropertyMapper<UIElement, IAndroidElementHandler>());
        handler.Log.Add(tag);
        return handler;
    }

    private sealed class AppButton : Button
    {
    }
}
