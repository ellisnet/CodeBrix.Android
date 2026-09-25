using CodeBrix.Android.Services;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.Tests.WinRT;

public class HapticWaveformsTests
{
    [Fact]
    public void Every_supported_waveform_has_an_android_constant()
    {
        //Act & Assert
        foreach (var waveform in HapticWaveforms.Supported)
        {
            HapticWaveforms.ToAndroid(waveform).Should().NotBeNull();
        }
    }

    [Fact]
    public void Click_is_the_context_click_and_success_the_confirm()
    {
        //Act & Assert
        HapticWaveforms.ToAndroid(HapticWaveforms.ClickWaveform).Should().Be(HapticWaveforms.ContextClick);
        HapticWaveforms.ToAndroid(HapticWaveforms.SuccessWaveform).Should().Be(HapticWaveforms.Confirm);
    }

    [Fact]
    public void A_continuous_waveform_is_not_played()
    {
        //Act & Assert
        HapticWaveforms.ToAndroid(HapticWaveforms.BuzzContinuousWaveform).Should().BeNull();
    }
}
