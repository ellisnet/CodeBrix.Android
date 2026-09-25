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
/// Classifies the running device from the Android UI mode type
/// (Configuration.UiMode &amp; UI_MODE_TYPE_MASK), the "android.hardware.type.pc" feature
/// and the smallest screen width in dp.
/// </summary>
internal static class DeviceFormClassifier
{
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

    /// <summary>The smallest width (dp) from which a touch device counts as a tablet.</summary>
    internal const int TabletSmallestWidthDp = 600;

    /// <summary>Returns the device form.</summary>
    internal static DeviceFormKind Classify(int uiMode, bool hasPcFeature, int smallestScreenWidthDp)
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
            case UiModeTypeDesk:
                return DeviceFormKind.Desktop;
        }

        if (hasPcFeature)
        {
            return DeviceFormKind.Desktop;
        }

        return smallestScreenWidthDp >= TabletSmallestWidthDp ? DeviceFormKind.Tablet : DeviceFormKind.Mobile;
    }
}
