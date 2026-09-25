using CodeBrix.Android.UI.Tests.HostFree;
using Microsoft.UI.Xaml.Controls;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.Tests.HostFree;

[Collection(HostFreeCoreCollection.Name)]
public class FrameTests
{
    public FrameTests() => HostFreeCore.EnsureInitialized();

    [Fact]
    public void Navigate_creates_the_page_and_sets_it_as_content()
    {
        //Arrange
        var frame = new Frame();

        //Act
        var navigated = frame.Navigate(typeof(FirstPage));
        HostFreeCore.RunPending();

        //Assert
        navigated.Should().BeTrue();
        frame.Content.Should().BeOfType<FirstPage>();
        frame.CurrentSourcePageType.Should().Be(typeof(FirstPage));
    }

    [Fact]
    public void GoBack_returns_to_the_previous_page()
    {
        //Arrange
        var frame = new Frame();
        frame.Navigate(typeof(FirstPage));
        HostFreeCore.RunPending();
        frame.Navigate(typeof(SecondPage), "parameter");
        HostFreeCore.RunPending();

        //Act
        var couldGoBack = frame.CanGoBack;
        frame.GoBack();
        HostFreeCore.RunPending();

        //Assert
        couldGoBack.Should().BeTrue();
        frame.Content.Should().BeOfType<FirstPage>();
        frame.CanGoForward.Should().BeTrue();
    }

    public sealed partial class FirstPage : Page
    {
    }

    public sealed partial class SecondPage : Page
    {
    }
}
