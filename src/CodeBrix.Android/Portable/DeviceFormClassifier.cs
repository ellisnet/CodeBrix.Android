namespace CodeBrix.Android.Portable;

/// <summary>
/// The device forms AnalyticsInfo reports, in the order (and with the numeric values) of
/// the Core enum Windows.System.Profile.Internal.CodeBrixDeviceForm.
/// </summary>
internal enum DeviceFormKind
{
    /// <summary>Unknown.</summary>
    Unknown = 0,

    /// <summary>A phone.</summary>
    Mobile = 1,

    /// <summary>A tablet.</summary>
    Tablet = 2,

    /// <summary>A television.</summary>
    Television = 3,

    /// <summary>A car head unit.</summary>
    Car = 4,

    /// <summary>A watch.</summary>
    Watch = 5,

    /// <summary>A virtual-reality headset.</summary>
    VirtualReality = 6,

    /// <summary>A desktop (Android desktop, desktop windowing, a docked phone).</summary>
    Desktop = 7,

    /// <summary>A game console.</summary>
    GameConsole = 8,
}

/// <summary>
/// Classifies the running device (decision D2, ruled by Jeremy 2026-09-26): the special Android UI mode types
/// (television, car, watch, VR headset) keep their own forms; every other device takes its form from the width
/// size class of the window it is showing: Compact -&gt; Mobile, Medium -&gt; Tablet, Expanded -&gt; Desktop.
/// The form is read at query time, so a docked phone that moves between size classes reports the current form.
/// </summary>
/// <remarks>
/// The width breakpoints are the Material 3 ones of the size-class service (CodeBrix.Android.UI
/// WindowSizeClasses): Compact &lt; 600 dp, Medium 600-839 dp, Expanded &gt;= 840 dp; an unknown width counts as
/// Expanded, as it does there. UI_MODE_TYPE_DESK and the "android.hardware.type.pc" feature no longer force
/// Desktop: a desk-docked or PC-class device in a narrow window is Mobile, as its window is (D2).
/// </remarks>
internal static class DeviceFormClassifier
{
    /// <summary>The operating-system family CodeBrix.Android names in DeviceFamily ("Android.&lt;form&gt;", D2).</summary>
    internal const string OperatingSystemFamily = "Android";

    /// <summary>UI_MODE_TYPE_MASK.</summary>
    internal const int UiModeTypeMask = 0x0f;

    /// <summary>UI_MODE_TYPE_DESK.</summary>
    internal const int UiModeTypeDesk = 0x02;

    /// <summary>UI_MODE_TYPE_CAR.</summary>
    internal const int UiModeTypeCar = 0x03;

    /// <summary>UI_MODE_TYPE_TELEVISION.</summary>
    internal const int UiModeTypeTelevision = 0x04;

    /// <summary>UI_MODE_TYPE_WATCH.</summary>
    internal const int UiModeTypeWatch = 0x06;

    /// <summary>UI_MODE_TYPE_VR_HEADSET.</summary>
    internal const int UiModeTypeVrHeadset = 0x07;

    /// <summary>The first window width (dp) of the Medium width size class (-&gt; Tablet).</summary>
    internal const double MediumMinWidthDp = 600;

    /// <summary>The first window width (dp) of the Expanded width size class (-&gt; Desktop).</summary>
    internal const double ExpandedMinWidthDp = 840;

    /// <summary>Returns the device form of a device in UI mode <paramref name="uiMode"/> showing a window <paramref name="windowWidthDp"/> wide.</summary>
    /// <param name="uiMode">Configuration.UiMode (only the type bits are read).</param>
    /// <param name="windowWidthDp">The width of the current window in dp (NaN or non-positive = unknown).</param>
    /// <returns>The form.</returns>
    internal static DeviceFormKind Classify(int uiMode, double windowWidthDp)
    {
        switch (uiMode & UiModeTypeMask)
        {
            case UiModeTypeTelevision:
                return DeviceFormKind.Television;
            case UiModeTypeCar:
                return DeviceFormKind.Car;
            case UiModeTypeWatch:
                return DeviceFormKind.Watch;
            case UiModeTypeVrHeadset:
                return DeviceFormKind.VirtualReality;
        }

        return FromWindowWidth(windowWidthDp);
    }

    /// <summary>The form of a window <paramref name="windowWidthDp"/> wide: its width size class (D2).</summary>
    /// <param name="windowWidthDp">The window width in dp (NaN or non-positive = unknown = Expanded).</param>
    /// <returns>Mobile (Compact), Tablet (Medium) or Desktop (Expanded).</returns>
    internal static DeviceFormKind FromWindowWidth(double windowWidthDp)
    {
        if (double.IsNaN(windowWidthDp) || windowWidthDp <= 0)
        {
            return DeviceFormKind.Desktop;
        }

        return windowWidthDp < MediumMinWidthDp ? DeviceFormKind.Mobile
            : windowWidthDp < ExpandedMinWidthDp ? DeviceFormKind.Tablet
            : DeviceFormKind.Desktop;
    }

    /// <summary>The DeviceFamily string of a form: "Android.&lt;form&gt;" (Core composes it from <see cref="OperatingSystemFamily"/> and the form).</summary>
    /// <param name="form">The form.</param>
    /// <returns>The string, e.g. "Android.Tablet".</returns>
    internal static string DeviceFamilyOf(DeviceFormKind form) => OperatingSystemFamily + "." + form;
}
