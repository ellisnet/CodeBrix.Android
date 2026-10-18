using System;
using System.IO;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.Tests.Handlers;

/// <summary>
/// Native text keeps fractional glyph advances (parity with the Skia heads' measure): the text measure's paint and the
/// TextBlock handler's text view share TextAndroidPlatform.NativeTextPaintFlags, which carries SubpixelText and LinearText. Both are
/// Android-only types, so their source is the fence.
/// </summary>
public sealed class NativeTextPaintFlagsTests
{
    [Fact]
    public void the_native_text_paint_flags_carry_subpixel_text()
    {
        //Arrange
        var source = File.ReadAllText(Path.Combine(SourceRoot(), "Android", "TextAndroidPlatform.cs"));

        //Assert
        source.Should().Contain("internal const APaintFlags NativeTextPaintFlags = APaintFlags.AntiAlias | APaintFlags.SubpixelText | APaintFlags.LinearText;");
        source.Should().Contain("new ATextPaint(NativeTextPaintFlags)");
    }

    [Fact]
    public void the_text_block_view_draws_with_the_same_flags_the_measure_uses()
    {
        //Arrange
        var source = File.ReadAllText(Path.Combine(SourceRoot(), "Handlers", "Text", "TextBlockHandler.cs"));

        //Assert
        source.Should().Contain("view.PaintFlags |= TextAndroidPlatform.NativeTextPaintFlags;");
    }

    private static string SourceRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "CodeBrix.Android.slnx")))
        {
            directory = directory.Parent;
        }

        directory.Should().NotBeNull("the tests run inside the repository");
        return Path.Combine(directory.FullName, "src", "CodeBrix.Android.UI");
    }
}
