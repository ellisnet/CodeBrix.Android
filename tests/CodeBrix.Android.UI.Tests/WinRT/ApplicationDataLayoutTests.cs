using System;
using System.IO;
using CodeBrix.Android.Portable;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.Tests.WinRT;

public class ApplicationDataLayoutTests
{
    [Fact]
    public void Layout_places_every_folder_under_the_files_and_cache_directories()
    {
        //Arrange
        // The sub-folder paths are built with Path.Combine, whose separator is the host's ('/' on
        // Android, Linux and macOS, '\' on Windows), so the expected values are built the same way
        // and this host-free test passes on every development OS.
        const string files = "/data/user/0/com.example/files";
        const string cache = "/data/user/0/com.example/cache";

        //Act
        var layout = new ApplicationDataLayout(files, cache + "/");

        //Assert
        layout.LocalFolderPath.Should().Be(files);
        layout.RoamingFolderPath.Should().Be(Path.Combine(files, "Roaming"));
        layout.SettingsFolderPath.Should().Be(Path.Combine(files, "Settings"));
        layout.LocalCacheFolderPath.Should().Be(cache);
        layout.TemporaryFolderPath.Should().Be(Path.Combine(cache, "Temp"));
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
