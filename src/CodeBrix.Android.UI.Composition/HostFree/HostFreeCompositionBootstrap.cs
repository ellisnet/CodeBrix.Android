using CodeBrix.Android.UI.Composition.Android;
using CodeBrix.Platform.Foundation.Extensibility;
using CodeBrix.Platform.UI.Composition.Contracts;

namespace CodeBrix.Android.UI.Composition.HostFree;

/// <summary>
/// Host-free registration (net10.0 flavor only): gives Core the inert composition platform
/// the Android build uses, so UI elements can be created and laid out without a device (the
/// host-free tests). Composition geometry is not registered (it needs android.graphics).
/// Idempotent.
/// </summary>
internal static class HostFreeCompositionBootstrap
{
    private static readonly object _gate = new();
    private static bool _registered;

    /// <summary>Registers the inert composition platform (once).</summary>
    internal static void EnsureRegistered()
    {
        lock (_gate)
        {
            if (_registered)
            {
                return;
            }

            var composition = new CompositionAndroidPlatform();
            ApiExtensibility.Register(typeof(ICompositionPlatform), _ => composition);
            _registered = true;
        }
    }
}
