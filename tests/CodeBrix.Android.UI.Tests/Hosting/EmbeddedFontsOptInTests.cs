using System;
using System.IO;
using CodeBrix.Android.UI.Portable;
using CodeBrix.Android.UI.Tests.HostFree;
using CodeBrix.Platform.UI;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.Tests.Hosting;

/// <summary>
/// CodeBrixApplication.RestrictToEmbeddedFonts: the application's opt-in sets the CodeBrix.Platform flag
/// FeatureConfiguration.Font.RestrictToEmbeddedFonts in OnCreate, before the platform bootstrap - on when asked, left
/// alone otherwise (not a latch: the flag stays settable directly). Runs in the host-free Core collection (the flag is
/// static Core state); each test restores it.
/// </summary>
[Collection(HostFreeCoreCollection.Name)]
public sealed class EmbeddedFontsOptInTests : IDisposable
{
    private readonly bool _saved;

    public EmbeddedFontsOptInTests()
    {
        _saved = FeatureConfiguration.Font.RestrictToEmbeddedFonts;
        FeatureConfiguration.Font.RestrictToEmbeddedFonts = false;
    }

    public void Dispose() => FeatureConfiguration.Font.RestrictToEmbeddedFonts = _saved;

    [Fact]
    public void an_application_that_does_not_ask_leaves_the_flag_off()
    {
        //Act
        var restricted = EmbeddedFontsOptIn.Apply(false);

        //Assert
        restricted.Should().BeFalse();
        FeatureConfiguration.Font.RestrictToEmbeddedFonts.Should().BeFalse();
    }

    [Fact]
    public void an_application_that_asks_sets_the_flag()
    {
        //Act
        var restricted = EmbeddedFontsOptIn.Apply(true);

        //Assert
        restricted.Should().BeTrue();
        FeatureConfiguration.Font.RestrictToEmbeddedFonts.Should().BeTrue();
    }

    [Fact]
    public void the_flag_is_not_a_latch_it_can_still_be_cleared_directly()
    {
        //Arrange
        EmbeddedFontsOptIn.Apply(true);

        //Act
        FeatureConfiguration.Font.RestrictToEmbeddedFonts = false;
        var restricted = EmbeddedFontsOptIn.Apply(false);

        //Assert
        restricted.Should().BeFalse();
    }

    [Fact]
    public void a_flag_the_app_set_directly_is_kept_when_the_override_is_off()
    {
        //Arrange
        FeatureConfiguration.Font.RestrictToEmbeddedFonts = true;

        //Act
        var restricted = EmbeddedFontsOptIn.Apply(false);

        //Assert
        restricted.Should().BeTrue();
    }

    [Fact]
    public void the_application_class_declares_the_opt_in_off_by_default_and_applies_it_before_the_bootstrap()
    {
        //Arrange - CodeBrixApplication is Android-only (not in this net10.0 build), so its source is the fence
        var source = File.ReadAllText(Path.Combine(RepoRoot(), "src", "CodeBrix.Android.UI", "Hosting", "CodeBrixApplication.cs"));
        var onCreate = source.Substring(source.IndexOf("public override void OnCreate()", StringComparison.Ordinal));
        var bootstrap = onCreate.IndexOf("AndroidPlatformBootstrap.EnsureRegistered()", StringComparison.Ordinal);
        var apply = onCreate.IndexOf("EmbeddedFontsOptIn.Apply(RestrictToEmbeddedFonts)", StringComparison.Ordinal);

        //Assert
        source.Should().Contain("protected virtual bool RestrictToEmbeddedFonts => false;");
        apply.Should().BeGreaterThan(0, "OnCreate applies the opt-in");
        apply.Should().BeLessThan(bootstrap, "the flag is set before the platform bootstrap and any text measure");
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
