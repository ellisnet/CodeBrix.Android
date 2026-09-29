using System.Runtime.CompilerServices;
using CodeBrix.Android.Services;
using CodeBrix.Platform.ApplicationModel.DataTransfer;
using CodeBrix.Platform.Contracts;
using CodeBrix.Platform.Extensions.Storage.Pickers;
using CodeBrix.Platform.Extensions.System;
using CodeBrix.Platform.Foundation.Extensibility;
using CodeBrix.Platform.Helpers.Theming;
using CodeBrix.Platform.UI.Notifications;
using Windows.ApplicationModel.Contacts;
using Windows.Devices.Haptics;
using Windows.Graphics.Display;
using Windows.Networking.Connectivity;
using Windows.Storage.Pickers;
using Windows.System.Profile.Internal;
using Windows.UI.ViewManagement;

namespace CodeBrix.Android.Android;

/// <summary>
/// Registers the Android implementation of every CodeBrix.Platform (WinRT surface) Core
/// contract and the WinRT registry extensions this assembly provides. Idempotent; runs
/// from the module initializer and from the CodeBrix.Android.UI bootstrap chain.
/// </summary>
internal static class AndroidPlatformBootstrap
{
    private static readonly object _gate = new();
    private static bool _registered;

    /// <summary>Gets a value indicating whether the registrations have run.</summary>
    internal static bool IsRegistered
    {
        get
        {
            lock (_gate)
            {
                return _registered;
            }
        }
    }

#pragma warning disable CA2255 // The module initializer is the platform bootstrap by design (B4 pattern).
    [ModuleInitializer]
#pragma warning restore CA2255
    internal static void EnsureRegistered()
    {
        lock (_gate)
        {
            if (_registered)
            {
                return;
            }

            var applicationData = new ApplicationDataAndroidPlatform();
            var globalization = new GlobalizationPreferencesAndroidPlatform();
            var graphicsImaging = new GraphicsImagingAndroidPlatform();
            ApiExtensibility.Register(typeof(IApplicationDataPlatform), _ => applicationData);
            ApiExtensibility.Register(typeof(IGlobalizationPreferencesPlatform), _ => globalization);
            ApiExtensibility.Register(typeof(IGraphicsImagingPlatform), _ => graphicsImaging);

            ApiExtensibility.Register(typeof(IAnalyticsInfoExtension), _ => new AnalyticsInfoAndroidExtension());
            ApiExtensibility.Register(typeof(IDeviceFamilyPlatform), _ => new DeviceFamilyAndroidPlatform());
            ApiExtensibility.Register(typeof(ISystemThemeHelperExtension), _ => SystemThemeHelperAndroidExtension.Create());
            ApiExtensibility.Register(typeof(IDisplayInformationExtension), _ => new DisplayInformationAndroidExtension());

            // AP1.12 (WPE1-13 item a): the application package's files (ms-appx:///) are the APK's assets.
            var packageFiles = new ApplicationPackageFilesAndroidPlatform();
            ApiExtensibility.Register(typeof(IApplicationPackageFilesPlatform), _ => packageFiles);

            // The services of AP4 (plan 2.3 registry row): clipboard, launcher, share, connectivity, haptics,
            // pickers over the Storage Access Framework, application view, badge (no-op), contact picker (D-O10).
            // The ones that show system UI go through the activity bridge CodeBrix.Android.UI installs.
            var clipboard = new ClipboardAndroidExtension();
            ApiExtensibility.Register(typeof(IClipboardExtension), _ => clipboard);
            ApiExtensibility.Register(typeof(ILauncherExtension), _ => new LauncherAndroidExtension());
            ApiExtensibility.Register(typeof(IDataTransferManagerExtension), _ => new DataTransferManagerAndroidExtension());
            ApiExtensibility.Register(typeof(IConnectionProfileExtension), _ => new ConnectionProfileAndroidExtension());
            ApiExtensibility.Register(typeof(ISimpleHapticsControllerExtension), _ => new SimpleHapticsControllerAndroidExtension());
            ApiExtensibility.Register(typeof(IVibrationDeviceExtension), _ => new VibrationDeviceAndroidExtension());
            ApiExtensibility.Register(typeof(IFileOpenPickerExtension), picker => new FileOpenPickerAndroidExtension((FileOpenPicker)picker));
            ApiExtensibility.Register(typeof(IFileSavePickerExtension), picker => new FileSavePickerAndroidExtension((FileSavePicker)picker));
            ApiExtensibility.Register(typeof(IFolderPickerExtension), _ => new FolderPickerAndroidExtension());
            ApiExtensibility.Register(typeof(IApplicationViewExtension), _ => new ApplicationViewAndroidExtension());
            ApiExtensibility.Register(typeof(IBadgeUpdaterExtension), _ => new BadgeUpdaterAndroidExtension());
            ApiExtensibility.Register(typeof(IContactPickerExtension), _ => new ContactPickerAndroidExtension());

            _registered = true;
        }
    }
}
