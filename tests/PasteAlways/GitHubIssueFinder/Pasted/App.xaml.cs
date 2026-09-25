using CodeBrix.Platform.Simple;
using GitHubIssueFinder.GitHub;
using GitHubIssueFinder.Helpers;
using GitHubIssueFinder.Settings;
using GitHubIssueFinder.Theming;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;

namespace GitHubIssueFinder;

public partial class App : Application
{
    //The size the window opens at, and the smallest size it may be dragged to. The launch
    //size fits the header, the two search rows and about a dozen result rows without
    //scrolling; the minimum is the point below which the header stops fitting on two lines
    //and the result columns start to collide.
    private const int LaunchWidth = 1180;
    private const int LaunchHeight = 800;
    private const int MinimumWidth = 760;
    private const int MinimumHeight = 520;

    public App()
    {
        //Set Roboto as the default font for all text in the application
        global::CodeBrix.Platform.UI.FeatureConfiguration.Font.DefaultTextFontFamily =
            "ms-appx:///CodeBrix.Platform.Fonts.Roboto/Fonts/Roboto.ttf";

        //Fonts consulted for characters the default font has no glyph for
        global::CodeBrix.Platform.UI.FeatureConfiguration.Font.FallbackFontFamilies =
        [
            "ms-appx:///CodeBrix.Platform.Fonts.Roboto/Fonts/NotoSans.ttf",
            "ms-appx:///CodeBrix.Platform.Fonts.Roboto/Fonts/NotoSansArmenian.ttf",
            "ms-appx:///CodeBrix.Platform.Fonts.Roboto/Fonts/NotoSansGeorgian.ttf",
        ];

        SimpleServiceResolver.CreateInstance(HostHelper.GetHost(), services =>
        {
            //Register the app's services here
            services.AddGitHubIssueSearch(new GitHubSearchOptions());
        });
        SimpleViewModel.SetIsDesignMode(false);

        //The first page's view model is built during InitializeComponent() and reads its
        //remembered values in its own constructor, so the store has to be open before that.
        SettingsService.Initialize();

        //Application.RequestedTheme may be set only here, before initialization completes, and
        //setting it at all is what makes the platform stop following the operating system. So it
        //is left alone for the "System default" choice and set for every explicit one.
        var scheme = ColorSchemes.Parse(
            SettingsService.Get(SettingKeys.ColorScheme, nameof(ColorScheme.SystemDefault)));
        if (scheme != ColorScheme.SystemDefault)
        {
            this.RequestedTheme = ColorSchemes.Get(scheme).BaseIsDark
                ? ApplicationTheme.Dark
                : ApplicationTheme.Light;
        }

        //The size the first window opens at. ApplicationView.PreferredLaunchViewSize is the
        //only public seam an application has for its own launch size: every desktop head
        //reads it while it is creating the native window and falls back to the platform's
        //own 1024 by 640 when it is empty, so it has to be set before any window exists.
        //On the Linux X11 head the numbers are NATIVE pixels of the window's CLIENT area,
        //which on a display at scale 1 is the same as logical units; how each of the other
        //heads reads them is written up in the report that accompanied this change. The
        //value is set on every launch, unconditionally, because the platform remembers it
        //in its own settings file; setting it every time keeps that file in step with this
        //source file instead of letting an old value linger.
        Windows.UI.ViewManagement.ApplicationView.PreferredLaunchViewSize =
            new Windows.Foundation.Size(LaunchWidth, LaunchHeight);

        InitializeComponent();
    }

    protected Window MainWindow { get; private set; }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        MainWindow = new Window
        {
            Title = "GitHubIssueFinder"
        };

        //The smallest size the user may drag the window to. The presenter is already in
        //place here: constructing a Window builds its native window straight away once the
        //application has finished initializing, and the window's default presenter is an
        //OverlappedPresenter. Setting the minimum now, before Activate(), means the window
        //manager has the constraint before the window is ever shown; setting it after
        //Activate() also works, but the window has been mapped once by then. No maximum is
        //set, so the window can still be resized up and maximized.
        if (MainWindow.AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
        {
            presenter.PreferredMinimumWidth = MinimumWidth;
            presenter.PreferredMinimumHeight = MinimumHeight;
        }

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
