using System;
using System.IO;
using CodeBrix.Android.Portable;
using SilverAssertions;
using Windows.System.Profile.Internal;
using Xunit;

namespace CodeBrix.Android.UI.Tests.WinRT;

/// <summary>
/// AnalyticsInfo.VersionInfo.DeviceFamilyVersion on Android: the release string and API level become the Version the
/// framework encodes (AndroidOsVersion, linked), the encoder round-trips it, and the platform bootstrap registers the
/// extension (a source fence: without the registration the framework encodes the Linux kernel's version instead).
/// </summary>
public class AndroidOsVersionTests
{
    [Theory]
    [InlineData("14", 34, "14.0.34.0")]
    [InlineData("15", 35, "15.0.35.0")]
    [InlineData("4.4.2", 19, "4.4.19.0")]
    [InlineData("4.4W", 20, "4.4.20.0")]
    [InlineData("8.1.0", 27, "8.1.27.0")]
    [InlineData("16", -1, "16.0.0.0")]
    public void a_numbered_release_is_major_minor_and_the_api_level(string release, int sdkInt, string expected)
    {
        //Act
        var version = AndroidOsVersion.FromRelease(release, sdkInt);

        //Assert
        version.ToString().Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Q")]
    [InlineData("Baklava")]
    [InlineData(".5")]
    public void a_release_without_a_leading_number_has_no_version(string release)
    {
        //Act
        var version = AndroidOsVersion.FromRelease(release, 36);

        //Assert
        version.Should().BeNull();
    }

    [Fact]
    public void the_encoded_value_decodes_to_the_release_and_api_level()
    {
        //Arrange
        var version = AndroidOsVersion.FromRelease("14", 34);

        //Act
        var encoded = VersionHelpers.ToDeviceFamilyVersion(version);
        var decoded = VersionHelpers.FromDeviceFamilyVersion(encoded);

        //Assert
        encoded.Should().Be(((14UL << 48) | (34UL << 16)).ToString());
        decoded.Should().Be(new Version(14, 0, 34, 0));
    }

    [Fact]
    public void the_platform_bootstrap_registers_the_extension_next_to_the_device_form_one()
    {
        //Arrange
        var source = File.ReadAllText(Path.Combine(RepoRoot(), "src", "CodeBrix.Android", "Android", "AndroidPlatformBootstrap.cs"));

        //Assert
        source.Should().Contain("ApiExtensibility.Register(typeof(IAnalyticsInfoExtension), _ => new AnalyticsInfoAndroidExtension());");
        source.Should().Contain("ApiExtensibility.Register(typeof(IAnalyticsVersionInfoExtension), _ => new AnalyticsVersionInfoAndroidExtension());");
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "CodeBrix.Android.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("The repository root was not found.");
    }
}
