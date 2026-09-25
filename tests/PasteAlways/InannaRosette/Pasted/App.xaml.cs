using CodeBrix.Platform.Simple;
using InannaRosette.Helpers;
using InannaRosette.Reading;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;

namespace InannaRosette;

public partial class App : Application
{
    //The size the window opens at, and the smallest size it may be dragged to. The launch
    //size gives the altar a rosette at its full card size with the 360-wide deck rail beside
    //it and the header's two rows of controls on one line each; the minimum is the point
    //below which the header buttons start to wrap and the rosette's station labels collide
    //with the cards on the diagonal petals.
    private const int LaunchWidth = 1440;
    private const int LaunchHeight = 900;
    private const int MinimumWidth = 1180;
    private const int MinimumHeight = 760;

    public App()
    {
        //Merriweather is the app's voice: an old-style serif for a temple oracle
        global::CodeBrix.Platform.UI.FeatureConfiguration.Font.DefaultTextFontFamily =
            "ms-appx:///CodeBrix.Platform.Fonts.Merriweather/Fonts/Merriweather.ttf";

        SimpleServiceResolver.CreateInstance(HostHelper.GetHost(), services =>
        {
            //Register the app's services here
            services.AddReading();
        });
        SimpleViewModel.SetIsDesignMode(false);

        //The size the first window opens at. ApplicationView.PreferredLaunchViewSize is the
        //only public seam an application has for its own launch size: every desktop head
        //reads it while it is creating the native window and falls back to the platform's
        //own default when it is empty, so it has to be set before any window exists. The
        //value is set on every launch, unconditionally, because the platform remembers it in
        //its own settings file; setting it every time keeps that file in step with this
        //source file instead of letting an old value linger.
        Windows.UI.ViewManagement.ApplicationView.PreferredLaunchViewSize =
            new Windows.Foundation.Size(LaunchWidth, LaunchHeight);

        RequestedTheme = ApplicationTheme.Dark;
        InitializeComponent();
    }

    protected Window? MainWindow { get; private set; }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        MainWindow = new Window { Title = "Rosette of Inanna" };

        //The smallest size the user may drag the window to. The presenter is already in
        //place here: constructing a Window builds its native window straight away once the
        //application has finished initializing, and the window's default presenter is an
        //OverlappedPresenter. Setting the minimum now, before Activate(), means the window
        //manager has the constraint before the window is ever shown. No maximum is set, so
        //the window can still be resized up and maximized.
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

    void OnNavigationFailed(object sender, NavigationFailedEventArgs e) =>
        throw new InvalidOperationException($"Failed to load {e.SourcePageType.FullName}: {e.Exception}");

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
