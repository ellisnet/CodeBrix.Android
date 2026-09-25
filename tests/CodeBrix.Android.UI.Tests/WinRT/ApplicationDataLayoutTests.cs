using System;
using CodeBrix.Android.Portable;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.Tests.WinRT;

public class ApplicationDataLayoutTests
{
    [Fact]
    public void Layout_places_every_folder_under_the_files_and_cache_directories()
    {
        //Act
        var layout = new ApplicationDataLayout("/data/user/0/com.example/files", "/data/user/0/com.example/cache/");

        //Assert
        layout.LocalFolderPath.Should().Be("/data/user/0/com.example/files");
        layout.RoamingFolderPath.Should().Be("/data/user/0/com.example/files/Roaming");
        layout.SettingsFolderPath.Should().Be("/data/user/0/com.example/files/Settings");
        layout.LocalCacheFolderPath.Should().Be("/data/user/0/com.example/cache");
        layout.TemporaryFolderPath.Should().Be("/data/user/0/com.example/cache/Temp");
    }

    [Theory]
    [InlineData(null, "/cache")]
    [InlineData("/files", "")]
    public void Layout_requires_both_directories(string files, string cache)
    {
        //Act
        Action act = () => _ = new ApplicationDataLayout(files, cache);

        //Assert
        act.Should().Throw<ArgumentException>();
    }
}
