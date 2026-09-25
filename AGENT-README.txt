================================================================================
AGENT-README: CodeBrix.Android
A Guide for AI Coding Agents — CONSUMING the CodeBrix.Android.ApacheLicenseForever NuGet package
================================================================================


OVERVIEW
========

CodeBrix.Android builds CodeBrix.Platform applications (C# and XAML, WinUI API)
as native Android apps. The pages, view models and code-behind an app already
has for CodeBrix.Platform compile unchanged for Android ("paste always"); on
Android every element is shown with a real Android or Material Components view
rather than drawn by CodeBrix.

The package contains the platform-neutral CodeBrix.Platform assemblies (the
same object model, bindings, resources and styles as on the desktop), the
CodeBrix.Android assemblies that implement them on Android, and the build logic
that compiles CodeBrix.Platform XAML for Android.

Target framework: net10.0-android36.1, minimum Android API level 33.
CodeBrix.Android is desktop-first (Android desktops such as Googlebooks, and
phones docked in desktop mode); phones and tablets are first-class targets.


INSTALLATION
============

NuGet package id:
    CodeBrix.Android.ApacheLicenseForever

dotnet CLI (in the Android head project):
    dotnet add package CodeBrix.Android.ApacheLicenseForever

Dependencies (pulled in automatically): the .NET bindings of Material
Components for Android and AndroidX (Xamarin.Google.Android.Material,
Xamarin.AndroidX.*), Microsoft.Extensions.Logging,
Microsoft.Extensions.DependencyInjection.Abstractions,
Microsoft.Extensions.Hosting.Abstractions, CodeBrix.ServiceLocator and the
CodeBrix Fluent symbols font package.

License: Apache License 2.0.

Requirements: the .NET 10 SDK with the "android" workload, and the Android SDK.


KEY NAMESPACES / USINGS
=======================

App code keeps the CodeBrix.Platform namespaces:

    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Data;
    using CodeBrix.Platform.UI;          // FeatureConfiguration and friends

Android-specific code (the head project only) uses the CodeBrix.Android
namespaces and Mono.Android:

    using CodeBrix.Android.UI.Hosting;       // CodeBrixApplication, CodeBrixActivity
    using CodeBrix.Android.UI.Logging;       // LogcatLoggerProvider
    using CodeBrix.Android.UI.Diagnostics;   // VisualTreeDump

Inside any namespace that starts with CodeBrix.Android, write Mono.Android
types with the global alias (see COMMON PITFALLS).


CORE API REFERENCE
==================

APP SHAPE
---------
A CodeBrix.Platform app is usually:
    MyApp.UI     shared project: App.xaml(.cs), Views/*.xaml(.cs)
    MyApp.Core   class library: view models, services
    one head project per platform

The Android head:
    MyApp.Android   net10.0-android36.1, OutputType Exe,
                    SupportedOSPlatformVersion 33, an ApplicationId;
                    imports MyApp.UI.projitems (the XAML is shared, not copied);
                    references MyApp.Core and CodeBrix.Android.ApacheLicenseForever.

THE ANDROID HEAD'S TWO CLASSES
------------------------------
The head adds an Android Application and a launcher activity; the pasted
App.xaml.cs (OnLaunched, Window, Frame.Navigate) stays unchanged.

    [Application]
    public class MainApplication : CodeBrixApplication
    {
        public MainApplication(IntPtr handle, JniHandleOwnership transfer)
            : base(handle, transfer) { }

        protected override Microsoft.UI.Xaml.Application CreateApp() => new App();
    }

    [Activity(MainLauncher = true,
              Theme = "@style/Theme.Material3.DayNight.NoActionBar",
              ConfigurationChanges = CodeBrixActivity.HandledConfigurationChanges)]
    public class MainActivity : CodeBrixActivity { }

  * CodeBrixApplication.OnCreate configures logging and registers the Android
    implementation of every CodeBrix.Platform platform contract, before any
    XAML type is used. The first CodeBrixActivity starts the XAML application
    (Application.Start -> App constructor -> OnLaunched).
  * The activity theme must be an AppCompat / Material Components theme
    (Theme.Material3.*); CodeBrixActivity is an AppCompatActivity.
  * ConfigurationChanges = CodeBrixActivity.HandledConfigurationChanges is
    required: resizing, docking, orientation, density and night-mode changes
    must not re-create the activity. (Android reads it from the manifest, so
    it cannot be inherited from the base class.)
  * Content is laid out edge to edge; Window.Bounds is the whole Android
    window and XamlRoot / the window's visible bounds exclude the system bars
    and display cutout.
  * One XAML Window per app in this version. If Android re-creates the
    activity, the same Window (and its Frame content) is shown again.
  * Logging: override ConfigureLogging(ILoggingBuilder) to change levels
    (default Information) or add providers; entries go to logcat with the
    app's package name as the tag (`adb logcat -s <package>`). Override
    LogVisualTreeAfterLayout => true to log a text dump of the visual tree
    after each layout pass (diagnostics).
  * Fonts: a FontFamily that names a font file (ms-appx:///... or an app
    path, including the CodeBrix font packages) is loaded from the APK (the
    package's .ttf.manifest picks the weight / italic / stretch face); a
    family NAME (e.g. Core's default "Segoe UI") uses the app's default font
    file, FeatureConfiguration.Font.DefaultTextFontFamily - set it in the App
    constructor as the desktop heads do. CodeBrix apps never fall back to a
    system font: only an app that sets no default font file gets Android's
    default typeface (and one logcat warning says so).
  * File pickers: FileOpenPicker / FileSavePicker use the Storage Access
    Framework and FolderPicker a document tree. The StorageFile / StorageFolder
    you get works on the chosen document itself through its own members
    (FileIO, OpenReadAsync, OpenStreamForReadAsync / ...ForWriteAsync, folder
    listing, CreateFileAsync, DeleteAsync). StorageFile.Path is a real local
    path made when you first read it - a copy of an opened document, or for a
    saved document a file copied back to it every time you close it after
    writing - so desktop code that opens file.Path keeps working; prefer the
    StorageFile's streams. A picked folder's Path is empty (use its members).
  * AnalyticsInfo.VersionInfo.DeviceFamily is "Android.<form>" (for example
    "Android.Mobile"); UISettings.AnimationsEnabled follows the system's
    "remove animations" setting.
  * Display: every element of the page gets a native Android view through an
    element handler. Core still does the layout; the views are placed exactly
    where Core laid the elements out. Panels, Border and ContentPresenter draw
    their backgrounds and borders natively (solid and gradient brushes, per-side
    thickness, per-corner radius), TextBlock is a native text view (inline runs,
    line height, selection), shapes draw their geometry, a Page draws its
    Background, ContentControl/UserControl/Page host their content directly, a
    ScrollViewer scrolls natively, and a control that has no native handler yet
    shows its Fluent template (mirrored by native views). Controls in their
    default style are native Material widgets (buttons, CheckBox, RadioButton,
    ToggleSwitch, Slider, progress, TextBox, PasswordBox, NumberBox,
    AutoSuggestBox, Image, lists and tabs); a control with its own Template keeps
    the Fluent look. Shapes draw and hit-test natively as WinUI does (caps,
    dashes, Stretch, gradient brushes). ColorPicker is native; DatePicker and
    TimePicker open Material picker dialogs. Expander is a native Material
    card with a native header row unless the AppContext switch
    CodeBrix.Android.UI.NativeExpander is false (then it keeps its Fluent
    template).
    A CommandBar is a Material app bar where the page puts it (a bottom app bar
    in a phone-width window, a top app bar with its Content text as title
    otherwise; SecondaryCommands in the overflow menu) unless the AppContext
    switch CodeBrix.Android.UI.NativeCommandBar is false; PipsPager,
    PagerControl, BreadcrumbBar, a single-selection CalendarView and
    CalendarDatePicker are native too. InfoBar, RatingControl, SplitButton and
    ToggleSplitButton are Material widgets drawn over their Fluent template
    (which keeps every event and part); InfoBadge, PersonPicture, ColorSpectrum
    and the DropDownButton (Material button with a chevron) are native;
    RefreshContainer pulls to refresh through a SwipeRefreshLayout; ScrollView
    scrolls natively; a monochrome BitmapIcon is tinted. TitleBar, ListBox,
    ListBoxItem, GroupItem, the list / picker flyout presenters, the rich-text
    family (RichTextBlock, RichEditBox, Paragraph, ...), Hub, SemanticZoom,
    ParallaxView, AnnotatedScrollBar, MapControl, the swap-chain panels and the
    legacy WebView are not implemented by the Platform on any head.
  * Look and adaptivity: an app that sets no control keys looks Material 3
    (tonal default Button, filled AccentButtonStyle, Material surface/text/
    outline roles; dynamic colour where the device offers it); every Fluent key
    an app defines wins. FontSize follows the user's font scale unless
    IsTextScaleFactorEnabled=false. An Auto NavigationView becomes a bottom bar
    / rail / drawer by window size class and switches live. Storyboards run.
  * Insets: the app is laid out edge to edge; a Page keeps its content clear of
    the status bar, navigation bar and display cutout it overlaps (its
    Background still runs under them). A NavigationView shown as a bottom bar /
    rail / drawer does the same: its bar keeps its destinations clear of the
    system bars and its content (unless that is a Page, which insets itself) is
    laid out clear of them. Any other root that is not a Page is not inset.
    Window.Bounds is the whole window; VisibleBounds excludes the bars.
  * Input: touch, mouse (hover, wheel, right click), stylus and hardware keys
    reach Core's routed events (PointerPressed, Tapped, KeyDown, keyboard
    accelerators, focus and Tab), and the Android back button goes back through
    a Frame that can go back. While a text box has the focus, a hardware key
    goes to the text box first and reaches KeyDown handlers only when the box
    did not use it (typed characters, caret keys and Backspace stay in the box;
    Enter arrives as the keyboard's action and is raised as VirtualKey.Enter;
    Tab, Escape and accelerators still reach the page). The soft keyboard opens
    when a text box is touched, not when a page first shows. The diagnostic projection viewer is off by
    default; override UseProjectionViewer => true only to inspect a page.

MyApp.Core must not bring the CodeBrix.Platform desktop packages into the
Android build. Multi-target it and choose the package per target framework:

    <TargetFrameworks>net10.0;net10.0-android36.1</TargetFrameworks>
    <ItemGroup Condition="'$(TargetFramework)' == 'net10.0'">
      <PackageReference Include="CodeBrix.Platform.ApacheLicenseForever" />
    </ItemGroup>
    <ItemGroup Condition="'$(TargetFramework)' == 'net10.0-android36.1'">
      <PackageReference Include="CodeBrix.Android.ApacheLicenseForever" />
    </ItemGroup>

WHAT THE PACKAGE DOES AT BUILD TIME
-----------------------------------
  * Runs the CodeBrix.Platform XAML source generator on every Page item
    (App.xaml included) with the XAML dialect of a CodeBrix.Platform desktop
    build: the same conditional XAML namespaces are included (not_android,
    skia, netstdref, ...) and excluded (android, not_skia, ...), and d:
    design-time attributes are ignored exactly as on the desktop.
  * Processes .resw string resources into embedded resources, as on the
    desktop.
  * Adds every app asset (Content items) and every asset of a referenced
    CodeBrix library package (fonts, images) to the APK as an Android asset
    whose path is its ms-appx path: ms-appx:///Assets/Logo.png is the asset
    Assets/Logo.png; ms-appx:///CodeBrix.Platform.Fonts.Roboto/Fonts/Roboto.ttf
    is the asset CodeBrix.Platform.Fonts.Roboto/Fonts/Roboto.ttf. (The Android
    SDK's default "Assets/** without the Assets/ prefix" item is turned off for
    this reason.)
  * Defines the compile constants
        HAS_CODEBRIX  __CODEBRIX__  HAS_CODEBRIX_WINUI  __CODEBRIX_WINUI__
        WINUI_WINDOWING  CODEBRIX_HAS_FRAMEWORKELEMENT_MEASUREOVERRIDE
        CODEBRIX_HAS_NO_IDEPENDENCYOBJECT  CODEBRIX_REFERENCE_API
        HAS_CODEBRIX_ANDROID  __CODEBRIX_ANDROID__
    and NOT HAS_CODEBRIX_SKIA / __CODEBRIX_SKIA__ / __DESKTOP__.
  * Release builds are trimmed; the head assembly and the assemblies of its
    project references are trimmer roots (bindings reach view-model
    properties by reflection), and so is every CodeBrix.Android assembly the
    app references (the platform loads them by name). The re-shipped
    CodeBrix.Platform Cores' roots of their Skia twins are switched off
    (CodeBrix.Platform.RootSkiaPlatformAssemblies=false: an Android app ships
    no Skia twin). Keep XAML resource trimming off.

BUILD PROPERTIES YOU MAY SET
----------------------------
  CodeBrixDragDropExternalSupport           default true
  CodeBrixEnableDynamicDataTemplateUpdate   default true in Debug, false in Release
  CodeBrixAndroidRootPlatformAssemblies     default true (false: the CodeBrix.Android
                                            assemblies are not trimmer roots)
  EnableDefaultAndroidAssetItems            default false (see above)


ADD-INS ON ANDROID
------------------
Each CodeBrix.Platform add-in has an Android package
(CodeBrix.Android.<AddIn>.ApacheLicenseForever): the add-in's platform-neutral
Core plus its Android assembly. Application code written against the add-in
compiles and runs unchanged; what differs on Android:
  * SkiaSharp.Views: SKXamlCanvas paints in software into a kept buffer and
    repaints only on Invalidate(), as on every head. SKSwapChainPanel is not
    supported (it throws NotSupportedException unless RaiseOnUnsupported is
    false, as the add-in documents for every head without a GPU panel).
  * AppSettings: the settings database lives under Context.FilesDir
    (CodeBrix/<app>/settings/settings.sqlite); backups, quarantine, export and
    import behave as on the desktop heads.
  * WebView and MediaPlayer: add the INTERNET permission to your
    AndroidManifest.xml for http(s) pages and clips
    (<uses-permission android:name="android.permission.INTERNET" />). WebView
    uses the device's system WebView; MediaPlayer uses Media3 ExoPlayer
    (ms-appx:/// sources are the app's assets).
  * TextLayout: the text engine is the add-in's Core; on Android it draws with
    SkiaSharp typefaces the Android assembly loads from the app's assets by the
    font rule above (a family name is the app's DefaultTextFontFamily file).
  * Graphics3DGL: GLCanvasElement renders on an OpenGL ES 3.0 context, so
    shaders must be GLSL ES (`#version 300 es` plus a precision statement);
    desktop GLSL (`#version 330 core`) does not compile. SkiaGLCanvasElement,
    OffscreenGLContext and SkiaGpuContext are available (OpenGL ES backend).


COMPLETE EXAMPLES
=================

A pasted page and its view model compile unchanged:

    <Page x:Class="MyApp.Views.MainPage"
          xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
          xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
      <StackPanel Spacing="16">
        <TextBlock Text="{Binding Greeting}" />
        <Button Content="Click me" Command="{Binding ClickCommand}" />
      </StackPanel>
    </Page>

    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
        }
    }

Code that must differ on Android uses the constants:

    #if HAS_CODEBRIX_ANDROID
        // Android-only code
    #endif


MINIMUM VIABLE PROJECT TEMPLATE
===============================

    <Project Sdk="Microsoft.NET.Sdk">
      <PropertyGroup>
        <TargetFramework>net10.0-android36.1</TargetFramework>
        <SupportedOSPlatformVersion>33</SupportedOSPlatformVersion>
        <OutputType>Exe</OutputType>
        <ApplicationId>com.example.myapp</ApplicationId>
        <Nullable>disable</Nullable>
        <ImplicitUsings>disable</ImplicitUsings>
      </PropertyGroup>
      <Import Project="..\MyApp.UI\MyApp.UI.projitems" Label="Shared" />
      <ItemGroup>
        <ProjectReference Include="..\MyApp.Core\MyApp.Core.csproj" />
        <PackageReference Include="CodeBrix.Android.ApacheLicenseForever" Version="..." />
      </ItemGroup>
    </Project>


PERFORMANCE TIPS
================

  * Deploy Debug builds with `dotnet build -t:Install` (fast deployment);
    do not side-load a Debug APK with `adb install`, it does not contain the
    assemblies.
  * Release builds are trimmed and ahead-of-time compiled; measure start-up
    on a Release build.


COMMON PITFALLS TO AVOID
========================

  * Namespaces: inside `namespace CodeBrix.Android.*` (and inside any of your
    own namespaces that contain a segment named Android) the identifier
    `Android` binds to the nearer namespace. Write Mono.Android types as
    `global::Android.Views.View`, or add an alias at the end of the using
    block: `using AView = global::Android.Views.View;`.
  * Never reference CodeBrix.Platform.ApacheLicenseForever (or a CodeBrix
    Platform head or add-in package) from the Android target framework: it
    brings the desktop Skia assemblies into the APK. Multi-target shared
    libraries as shown under APP SHAPE.
  * Do not put app assets under the Android `Assets/` convention expecting the
    prefix to be stripped: CodeBrix.Android keeps ms-appx paths as they are.
  * ControlTemplates written for CodeBrix.Platform controls are accepted but
    have no effect on controls that CodeBrix.Android shows as native widgets.


WHAT THIS PACKAGE DOES NOT DO
=============================

  * It does not draw CodeBrix.Platform controls itself; the look is Material
    3 as Material Components for Android provides it. Pixel-identical output
    with the desktop heads is not a goal.
  * It does not support iOS (see CodeBrix.Mobile) or Android versions below
    API 33.
  * It does not use the CodeBrix.Platform desktop head or runtime packages.


WORKING EXAMPLES ON GITHUB
==========================

  Samples:  https://github.com/ellisnet/CodeBrix.Android/tree/main/samples
            samples/HelloPaste: two CodeBrix.Samples pages pasted unchanged,
            with multi-targeted .Core libraries and an Android head.


QUICK REFERENCE CARD
====================

  Package      CodeBrix.Android.ApacheLicenseForever (Android head, and the
               Android target framework of shared libraries)
  TFM          net10.0-android36.1, SupportedOSPlatformVersion 33
  XAML         unchanged CodeBrix.Platform XAML, desktop dialect
  Assets       ms-appx:///<path>  ==  Android asset <path>
  Constants    HAS_CODEBRIX_ANDROID / __CODEBRIX_ANDROID__ (no *_SKIA_*)
  Mono.Android global::Android.* inside CodeBrix.Android namespaces
  Head         MainApplication : CodeBrixApplication (CreateApp => new App()),
               MainActivity : CodeBrixActivity (ConfigurationChanges =
               CodeBrixActivity.HandledConfigurationChanges, Material3 theme)
  Deploy       dotnet build -t:Install (Debug), Release APK for side-loading
================================================================================
