using CodeBrix.Android.Services;
using SilverAssertions;
using Windows.Networking.Connectivity;
using Xunit;

namespace CodeBrix.Android.UI.Tests.WinRT;

public class ConnectivityLevelsTests
{
    [Theory]
    [InlineData(true, true, NetworkConnectivityLevel.InternetAccess)]
    [InlineData(true, false, NetworkConnectivityLevel.ConstrainedInternetAccess)]
    [InlineData(false, false, NetworkConnectivityLevel.LocalAccess)]
    [InlineData(false, true, NetworkConnectivityLevel.LocalAccess)]
    public void The_capabilities_give_the_level(bool internet, bool validated, NetworkConnectivityLevel expected)
    {
        //Act & Assert
        ConnectivityLevels.From(internet, validated).Should().Be(expected);
    }
}
