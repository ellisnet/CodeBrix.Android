using System;
using System.IO;
using System.Reflection;
using CodeBrix.Android.Portable;
using CodeBrix.Android.UI.Tests.HostFree;
using CodeBrix.Platform.Contracts;
using SilverAssertions;
using Windows.Storage;
using Xunit;

namespace CodeBrix.Android.UI.Tests.WinRT;

/// <summary>
/// ApplicationDataFolders: the local, roaming, cache and temporary folders exist after their first query; the settings
/// folder is never created by a query - Core's ApplicationData.LocalSettings creates it when the first setting is
/// written (no empty files/Settings folder for an app that never uses LocalSettings). Runs in the host-free Core
/// collection: the last test points Core's ApplicationData at these folders for one fresh instance and restores it.
/// </summary>
[Collection(HostFreeCoreCollection.Name)]
public sealed class ApplicationDataFoldersTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "cbx-appdata-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void querying_the_other_folders_creates_them()
    {
        //Arrange
        var folders = CreateFolders();

        //Act
        var local = folders.GetLocalFolderPath();
        var roaming = folders.GetRoamingFolderPath();
        var cache = folders.GetLocalCacheFolderPath();
        var temporary = folders.GetTemporaryFolderPath();

        //Assert
        Directory.Exists(local).Should().BeTrue();
        Directory.Exists(roaming).Should().BeTrue();
        Directory.Exists(cache).Should().BeTrue();
        Directory.Exists(temporary).Should().BeTrue();
    }

    [Fact]
    public void querying_the_settings_path_creates_nothing()
    {
        //Arrange
        var folders = CreateFolders();

        //Act
        var settings = folders.GetSettingsFolderPath();

        //Assert
        settings.Should().Be(Path.Combine(_root, "files", "Settings"));
        Directory.Exists(settings).Should().BeFalse();
    }

    [Fact]
    public void the_layout_is_computed_once_on_first_use()
    {
        //Arrange
        var calls = 0;
        var folders = new ApplicationDataFolders(() =>
        {
            calls++;
            return new ApplicationDataLayout(Path.Combine(_root, "files"), Path.Combine(_root, "cache"));
        });

        //Act
        var before = calls;
        folders.GetSettingsFolderPath();
        folders.GetLocalFolderPath();

        //Assert
        before.Should().Be(0);
        calls.Should().Be(1);
    }

    [Fact]
    public void writing_a_setting_through_core_creates_the_settings_folder()
    {
        //Arrange
        var platformField = typeof(ApplicationData).GetField("_applicationDataPlatform", BindingFlags.NonPublic | BindingFlags.Static);
        platformField.Should().NotBeNull("Core's ApplicationData resolves its platform into a private static field");
        var saved = platformField.GetValue(null);
        var folders = CreateFolders();
        var settingsFolder = folders.GetSettingsFolderPath();
        try
        {
            platformField.SetValue(null, (IApplicationDataPlatform)folders);
            var data = (ApplicationData)Activator.CreateInstance(typeof(ApplicationData), nonPublic: true);

            //Act
            var container = data.LocalSettings;
            var existsAfterOpen = Directory.Exists(settingsFolder);
            container.Values["answer"] = 42;

            //Assert
            existsAfterOpen.Should().BeFalse("opening LocalSettings reads only an existing file");
            Directory.Exists(settingsFolder).Should().BeTrue("the first written setting creates the folder");
            Directory.GetFiles(settingsFolder).Should().NotBeEmpty();
        }
        finally
        {
            platformField.SetValue(null, saved);
        }
    }

    private ApplicationDataFolders CreateFolders() =>
        new(() => new ApplicationDataLayout(Path.Combine(_root, "files"), Path.Combine(_root, "cache")));
}
