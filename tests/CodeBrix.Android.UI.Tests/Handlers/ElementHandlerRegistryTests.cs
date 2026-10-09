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

    [Fact]
    public void FactoryFor_returns_the_registration_that_serves_a_derived_type()
    {
        //Arrange
        var registry = new ElementHandlerRegistry { Fallback = _ => Tagged("fallback") };
        registry.Register<Control>(_ => Tagged("control"));

        //Act
        var factory = registry.FactoryFor(typeof(Button));
        var handler = (RecordingHandler)factory(new Button());

        //Assert
        handler.Log.Should().Contain("control");
    }

    [Fact]
    public void FactoryFor_returns_the_fallback_when_no_registration_applies()
    {
        //Arrange
        var registry = new ElementHandlerRegistry { Fallback = _ => Tagged("fallback") };

        //Act
        var handler = (RecordingHandler)registry.FactoryFor(typeof(Border))(new Border());

        //Assert
        handler.Log.Should().Contain("fallback");
    }

    [Fact]
    public void A_wrapping_registration_keeps_the_handler_of_the_base_registration()
    {
        //Arrange
        var registry = new ElementHandlerRegistry();
        registry.Register<Button>(_ => Tagged("button"));
        var inner = registry.FactoryFor(typeof(AppButton));
        var wrapped = 0;
        registry.Register<AppButton>(element =>
        {
            wrapped++;
            return inner(element);
        });

        //Act
        var handler = (RecordingHandler)registry.CreateHandler(new AppButton());
        var other = (RecordingHandler)registry.CreateHandler(new Button());

        //Assert
        handler.Log.Should().Contain("button");
        other.Log.Should().Contain("button");
        wrapped.Should().Be(1);
    }

    [Fact]
    public void FactoryFor_answers_null_without_a_registration_or_fallback()
    {
        //Arrange
        var registry = new ElementHandlerRegistry();

        //Act
        var handler = registry.FactoryFor(typeof(Border))(new Border());

        //Assert
        handler.Should().BeNull();
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
