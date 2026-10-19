using System;
using System.IO;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.Tests.Handlers;

/// <summary>
/// ThemeResources.TryFind probes OPTIONAL theme keys (lightweight-styling keys an app may or may not define), so its
/// top-level fallback must be the quiet lookup: ResourceResolver.ResolveResourceStatic logs "Couldn't statically resolve
/// resource" for every miss, which filled app logs with one warning per optional key. A source fence (the probe needs the
/// Core's dictionaries, which the host-free collection does not populate the same way a running app does).
/// </summary>
public class ThemeResourcesProbeTests
{
    [Fact]
    public void the_top_level_fallback_is_the_quiet_lookup()
    {
        //Arrange
        var source = File.ReadAllText(Path.Combine(RepoRoot(), "src", "CodeBrix.Android.UI", "Handlers", "Inputs", "Support", "ThemeResources.cs"));

        //Assert
        source.Should().Contain("ResourceResolver.TryTopLevelRetrieval(new SpecializedResourceDictionary.ResourceKey(key), null, out value)");
        source.Should().NotContain("ResolveResourceStatic(key", "ResolveResourceStatic warns once per key it does not find");
    }

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
