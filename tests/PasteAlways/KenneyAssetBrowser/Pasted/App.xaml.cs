using KenneyAssetBrowser.Helpers;
using KenneyAssetBrowser.Settings;
using CodeBrix.Platform.Simple;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;

namespace KenneyAssetBrowser;

public partial class App : Application
{
    public App()
    {
        //Set Merriweather as the default font for all text in the application
        global::CodeBrix.Platform.UI.FeatureConfiguration.Font.DefaultTextFontFamily =
            "ms-appx:///CodeBrix.Platform.Fonts.Merriweather/Fonts/Merriweather.ttf";

        //Fonts consulted for characters the default font has no glyph for
        global::CodeBrix.Platform.UI.FeatureConfiguration.Font.FallbackFontFamilies =
        [
            "ms-appx:///CodeBrix.Platform.Fonts.Merriweather/Fonts/NotoSerif.ttf",
            "ms-appx:///CodeBrix.Platform.Fonts.Merriweather/Fonts/NotoSerifArmenian.ttf",
            "ms-appx:///CodeBrix.Platform.Fonts.Merriweather/Fonts/NotoSerifGeorgian.ttf",
        ];

        SimpleServiceResolver.CreateInstance(HostHelper.GetHost(), services =>
        {
            //Register the app's services here
            services.AddKenneyAssetBrowser();
        });
        SimpleViewModel.SetIsDesignMode(false);

        //Open (or silently create) the single portable settings.sqlite store —
        //  including its startup auto-backup and pruning — before any UI renders.
        SettingsService.Initialize();

        InitializeComponent();
    }

    protected Window MainWindow { get; private set; }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        MainWindow = new Window
        {
            Title = "KenneyAssetBrowser"
        };

        if (MainWindow.Content is not Frame rootFrame)
        {
            rootFrame = new Frame();
            MainWindow.Content = rootFrame;
            rootFrame.NavigationFailed += OnNavigationFailed;
        }

        if (rootFrame.Content == null)
        {
            rootFrame.Navigate(typeof(Views.MainPage), args.Arguments);
        }

        MainWindow.Activate();
    }

    void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
    {
        throw new InvalidOperationException($"Failed to load {e.SourcePageType.FullName}: {e.Exception}");
    }

    // Called from each head's Program.Main BEFORE building the host.
    public static void InitializeLogging()
    {
#if DEBUG
        var factory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
            builder.AddFilter("CodeBrix.Platform", LogLevel.Warning);
            builder.AddFilter("Windows", LogLevel.Warning);
            builder.AddFilter("Microsoft", LogLevel.Warning);
        });

        global::CodeBrix.Platform.Extensions.LogExtensionPoint.AmbientLoggerFactory = factory;
        global::CodeBrix.Platform.UI.Adapter.Microsoft.Extensions.Logging.LoggingAdapter.Initialize();
#endif
    }
}
