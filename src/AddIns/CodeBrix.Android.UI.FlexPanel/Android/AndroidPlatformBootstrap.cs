using System.Runtime.CompilerServices;
using CodeBrix.Android.UI.Handlers;
using UIBootstrap = CodeBrix.Android.UI.Android.AndroidPlatformBootstrap;

namespace CodeBrix.Android.UI.FlexPanel.Android;

/// <summary>
/// The Android side of the FlexPanel add-in: FlexPanel (Core by whole) is a Panel, shown by the framework's panel
/// handler; nothing else is registered. Idempotent; runs as the module initializer (the CodeBrix.Android.UI bootstrap
/// loads this assembly by name at start-up) and checks that the panel registration serves FlexPanel.
/// </summary>
internal static class AndroidPlatformBootstrap
{
    private static readonly object _gate = new();
    private static bool _registered;

    /// <summary>Gets a value indicating whether the bootstrap has run.</summary>
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

    /// <summary>Runs the bootstrap (once).</summary>
#pragma warning disable CA2255 // The module initializer is the platform bootstrap by design (twin loading by name).
    [ModuleInitializer]
#pragma warning restore CA2255
    internal static void EnsureRegistered()
    {
        UIBootstrap.EnsureRegistered();

        lock (_gate)
        {
            // Nothing to register: FlexPanel resolves to the framework's Panel registration (most-derived type wins,
            // and no add-in registers a FlexPanel handler of its own).
            _registered = true;
        }
    }
}
