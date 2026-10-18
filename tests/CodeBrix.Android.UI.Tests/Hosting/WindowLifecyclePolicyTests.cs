using System;
using System.IO;
using CodeBrix.Android.UI.Portable;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.Tests.Hosting;

/// <summary>
/// WindowLifecyclePolicy: the host never closes the single XAML window when an activity is destroyed (backing out keeps it
/// open, the next activity shows it again - a relaunch never shows a splash or a blank page); an app's own Window.Close is
/// closed by Core alone (Closed raised once) and a later launch activates that closed window again.
/// </summary>
public sealed class WindowLifecyclePolicyTests
{
    [Fact]
    public void the_next_activity_shows_a_window_backing_out_did_not_close()
    {
        //Act
        var attached = WindowLifecyclePolicy.OnActivityAttached(wasShown: true, closedByApp: false);

        //Assert
        attached.Should().Be(AttachedActivityWindowAction.ShowAgain);
    }

    [Fact]
    public void a_launch_after_the_app_closed_its_window_activates_it_again()
    {
        //Act - Core resets WasShown when it closes the single window
        var attached = WindowLifecyclePolicy.OnActivityAttached(wasShown: false, closedByApp: true);

        //Assert
        attached.Should().Be(AttachedActivityWindowAction.Reactivate);
    }

    [Fact]
    public void a_window_the_app_never_activated_waits_for_its_activation()
    {
        //Act
        var attached = WindowLifecyclePolicy.OnActivityAttached(wasShown: false, closedByApp: false);

        //Assert
        attached.Should().Be(AttachedActivityWindowAction.WaitForActivation);
    }

    [Fact]
    public void the_host_never_closes_the_window_so_closed_is_raised_once_by_core()
    {
        //Arrange - CodeBrixActivity and AndroidNativeWindowWrapper are Android-only, so their source is the fence
        var hosting = Path.Combine(RepoRoot(), "src", "CodeBrix.Android.UI", "Hosting");
        var activity = File.ReadAllText(Path.Combine(hosting, "CodeBrixActivity.cs"));
        var wrapper = File.ReadAllText(Path.Combine(hosting, "AndroidNativeWindowWrapper.cs"));
        var closeCore = wrapper.Substring(wrapper.IndexOf("protected override void CloseCore()", StringComparison.Ordinal));
        closeCore = closeCore.Substring(0, closeCore.IndexOf("\n    }", StringComparison.Ordinal));

        //Assert - no second Closing/Closed from the activity's destroy; CloseCore (Core's own close) only marks and finishes
        activity.Should().NotContain("OnNativeClosed");
        wrapper.Should().NotContain("RaiseClosing()");
        closeCore.Should().Contain("_closedByApp = true;");
        closeCore.Should().Contain("activity.Finish();");
        wrapper.Should().Contain("WindowLifecyclePolicy.OnActivityAttached(WasShown, _closedByApp)");
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
