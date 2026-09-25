using System.Runtime.CompilerServices;
using CodeBrix.Platform.Foundation.Extensibility;
using CodeBrix.Platform.UI.Toolkit.Contracts;

namespace CodeBrix.Android.UI.Toolkit.Android;

/// <summary>
/// Registers the Android implementation of every CodeBrix.Platform.UI.Toolkit Core
/// contract. Idempotent; runs from the module initializer and from the
/// CodeBrix.Android.UI bootstrap chain.
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

    /// <summary>The registered elevation platform (null before registration).</summary>
    internal static ElevationAndroidPlatform Elevation { get; private set; }

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

            var elevation = new ElevationAndroidPlatform();
            Elevation = elevation;
            ApiExtensibility.Register(typeof(IElevationPlatform), _ => elevation);

            _registered = true;
        }
    }
}
