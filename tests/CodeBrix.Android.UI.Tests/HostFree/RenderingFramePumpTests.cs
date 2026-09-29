using System;
using System.Collections.Generic;
using CodeBrix.Android.UI.Platform.Animation.Portable;
using Microsoft.UI.Xaml.Media;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.Tests.HostFree;

/// <summary>
/// AP10-C (FIXLIST A:161 "CompositionTarget.Rendering is never raised on Android"): the per-frame decision of the
/// Android frame clock (CoreAnimationTicker, AP5) run against the extracted Core - Rendering is raised once per
/// display frame while something is subscribed, and the loop asks for no frame once nothing is.
/// </summary>
[Collection(HostFreeCoreCollection.Name)]
public class RenderingFramePumpTests
{
    public RenderingFramePumpTests() => HostFreeCore.EnsureInitialized();

    [Fact]
    public void The_Core_build_exposes_the_Rendering_subscriber_list_the_pump_reads()
    {
        //Assert (a renamed field would silently turn the idle frame loop into an every-frame loop)
        RenderingFramePump.CanReadSubscribers.Should().BeTrue();
    }

    [Fact]
    public void With_no_subscribers_a_frame_raises_nothing_and_asks_for_no_next_frame()
    {
        //Arrange
        var errors = new List<Exception>();

        //Act
        var next = RenderingFramePump.OnFrame(errors.Add);

        //Assert
        RenderingFramePump.HasSubscribers().Should().BeFalse();
        next.Should().BeFalse();
        errors.Should().BeEmpty();
    }

    [Fact]
    public void A_subscriber_is_raised_once_per_frame_while_subscribed_and_the_loop_stops_when_it_leaves()
    {
        //Arrange
        var raised = 0;
        EventHandler<object> handler = (_, _) => raised++;
        CompositionTarget.Rendering += handler;
        try
        {
            //Act
            var first = RenderingFramePump.OnFrame(null);
            var second = RenderingFramePump.OnFrame(null);

            //Assert
            RenderingFramePump.HasSubscribers().Should().BeTrue();
            first.Should().BeTrue("a subscriber remains, so the next display frame is needed");
            second.Should().BeTrue();
            raised.Should().Be(2, "one raise per display frame");
        }
        finally
        {
            CompositionTarget.Rendering -= handler;
        }

        //Act
        var afterLeaving = RenderingFramePump.OnFrame(null);

        //Assert
        RenderingFramePump.HasSubscribers().Should().BeFalse();
        afterLeaving.Should().BeFalse("nothing is subscribed: the frame loop stops");
        raised.Should().Be(2);
    }

    [Fact]
    public void A_subscriber_that_leaves_in_its_handler_ends_the_loop_on_that_frame()
    {
        //Arrange (TeachingTip's QueueCallbackForCompositionRendering: subscribe, run once, unsubscribe)
        var raised = 0;
        EventHandler<object> handler = null;
        handler = (_, _) =>
        {
            raised++;
            CompositionTarget.Rendering -= handler;
        };
        CompositionTarget.Rendering += handler;

        //Act
        var next = RenderingFramePump.OnFrame(null);

        //Assert
        raised.Should().Be(1);
        next.Should().BeFalse();
        RenderingFramePump.HasSubscribers().Should().BeFalse();
    }

    [Fact]
    public void A_handler_that_throws_is_reported_and_the_loop_goes_on_while_it_is_subscribed()
    {
        //Arrange
        var errors = new List<Exception>();
        EventHandler<object> handler = (_, _) => throw new InvalidOperationException("boom");
        CompositionTarget.Rendering += handler;
        try
        {
            //Act
            var next = RenderingFramePump.OnFrame(errors.Add);

            //Assert
            errors.Should().ContainSingle().Which.Message.Should().Be("boom");
            next.Should().BeTrue();
        }
        finally
        {
            CompositionTarget.Rendering -= handler;
        }
    }

    [Fact]
    public void The_Rendering_event_carries_a_rendering_time_that_does_not_go_back()
    {
        //Arrange
        var times = new List<TimeSpan>();
        EventHandler<object> handler = (_, e) => times.Add(((RenderingEventArgs)e).RenderingTime);
        CompositionTarget.Rendering += handler;
        try
        {
            //Act
            RenderingFramePump.OnFrame(null);
            RenderingFramePump.OnFrame(null);
        }
        finally
        {
            CompositionTarget.Rendering -= handler;
        }

        //Assert
        times.Should().HaveCount(2);
        times[1].Should().BeGreaterThanOrEqualTo(times[0]);
    }
}
