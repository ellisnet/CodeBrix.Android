#nullable disable

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CodeBrix.Audio.Android;
using CodeBrix.Platform.UI.AddIn.VideoPlayer.UIReqs.Support;
using CodeBrix.Platform.UI.Core.UIReqs.Hosting;
using CodeBrix.Platform.WinUI.Graphics3DGL;
using CodeBrix.VideoPlayback.Rendering;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Reqnroll;
using SilverAssertions;
using VideoPlayerElement = CodeBrix.Platform.UI.VideoPlayer.Skia.VideoPlayer;

namespace CodeBrix.Android.UIReqs.Device.AndroidSteps;

/// <summary>
/// The steps of the Android-only "The VideoPlayer on Android" feature (AndroidFeatures/AndroidVideoPlayer): the VideoPlayer
/// add-in's own element, its picture on the SkiaSharp.Views canvas, the Android graphics context, the Android audio
/// output and ms-appx video copied out of the APK.
/// </summary>
[Binding]
public sealed class VideoPlayerAndroidSteps
{
    private static string _failure;

    /// <summary>Closes the players this group opened (the copied group's own hook is scoped to its features).</summary>
    [AfterScenario]
    public static async Task Close()
    {
        if (!TestTargetFixture.IsLaunched)
        {
            return;
        }

        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            foreach (var name in ElementRegistry.Names)
            {
                if (ElementRegistry.TryResolve(name, out var element) && element is VideoPlayerElement player)
                {
                    player.Close();
                }
            }
        }).ConfigureAwait(false);
    }

    /// <summary>A muted VideoPlayer of a size.</summary>
    [Given("a VideoPlayer named {string} {int} by {int} is on the panel, muted")]
    public Task Given_a_player(string name, int width, int height) => ShowAsync(name, width, height, null);

    /// <summary>A muted VideoPlayer of a size, with a render path.</summary>
    [Given("a VideoPlayer named {string} {int} by {int} is on the panel, muted, with the render path {string}")]
    public Task Given_a_player_with_path(string name, int width, int height, string path) => ShowAsync(name, width, height, Enum.Parse<VideoRenderPath>(path));

    /// <summary>Opens one of the copied group's clips (by path, as the group does).</summary>
    [When("the video {string} of the VideoPlayer group is opened in the VideoPlayer {string}")]
    public async Task When_clip_opened(string clip, string name)
    {
        var path = VideoFixtures.Require(clip);
        await TestTargetFixture.RunOnUIThreadAsync(() => Player(name).Source = path).ConfigureAwait(false);
        await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
    }

    /// <summary>Sets a Source as written.</summary>
    [When("the Source of the VideoPlayer {string} is {string}")]
    public async Task When_source(string name, string source)
    {
        await TestTargetFixture.RunOnUIThreadAsync(() => Player(name).Source = source).ConfigureAwait(false);
        await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
    }

    /// <summary>A presented picture exists.</summary>
    [Then("the VideoPlayer {string} presents a frame within {int} milliseconds")]
    public async Task Then_presents(string name, int milliseconds)
    {
        var presented = false;
        for (var waited = 0; waited < milliseconds && !presented && _failure == null; waited += 100)
        {
            await Task.Delay(100).ConfigureAwait(false);
            presented = await OnUIThreadAsync(() =>
            {
                using var picture = Player(name).CapturePresentedFrame();
                return picture != null;
            }).ConfigureAwait(false);
        }

        _failure.Should().BeNull();
        presented.Should().BeTrue("the player presents the clip's first picture");
    }

    /// <summary>No MediaFailed.</summary>
    [Then("the VideoPlayer {string} reported no failure")]
    public void Then_no_failure(string name) => _failure.Should().BeNull();

    /// <summary>The element's type comes from the Android add-in.</summary>
    [Then("{string} is a VideoPlayer of the Android VideoPlayer add-in")]
    public async Task Then_is_android_player(string name)
    {
        var assembly = await OnUIThreadAsync(() => Player(name).GetType().Assembly.GetName().Name).ConfigureAwait(false);
        assembly.Should().Be("CodeBrix.Android.UI.VideoPlayer");
    }

    /// <summary>The surface element's handler and native view.</summary>
    [Then("the picture of the VideoPlayer {string} is on the Android Skia canvas view")]
    public async Task Then_on_skia_view(string name)
    {
        (string surface, string handler, string view) = await OnUIThreadAsync(() =>
        {
            var player = Player(name);
            var child = VisualTreeHelper.GetChildrenCount(player) > 0 ? VisualTreeHelper.GetChild(player, 0) as UIElement : null;
            var h = child == null ? null : CodeBrix.Android.UI.Policy.PolicyDiagnostics.HandlerOf(child);
            return (child?.GetType().Name, h?.GetType().Name, (h as CodeBrix.Android.UI.Handlers.IViewHandler)?.NativeView?.GetType().Name);
        }).ConfigureAwait(false);
        surface.Should().Be("VideoSurfaceElement");
        handler.Should().Be("SkiaCanvasElementHandler");
        view.Should().Be("SkiaCanvasView");
    }

    /// <summary>The asset is under the copy folder.</summary>
    [Then("the video asset {string} has been copied out of the APK")]
    public void Then_copied(string asset) =>
        File.Exists(Path.Combine(AndroidPackagedAssets.RootDirectory(), asset)).Should().BeTrue("an ms-appx video is copied out before it is opened");

    /// <summary>Gpu iff a SkiaGpuContext can be made for the player's XamlRoot.</summary>
    [Then("the active render path of the VideoPlayer {string} is Gpu exactly when an Android graphics context can be made")]
    public async Task Then_gpu_iff_context(string name)
    {
        (VideoRenderBackend active, bool canMake) = await OnUIThreadAsync(() =>
        {
            var player = Player(name);
            var ok = SkiaGpuContext.TryCreate(player.XamlRoot, out var context);
            context?.Dispose();
            return (player.ActiveRenderPath, ok);
        }).ConfigureAwait(false);
        global::Android.Util.Log.Info("UIReqs.Video", $"active render path {active}; a graphics context can be made: {canMake}");
        active.Should().Be(canMake ? VideoRenderBackend.Gpu : VideoRenderBackend.Cpu);
    }

    /// <summary>Plays the player.</summary>
    [When("the VideoPlayer {string} plays")]
    public async Task When_plays(string name) =>
        await TestTargetFixture.RunOnUIThreadAsync(() => Player(name).Play()).ConfigureAwait(false);

    /// <summary>The position passes a mark.</summary>
    [Then("the VideoPlayer {string} plays past {float} seconds within {int} milliseconds")]
    public async Task Then_plays_past(string name, float seconds, int milliseconds)
    {
        var position = 0d;
        for (var waited = 0; waited < milliseconds && position <= seconds && _failure == null; waited += 100)
        {
            await Task.Delay(100).ConfigureAwait(false);
            position = await OnUIThreadAsync(() => Player(name).PositionSeconds).ConfigureAwait(false);
        }

        _failure.Should().BeNull();
        position.Should().BeGreaterThan(seconds);
    }

    private static async Task ShowAsync(string name, int width, int height, VideoRenderPath? path)
    {
        ElementRegistry.Clear();
        _failure = null;
        Grid grid = null;
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            var player = new VideoPlayerElement { Width = width, Height = height, Volume = 0, IsMuted = true };
            if (path is { } p)
            {
                player.RenderPath = p;
            }
            else
            {
                player.RenderPath = VideoRenderPath.Cpu;
            }

            player.MediaFailed += (_, e) => _failure = e.Message;
            ElementRegistry.Register(name, player);
            grid = new Grid { Width = width, Height = height, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            grid.Children.Add(player);
        }).ConfigureAwait(false);
        await TestTargetFixture.SetContentAsync(grid).ConfigureAwait(false);
        await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
    }

    private static VideoPlayerElement Player(string name) => (VideoPlayerElement)ElementRegistry.Resolve(name);

    private static async Task<T> OnUIThreadAsync<T>(Func<T> func)
    {
        T result = default;
        await TestTargetFixture.RunOnUIThreadAsync(() => result = func()).ConfigureAwait(false);
        return result;
    }
}
