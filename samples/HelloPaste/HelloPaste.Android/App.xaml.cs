using System;
using CodeBrix.Platform.Simple;
using JustBetweenUs.Encryption;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using PdfSideBySide.PdfRender;

namespace HelloPaste;

/// <summary>
/// The XAML application of HelloPaste: the two source apps' App constructors folded into
/// one (default font, service registrations), and an OnLaunched that navigates the root
/// Frame to the start page the launch intent asked for.
/// </summary>
public partial class App : Application
{
    /// <summary>Creates the application (default font, services, resources).</summary>
    public App()
    {
        //Set Roboto as the default font for all text in the application (PdfSideBySide's choice;
        //  the JustBetweenUs page sets Open Sans on itself)
        global::CodeBrix.Platform.UI.FeatureConfiguration.Font.DefaultTextFontFamily =
            "ms-appx:///CodeBrix.Platform.Fonts.Roboto/Fonts/Roboto.ttf";

        SimpleServiceResolver.CreateInstance(JustBetweenUs.Helpers.HostHelper.GetHost(), services =>
        {
            services.AddEncryption();
            services.AddPdfRender();
        });
        SimpleViewModel.SetIsDesignMode(false);

        InitializeComponent();
    }

    /// <summary>The application's window.</summary>
    protected Window MainWindow { get; private set; }

    /// <inheritdoc />
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        MainWindow = new Window
        {
            Title = "HelloPaste"
        };

        if (MainWindow.Content is not Frame rootFrame)
        {
            rootFrame = new Frame();
            MainWindow.Content = rootFrame;
            rootFrame.NavigationFailed += OnNavigationFailed;
        }

        if (rootFrame.Content == null)
        {
            rootFrame.Navigate(StartPages.Resolve(StartPages.Requested), args.Arguments);
        }

        MainWindow.Activate();
    }

    void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
    {
        throw new InvalidOperationException($"Failed to load {e.SourcePageType.FullName}: {e.Exception}");
    }
}
