namespace CodeBrix.Android.Services;

/// <summary>The WinRT haptic waveforms Android's view haptics can play, and their HapticFeedbackConstants.</summary>
internal static class HapticWaveforms
{
    /// <summary>HapticFeedbackConstants.VIRTUAL_KEY.</summary>
    internal const int VirtualKey = 1;

    /// <summary>HapticFeedbackConstants.CLOCK_TICK.</summary>
    internal const int ClockTick = 4;

    /// <summary>HapticFeedbackConstants.CONTEXT_CLICK.</summary>
    internal const int ContextClick = 6;

    /// <summary>HapticFeedbackConstants.VIRTUAL_KEY_RELEASE.</summary>
    internal const int VirtualKeyRelease = 8;

    /// <summary>HapticFeedbackConstants.CONFIRM.</summary>
    internal const int Confirm = 16;

    /// <summary>HapticFeedbackConstants.REJECT.</summary>
    internal const int Reject = 17;

    /// <summary>The Click waveform (HID haptics usage 0x1003, KnownSimpleHapticsControllerWaveforms.Click).</summary>
    internal const ushort ClickWaveform = 0x1003;

    /// <summary>The BuzzContinuous waveform (HID 0x1004).</summary>
    internal const ushort BuzzContinuousWaveform = 0x1004;

    /// <summary>The Press waveform (HID 0x1006).</summary>
    internal const ushort PressWaveform = 0x1006;

    /// <summary>The Release waveform (HID 0x1007).</summary>
    internal const ushort ReleaseWaveform = 0x1007;

    /// <summary>The Hover waveform (HID 0x1008).</summary>
    internal const ushort HoverWaveform = 0x1008;

    /// <summary>The Success waveform (HID 0x1009).</summary>
    internal const ushort SuccessWaveform = 0x1009;

    /// <summary>The Error waveform (HID 0x100A).</summary>
    internal const ushort ErrorWaveform = 0x100A;

    /// <summary>
    /// The waveforms offered. The values are the HID haptics usages WinRT's KnownSimpleHapticsControllerWaveforms
    /// returns; CodeBrix.Platform's KnownSimpleHapticsControllerWaveforms is not implemented, so they are spelled out.
    /// </summary>
    internal static ushort[] Supported => [ClickWaveform, PressWaveform, ReleaseWaveform, HoverWaveform, SuccessWaveform, ErrorWaveform];

    /// <summary>The HapticFeedbackConstants value that plays <paramref name="waveform"/>, or null.</summary>
    internal static int? ToAndroid(ushort waveform) => waveform switch
    {
        ClickWaveform => ContextClick,
        PressWaveform => VirtualKey,
        ReleaseWaveform => VirtualKeyRelease,
        HoverWaveform => ClockTick,
        SuccessWaveform => Confirm,
        ErrorWaveform => Reject,
        _ => null,
    };
}
