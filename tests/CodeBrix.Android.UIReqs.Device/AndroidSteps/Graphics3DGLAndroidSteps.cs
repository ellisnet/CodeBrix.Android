using System;
using System.Reflection;
using System.Threading.Tasks;
using CodeBrix.Platform.UI.AddIn.Graphics3DGL.UIReqs.Support;
using CodeBrix.Platform.UI.Core.UIReqs.Hosting;
using CodeBrix.Platform.UI.Core.UIReqs.Support;
using Microsoft.UI.Xaml;
using Reqnroll;
using SilverAssertions;

namespace CodeBrix.Android.UIReqs.Device.AndroidSteps;

/// <summary>
/// AP7-A: the steps of the Android-only group AndroidGraphics3DGL - GL canvases (the copied Graphics3DGL fixtures)
/// that must draw again by themselves when invalidated; no step here captures a frame, because a capture is itself a
/// drawing pass.
/// </summary>
[Binding]
public sealed class Graphics3DGLAndroidSteps
{
    private const string PictureViewName = "GLPictureViewGroup";
    private const string NotedKey = "android.graphics3dgl.noted.";
    private const string TimesKey = "android.graphics3dgl.times.";
    private static readonly TimeSpan SettleBudget = TimeSpan.FromSeconds(5);
    private readonly ScenarioContext _scenarioContext;

    /// <summary>Creates the steps for one scenario.</summary>
    /// <param name="scenarioContext">The scenario.</param>
    public Graphics3DGLAndroidSteps(ScenarioContext scenarioContext) => _scenarioContext = scenarioContext;

    /// <summary>Waits (without capturing a frame) until a GL canvas has drawn at least once.</summary>
    /// <param name="name">The canvas.</param>
    /// <param name="seconds">The budget.</param>
    [Given("the GL canvas {string} draws within {int} seconds without a frame being captured")]
    [Then("the GL canvas {string} draws within {int} seconds without a frame being captured")]
    public async Task The_GL_canvas_draws_within(string name, int seconds)
    {
        var drew = await Poll.UntilAsync(async () => await RenderCountAsync(name).ConfigureAwait(false) >= 1,
            TimeSpan.FromSeconds(seconds)).ConfigureAwait(false);

        drew.Should().BeTrue("\"{0}\" must draw by itself once it is shown", name);
    }

    /// <summary>Waits (without capturing a frame) until a GPU canvas has painted at least once.</summary>
    /// <param name="name">The canvas.</param>
    /// <param name="seconds">The budget.</param>
    [Given("the GPU canvas {string} paints within {int} seconds without a frame being captured")]
    public async Task The_GPU_canvas_paints_within(string name, int seconds)
    {
        var painted = await Poll.UntilAsync(async () => await PaintCountAsync(name).ConfigureAwait(false) >= 1,
            TimeSpan.FromSeconds(seconds)).ConfigureAwait(false);

        painted.Should().BeTrue("\"{0}\" must paint by itself once it is shown", name);
    }

    /// <summary>Invalidates a GL canvas a number of times, letting it settle in between (no frame captured).</summary>
    /// <param name="name">The canvas.</param>
    /// <param name="times">How many invalidations.</param>
    [When("the GL canvas {string} is invalidated {int} times, a second apart, without a frame being captured")]
    public async Task The_GL_canvas_is_invalidated(string name, int times)
    {
        _scenarioContext[NotedKey + name] = await RenderCountAsync(name).ConfigureAwait(false);
        _scenarioContext[TimesKey + name] = times;
        for (var i = 0; i < times; i++)
        {
            await TestTargetFixture.RunOnUIThreadAsync(() => Fixture(name).Invalidate()).ConfigureAwait(false);
            await Task.Delay(1000).ConfigureAwait(false);
        }
    }

    /// <summary>Invalidates a GPU canvas a number of times, letting it settle in between (no frame captured).</summary>
    /// <param name="name">The canvas.</param>
    /// <param name="times">How many invalidations.</param>
    [When("the GPU canvas {string} is invalidated {int} times, a second apart, without a frame being captured")]
    public async Task The_GPU_canvas_is_invalidated(string name, int times)
    {
        _scenarioContext[NotedKey + name] = await PaintCountAsync(name).ConfigureAwait(false);
        _scenarioContext[TimesKey + name] = times;
        for (var i = 0; i < times; i++)
        {
            await TestTargetFixture.RunOnUIThreadAsync(() => SkiaCanvas(name).Invalidate()).ConfigureAwait(false);
            await Task.Delay(1000).ConfigureAwait(false);
        }
    }

    /// <summary>Asserts one draw per invalidation (waiting briefly for the last one; no frame captured).</summary>
    /// <param name="name">The canvas.</param>
    [Then("the GL canvas {string} drew once for every invalidation")]
    public async Task The_GL_canvas_drew_once_for_every_invalidation(string name)
    {
        var expected = (int)_scenarioContext[NotedKey + name] + (int)_scenarioContext[TimesKey + name];
        await Poll.UntilAsync(async () => await RenderCountAsync(name).ConfigureAwait(false) >= expected, SettleBudget).ConfigureAwait(false);

        (await RenderCountAsync(name).ConfigureAwait(false)).Should().Be(expected,
            "\"{0}\" must draw exactly once for each invalidation, by itself", name);
    }

    /// <summary>Asserts one paint per invalidation (waiting briefly for the last one; no frame captured).</summary>
    /// <param name="name">The canvas.</param>
    [Then("the GPU canvas {string} painted once for every invalidation")]
    public async Task The_GPU_canvas_painted_once_for_every_invalidation(string name)
    {
        var expected = (int)_scenarioContext[NotedKey + name] + (int)_scenarioContext[TimesKey + name];
        await Poll.UntilAsync(async () => await PaintCountAsync(name).ConfigureAwait(false) >= expected, SettleBudget).ConfigureAwait(false);

        (await PaintCountAsync(name).ConfigureAwait(false)).Should().Be(expected,
            "\"{0}\" must paint exactly once for each invalidation, by itself", name);
    }

    /// <summary>Asserts the element's native view is the Graphics3DGL add-in's picture view.</summary>
    /// <param name="name">The element.</param>
    [Then("{string} is shown by the native GL picture view")]
    public static async Task Is_shown_by_the_native_GL_picture_view(string name)
    {
        var viewType = string.Empty;
        await TestTargetFixture.RunOnUIThreadAsync(() => viewType = NativeViewTypeOf(ElementRegistry.Resolve(name))).ConfigureAwait(false);

        viewType.Should().Be(PictureViewName, "\"{0}\" must be shown by the Graphics3DGL add-in's native view", name);
    }

    private static async Task<int> RenderCountAsync(string name)
    {
        var count = 0;
        await TestTargetFixture.RunOnUIThreadAsync(() => count = Fixture(name).RenderCount).ConfigureAwait(false);
        return count;
    }

    private static async Task<int> PaintCountAsync(string name)
    {
        var count = 0;
        await TestTargetFixture.RunOnUIThreadAsync(() => count = SkiaCanvas(name).PaintCount).ConfigureAwait(false);
        return count;
    }

    private static GLCanvasFixture Fixture(string name) =>
        ElementRegistry.Resolve(name) as GLCanvasFixture
        ?? throw new NotSupportedException($"\"{name}\" is not a TriangleCanvas.");

    private static SkiaGlCanvas SkiaCanvas(string name) =>
        ElementRegistry.Resolve(name) as SkiaGlCanvas
        ?? throw new NotSupportedException($"\"{name}\" is not a SkiaGlCanvas.");

    private static string NativeViewTypeOf(UIElement element)
    {
        var handler = typeof(UIElement).GetProperty("Handler", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)?.GetValue(element);
        for (var type = handler?.GetType(); type != null; type = type.BaseType)
        {
            var property = type.GetProperty("NativeView", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly);
            if (property != null)
            {
                return property.GetValue(handler)?.GetType().Name ?? "none";
            }
        }

        return "none";
    }
}
