using CodeBrix.Android.ParityScore.Scanning;
using CodeBrix.Android.ParityScore.Tests.Fixture;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.ParityScore.Tests.Scanning;

public class HandlerScannerTests
{
    private const string ButtonMapper = "ParityFixture.Handlers.ButtonHandler::Mapper";
    private const string SliderMapper = "ParityFixture.Handlers.SliderHandler::Mapper";

    private static HandlerModel ScanFixture()
    {
        using var set = FixtureSupport.OpenSet();
        return new HandlerScanner(FixtureSupport.Conventions, set).Scan(set.Assemblies);
    }

    [Fact]
    public void Collection_initializer_keys_belong_to_the_stored_mapper()
    {
        //Act
        var model = ScanFixture();

        //Assert
        model.Mappers[ButtonMapper].Keys.Should().Contain("ParityFixture.Xaml.Button.ContentProperty");
        model.Mappers[ButtonMapper].ElementType.Should().Be("ParityFixture.Xaml.Button");
        model.Mappers[ButtonMapper].HandlerType.Should().Be("ParityFixture.Handlers.ButtonHandler");
    }

    [Fact]
    public void Chained_mapper_keys_are_effective()
    {
        //Act
        var model = ScanFixture();

        //Assert
        model.Mappers[ButtonMapper].Chained.Should().Contain("ParityFixture.Handlers.ViewMappers::ViewMapper");
        model.EffectiveKeys(ButtonMapper).Should().Contain("ParityFixture.Xaml.UIElement.OpacityProperty");
    }

    [Fact]
    public void Helper_method_keys_are_effective()
    {
        //Act
        var model = ScanFixture();

        //Assert
        model.EffectiveKeys(ButtonMapper).Should().Contain("ParityFixture.Xaml.Control.ForegroundProperty");
    }

    [Fact]
    public void Policy_append_adds_a_key_to_the_named_mapper()
    {
        //Act
        var model = ScanFixture();

        //Assert
        model.Mappers[SliderMapper].Keys.Should().Contain("ParityFixture.Xaml.Control.ForegroundProperty");
        model.Mappers[SliderMapper].Keys.Should().Contain("ParityFixture.Xaml.Slider.ValueProperty");
        model.Mappers[SliderMapper].Keys.Should().NotContain("ParityFixture.Xaml.Slider.StepFrequencyProperty");
    }

    [Fact]
    public void Handler_mapper_is_the_one_passed_to_the_base_constructor()
    {
        //Act
        var model = ScanFixture();

        //Assert
        model.HandlerMappers["ParityFixture.Handlers.ButtonHandler"].Should().Be(ButtonMapper);
        model.HandlerMappers["ParityFixture.Handlers.SliderHandler"].Should().Be(SliderMapper);
    }

    [Fact]
    public void Method_group_factory_registration_finds_its_native_handler()
    {
        //Act
        var model = ScanFixture();

        //Assert
        var registration = model.Registrations["ParityFixture.Xaml.Button"];
        registration.Kind.Should().Be(RegistrationKind.Native);
        registration.HandlerTypes.Should().Equal("ParityFixture.Handlers.ButtonHandler");
    }

    [Fact]
    public void Lambda_factory_registration_finds_its_native_handler()
    {
        //Act
        var model = ScanFixture();

        //Assert
        model.Registrations["ParityFixture.Xaml.Slider"].HandlerTypes.Should().Equal("ParityFixture.Handlers.SliderHandler");
    }

    [Fact]
    public void Fallback_only_and_null_factories_are_not_native()
    {
        //Act
        var model = ScanFixture();

        //Assert
        model.Registrations["ParityFixture.Xaml.Control"].Kind.Should().Be(RegistrationKind.Fallback);
        model.Registrations["ParityFixture.Xaml.FrameworkElement"].Kind.Should().Be(RegistrationKind.CorePath);
    }
}
