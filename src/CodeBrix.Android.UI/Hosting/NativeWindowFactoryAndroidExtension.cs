using CodeBrix.Platform.UI.Xaml.Controls;
using Microsoft.UI.Xaml;

namespace CodeBrix.Android.UI.Hosting;

/// <summary>
/// The Android <see cref="INativeWindowFactoryExtension"/>: a XAML Window is shown by a
/// <see cref="CodeBrixActivity"/>. In this phase one window is supported (the first window
/// binds to the activity that started the XAML application, and survives the activity
/// being re-created); a second <c>Window.Activate()</c> launching its own activity
/// (desktop windowing) comes with the navigation and windowing work.
/// </summary>
internal sealed class NativeWindowFactoryAndroidExtension : INativeWindowFactoryExtension
{
    internal static readonly NativeWindowFactoryAndroidExtension Instance = new();

    /// <summary>The wrapper of the (single) window, once created.</summary>
    internal AndroidNativeWindowWrapper MainWindow { get; private set; }

    /// <inheritdoc />
    public bool SupportsMultipleWindows => false;

    /// <inheritdoc />
    public bool SupportsClosingCancellation => false;

    /// <inheritdoc />
    public INativeWindowWrapper CreateWindow(Window window, XamlRoot xamlRoot)
    {
        var wrapper = new AndroidNativeWindowWrapper(window, xamlRoot);
        _ = new AndroidXamlRootHost(window, xamlRoot, wrapper);
        MainWindow ??= wrapper;

        var activity = ActivityRegistry.Latest;
        if (activity != null)
        {
            wrapper.AttachActivity(activity);
        }

        return wrapper;
    }
}
