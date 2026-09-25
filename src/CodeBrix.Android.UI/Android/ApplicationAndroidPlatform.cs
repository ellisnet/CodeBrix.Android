using CodeBrix.Android.UI.Hosting;
using CodeBrix.Platform.Foundation.Extensibility;
using CodeBrix.Platform.UI.Contracts;
using CodeBrix.Platform.UI.Xaml.Controls;

namespace CodeBrix.Android.UI.Android;

/// <summary>
/// The Android implementation of <see cref="IApplicationPlatform"/>. Core calls
/// <see cref="RegisterExtensions"/> from the XAML Application's static constructor; it
/// registers the Android registry extensions of this assembly (the native window factory).
/// </summary>
internal sealed class ApplicationAndroidPlatform : IApplicationPlatform
{
    private static readonly object _gate = new();
    private static bool _registered;

    /// <inheritdoc />
    public void RegisterExtensions()
    {
        lock (_gate)
        {
            if (_registered)
            {
                return;
            }

            ApiExtensibility.Register(typeof(INativeWindowFactoryExtension), _ => NativeWindowFactoryAndroidExtension.Instance);
            _registered = true;
        }
    }
}
