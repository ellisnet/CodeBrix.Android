using System.Runtime.CompilerServices;
using CodeBrix.Platform.AppSettings.Contracts;
using CodeBrix.Platform.Foundation.Extensibility;

namespace CodeBrix.Android.AppSettings.Android;

/// <summary>
/// Registers the Android side of the AppSettings add-in: IAppSettingsStoragePlatform (SQLite under Context.FilesDir).
/// Idempotent; runs as the module initializer (the Core's PlatformContract loads this assembly BY NAME when the first
/// store opens; the CodeBrix.Android.UI bootstrap also loads it at start-up). Needs no UI: a store can open before the
/// XAML application starts.
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

    /// <summary>Registers the add-in (once).</summary>
#pragma warning disable CA2255 // The module initializer is the platform bootstrap by design (twin loading by name).
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

            if (!ApiExtensibility.IsRegistered<IAppSettingsStoragePlatform>())
            {
                var storage = new AppSettingsStorageAndroidPlatform();
                ApiExtensibility.Register(typeof(IAppSettingsStoragePlatform), _ => storage);
            }

            _registered = true;
        }
    }
}
