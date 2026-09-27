using System;
using System.Collections.Generic;
using System.IO;
using CodeBrix.Android.ParityScore.Scanning;

namespace CodeBrix.Android.ParityScore.Tests.Fixture;

/// <summary>The fixture conventions and a reader of this test assembly.</summary>
internal static class FixtureSupport
{
    internal static ParityConventions Conventions { get; } = new()
    {
        DependencyPropertyType = "ParityFixture.Xaml.DependencyProperty",
        BaseElementTypes = new[] { "ParityFixture.Xaml.UIElement", "ParityFixture.Xaml.FrameworkElement" },
        NotImplementedAttribute = "ParityFixture.Xaml.NotImplementedAttribute",
        RaiseNotImplementedType = "ParityFixture.Xaml.ApiInformation",
        MapperNamespace = "ParityFixture.Handlers",
        RegistrationTypes = new HashSet<string>(StringComparer.Ordinal) { "ParityFixture.Handlers.ElementHandlerRegistry", "ParityFixture.Handlers.CodeBrixHandlers" },
        HandlerInterface = "ParityFixture.Handlers.IAndroidElementHandler",
        FallbackHandler = "ParityFixture.Handlers.TemplatedFallbackHandler",
    };

    /// <summary>This test assembly's file.</summary>
    internal static string AssemblyPath => typeof(FixtureSupport).Assembly.Location;

    /// <summary>A set holding this test assembly.</summary>
    internal static AssemblySet OpenSet()
    {
        var set = new AssemblySet();
        set.Add(AssemblyPath);
        return set;
    }

    /// <summary>A fresh, empty temporary folder.</summary>
    internal static string TempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "parity-score-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>The repository root (the folder holding CodeBrix.Android.slnx), or null.</summary>
    internal static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CodeBrix.Android.slnx")))
            {
                return directory.FullName;
            }
        }

        return null;
    }
}
