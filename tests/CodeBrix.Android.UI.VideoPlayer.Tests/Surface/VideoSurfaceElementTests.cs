using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using CodeBrix.Platform.UI.VideoPlayer.Skia.Internal;
using SilverAssertions;
using SkiaSharp;
using Windows.Foundation;
using Xunit;

namespace CodeBrix.Android.UI.VideoPlayer.Tests.Surface;

/// <summary>
/// The picture's paint (Internal/VideoSurfaceElement, linked) before the first frame. The fence of the Platform's
/// VideoPlayer fix ported at pin 1.0.287.1311: with no frame the paint draws NOTHING - never Clear(Transparent), which
/// would erase what the canvas already holds behind the player.
/// </summary>
public class VideoSurfaceElementTests
{
    private static readonly MethodInfo PaintFrame = typeof(VideoSurfaceElement)
        .GetMethod("PaintFrame", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("VideoSurfaceElement no longer has PaintFrame(SKCanvas, Size).");

    [Fact]
    public void Before_any_frame_the_paint_leaves_what_is_behind_the_player_untouched()
    {
        //Arrange
        var surface = NewSurfaceWithoutXaml();
        using var bitmap = new SKBitmap(new SKImageInfo(32, 24, SKColorType.Rgba8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(new SKColor(0x00, 0xFF, 0x00));

        //Act
        PaintFrame.Invoke(surface, new object[] { canvas, new Size(32, 24) });
        canvas.Flush();

        //Assert
        bitmap.GetPixel(0, 0).Should().Be(new SKColor(0x00, 0xFF, 0x00));
        bitmap.GetPixel(16, 12).Should().Be(new SKColor(0x00, 0xFF, 0x00));
        bitmap.GetPixel(31, 23).Should().Be(new SKColor(0x00, 0xFF, 0x00));
    }

    // The paint touches only the frame gate, the frame and the stretch; the element's constructor (dispatcher timer,
    // composition) is not needed for it, so the object is made without running it and the gate is set by hand.
    private static VideoSurfaceElement NewSurfaceWithoutXaml()
    {
        StartHostFreeCore();
        var surface = (VideoSurfaceElement)RuntimeHelpers.GetUninitializedObject(typeof(VideoSurfaceElement));
        var gate = typeof(VideoSurfaceElement).GetField("_frameGate", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("VideoSurfaceElement no longer has its _frameGate.");
        gate.SetValue(surface, new object());
        return surface;
    }

    // The XAML element types' static constructors need a dispatcher pump: the net10.0 flavors of
    // CodeBrix.Android.UI.Dispatching / .Composition provide the host-free ones CodeBrix.Android.UI.Tests uses.
    private static void StartHostFreeCore()
    {
        Start("CodeBrix.Android.UI.Dispatching.HostFree.ManagedDispatcherPump, CodeBrix.Android.UI.Dispatching");
        Start("CodeBrix.Android.UI.Composition.HostFree.HostFreeCompositionBootstrap, CodeBrix.Android.UI.Composition");

        static void Start(string typeName)
        {
            var type = Type.GetType(typeName, throwOnError: true);
            var ensure = type.GetMethod("EnsureRegistered", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException(typeName + " no longer has EnsureRegistered().");
            ensure.Invoke(null, null);
        }
    }
}
