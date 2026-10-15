using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace WebGpuDemo;

/// <summary>
/// The XAML application of WebGpuDemo on Android: the CodeBrix.Platform sample's App (default font, the main page in a
/// Frame) without its desktop-only parts (the generic host and the console logger: on Android the head's
/// CodeBrixApplication routes logging to logcat).
/// </summary>
public partial class App : Application
{
    private Window _window;

    /// <summary>Creates the application (default font, resources).</summary>
    public App()
    {
        // Open Sans for all text in the application.
        global::CodeBrix.Platform.UI.FeatureConfiguration.Font.DefaultTextFontFamily =
            "ms-appx:///CodeBrix.Platform.Fonts.OpenSans/Fonts/OpenSans.ttf";

        InitializeComponent();
    }

    /// <summary>The main page, once the application has launched (the frame-rate log reads its current page).</summary>
    public static Views.MainPage MainPage { get; private set; }

    /// <inheritdoc />
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var frame = new Frame();
        frame.NavigationFailed += OnNavigationFailed;
        _window = new Window { Title = "WebGpu Demo", Content = frame };
        frame.Navigate(typeof(Views.MainPage), args.Arguments);
        MainPage = frame.Content as Views.MainPage;
        _window.Activate();
    }

    private static void OnNavigationFailed(object sender, NavigationFailedEventArgs e) =>
        throw new InvalidOperationException($"Failed to load {e.SourcePageType.FullName}", e.Exception);
}
