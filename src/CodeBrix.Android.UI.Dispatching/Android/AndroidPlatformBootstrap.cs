using System.Runtime.CompilerServices;
using CodeBrix.Platform.Foundation.Extensibility;
using CodeBrix.Platform.UI.Dispatching.Contracts;

namespace CodeBrix.Android.UI.Dispatching.Android;

/// <summary>
/// Registers the Android implementation of every CodeBrix.Platform.UI.Dispatching Core
/// contract. Idempotent; runs from the module initializer and from the CodeBrix.Android.UI
/// bootstrap chain (whichever comes first).
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

            var pump = new DispatcherPumpAndroidPlatform();
            ApiExtensibility.Register(typeof(IDispatcherPumpPlatform), _ => pump);

            _registered = true;
        }
    }
}
