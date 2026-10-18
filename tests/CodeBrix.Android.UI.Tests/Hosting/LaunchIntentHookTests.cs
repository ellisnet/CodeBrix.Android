using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.Tests.Hosting;

/// <summary>
/// The Android-only half of the single-instance rule (CodeBrixActivity and CodeBrixApplication are not in this net10.0
/// build, so their source is the fence): the launch-intent hook, the order of the second-instance check in OnCreate,
/// the forward-then-finish path, and the single-task launch mode of the template's and every sample's main activity.
/// </summary>
public sealed class LaunchIntentHookTests
{
    [Fact]
    public void the_application_exposes_an_overridable_launch_intent_hook_that_does_nothing_by_default()
    {
        //Arrange
        var source = Source("src", "CodeBrix.Android.UI", "Hosting", "CodeBrixApplication.cs");

        //Assert
        source.Should().Contain("protected virtual void OnLaunchIntent(AIntent intent)\n    {\n    }");
        source.Should().Contain("internal void DeliverLaunchIntent(AIntent intent)");
        Between(source, "internal void DeliverLaunchIntent", "/// <summary>Creates the app's XAML Application").Should().Contain("OnLaunchIntent(intent);");
    }

    [Fact]
    public void on_new_intent_surfaces_every_launch_except_the_hosts_bring_to_front_intent()
    {
        //Arrange
        var source = Source("src", "CodeBrix.Android.UI", "Hosting", "CodeBrixActivity.cs");
        var onNewIntent = Between(source, "protected override void OnNewIntent(AIntent intent)", "internal void ReceiveLaunchIntent");
        var receive = Between(source, "internal void ReceiveLaunchIntent(AIntent intent)", "private void ForwardToRunningInstance");

        //Assert
        onNewIntent.Should().Contain("base.OnNewIntent(intent);");
        onNewIntent.Should().Contain("ActivityInstancePolicy.SurfacesAsLaunch(intent.GetBooleanExtra(ActivityInstancePolicy.BringToFrontExtra, false))");
        onNewIntent.Should().Contain("ReceiveLaunchIntent(intent);");
        receive.Should().Contain("Intent = intent;");
        receive.Should().Contain("DeliverLaunchIntent(intent)");
    }

    [Fact]
    public void on_create_decides_before_it_builds_anything_so_a_second_instance_never_gets_a_window()
    {
        //Arrange
        var source = Source("src", "CodeBrix.Android.UI", "Hosting", "CodeBrixActivity.cs");
        var onCreate = Between(source, "protected override void OnCreate(ABundle savedInstanceState)", "protected override void OnNewIntent");
        var decide = onCreate.IndexOf("ActivityInstancePolicy.Decide(", StringComparison.Ordinal);
        var forward = onCreate.IndexOf("ForwardToRunningInstance(host);\n            return;", StringComparison.Ordinal);

        //Assert
        decide.Should().BeGreaterThan(onCreate.IndexOf("base.OnCreate(savedInstanceState);", StringComparison.Ordinal));
        forward.Should().BeGreaterThan(decide);
        forward.Should().BeLessThan(onCreate.IndexOf("new CodeBrixRootLayout(this)", StringComparison.Ordinal));
        forward.Should().BeLessThan(onCreate.IndexOf("ActivityRegistry.OnCreated(this)", StringComparison.Ordinal));
        forward.Should().BeLessThan(onCreate.IndexOf("EnsureXamlApplicationStarted(this)", StringComparison.Ordinal));
        onCreate.Should().Contain("host?.IsFinishing == true");
    }

    [Fact]
    public void on_create_hands_the_launch_that_created_a_new_activity_to_the_application_after_showing_the_window()
    {
        //Arrange
        var source = Source("src", "CodeBrix.Android.UI", "Hosting", "CodeBrixActivity.cs");
        var onCreate = Between(source, "protected override void OnCreate(ABundle savedInstanceState)", "protected override void OnNewIntent");
        var attach = onCreate.IndexOf("existingWindow.AttachActivity(this);", StringComparison.Ordinal);
        var surface = onCreate.IndexOf("ActivityInstancePolicy.SurfacesCreateAsLaunch(action, savedInstanceState != null, launch.GetBooleanExtra(ActivityInstancePolicy.BringToFrontExtra, false))", StringComparison.Ordinal);
        var deliver = onCreate.IndexOf("application.DeliverLaunchIntent(launch);", StringComparison.Ordinal);

        //Assert - the window is shown first, then the launch is surfaced by the policy's rule (same as OnNewIntent)
        attach.Should().BeGreaterThan(-1);
        surface.Should().BeGreaterThan(attach);
        deliver.Should().BeGreaterThan(surface);
        deliver.Should().BeLessThan(onCreate.IndexOf("EnsureXamlApplicationStarted(this)", StringComparison.Ordinal));
    }

    [Fact]
    public void the_second_instance_warns_forwards_its_intent_finishes_and_reorders_the_running_activity_to_the_front()
    {
        //Arrange
        var source = Source("src", "CodeBrix.Android.UI", "Hosting", "CodeBrixActivity.cs");
        var path = Between(source, "private void ForwardToRunningInstance(CodeBrixActivity running)", "/// <inheritdoc />");
        var warn = path.IndexOf("LogWarning(", StringComparison.Ordinal);
        var forwardIntent = path.IndexOf("running.ReceiveLaunchIntent(launch);", StringComparison.Ordinal);
        var finish = path.IndexOf("Finish();", StringComparison.Ordinal);
        var start = path.IndexOf("StartActivity(bringToFront);", StringComparison.Ordinal);

        //Assert
        path.Should().Contain("single-task");
        path.Should().Contain("AActivityFlags.NewTask | AActivityFlags.ReorderToFront");
        path.Should().Contain("PutExtra(ActivityInstancePolicy.BringToFrontExtra, true)");
        path.Should().NotContain("MoveTaskToFront", "REORDER_TASKS would need a manifest permission");
        warn.Should().BeGreaterThan(-1);
        forwardIntent.Should().BeGreaterThan(warn);
        finish.Should().BeGreaterThan(forwardIntent);
        start.Should().BeGreaterThan(finish, "the reorder must find the running instance, not this finishing one");
    }

    [Fact]
    public void a_finishing_activity_hands_the_window_over_and_never_closes_a_window_another_activity_shows()
    {
        //Arrange - found along the way (WP9): the activity that lost the window kept its WindowWrapper, so its stop
        // hid and its destroy CLOSED the window the new activity showed.
        var activity = Source("src", "CodeBrix.Android.UI", "Hosting", "CodeBrixActivity.cs");
        var wrapper = Source("src", "CodeBrix.Android.UI", "Hosting", "AndroidNativeWindowWrapper.cs");
        var attach = Between(wrapper, "internal void AttachActivity(CodeBrixActivity activity)", "internal void DetachActivity");

        //Assert
        activity.Should().Contain("wrapper?.DetachActivity(this);");
        attach.Should().Contain("previous.ReleaseWindow(this);");
        attach.IndexOf("previous.ReleaseWindow(this);", StringComparison.Ordinal)
            .Should().BeLessThan(attach.IndexOf("Activity = activity;", StringComparison.Ordinal));
    }

    [Fact]
    public void the_template_and_every_codebrix_main_activity_in_the_repository_is_single_task()
    {
        //Arrange
        var root = RepoRoot();
        var files = new[] { "templates", "samples", "tests" }
            .SelectMany(folder => Directory.EnumerateFiles(Path.Combine(root, folder), "MainActivity.cs", SearchOption.AllDirectories))
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                           && !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .ToList();

        //Assert
        files.Should().Contain(file => file.EndsWith(Path.Combine("TemplateApp.Android", "MainActivity.cs"), StringComparison.Ordinal));
        foreach (var file in files)
        {
            var source = File.ReadAllText(file);
            var attribute = Regex.Match(source, @"\[Activity\((?<args>.*?)\)\]", RegexOptions.Singleline).Groups["args"].Value;
            var singleTask = attribute.Contains("LaunchMode = LaunchMode.SingleTask", StringComparison.Ordinal)
                || (attribute.Contains("LaunchMode = MainActivity.MainLaunchMode", StringComparison.Ordinal)
                    && Regex.IsMatch(source, @"#else\s+internal const LaunchMode MainLaunchMode = LaunchMode\.SingleTask;"));
            singleTask.Should().BeTrue($"{Path.GetRelativePath(root, file)} declares its main activity single-task");
        }
    }

    private static string Between(string source, string start, string end)
    {
        var from = source.IndexOf(start, StringComparison.Ordinal);
        from.Should().BeGreaterThan(-1, $"the source contains '{start}'");
        var to = source.IndexOf(end, from + start.Length, StringComparison.Ordinal);
        to.Should().BeGreaterThan(from, $"'{end}' follows '{start}'");
        return source.Substring(from, to - from);
    }

    private static string Source(params string[] parts) =>
        File.ReadAllText(Path.Combine(RepoRoot(), Path.Combine(parts))).Replace("\r\n", "\n", StringComparison.Ordinal);

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "CodeBrix.Android.slnx")))
        {
            directory = directory.Parent;
        }

        directory.Should().NotBeNull("the tests run inside the repository");
        return directory!.FullName;
    }
}
