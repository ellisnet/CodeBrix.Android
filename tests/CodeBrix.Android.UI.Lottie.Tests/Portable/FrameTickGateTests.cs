using System;
using System.Linq;
using CodeBrix.Android.UI.Lottie.Portable;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.Lottie.Tests.Portable;

/// <summary>AP1.12: which Choreographer frames raise a Lottie tick (the Android frame clock, WPE1-13 item b).</summary>
public class FrameTickGateTests
{
    private const long Frame60 = 16_666_667L;

    private static int[] TickedFrames(double fps, long frameNanos, int frames)
    {
        var gate = new FrameTickGate { IntervalNanos = TimeSpan.FromSeconds(Math.Max(1 / 120d, 1 / fps)).Ticks * 100L };
        return Enumerable.Range(0, frames).Where(i => gate.OnFrame(1_000_000_000L + (i * frameNanos))).ToArray();
    }

    [Fact]
    public void The_first_frame_after_a_start_always_ticks()
    {
        //Arrange
        var gate = new FrameTickGate { IntervalNanos = 1_000_000_000L };

        //Act
        var ticked = gate.OnFrame(123_456_789L);

        //Assert
        ticked.Should().BeTrue();
    }

    [Fact]
    public void A_30_fps_animation_on_a_60_Hz_display_ticks_on_every_second_frame()
    {
        //Act
        var ticked = TickedFrames(30, Frame60, 12);

        //Assert
        ticked.Should().Equal(0, 2, 4, 6, 8, 10);
    }

    [Fact]
    public void A_60_fps_animation_on_a_60_Hz_display_ticks_on_every_frame()
    {
        //Act
        var ticked = TickedFrames(60, Frame60, 6);

        //Assert
        ticked.Should().Equal(0, 1, 2, 3, 4, 5);
    }

    [Fact]
    public void A_24_fps_animation_on_a_60_Hz_display_ticks_24_times_a_second_give_or_take_one()
    {
        //Act
        var ticked = TickedFrames(24, Frame60, 600);

        //Assert
        ticked.Length.Should().BeInRange(239, 241);
    }

    [Fact]
    public void A_frame_long_after_the_due_tick_restarts_the_schedule_without_a_burst()
    {
        //Arrange
        var gate = new FrameTickGate { IntervalNanos = 2 * Frame60 };
        gate.OnFrame(0);

        //Act
        var late = gate.OnFrame(100 * Frame60);
        var next = gate.OnFrame(101 * Frame60);
        var after = gate.OnFrame(102 * Frame60);

        //Assert
        late.Should().BeTrue();
        next.Should().BeFalse();
        after.Should().BeTrue();
    }

    [Fact]
    public void A_120_fps_animation_on_a_60_Hz_display_ticks_on_every_frame_and_no_more()
    {
        //Act
        var ticked = TickedFrames(120, Frame60, 10);

        //Assert
        ticked.Length.Should().Be(10);
    }

    [Fact]
    public void Reset_makes_the_next_frame_tick_again()
    {
        //Arrange
        var gate = new FrameTickGate { IntervalNanos = 1_000_000_000L };
        gate.OnFrame(0);
        gate.OnFrame(Frame60).Should().BeFalse();

        //Act
        gate.Reset();

        //Assert
        gate.OnFrame(2 * Frame60).Should().BeTrue();
    }
}
