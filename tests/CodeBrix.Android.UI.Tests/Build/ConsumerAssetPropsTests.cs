using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.Tests.Build;

/// <summary>
/// AP1.12 fence: the consumer build keeps every app asset at its ms-appx path in the APK. The Android SDK strips its
/// MonoAndroidAssetsPrefix ("Assets" by default) from each asset's logical name, which put ms-appx:///Assets/x.json at the
/// asset "x.json"; the consumer props now set a prefix no ms-appx path starts with (the device fence is the AndroidNative
/// scenario "A Lottie document named by an ms-appx URI loads from the app's assets").
/// </summary>
public class ConsumerAssetPropsTests
{
    private static XElement Property(string name)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "CodeBrix.Android.slnx")))
        {
            directory = directory.Parent;
        }

        directory.Should().NotBeNull("the tests run inside the repository");
        var props = XDocument.Load(Path.Combine(directory.FullName, "build", "nuget", "buildTransitive", "CodeBrix.Android.ApacheLicenseForever.props"));
        return props.Descendants().FirstOrDefault(e => e.Name.LocalName == name);
    }

    [Fact]
    public void The_consumer_props_turn_off_the_default_Assets_items()
    {
        //Act
        var property = Property("EnableDefaultAndroidAssetItems");

        //Assert
        property.Should().NotBeNull();
        property.Value.Should().Be("false");
    }

    [Fact]
    public void The_consumer_props_set_an_assets_prefix_no_ms_appx_path_starts_with()
    {
        //Act
        var property = Property("MonoAndroidAssetsPrefix");

        //Assert
        property.Should().NotBeNull();
        property.Value.Should().NotBeNullOrWhiteSpace();
        property.Value.Should().NotBe("Assets");
        property.Value.Should().NotContain("/");
        property.Attribute("Condition")?.Value.Should().Contain("'$(MonoAndroidAssetsPrefix)' == ''");
    }
}
