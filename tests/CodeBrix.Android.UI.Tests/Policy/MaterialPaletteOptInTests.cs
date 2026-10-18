using System;
using System.IO;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.Tests.Policy;

/// <summary>
/// The Material palette is opt-in (ThemeBridge and CodeBrixApplication are Android-only, so their source is the
/// fence): the bridge starts Fluent, the application exposes UseMaterialPalette (default false), and it completes the
/// bridge's installation right after the XAML Application is created - the posted installation runs before the
/// application exists and could never write the roles.
/// </summary>
public sealed class MaterialPaletteOptInTests
{
    [Fact]
    public void the_theme_bridge_starts_with_the_fluent_palette()
    {
        //Arrange
        var source = Source("src", "CodeBrix.Android.UI", "Policy", "ThemeBridge.cs");

        //Assert
        source.Should().Contain("private static bool _materialPalette;");
        source.Should().NotContain("private static bool _materialPalette = true;");
        source.Should().Contain("internal static void OnXamlApplicationStarted(bool materialPalette)");
        Between(source, "internal static void OnXamlApplicationStarted(bool materialPalette)", "internal static void Apply()")
            .Should().Contain("EnsureInstalled();\n        MaterialPalette = materialPalette;");
    }

    [Fact]
    public void the_application_opts_in_through_use_material_palette_right_after_the_xaml_application_starts()
    {
        //Arrange
        var source = Source("src", "CodeBrix.Android.UI", "Hosting", "CodeBrixApplication.cs");
        var start = Between(source, "internal void EnsureXamlApplicationStarted(CodeBrixActivity activity)", "private void ConfigureLoggingCore()");
        var created = start.IndexOf("MUX.Application.Start(", StringComparison.Ordinal);
        var bridged = start.IndexOf("Policy.ThemeBridge.OnXamlApplicationStarted(UseMaterialPalette);", StringComparison.Ordinal);

        //Assert
        source.Should().Contain("protected virtual bool UseMaterialPalette => false;");
        created.Should().BeGreaterThan(-1);
        bridged.Should().BeGreaterThan(created);
    }

    [Fact]
    public void the_consumer_readme_documents_fluent_as_the_default_and_the_override()
    {
        //Arrange
        var readme = Source("AGENT-README.txt");

        //Assert
        readme.Should().Contain("protected override bool UseMaterialPalette => true;");
        readme.Should().Contain("by default an app shows the framework's Fluent");
        readme.Should().NotContain("an app that sets no control keys looks Material 3\n");
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

        return directory?.FullName ?? throw new InvalidOperationException("The repository root was not found.");
    }
}
