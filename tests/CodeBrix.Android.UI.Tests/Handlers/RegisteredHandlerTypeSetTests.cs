using System;
using CodeBrix.Android.UI.Handlers;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.Tests.Handlers;

public class RegisteredHandlerTypeSetTests
{
    private interface IFirst
    {
    }

    private interface ISecond
    {
    }

    [Fact]
    public void Resolve_returns_an_exact_registration()
    {
        //Arrange
        var set = new RegisteredHandlerTypeSet();
        set.Add(typeof(Microsoft.UI.Xaml.Controls.Button));

        //Act
        var resolved = set.Resolve(typeof(Microsoft.UI.Xaml.Controls.Button));

        //Assert
        resolved.Should().Be(typeof(Microsoft.UI.Xaml.Controls.Button));
    }

    [Fact]
    public void Resolve_picks_the_most_derived_registered_base_type()
    {
        //Arrange
        var set = new RegisteredHandlerTypeSet();
        set.Add(typeof(Microsoft.UI.Xaml.UIElement));
        set.Add(typeof(Microsoft.UI.Xaml.Controls.Control));
        set.Add(typeof(Microsoft.UI.Xaml.Controls.Primitives.ButtonBase));

        //Act
        var resolved = set.Resolve(typeof(AppButton));

        //Assert
        resolved.Should().Be(typeof(Microsoft.UI.Xaml.Controls.Primitives.ButtonBase));
    }

    [Fact]
    public void Resolve_prefers_a_concrete_registration_over_an_interface()
    {
        //Arrange
        var set = new RegisteredHandlerTypeSet();
        set.Add(typeof(IFirst));
        set.Add(typeof(Microsoft.UI.Xaml.Controls.Control));

        //Act
        var resolved = set.Resolve(typeof(TwoInterfaceControl));

        //Assert
        resolved.Should().Be(typeof(Microsoft.UI.Xaml.Controls.Control));
    }

    [Fact]
    public void Resolve_throws_for_two_unrelated_interface_matches()
    {
        //Arrange
        var set = new RegisteredHandlerTypeSet();
        set.Add(typeof(IFirst));
        set.Add(typeof(ISecond));

        //Act
        var act = () => set.Resolve(typeof(TwoInterfaceObject));

        //Assert
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Resolve_returns_null_when_nothing_applies()
    {
        //Arrange
        var set = new RegisteredHandlerTypeSet();
        set.Add(typeof(Microsoft.UI.Xaml.Controls.TextBlock));

        //Act
        var resolved = set.Resolve(typeof(Microsoft.UI.Xaml.Controls.Border));

        //Assert
        resolved.Should().BeNull();
    }

    private sealed class AppButton : Microsoft.UI.Xaml.Controls.Button
    {
    }

    private sealed class TwoInterfaceControl : Microsoft.UI.Xaml.Controls.Control, IFirst, ISecond
    {
    }

    private sealed class TwoInterfaceObject : IFirst, ISecond
    {
    }
}
