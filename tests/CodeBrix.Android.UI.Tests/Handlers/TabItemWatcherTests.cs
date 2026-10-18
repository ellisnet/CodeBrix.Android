using System;
using System.IO;
using CodeBrix.Android.UI.Handlers;
using CodeBrix.Android.UI.Tests.HostFree;
using Microsoft.UI.Xaml.Controls;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.Tests.Handlers;

/// <summary>
/// TabItemWatcher: the native TabView strip refreshes when a shown tab's Header (a renamed tab, a "*" dirty marker) or
/// IsClosable changes; tabs no longer shown and a disconnected handler leave no registration behind.
/// </summary>
[Collection(HostFreeCoreCollection.Name)]
public sealed class TabItemWatcherTests
{
    public TabItemWatcherTests() => HostFreeCore.EnsureInitialized();

    [Fact]
    public void a_renamed_tab_refreshes_the_strip()
    {
        //Arrange
        var refreshes = 0;
        var watcher = new TabItemWatcher(() => refreshes++);
        var tab = new TabViewItem { Header = "Unsaved Image 1" };
        watcher.Sync(new[] { tab });

        //Act
        tab.Header = "drawing.png";
        tab.Header = "drawing.png*";

        //Assert
        refreshes.Should().Be(2);
    }

    [Fact]
    public void a_tab_that_becomes_closable_refreshes_the_strip()
    {
        //Arrange
        var refreshes = 0;
        var watcher = new TabItemWatcher(() => refreshes++);
        var tab = new TabViewItem { IsClosable = false };
        watcher.Sync(new[] { tab });

        //Act
        tab.IsClosable = true;

        //Assert
        refreshes.Should().Be(1);
    }

    [Fact]
    public void a_tab_no_longer_shown_is_released()
    {
        //Arrange
        var refreshes = 0;
        var watcher = new TabItemWatcher(() => refreshes++);
        var kept = new TabViewItem { Header = "a" };
        var removed = new TabViewItem { Header = "b" };
        watcher.Sync(new[] { kept, removed });

        //Act
        watcher.Sync(new[] { kept, null });
        removed.Header = "b2";
        kept.Header = "a2";

        //Assert
        watcher.Count.Should().Be(1);
        refreshes.Should().Be(1);
    }

    [Fact]
    public void clear_releases_every_tab()
    {
        //Arrange
        var refreshes = 0;
        var watcher = new TabItemWatcher(() => refreshes++);
        var tab = new TabViewItem { Header = "a" };
        watcher.Sync(new[] { tab });
        watcher.Sync(new[] { tab });

        //Act
        watcher.Clear();
        tab.Header = "b";

        //Assert
        watcher.Count.Should().Be(0);
        refreshes.Should().Be(0);
    }

    [Fact]
    public void the_tab_view_handler_syncs_on_every_read_and_clears_on_disconnect()
    {
        //Arrange - TabViewHandler is Android-only, so its source is the fence
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "CodeBrix.Android.slnx")))
        {
            directory = directory.Parent;
        }

        directory.Should().NotBeNull("the tests run inside the repository");
        var source = File.ReadAllText(Path.Combine(directory.FullName, "src", "CodeBrix.Android.UI", "Handlers", "Tabs", "TabViewHandler.cs"));

        //Assert
        source.Should().Contain("_tabWatcher.Sync(containers);");
        source.Should().Contain("_tabWatcher.Clear();");
        source.Should().Contain("new TabItemWatcher(PostRefresh)");
    }
}
