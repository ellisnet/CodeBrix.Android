#nullable disable

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using CodeBrix.Android.UI.Platform.Animation;
using CodeBrix.Platform.UI.Core.UIReqs.Hosting;
using Microsoft.UI.Xaml.Media;
using Reqnroll;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UIReqs.Device.AndroidSteps;

/// <summary>
/// AP10-C (FIXLIST A:161): the steps of the AndroidNative scenario "CompositionTarget.Rendering ticks while subscribed
/// and stops when not" - app code subscribes to CompositionTarget.Rendering directly (as a TeachingTip's open or a game
/// loop does) and the Android frame clock (CoreAnimationTicker on Choreographer frames) raises it once per display frame
/// until the handler leaves, then asks for no more frames.
/// </summary>
[Binding]
public sealed class RenderingPumpSteps
{
    private EventHandler<object> _handler;
    private int _raised;
    private long _tickerFramesAtSubscribe;
    private int _raisedAtUnsubscribe;
    private long _tickerFramesWhileSubscribed;
    private Stopwatch _subscribed;
    private double _subscribedMs;

    /// <summary>Subscribes a counting handler to CompositionTarget.Rendering on the UI thread.</summary>
    [When("app code subscribes to CompositionTarget.Rendering")]
    public Task When_app_code_subscribes() => TestTargetFixture.RunOnUIThreadAsync(() =>
    {
        _raised = 0;
        _handler = (_, _) => _raised++;
        _tickerFramesAtSubscribe = CoreAnimationTicker.FrameCount;
        _subscribed = Stopwatch.StartNew();
        CompositionTarget.Rendering += _handler;
    });

    /// <summary>Waits, then asserts the handler was raised on display frames, at most once per frame.</summary>
    [Then("CompositionTarget.Rendering is raised on display frames")]
    public async Task Then_Rendering_is_raised_on_display_frames()
    {
        await Task.Delay(300, TestContext.Current.CancellationToken).ConfigureAwait(false);
        var (raised, frames, ticking) = await OnUIThreadAsync(() => (_raised, CoreAnimationTicker.FrameCount - _tickerFramesAtSubscribe, CoreAnimationTicker.IsTicking)).ConfigureAwait(false);
        raised.Should().BeGreaterThan(3, "a subscribed Rendering handler is raised on the display's frames");
        ticking.Should().BeTrue("the frame clock keeps asking for frames while a handler is subscribed");
        raised.Should().BeLessThanOrEqualTo((int)frames, "one raise per frame the clock ticked (never two in one frame)");
    }

    /// <summary>Unsubscribes the handler on the UI thread.</summary>
    [When("app code unsubscribes from CompositionTarget.Rendering")]
    public Task When_app_code_unsubscribes() => TestTargetFixture.RunOnUIThreadAsync(() =>
    {
        CompositionTarget.Rendering -= _handler;
        _raisedAtUnsubscribe = _raised;
        _tickerFramesWhileSubscribed = CoreAnimationTicker.FrameCount - _tickerFramesAtSubscribe;
        _subscribedMs = _subscribed.Elapsed.TotalMilliseconds;
    });

    /// <summary>Asserts the handler is no longer raised and the frame clock asks for no frames.</summary>
    [Then("CompositionTarget.Rendering is no longer raised and the frame clock is idle")]
    public async Task Then_Rendering_is_no_longer_raised()
    {
        await Task.Delay(300, TestContext.Current.CancellationToken).ConfigureAwait(false);
        var (raised, ticking, subscribers) = await OnUIThreadAsync(() => (_raised, CoreAnimationTicker.IsTicking, CoreAnimationTicker.HasSubscribers())).ConfigureAwait(false);
        raised.Should().Be(_raisedAtUnsubscribe, "a handler that left is not raised again");
        _raisedAtUnsubscribe.Should().Be((int)_tickerFramesWhileSubscribed, "every frame the clock ticked while subscribed raised the handler exactly once");
        _raisedAtUnsubscribe.Should().BeLessThanOrEqualTo((int)(_subscribedMs / 8.0) + 2, "at most one raise per display frame (120 Hz bound)");
        subscribers.Should().BeFalse("nothing else in the harness page subscribes to Rendering");
        ticking.Should().BeFalse("with no subscriber the frame clock requests no frames");
    }

    private static async Task<T> OnUIThreadAsync<T>(Func<T> read)
    {
        T value = default;
        await TestTargetFixture.RunOnUIThreadAsync(() => value = read()).ConfigureAwait(false);
        return value;
    }
}
