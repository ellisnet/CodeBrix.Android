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
/// (IFontSourcePlatform&lt;SKTypeface&gt;, the TextLayout add-in), IAnimationSettingsPlatform and IDeviceFamilyPlatform.
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

    /// <summary>Asserts the operating-system part of the device family (decision D2, provisional "Android.&lt;form&gt;").</summary>
    [Then("AnalyticsInfo.VersionInfo.DeviceFamily starts with {string}")]
    public async Task Then_the_device_family_starts_with(string prefix)
    {
        string family = null;
        await TestTargetFixture.RunOnUIThreadAsync(() => family = AnalyticsInfo.VersionInfo.DeviceFamily).ConfigureAwait(false);
        family.Should().StartWith(prefix);
    }
}
