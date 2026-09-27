using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using CodeBrix.Platform.UI.Lottie.Engine;
using SilverAssertions;
using SkiaSharp;
using Xunit;

namespace CodeBrix.Android.UI.Lottie.Tests.Engine;

/// <summary>
/// The Lottie Core's engine, driven as the Android canvas supply drives it: every frame is rendered through the
/// object-typed callback onto an SKCanvas, and the ticks come from a tick source the test owns, so nothing here
/// depends on a dispatcher, a timer or the load on the machine. The animation is the UIReqs group's "pulse": a red
/// 30 by 30 square that crosses a 100 by 100 composition in one second (30 frames at 30 fps).
/// </summary>
public class LottieEngineTests
{
    private const int Size = 100;

    [Fact]
    public void Setting_the_progress_puts_an_exact_frame_on_the_canvas()
    {
        //Arrange
        var player = NewPlayer(out _, out _);

        //Act
        player.SetProgress(0);
        var start = RenderFrame(player);
        player.SetProgress(1);
        var end = RenderFrame(player);
        player.SetProgress(0.5);
        var middle = RenderFrame(player);

        //Assert
        IsRed(start, 20, 50).Should().BeTrue("the square starts at the left");
        IsBlank(start, 80, 50).Should().BeTrue("nothing is on the right at the start");
        IsRed(end, 80, 50).Should().BeTrue("the square ends at the right");
        IsBlank(end, 20, 50).Should().BeTrue("nothing is on the left at the end");
        IsRed(middle, 50, 50).Should().BeTrue("half-way through, the square is centred");
        IsBlank(middle, 10, 50).Should().BeTrue("half-way through, the left edge is blank");
        IsBlank(middle, 90, 50).Should().BeTrue("half-way through, the right edge is blank");
    }

    [Fact]
    public void Playing_starts_a_new_tick_source_at_the_animation_frame_rate_and_each_tick_asks_for_a_repaint()
    {
        //Arrange
        var player = NewPlayer(out var ticks, out var playing);
        var repaints = 0;
        player.InvalidateRequested += () => repaints++;

        //Act
        player.Play(0, 1, looped: true);
        var source = ticks[0];
        source.Fire();
        source.Fire();
        source.Fire();

        //Assert
        ticks.Count.Should().Be(1);
        source.IsRunning.Should().BeTrue();
        source.Interval.Should().Be(TimeSpan.FromSeconds(1 / 30d));
        repaints.Should().Be(3);
        playing.Should().Equal(true);
    }

    [Fact]
    public void A_segment_that_is_not_looped_rests_on_its_end_frame_whenever_the_frame_after_its_end_is_drawn()
    {
        //Arrange
        var player = NewPlayer(out var ticks, out var playing);
        player.SetProgress(0.5);
        var expected = RenderFrame(player);
        playing.Clear();

        //Act
        player.Play(0, 0.5, looped: false);
        Thread.Sleep(700); // well past the 500 ms segment: the overshoot must not show
        var rested = RenderFrame(player);
        var later = RenderFrame(player);

        //Assert
        playing.Should().Equal(true, false);
        ticks[0].IsRunning.Should().BeFalse();
        Pixels(rested).Should().Equal(Pixels(expected));
        Pixels(later).Should().Equal(Pixels(expected));
    }

    [Fact]
    public void A_paused_animation_draws_the_same_frame_until_it_is_resumed()
    {
        //Arrange
        var player = NewPlayer(out var ticks, out var playing);
        player.Play(0, 1, looped: true);
        Thread.Sleep(100);

        //Act
        player.Pause();
        var paused = RenderFrame(player);
        Thread.Sleep(200);
        var still = RenderFrame(player);
        player.Resume();

        //Assert
        Pixels(still).Should().Equal(Pixels(paused));
        ticks[0].IsRunning.Should().BeTrue();
        playing.Should().Equal(true, false, true);
    }

    private static LottiePlayer NewPlayer(out List<ManualTickSource> ticks, out List<bool> playing)
    {
        var created = new List<ManualTickSource>();
        var states = new List<bool>();
        var player = new LottiePlayer(
            () =>
            {
                var source = new ManualTickSource();
                created.Add(source);
                return source;
            },
            action => action());
        player.IsPlayingChanged += states.Add;
        player.Animation = LottiePlayer.CreateAnimation(ReadPulse());
        ticks = created;
        playing = states;
        return player;
    }

    private static string ReadPulse()
    {
        using var stream = typeof(LottieEngineTests).Assembly.GetManifestResourceStream("Lottie.pulse.json");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static SKBitmap RenderFrame(LottiePlayer player)
    {
        var bitmap = new SKBitmap(new SKImageInfo(Size, Size, SKColorType.Rgba8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);

        // What the Android canvas supply hands the Core: the canvas as an object, the area in DIPs.
        Action<object, SKSize> callback = (c, area) => player.Render((SKCanvas)c, area, LottieStretch.Uniform, 1, null);
        callback(canvas, new SKSize(Size, Size));
        canvas.Flush();
        return bitmap;
    }

    private static bool IsRed(SKBitmap bitmap, int x, int y)
    {
        var c = bitmap.GetPixel(x, y);
        return c.Red > 200 && c.Green < 60 && c.Blue < 60 && c.Alpha > 200;
    }

    private static bool IsBlank(SKBitmap bitmap, int x, int y) => bitmap.GetPixel(x, y).Alpha == 0;

    private static uint[] Pixels(SKBitmap bitmap)
    {
        var result = new uint[bitmap.Width * bitmap.Height];
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                result[(y * bitmap.Width) + x] = (uint)bitmap.GetPixel(x, y);
            }
        }

        return result;
    }

    private sealed class ManualTickSource : ITickSource
    {
        public TimeSpan Interval { get; set; }

        public bool IsRunning { get; private set; }

        public event Action Tick;

        public void Start() => IsRunning = true;

        public void Stop() => IsRunning = false;

        public void Fire() => Tick?.Invoke();
    }
}
