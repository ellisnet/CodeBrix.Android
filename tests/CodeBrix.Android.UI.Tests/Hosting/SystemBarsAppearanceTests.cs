using System;
using System.IO;
using CodeBrix.Android.UI.Portable;
using CodeBrix.Android.UI.Tests.HostFree;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.Tests.Hosting;

/// <summary>
/// SystemBarsAppearance: the system bars' icon colour follows the theme the window's content shows (its ActualTheme),
/// not only Application.RequestedTheme - an app that switches theme on its root element flips the bars - and falls back
/// to the application's theme while the window has no content.
/// </summary>
[Collection(HostFreeCoreCollection.Name)]
public sealed class SystemBarsAppearanceTests
{
    public SystemBarsAppearanceTests() => HostFreeCore.EnsureInitialized();

    [Fact]
    public void the_theme_source_is_the_top_element_of_the_page()
    {
        //Arrange - window content -> page-like UserControl -> Border -> the page's root grid
        var root = new Grid();
        var page = new UserControl { Content = new Border { Child = root } };
        var content = new ContentControl { Content = page };

        //Act
        var source = SystemBarsAppearance.FindThemeSource(content);

        //Assert
        source.Should().BeSameAs(root);
    }

    [Fact]
    public void a_content_host_without_element_content_is_its_own_theme_source()
    {
        //Arrange
        var content = new ContentControl { Content = "text" };

        //Act
        var source = SystemBarsAppearance.FindThemeSource(content);

        //Assert
        source.Should().BeSameAs(content);
        SystemBarsAppearance.FindThemeSource(null).Should().BeNull();
    }

    [Fact]
    public void a_theme_set_on_the_page_root_reaches_the_theme_source()
    {
        //Arrange - an app that sets RequestedTheme on its page's root grid (GitHubIssueFinder does)
        var root = new Grid();
        var content = new ContentControl { Content = new UserControl { Content = root } };
        var source = SystemBarsAppearance.FindThemeSource(content);

        //Act
        root.RequestedTheme = ElementTheme.Dark;

        //Assert
        SystemBarsAppearance.UseLightBars(true, source.ActualTheme, ApplicationTheme.Light).Should().BeFalse();
    }

    [Theory]
    [InlineData(ElementTheme.Light, ApplicationTheme.Dark, true)]
    [InlineData(ElementTheme.Dark, ApplicationTheme.Light, false)]
    [InlineData(ElementTheme.Light, ApplicationTheme.Light, true)]
    [InlineData(ElementTheme.Dark, ApplicationTheme.Dark, false)]
    public void the_content_actual_theme_decides_the_bars(ElementTheme content, ApplicationTheme application, bool expected)
    {
        //Act
        var light = SystemBarsAppearance.UseLightBars(hasContent: true, content, application);

        //Assert
        light.Should().Be(expected);
    }

    [Theory]
    [InlineData(ApplicationTheme.Light, true)]
    [InlineData(ApplicationTheme.Dark, false)]
    public void without_content_the_application_theme_decides_the_bars(ApplicationTheme application, bool expected)
    {
        //Act
        var light = SystemBarsAppearance.UseLightBars(hasContent: false, ElementTheme.Dark, application);

        //Assert
        light.Should().Be(expected);
    }

    [Theory]
    [InlineData(ApplicationTheme.Light, true)]
    [InlineData(ApplicationTheme.Dark, false)]
    public void content_without_a_resolved_theme_falls_back_to_the_application_theme(ApplicationTheme application, bool expected)
    {
        //Act
        var light = SystemBarsAppearance.UseLightBars(hasContent: true, ElementTheme.Default, application);

        //Assert
        light.Should().Be(expected);
    }

    [Fact]
    public void the_window_wrapper_follows_the_content_theme_and_its_replacement()
    {
        //Arrange - AndroidNativeWindowWrapper is Android-only (not in this net10.0 build), so its source is the fence
        var source = File.ReadAllText(Path.Combine(RepoRoot(), "src", "CodeBrix.Android.UI", "Hosting", "AndroidNativeWindowWrapper.cs"));

        //Assert
        source.Should().Contain("SystemBarsAppearance.UseLightBars(");
        source.Should().Contain("SystemBarsAppearance.FindThemeSource(Window?.Content)");
        source.Should().Contain("source.ActualThemeChanged += OnThemeSourceActualThemeChanged;");
        source.Should().Contain("source.Unloaded += OnThemeSourceUnloaded;");
        source.Should().Contain("source.LayoutUpdated += OnThemeSourceLayoutUpdated;");
        source.Should().Contain("RequestedThemeChanged +=");
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "CodeBrix.Android.slnx")))
        {
            directory = directory.Parent;
        }

        directory.Should().NotBeNull("the tests run inside the repository");
        return directory.FullName;
    }
}
