#nullable disable

using System.Threading.Tasks;
using CodeBrix.Android.UI.TextLayout.Android;
using CodeBrix.Platform.UI.Core.UIReqs.Hosting;
using Reqnroll;
using SilverAssertions;
using Windows.System.Profile;
using Windows.UI.ViewManagement;
using AValueAnimator = Android.Animation.ValueAnimator;

namespace CodeBrix.Android.UIReqs.Device.AndroidSteps;

/// <summary>
/// AP1.9: the steps of the Android-only "Platform contracts of the engine pass" feature
/// (AndroidFeatures/AndroidNative/EngineContracts.feature) - the font source of the text engine
/// (IFontSourcePlatform&lt;SKTypeface&gt;, the TextLayout add-in), IAnimationSettingsPlatform and IDeviceFamilyPlatform; AP1.11
/// adds the D2 device-form and device-family steps (used by AndroidPolicy/SizeClasses.feature).
/// </summary>
[Binding]
public sealed class EngineContractsSteps
{
    /// <summary>Asserts which font source the text engine resolves (ApiExtensibility, as TextLayout.Core does).</summary>
    [Then("the text engine's font source is {string}")]
    public async Task Then_the_font_source_is(string typeName)
    {
        string actual = null;
        await TestTargetFixture.RunOnUIThreadAsync(() => actual = FontSourceProbe.RegisteredSourceType()).ConfigureAwait(false);
        actual.Should().Be(typeName);
    }

    /// <summary>Asserts that a family NAME gets the app's default text font file (the never-a-system-font rule).</summary>
    [Then("the text engine gives the family {string} the app's default text font")]
    public async Task Then_the_family_gets_the_default_font(string family)
    {
        string named = null;
        string fallback = null;
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            named = FontSourceProbe.FamilyNameOf(family);
            fallback = FontSourceProbe.DefaultFamilyName();
        }).ConfigureAwait(false);
        fallback.Should().NotBeNullOrEmpty("the app's default text font file must load as a SkiaSharp typeface");
        named.Should().Be(fallback);
    }

    private (int Lines, bool RightToLeft, float Width, float Height) _lastLayout;

    /// <summary>AP7-B: lays one run out with TextLayout.Core's engine (the app's default text font), on the UI thread.</summary>
    [When("the text engine lays out {string} at size {int}")]
    public Task When_the_engine_lays_out(string text, int size) => LayOutAsync(text, size, null);

    /// <summary>AP7-B: lays one run out with a wrapping width.</summary>
    [When("the text engine lays out {string} at size {int} within {int} pixels")]
    public Task When_the_engine_lays_out_within(string text, int size, int width) => LayOutAsync(text, size, width);

    /// <summary>AP7-B: asserts that the engine found and bound ICU on the device.</summary>
    [Then("the text engine has bound the device's ICU")]
    public async Task Then_the_engine_has_bound_icu()
    {
        TextEngineProbe.IcuState state = default;
        await TestTargetFixture.RunOnUIThreadAsync(() => state = TextEngineProbe.ReadIcuState()).ConfigureAwait(false);
        global::Android.Util.Log.Info("UIReqs.TextEngine", $"ICU version {state.Version}, library loaded {state.LibraryLoaded}");
        state.LibraryLoaded.Should().BeTrue("the engine's first layout loads the device's ICU library");
        state.IsBound.Should().BeTrue($"the engine binds ICU 50-100 by its versioned exports (found version {state.Version})");
    }

    /// <summary>AP7-B: asserts the base direction of the last layout.</summary>
    [Then("the text engine's last layout reads right to left")]
    public void Then_the_last_layout_reads_right_to_left()
    {
        _lastLayout.RightToLeft.Should().BeTrue("ICU's bidi resolves a Hebrew-first paragraph right to left");
        _lastLayout.Width.Should().BeGreaterThan(0f, "the app's default text font measures the text");
    }

    /// <summary>AP7-B: asserts that the last layout wrapped.</summary>
    [Then("the text engine's last layout has at least {int} lines")]
    public void Then_the_last_layout_has_at_least_lines(int lines) =>
        _lastLayout.Lines.Should().BeGreaterThanOrEqualTo(lines, "ICU's line breaker wraps the text at the width");

    private async Task LayOutAsync(string text, int size, int? width)
    {
        await TestTargetFixture.RunOnUIThreadAsync(() => _lastLayout = TextEngineProbe.LayOut(text, size, width)).ConfigureAwait(false);
    }

    /// <summary>Asserts that UISettings.AnimationsEnabled reads the system animator duration scale.</summary>
    [Then("UISettings.AnimationsEnabled is what the system animator duration scale says")]
    public async Task Then_AnimationsEnabled_follows_the_scale()
    {
        var core = true;
        var system = true;
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            core = new UISettings().AnimationsEnabled;
            system = AValueAnimator.AreAnimatorsEnabled();
        }).ConfigureAwait(false);
        core.Should().Be(system);
    }

    /// <summary>
    /// [AP9-2] The precondition of the Android-only group AndroidAnimatorOn: the runner starts that group with the system
    /// animator duration scale at 1 (every other group runs with it at 0), so the system animations are on and
    /// UISettings.AnimationsEnabled says so. A run that did not set the scale fails here, by name, instead of on a still frame.
    /// </summary>
    [Given("the system animations are on")]
    public async Task Given_the_system_animations_are_on()
    {
        var core = false;
        var system = false;
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            core = new UISettings().AnimationsEnabled;
            system = AValueAnimator.AreAnimatorsEnabled();
        }).ConfigureAwait(false);
        system.Should().BeTrue("the runner starts the AndroidAnimatorOn group with the system animator duration scale at 1");
        core.Should().BeTrue("UISettings.AnimationsEnabled follows the system animator duration scale");
    }

    /// <summary>Asserts the operating-system part of the device family (decision D2: "Android.&lt;form&gt;").</summary>
    [Then("AnalyticsInfo.VersionInfo.DeviceFamily starts with {string}")]
    public async Task Then_the_device_family_starts_with(string prefix)
    {
        string family = null;
        await TestTargetFixture.RunOnUIThreadAsync(() => family = AnalyticsInfo.VersionInfo.DeviceFamily).ConfigureAwait(false);
        family.Should().StartWith(prefix);
    }

    /// <summary>
    /// AP1.11 (decision D2): asserts the whole device family string ("Android.Mobile" / "Android.Tablet" /
    /// "Android.Desktop" for a Compact / Medium / Expanded window), read now.
    /// </summary>
    [Then("AnalyticsInfo.VersionInfo.DeviceFamily is {string}")]
    public async Task Then_the_device_family_is(string expected)
    {
        string family = null;
        await TestTargetFixture.RunOnUIThreadAsync(() => family = AnalyticsInfo.VersionInfo.DeviceFamily).ConfigureAwait(false);
        family.Should().Be(expected, "the device family names the form of the window's current width size class");
    }

    /// <summary>AP1.11 (decision D2): asserts AnalyticsInfo.DeviceForm (the window's width size class), read now.</summary>
    [Then("AnalyticsInfo.DeviceForm is {string}")]
    public async Task Then_the_device_form_is(string expected)
    {
        string form = null;
        await TestTargetFixture.RunOnUIThreadAsync(() => form = AnalyticsInfo.DeviceForm).ConfigureAwait(false);
        form.Should().Be(expected, "the device form is the window's current width size class");
    }
}
