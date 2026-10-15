using System;
using System.IO;
using System.Reflection;
using CodeBrix.Android.UI.Portable;
using CodeBrix.Platform.UI.Hosting;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.Tests.Hosting;

/// <summary>The non-parallel collection the latch tests run in (they flip an application-wide switch).</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class DirectGpuLatchCollection
{
    /// <summary>The collection name.</summary>
    public const string Name = "Direct GPU rendering latch";
}

/// <summary>
/// CodeBrixApplication.UseDirectGpuRenderingMode: the application's opt-in is applied to the CodeBrix.Platform latch
/// (DirectGpuRenderingMode) in OnCreate - on when asked, never off, default off. The latch is a one-way switch, so each
/// test saves and restores its private field by reflection (the discipline the WebGpu add-in suite uses).
/// </summary>
[Collection(DirectGpuLatchCollection.Name)]
public sealed class DirectGpuRenderingOptInTests : IDisposable
{
    private static readonly FieldInfo Latch =
        typeof(DirectGpuRenderingMode).GetField("_isEnabled", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("DirectGpuRenderingMode no longer has a private static _isEnabled latch.");

    private readonly bool _saved;

    public DirectGpuRenderingOptInTests()
    {
        _saved = DirectGpuRenderingMode.IsEnabled;
        Latch.SetValue(null, false);
    }

    public void Dispose() => Latch.SetValue(null, _saved);

    [Fact]
    public void an_application_that_does_not_ask_leaves_direct_gpu_rendering_off()
    {
        //Act
        var enabled = DirectGpuRenderingOptIn.Apply(false);

        //Assert
        enabled.Should().BeFalse();
        DirectGpuRenderingMode.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void an_application_that_asks_turns_direct_gpu_rendering_on()
    {
        //Act
        var enabled = DirectGpuRenderingOptIn.Apply(true);

        //Assert
        enabled.Should().BeTrue();
        DirectGpuRenderingMode.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void the_latch_is_one_way_a_later_false_never_turns_it_off()
    {
        //Arrange
        DirectGpuRenderingOptIn.Apply(true);

        //Act
        var enabled = DirectGpuRenderingOptIn.Apply(false);

        //Assert
        enabled.Should().BeTrue();
        DirectGpuRenderingMode.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void the_application_class_declares_the_opt_in_off_by_default_and_applies_it_in_on_create()
    {
        //Arrange - CodeBrixApplication is Android-only (not in this net10.0 build), so its source is the fence
        var source = File.ReadAllText(Path.Combine(RepoRoot(), "src", "CodeBrix.Android.UI", "Hosting", "CodeBrixApplication.cs"));
        var onCreate = source.Substring(source.IndexOf("public override void OnCreate()", StringComparison.Ordinal));
        var bootstrap = onCreate.IndexOf("AndroidPlatformBootstrap.EnsureRegistered()", StringComparison.Ordinal);
        var apply = onCreate.IndexOf("DirectGpuRenderingOptIn.Apply(UseDirectGpuRenderingMode)", StringComparison.Ordinal);

        //Assert
        source.Should().Contain("protected virtual bool UseDirectGpuRenderingMode => false;");
        source.Should().Contain("EXPERIMENTAL");
        apply.Should().BeGreaterThan(0, "OnCreate applies the opt-in");
        apply.Should().BeLessThan(bootstrap, "the latch is set before the platform bootstrap and any XAML type");
    }

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
