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

Add-ins: each CodeBrix.Platform add-in has its own Android package, named after
the add-in's CodeBrix.Platform package (see ADD-INS ON ANDROID for the list).
Reference it from the Android target framework instead of the add-in's
CodeBrix.Platform package:
    dotnet add package CodeBrix.Android.Svg.ApacheLicenseForever
Every CodeBrix.Android package of one release depends on the others of the
same release at exactly that version; keep them on one version.

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
  * The soft keyboard: CodeBrixApplication.SoftInputAdjust (the enum
    CodeBrix.Android.UI.Hosting.SoftInputAdjust) says how the window makes
    room for it: Pan (the default) pans the window so the focused text field
    stays visible, the page keeps its size; Resize lays the page out again
    above the keyboard (popups keep the whole window); Unspecified leaves it
    to Android. Resize is CodeBrix.Platform's own layout, not Android's: the
    keyboard's height is withheld from the bottom of the window's content
    (the root's content bottom occlusion inset, the mechanism the Platform's
    own on-screen keyboard uses), so the page's bottom edge sits on the
    keyboard's top edge. Set it in the MainApplication constructor or
    OnCreate (SoftInputAdjust = SoftInputAdjust.Resize;); setting it while
    the app runs re-applies it to every activity. Until it is set, an
    activity that declares its own [Activity(WindowSoftInputMode =
    ...Adjust...)] keeps that. InputPane.OccludedRect reports the keyboard
    in every mode.
    A custom text control (AdvancedTextEdit, TerminalView) opens the keyboard
    whenever it gets the focus, programmatic focus included - also the focus
    Core gives the first focusable control when a page appears, so on a phone
    a page whose first focusable control is a terminal or an editor can open
    with the keyboard up (a text box does not: it opens the keyboard only
    when it is touched).
    In Pan mode a custom text control (AdvancedTextEdit, TerminalView) is
    panned by its CARET too (a terminal by its cursor cell, while the hosted
    program shows the cursor); Resize remains the choice when the whole
    control should stay above the keyboard.
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
    StorageFile's streams. A picked folder's Path is empty: a picked folder
    has no local path on Android, so desktop code that hands folder.Path to
    System.IO finds nothing there - use the StorageFolder's own members
    (GetFilesAsync, GetFolderAsync, CreateFileAsync, ...) instead.
  * AnalyticsInfo.VersionInfo.DeviceFamily is "Android.<form>", where the
    form comes from the window size class: "Android.Mobile" (compact width),
    "Android.Tablet" (medium) or "Android.Desktop" (expanded - a Googlebook,
    or a phone docked in desktop mode). AnalyticsInfo.DeviceForm ("Mobile",
    "Tablet", "Desktop") is read from the current window every time you ask,
    so it follows a phone that is docked or undocked; DeviceFamily is read
    the same way, so it too names the form of the window at the moment you
    ask (read it again after a resize instead of caching it). Television,
    car, watch and VR-headset devices report those forms. UISettings.
    AnimationsEnabled follows the system's "remove animations" setting.
  * CompositionTarget.Rendering is raised once per display frame (from the
    display's frame callbacks) for as long as anything subscribes to it -
    Storyboards, a TeachingTip opening, your own per-frame code - and stops
    when the last handler unsubscribes, so an idle app costs no frames.
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
    AutoSuggestBox, Image, lists and tabs); a control with its own
    ControlTemplate (a local Template, or a Style that sets one) is not a
    native widget: its template is expanded and shown with native views, as
    it is written. Shapes draw and hit-test natively as WinUI does (caps,
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
    ListBoxItem and GroupItem are Core controls on their templates (a grouped
    ListView/GridView shows its group headers). The list / picker flyout
    presenters, the rich-text family (RichTextBlock, RichEditBox, Paragraph, ...), Hub, SemanticZoom,
    ParallaxView, AnnotatedScrollBar, MapControl, the swap-chain panels and the
    legacy WebView are not implemented by the Platform on any head.
  * Look and adaptivity: an app that sets no control keys looks Material 3
    (tonal default Button, filled AccentButtonStyle, Material surface/text/
    outline roles; dynamic colour where the device offers it); every Fluent key
    an app defines wins. FontSize follows the user's font scale unless
    IsTextScaleFactorEnabled=false. An Auto NavigationView becomes a bottom bar
    / rail / drawer by window size class and switches live; a TriPaneView
    shows one / two / three panes the same way (see ADD-INS ON ANDROID).
    Storyboards run.
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
  * Accessibility / automation names: AutomationProperties.Name is the one to
    set. On Android it becomes the content description of the element's native
    view - what TalkBack speaks and what UI Automator matches as the content
    description (content-desc, By.desc) - and for a control whose view hosts
    the interactive widget (Button, TextBox, a Material widget drawn over a
    template) the widget gets it. It is applied when the element appears and
    follows every later change (binding, style, code); a cleared or blank name
    removes it, so TalkBack reads the view's own text again (a control that
    labels its widget itself, such as RatingControl or PersonPicture, shows its
    own label again). Leading and trailing spaces are trimmed.
    AutomationProperties.AutomationId becomes the view's tag (Android has no
    resource id an app can set at run time; UI Automator does not read tags).
    The other AutomationProperties are accepted and not mapped. In a page whose
    default xmlns is the clr-namespace form (as in the application template),
    declare xmlns:auto="using:Microsoft.UI.Xaml.Automation" and write
    auto:AutomationProperties.Name="..." (the owner type must resolve; with the
    WinUI presentation xmlns as the default it resolves without a prefix).

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
    SDK's default "Assets/** without the Assets/ prefix" item is turned off,
    and its assets prefix is set to one no ms-appx path starts with, for this
    reason.)
    Core reads those assets for every ms-appx:/// URI (StorageFile.
    GetFileFromApplicationUriAsync returns a read-only package file; images,
    Lottie documents, font manifests and RandomAccessStreamReference too).
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
  CodeBrixAndroidXamlScan                   default true (false: no CBAND warnings
                                            for XAML, see below)

BUILD DIAGNOSTICS (CBAND)
-------------------------
The package's analyzer reports, as WARNINGS that never fail a build (they are
kept out of TreatWarningsAsErrors), the CodeBrix.Platform constructs Android
accepts but does not show as they look on the desktop, at the C# line or the
XAML file and line that uses them:
  CBAND0001  a ControlTemplate on a control Android shows as a native control
  CBAND0002  template members (OnApplyTemplate, GetTemplateChild,
             VisualStateManager.GoToState) used on such a control
  CBAND0003  composition APIs, system backdrops, ThemeShadow (ignored)
  CBAND0004  3-D projections and 3-D transforms (ignored)
  CBAND0005  ScrollViewer zoom (ignored)
  CBAND0006  a PasswordChar that is not a single character
  CBAND0007  frame-buffer head options (no effect)
  CBAND0008  acrylic and Mica materials (a solid fallback colour)
Silence an id with NoWarn as usual (or #pragma warning disable in C#).

SWITCHES AND SEAMS AN APP MAY USE
---------------------------------
The package registers the Android implementation of every CodeBrix.Platform
platform contract itself (application data, globalization, imaging, device
family and display information, the app-package files behind ms-appx:///,
clipboard, launcher, sharing, connectivity, haptics, the pickers, ...) before
any XAML type is used. An app never registers one of those contracts (a
second registration throws) and cannot register native element handlers
(see WHAT THIS PACKAGE DOES NOT DO). What an app MAY set or override:

  CodeBrixApplication (the MainApplication base class):
    CreateApp()                    required: return new App()
    SoftInputAdjust                Pan (default) / Resize / Unspecified - the
                                   soft keyboard, see above
    ConfigureLogging(builder)      log levels and extra providers
    LogTag                         the logcat tag (default: package name)
    LogVisualTreeAfterLayout       diagnostic tree dump (default false)
    UseProjectionViewer            diagnostic overlay (default false)
  App constructor (as on the desktop heads):
    FeatureConfiguration.Font.DefaultTextFontFamily = the app's default font
    file (see Fonts above).
  AppContext switches (in the head's csproj, read once at start-up):
      <ItemGroup>
        <RuntimeHostConfigurationOption Include="CodeBrix.Android.UI.NativeComboBox"
                                        Value="true" />
      </ItemGroup>
    CodeBrix.Android.UI.NativeComboBox       default false: a ComboBox keeps
                                             its Fluent template and Core's
                                             drop-down; true shows the Material
                                             exposed drop-down menu instead
    CodeBrix.Android.UI.NativeExpander       default true (false: Expander
                                             keeps its Fluent template)
    CodeBrix.Android.UI.NativeCommandBar     default true (false: CommandBar
                                             keeps its Fluent template)
    CodeBrix.Android.UI.AdaptiveTriPaneView  default true (false: TriPaneView
                                             does not adapt to the window width)
  Audio without the add-ins: an app that uses CodeBrix.Audio or
    CodeBrix.VideoPlayback directly (not through the AudioPlayer / VideoPlayer
    add-ins, which do this for you) references
    CodeBrix.Audio.Android.ApacheLicenseForever (CodeBrix.Audio's Android
    backend, same external API) in its Android head and initialises it once,
    in MainApplication.OnCreate after base.OnCreate():
        CodeBrix.Audio.Android.CodeBrixAndroidAudio.Initialize(this);
    (it is idempotent). Do not reference the desktop
    CodeBrix.Audio.MitLicenseForever package from the Android head.
  Codec opt-ins: AV1 (CodeBrix.VideoPlayback.Dav1d.BsdLicenseForever) and Opus
    (CodeBrix.Audio.Opus.BsdLicenseForever) are referenced and their
    Register() methods called by the app, exactly as on the desktop heads.


ADD-INS ON ANDROID
------------------
Each add-in's Android package holds the add-in's platform-neutral assembly and
its Android assembly, and depends on the framework package (and on the add-in
packages it builds on). Application code and XAML written against the add-in
compile and run unchanged. The add-in packages:

    CodeBrix.Platform add-in   Android package
    AdvancedTextEdit           CodeBrix.Android.AdvancedTextEdit.ApacheLicenseForever
                               (brings TextLayout and SkiaSharp.Views)
    AppSettings                CodeBrix.Android.AppSettings.ApacheLicenseForever
    AudioPlayer                CodeBrix.Android.AudioPlayer.ApacheLicenseForever
                               (brings CodeBrix.Audio.Android, CodeBrix.Audio's Android
                               backend)
    CommandBar                 CodeBrix.Android.CommandBar.ApacheLicenseForever
                               (brings Svg and SkiaSharp.Views)
    FlexPanel                  CodeBrix.Android.FlexPanel.ApacheLicenseForever
    Graphics2DSK               CodeBrix.Android.Graphics2DSK.ApacheLicenseForever
                               (brings SkiaSharp.Views)
    Graphics3DGL               CodeBrix.Android.Graphics3DGL.ApacheLicenseForever
    Lottie                     CodeBrix.Android.Lottie.ApacheLicenseForever
                               (brings SkiaSharp.Views)
    MediaPlayer                CodeBrix.Android.MediaPlayer.ApacheLicenseForever
    PlotterView                CodeBrix.Android.PlotterView.ApacheLicenseForever
                               (brings TextLayout, SkiaSharp.Views and CodeBrix.Plotter)
    SkiaSharp.Views            CodeBrix.Android.SkiaSharp.Views.ApacheLicenseForever
    Svg                        CodeBrix.Android.Svg.ApacheLicenseForever
                               (brings SkiaSharp.Views)
    TerminalView               CodeBrix.Android.TerminalView.ApacheLicenseForever
                               (brings TextLayout and SkiaSharp.Views)
    TextLayout                 CodeBrix.Android.TextLayout.ApacheLicenseForever
    VideoPlayer                CodeBrix.Android.VideoPlayer.ApacheLicenseForever
                               (brings SkiaSharp.Views, Graphics3DGL, CodeBrix.VideoPlayback
                               and CodeBrix.Audio.Android)
    WebView                    CodeBrix.Android.WebView.ApacheLicenseForever

The TriPaneView of CodeBrix.Platform's Toolkit is part of the framework package.
What differs on Android:
  * FlexPanel: the panel is laid out by its Core exactly as on the desktop
    heads and shown by the framework's panel handler (nothing Android-specific).
  * Svg: SVG images are parsed and drawn by the add-in's Core (CodeBrix.
    SkiaSvg) on a native Skia view, as on every head.
  * Graphics2DSK: SKCanvasElement's RenderOverride runs on each draw of a
    native Skia view (one canvas unit = one DIP, clipped to the element).
  * CommandBar: the add-in's tool bar controls (ToolBar, ToolBarTray, ...)
    keep their templates and behave as on the desktop heads; their SVG icons
    are rasterized at the icon size and tinted by the Svg add-in. (The
    framework's own CommandBar control is a separate, native Material app
    bar - see Display above.)
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
    Shaping (HarfBuzz), bidi and line breaking (the device's ICU, found by the
    engine on its first layout; Android 13+ always has it) run on the device
    exactly as on the desktop heads. Draw a layout on an SKXamlCanvas (add the
    SkiaSharp.Views package too); the engine has no element of its own.
  * Lottie: AnimatedVisualPlayer with LottieVisualSource /
    ThemableLottieVisualSource plays as on the desktop heads (the Core decodes
    with Skottie; its frames are ticked by the display's Choreographer); each
    frame is drawn on a native Skia view. Name the document with ms-appx:///
    (an app asset, read from the APK's assets), embedded:// (a resource of your
    assembly), ms-appdata:/// or http(s). The framework's
    ProgressRing does not need this add-in on Android (it is the native
    Material indicator).
  * TriPaneView (framework package, no add-in): the control and its engine
    work as on the desktop heads (weights, minimize/restore, grips, events).
    On top, it follows the window's width size class, live: Compact = one pane
    (the upper one; the dividers become restore grips and a tap on a grip
    switches panes), Medium = the side pane and one stacked pane, Expanded =
    three panes. The form is only what is DISPLAYED: a pane the window has no
    room for is shown minimized with its restore grip, but your percent
    properties and IsMinimized flags are never written (they keep your values,
    and DividerDragCompleted is not raised for a form change), so your layout is
    back exactly as it was when the window widens. With RestoreGripMode Never
    there are no grips, so your code must switch panes. A finger within 48 dp of
    a divider drags it and raises the divider's own DragStarted/DragDelta/
    DragCompleted events, as a mouse does; a mouse uses the divider exactly as
    on the desktop. Turn the adaptive form off with
    the AppContext switch CodeBrix.Android.UI.AdaptiveTriPaneView = false.
  * TerminalView: TerminalControl works as on the desktop heads (feed, grid
    fitting and GridResized, colours, fonts, scrollback and its scroll bar,
    Shift+PageUp/PageDown, the finger or mouse drag selection, Ctrl+Shift+C/V,
    the right-click menu, TitleChanged); the grid is painted on a native Skia
    view. Hardware keys (and adb input) reach it as on the desktop. When it
    gets the focus the soft keyboard opens with a terminal layout (no
    suggestions or autocorrection; the digits row is shown): every key reaches
    InputEmitted at once, the keyboard's Enter is CR and its delete is DEL. A
    finger or pen on the terminal brings a dismissed keyboard back; a mouse
    does not. The keyboard covers the bottom rows of a terminal that fills the
    window (the control is not resized for it).
  * AdvancedTextEdit: the editor works as on the desktop heads (the document,
    highlighting, folding, line numbers, the search panel, completion windows,
    undo/redo, the caret and the drag selection of the editor itself); every
    surface (the text and each margin) is drawn on a native Skia view. Hardware
    keys (and adb input) reach it as on the desktop. When its text area gets
    the focus the soft keyboard opens as a text editor (suggestions,
    autocorrection, Enter is a line break) that SEES the document: it reads the
    text around the caret, composes a word in place (underlined until it is
    committed), replaces a word it corrects, deletes around the caret and moves
    the selection; what it types goes through the editor's own typing path, so
    TextEntering/TextEntered (and a completion window opened on them) behave as
    for a hardware key. A read-only editor opens no keyboard. The keyboard
    covers the part of the editor below its top edge (the control is not
    resized for it). There are no native selection handles: a finger drag on
    the text selects, as a mouse drag does.
  * Graphics3DGL: GLCanvasElement renders on an OpenGL ES 3.0 context, so
    shaders must be GLSL ES (`#version 300 es` plus a precision statement);
    desktop GLSL (`#version 330 core`) does not compile. SkiaGLCanvasElement,
    OffscreenGLContext and SkiaGpuContext are available (OpenGL ES backend).
  * PlotterView: PlotterControl works as on the desktop heads (the model, the
    controller and its bindings, the tracker, the zoom rectangle, keys, the
    mouse and its wheel); the chart is painted on a native Skia view, with the
    typefaces of the TextLayout add-in's font source. Fingers use the chart
    engine's default touch binding: one finger pans, two fingers pinch-zoom,
    a finger held on a series shows the tracker.
  * AudioPlayer: AudioPlayer, SoundEffect and MidiPlayer work as on the
    desktop heads (the transport and its bindings, sound-effect voices, MIDI
    through SoundFont / SFZ / Decent Sampler instruments), playing through
    CodeBrix.Audio.Android, which the add-in initialises at start-up (do not
    reference the desktop CodeBrix.Audio.MitLicenseForever package from the
    Android head; codec and synthesizer add-ons such as CodeBrix.Audio.Opus
    are referenced and registered exactly as on the desktop). ms-appx:///
    sources are the app's assets: an asset is copied out of the APK on first
    use (once per installed build), with its folder for an SFZ or Decent
    Sampler preset whose samples sit beside it. SoundEffect reads an
    ms-appx:/// source by path before anything copies it out, so give it an
    embedded:// resource, a file path or a stream instead.
  * VideoPlayer: the VideoPlayer element works as on the desktop heads (WebM
    and .cbv Mode 1 / Mode 2 clips, the transport and its bindings, Stretch,
    render paths, effects, layers, captions and chapters); the picture is
    drawn on a native Skia view and composed on the GPU through an OpenGL ES
    context when one can be made (ActiveRenderPath says which), and the sound
    plays through CodeBrix.Audio.Android. AV1 and Opus remain the
    application's own opt-ins (reference CodeBrix.VideoPlayback.Dav1d.
    BsdLicenseForever and CodeBrix.Audio.Opus.BsdLicenseForever and call their
    Register() methods; dav1d ships android-arm64 and android-x64 natives).
    ms-appx:/// clips are the app's assets, copied out of the APK before they
    are opened. CodeBrix.VideoPlayback.Authoring (encoding) is not supported on
    Android.


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
    An app file reaches ms-appx:/// only as a Content item (from the shared
    project, the head or a referenced library, as on the desktop heads, e.g.
    <Content Include="Assets\**" />); a file that is not a Content item is
    not packaged.
  * A ControlTemplate (or a Style that sets Template) on a control takes that
    control off its native widget: the template is shown as written, with
    native views, but the control loses the Material widget's look, ripple and
    accessibility. Leave the Template of Button, CheckBox, TextBox, ... unset
    to get the native control (CBAND0001 points such templates out); restyle
    it with properties and the Fluent lightweight-styling resource keys.


WHAT THIS PACKAGE DOES NOT DO
=============================

  * It does not draw CodeBrix.Platform controls itself; the look is Material
    3 as Material Components for Android provides it. Pixel-identical output
    with the desktop heads is not a goal.
  * It does not support iOS (see CodeBrix.Mobile) or Android versions below
    API 33.
  * It does not use the CodeBrix.Platform desktop head or runtime packages.
  * It has no Android package for a CodeBrix.Platform add-in that is not in
    the list under ADD-INS ON ANDROID (the list is complete). Not supported
    on Android: camera capture, the GameEngine canvas, TkCanvas,
    CodeBrix.VideoPlayback.Authoring (encoding), and hardware-device drivers
    that have no Android build (an app that talks to such a device through a
    desktop driver library can run only its non-hardware paths, such as a
    simulated source).
  * It does not let an app register its own native element handlers: the
    handler-authoring API is internal.
  * The CodeBrix.Platform types that are not implemented on any head are not
    implemented here either (the list / picker flyout presenters, the rich-text family, Hub,
    SemanticZoom, MapControl, the swap-chain panels, ...).


WORKING EXAMPLES ON GITHUB
==========================

  Samples:  https://github.com/ellisnet/CodeBrix.Android/tree/main/samples
            samples/HelloPaste: two CodeBrix.Samples pages pasted unchanged,
            with multi-targeted .Core libraries and an Android head.


QUICK REFERENCE CARD
====================

  Package      CodeBrix.Android.ApacheLicenseForever (Android head, and the
               Android target framework of shared libraries)
  Add-ins      CodeBrix.Android.<AddIn>.ApacheLicenseForever, one version with
               the framework package
  TFM          net10.0-android36.1, SupportedOSPlatformVersion 33
  XAML         unchanged CodeBrix.Platform XAML, desktop dialect
  Assets       ms-appx:///<path>  ==  Android asset <path>
  Constants    HAS_CODEBRIX_ANDROID / __CODEBRIX_ANDROID__ (no *_SKIA_*)
  Mono.Android global::Android.* inside CodeBrix.Android namespaces
  Head         MainApplication : CodeBrixApplication (CreateApp => new App()),
               MainActivity : CodeBrixActivity (ConfigurationChanges =
               CodeBrixActivity.HandledConfigurationChanges, Material3 theme)
  Deploy       dotnet build -t:Install (Debug), Release APK for side-loading
  Warnings     CBANDnnnn: accepted-but-ignored constructs, never errors
               (see BUILD DIAGNOSTICS)
================================================================================
