// Ported from CodeBrix.Platform (Apache License 2.0; THIRD-PARTY-NOTICES.txt item 3): src/AddIns/Platform.UI.VideoPlayer.Tests/VideoFrameReadbackTests.cs
// (branch main @ a26d07f74c1396007bd3e3480872456538e3876b; the working tree the pinned build 1.0.287.1311 was packed from; the WP8a fix, uncommitted there).
// Changes are marked ANDROID PORT: (1) the namespace of this test assembly (the helper is linked in from the Android add-in).

#nullable enable

#nullable enable

using System;
using System.Runtime.InteropServices;
using CodeBrix.Platform.UI.VideoPlayer.Skia.Internal;
using SilverAssertions;
using SkiaSharp;
using Xunit;

namespace CodeBrix.Android.UI.VideoPlayer.Tests.Surface; // ANDROID PORT (1)

/// <summary>
/// The lifetime of the pixels behind a presented picture. The desktop heads RECORD the window frame (with the player's
/// picture in it) on the user-interface thread and replay that recording later on the render thread, so a recording
/// can outlive the next frames, a resize and the player's teardown. The pixels it draws must stay put until Skia lets
/// go of the picture: rewriting them tore the picture, and freeing them crashed the replay (a use-after-free).
/// </summary>
/// <remarks>
/// Processor (raster) images only - the readback is the same call on both render paths, so no graphics device is needed.
/// </remarks>
public class VideoFrameReadbackTests
{
    [Fact]
    public void a_recorded_picture_keeps_its_pixels_while_later_frames_are_read_back()
    {
        // The readback used to alternate two bitmaps, so the third frame rewrote the first frame's pixels while a
        // recording could still draw them.
        //Arrange
        using var readback = new VideoFrameReadback();
        using var recording = RecordAndRelease(readback, SKColors.Red, 16, 16);

        //Act
        using var latest = PresentSequence(readback, 16, 16, SKColors.Lime, SKColors.Blue, SKColors.Yellow, SKColors.Blue);

        //Assert
        Replay(recording, 16, 16).GetPixel(8, 8).Should().Be(SKColors.Red);
        PixelOf(latest, 8, 8).Should().Be(SKColors.Blue);
    }

    [Fact]
    public void a_picture_recorded_before_a_resize_and_a_teardown_still_plays_after_them()
    {
        // A new frame size used to dispose the bitmap a recording still drew, and disposing the driver freed both.
        //Arrange
        var readback = new VideoFrameReadback();
        using var recording = RecordAndRelease(readback, SKColors.Red, 16, 16);

        //Act
        PresentSequence(readback, 40, 30, SKColors.Blue, SKColors.Blue, SKColors.Lime)!.Dispose();
        readback.Dispose();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        ScribbleOverFreedMemory();

        //Assert
        Replay(recording, 16, 16).GetPixel(8, 8).Should().Be(SKColors.Red);
    }

    [Fact]
    public void steady_state_reads_back_into_two_buffers_and_allocates_no_more()
    {
        //Arrange
        using var readback = new VideoFrameReadback();

        //Act: the surface holds only the newest picture and disposes the previous one, as it does when nothing
        //recorded the older ones.
        using var latest = PresentSequence(readback, 32, 32, SKColors.Red, SKColors.Lime, SKColors.Blue, SKColors.Red,
            SKColors.Lime, SKColors.Blue, SKColors.Red, SKColors.Lime, SKColors.Blue, SKColors.Red);

        //Assert
        readback.BufferCount.Should().Be(2);
        PixelOf(latest, 4, 4).Should().Be(SKColors.Red);
    }

    [Fact]
    public void a_held_picture_is_never_overwritten_and_a_third_buffer_is_used_meanwhile()
    {
        //Arrange
        using var readback = new VideoFrameReadback();
        using var held = readback.Read(Solid(SKColors.Red, 8, 8))!;

        //Act
        using var latest = PresentSequence(readback, 8, 8, SKColors.Lime, SKColors.Blue, SKColors.Yellow);

        //Assert
        PixelOf(held, 2, 2).Should().Be(SKColors.Red);
        PixelOf(latest, 2, 2).Should().Be(SKColors.Yellow);
        readback.BufferCount.Should().Be(3);
    }

    [Fact]
    public void an_empty_image_gives_no_picture()
    {
        //Arrange
        using var readback = new VideoFrameReadback();
        using var empty = SKImage.Create(new SKImageInfo(0, 0));

        //Act / Assert
        (empty is null || readback.Read(empty) is null).Should().BeTrue();
        readback.BufferCount.Should().Be(0);
    }

    // Reads one frame of the colour, records a picture that draws it (as the head records the window frame), and lets
    // go of the presented image the way the surface does when the next frame replaces it.
    private static SKPicture RecordAndRelease(VideoFrameReadback readback, SKColor colour, int width, int height)
    {
        using var source = Solid(colour, width, height);
        using var presented = readback.Read(source)!;
        using var recorder = new SKPictureRecorder();
        var canvas = recorder.BeginRecording(new SKRect(0, 0, width, height));
        canvas.DrawImage(presented, 0, 0, new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None));
        return recorder.EndRecording();
    }

    // Presents each colour in turn, disposing the previous picture as the surface element does; returns the last.
    private static SKImage? PresentSequence(VideoFrameReadback readback, int width, int height, params SKColor[] colours)
    {
        SKImage? current = null;
        foreach (var colour in colours)
        {
            using var source = Solid(colour, width, height);
            var next = readback.Read(source);
            current?.Dispose();
            current = next;
        }

        return current;
    }

    private static SKImage Solid(SKColor colour, int width, int height)
    {
        using var surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
        surface.Canvas.Clear(colour);
        return surface.Snapshot();
    }

    private static SKColor PixelOf(SKImage? image, int x, int y)
    {
        image.Should().NotBeNull();
        using var bitmap = SKBitmap.FromImage(image!);
        return bitmap.GetPixel(x, y);
    }

    private static SKBitmap Replay(SKPicture recording, int width, int height)
    {
        var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        canvas.DrawPicture(recording);
        return bitmap;
    }

    // Freed native blocks of these sizes are likely to be handed out again by the next plain allocations of the same
    // size: fill a few with a colour no test uses, so a replay over freed pixels shows a wrong colour instead of
    // passing by luck.
    private static void ScribbleOverFreedMemory()
    {
        var blocks = new IntPtr[32];
        for (var i = 0; i < blocks.Length; i++)
        {
            var bytes = i % 2 == 0 ? 16 * 16 * 4 : 40 * 30 * 4;
            blocks[i] = Marshal.AllocHGlobal(bytes);
            var magenta = new byte[bytes];
            for (var b = 0; b < bytes; b += 4)
            {
                magenta[b] = 0xFF;     // blue
                magenta[b + 2] = 0xFF; // red
                magenta[b + 3] = 0xFF; // alpha
            }

            Marshal.Copy(magenta, 0, blocks[i], bytes);
        }

        foreach (var block in blocks)
        {
            Marshal.FreeHGlobal(block);
        }
    }
}
