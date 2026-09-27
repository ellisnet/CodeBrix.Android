using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.WinUI.Graphics3DGL.Tests.Public;

/// <summary>
/// API parity of the Android SkiaGLCanvasElement with the Platform flavor, whose constructor is
/// <c>SkiaGLCanvasElement(Func&lt;Window&gt;? getWindowFunc = null)</c>: a pasted page writes <c>new SkiaGLCanvasElement()</c>
/// (FIXLIST [AP8-T] helper B). The element is Android code (net10.0-android), so the fence reads its source; the
/// SimpleCbxVideoPlayer paste-always head compiles the parameterless call for real.
/// </summary>
public class SkiaGLCanvasElementTests
{
    private static string Source()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
        {
            var path = Path.Combine(directory.FullName, "src", "AddIns", "CodeBrix.Android.WinUI.Graphics3DGL", "Public", "SkiaGLCanvasElement.cs");
            if (File.Exists(path))
            {
                return File.ReadAllText(path);
            }
        }

        throw new FileNotFoundException("SkiaGLCanvasElement.cs not found above " + AppContext.BaseDirectory);
    }

    [Fact]
    public void Every_public_constructor_can_be_called_without_arguments()
    {
        //Arrange
        var source = Source();

        //Act
        var constructors = Regex.Matches(source, @"public\s+SkiaGLCanvasElement\s*\(([^)]*)\)")
            .Select(m => m.Groups[1].Value)
            .ToList();

        //Assert
        constructors.Should().NotBeEmpty();
        foreach (var parameters in constructors)
        {
            var list = parameters.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            list.Should().OnlyContain(p => p.Contains('='), "the Platform flavor's constructor is SkiaGLCanvasElement(Func<Window>? getWindowFunc = null)");
        }
    }
}
