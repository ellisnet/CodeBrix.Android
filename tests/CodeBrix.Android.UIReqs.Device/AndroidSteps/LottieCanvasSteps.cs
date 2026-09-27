#nullable disable

using System;
using System.Linq;
using System.Threading.Tasks;
using CodeBrix.Android.UI.Lottie.Android;
using CodeBrix.Platform.UI.Core.UIReqs.Hosting;
using CodeBrix.Platform.UI.Core.UIReqs.Support;
using CommunityToolkit.WinUI.Lottie;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Reqnroll;
using SilverAssertions;

namespace CodeBrix.Android.UIReqs.Device.AndroidSteps;

/// <summary>
/// AP7-B: the steps of the Android-only Lottie fences in "Platform contracts of the engine pass"
/// (AndroidFeatures/AndroidNative/EngineContracts.feature): which canvas supply the Lottie Core resolved, that the
/// player's render surface is the Android canvas element and has painted, and an animation source named by a URI.
/// The copied Lottie group's own steps are scoped to its features (Runtime/AddInScope), so these are separate.
/// </summary>
[Binding]
public sealed class LottieCanvasSteps
{
    private static readonly TimeSpan LoadBudget = TimeSpan.FromSeconds(10);

    /// <summary>Gives a player a Lottie source for a URI.</summary>
    [When("the Lottie source of {string} is {string}")]
    public Task When_the_Lottie_source_is(string name, string uri) =>
        TestTargetFixture.RunOnUIThreadAsync(() => PlayerOf(name).Source = new LottieVisualSource { UriSource = new Uri(uri) });

    /// <summary>Waits (bounded) until a player's animation has loaded, then drains the UI thread.</summary>
    [When("the Lottie animation of {string} has loaded")]
    public async Task When_the_Lottie_animation_has_loaded(string name)
    {
        var loaded = await Poll.UntilAsync(
            async () =>
            {
                var done = false;
                await TestTargetFixture.RunOnUIThreadAsync(() => done = PlayerOf(name).IsAnimatedVisualLoaded).ConfigureAwait(false);
                return done;
            },
            LoadBudget,
            TimeSpan.FromMilliseconds(50)).ConfigureAwait(false);
        loaded.Should().BeTrue("the animation of \"{0}\" must load within {1} ms", name, LoadBudget.TotalMilliseconds);
        await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
    }

    /// <summary>Asserts which canvas supply the add-in registered.</summary>
    [Then("the Lottie canvas supply is {string}")]
    public async Task Then_the_Lottie_canvas_supply_is(string typeName)
    {
        string actual = null;
        await TestTargetFixture.RunOnUIThreadAsync(() => actual = AndroidPlatformBootstrap.CanvasPlatform?.GetType().Name).ConfigureAwait(false);
        actual.Should().Be(typeName);
    }

    /// <summary>Asserts that the player's render surface is the Android canvas element and that it has painted.</summary>
    [Then("the Lottie animation of {string} is painted on the Android canvas supply")]
    public async Task Then_the_animation_is_painted_on_the_canvas_supply(string name)
    {
        LottieCanvasElement surface = null;
        var painted = 0;
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            surface = Descendants(PlayerOf(name)).OfType<LottieCanvasElement>().FirstOrDefault();
            painted = surface?.PaintCount ?? 0;
        }).ConfigureAwait(false);

        surface.Should().NotBeNull("the Lottie source puts the Android canvas element into \"{0}\"", name);
        painted.Should().BeGreaterThan(0, "the native view runs the source's render callback when Android draws it");
    }

    private static AnimatedVisualPlayer PlayerOf(string name) =>
        ElementRegistry.Resolve(name) as AnimatedVisualPlayer
        ?? throw new NotSupportedException($"\"{name}\" is not an AnimatedVisualPlayer.");

    private static System.Collections.Generic.IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var deeper in Descendants(child))
            {
                yield return deeper;
            }
        }
    }
}
