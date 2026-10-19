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
SDK 10.0.400 or later for the CBAND build diagnostics (BUILD DIAGNOSTICS below);
an older SDK still builds the app, without them.


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
A new page (Views/NewPage.xaml + .xaml.cs) must be listed in the shared
MyApp.UI.projitems, as the existing pages are (a <Page> item for the .xaml and
a <Compile> item for its code-behind); a page that is only on disk is not
built into the Android head.

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
              LaunchMode = LaunchMode.SingleTask,
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
  * LaunchMode = LaunchMode.SingleTask (using Android.Content.PM) is
    required: the app has ONE XAML window, shown by one activity. A repeated
    launch - the launcher icon after the app was started another way (an
    `adb shell am start -n`, a deep link, a notification), another deep
    link, another notification tap - then reaches the running activity
    instead of creating a second one. Its intent arrives in
    CodeBrixApplication.OnLaunchIntent(Intent) (override it in
    MainApplication; main thread; the default does nothing), and it is the
    activity's Intent from then on. The intent that started the app is not
    repeated there: read it from the main activity's Intent (for example
    before base.OnCreate, as the samples read their start page). A repeated
    launch can be the launcher's plain MAIN intent - look at the action,
    data and extras. A launch that reaches the running app after Back
    finished its activity (API 36 and later; see below) arrives there too,
    once the window shows in the new activity; an activity Android
    re-creates on its own does not repeat its intent. A MainActivity that
    overrides OnNewIntent must call the base method.
    An activity that is not single-task still never shows a blank second
    window: when Android creates a second instance while the first shows the
    window, the host logs one warning naming this requirement, hands the
    second instance's intent to OnLaunchIntent, brings the running activity
    to the front and finishes the second instance (its IsFinishing is true
    when base.OnCreate returns - code after base.OnCreate in an override
    should return early then).
    Backing out of the app (Back on its first page) does not close the XAML
    window: Window.Closed is raised only for the app's own Window.Close. A
    later launch of the still-running app shows the same window with its
    page and state, on every Android version. After the app's own
    Window.Close, a later launch of the still-running process activates the
    same window again (OnLaunched does not run again).
  * Content is laid out edge to edge; Window.Bounds is the whole Android
    window and XamlRoot / the window's visible bounds exclude the system bars
    and display cutout. The strips behind the status and navigation bars (and
    anything your page leaves transparent) show the page background of the
    theme your page shows - ApplicationPageBackgroundThemeBrush under the
    RequestedTheme of the page's top element, so a dark page in a light app
    gets dark strips with light icons, as its own background would be.
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
    A custom text control (AdvancedTextEdit, TerminalView) follows the text
    box's rule: the soft keyboard comes up when a finger or pen presses the
    control, not when it gets the focus. Focus given any other way (a Focus()
    call, keyboard focus, the focus Core gives a page's first focusable
    control) keeps the control focused and silent - it hears every key at
    once, and the screen stays clear. After the user dismisses the keyboard
    the control keeps the focus and the next tap brings the keyboard back. To
    open the keyboard without a tap (for example once a page has loaded), call
    InputPane.GetForCurrentView().TryShow(); TryHide() closes it. With a
    hardware keyboard, typing works straight away, and Android's own setting
    decides whether a tap also shows the on-screen keyboard.
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
    StorageFile's streams. Launcher.LaunchUriAsync(new Uri(file.Path)) opens
    the document itself (another app cannot open a file:// path in your app's
    storage, so the launcher views the document's content URI, or for any
    other file of yours a copy shared through the CodeBrix FileProvider, with
    read permission granted). A picked folder's Path is empty: a picked folder
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
    car, watch and VR-headset devices report those forms.
    AnalyticsInfo.VersionInfo.DeviceFamilyVersion encodes the Android release
    the WinUI way (major in the top 16 bits, then minor, then - in the third
    component - the API level, revision 0): decode it with the usual shifts
    and "Android 14, API level 34" is 14.0.34.0; a preview release without a
    number reports "0". UISettings.
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
    CalendarDatePicker are native too (a native CalendarView with no
    SelectedDate marks today, as the Material calendar always marks a day; a
    native Slider is 48 dp tall - Material's minimum touch target - where the
    Fluent Slider is 32 dp: both are accepted native differences). InfoBar, RatingControl, SplitButton and
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
  * Look and adaptivity: by default an app shows the framework's Fluent
    colours, the same look as on the desktop heads. An app that overrides
    CodeBrixApplication.UseMaterialPalette => true looks Material 3 where it
    sets no control keys (tonal default Button, filled AccentButtonStyle,
    Material surface/text/outline roles; dynamic colour where the device
    offers it); every Fluent key an app defines wins either way. FontSize
    follows the user's font scale unless
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
    when a text box is touched, not when a page first shows, and goes when the
    text box (TextBox, PasswordBox, NumberBox, AutoSuggestBox, an editable
    ComboBox, a PagerControl number field, a ColorPicker channel field) that
    has it leaves the page. The diagnostic projection viewer is off by
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
    auto:AutomationProperties.Name="..."; without the prefix the owner type does
    not resolve and the build reports XAML error UXAML0001 at the attribute
    (with the WinUI presentation xmlns as the default it resolves without a
    prefix).

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
XAML file and line that uses them. One of them, CBAND0001, is INFORMATION
rather than a warning (it is not shown by a command-line build; an IDE lists
it): the construct it records is honoured, not ignored. The analyzer is built
against the compiler of SDK 10.0.4xx (Roslyn 5.9): an older SDK's compiler
cannot load it, reports CS8032 and the CBAND warnings are off (the app still
builds):
  CBAND0001  a ControlTemplate on a control Android would otherwise show as a
             native control (information: the template is shown as written)
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
    UseDirectGpuRenderingMode      EXPERIMENTAL (default false): the Android
                                   form of the desktop heads'
                                   UseDirectGpuRenderingMode() host-builder
                                   call - a one-way, app-wide latch
                                   (DirectGpuRenderingMode.IsEnabled), read
                                   once in OnCreate; add-ins that produce GPU
                                   content then present it directly (the
                                   WebGpu add-in: DirectSurface, see ADD-INS
                                   ON ANDROID) and fall back to their
                                   read-back path on their own:
        protected override bool UseDirectGpuRenderingMode => true;
    RestrictToEmbeddedFonts        default false: the Android form of the
                                   desktop heads' RestrictToEmbeddedFonts()
                                   host-builder call - sets the ordinary flag
                                   FeatureConfiguration.Font.
                                   RestrictToEmbeddedFonts in OnCreate, before
                                   any text is measured (not a latch: the app
                                   may also set that flag directly, before the
                                   first text is measured). The TextLayout
                                   add-in's text engine then never looks in
                                   /system/fonts for a glyph the app's fonts
                                   lack (it shows the missing-glyph box).
                                   Text drawn by Android's own text stack (the
                                   native TextBlock / TextBox views) keeps
                                   Android's per-glyph fallback - Android
                                   offers no way to switch it off. A parity-
                                   testing tool: Android's system fonts are
                                   stable, apps do not normally need it:
        protected override bool RestrictToEmbeddedFonts => true;
    UseMaterialPalette             default false (Fluent colours, as on the
                                   desktop heads): true writes the Material 3
                                   colour roles - baseline, or the wallpaper's
                                   dynamic palette where the device offers one
                                   - into the Fluent theme keys the app does
                                   not define, right after the App is created;
                                   keys the app defines always win; a
                                   day/night flip re-derives the roles:
        protected override bool UseMaterialPalette => true;
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


GAMEPADS
--------
A game controller (Bluetooth or USB; Xbox-style, PlayStation-style, any
standard HID gamepad) is read through CodeBrix.Android.UI.Input.Gamepads,
built into this package - no add-in, no native library. Until an app starts
it, a controller behaves as Android makes it behave (its D-pad moves the
focus, its buttons are ordinary keys). Once started, every gamepad button,
stick and trigger event is consumed by the gamepads and reaches nothing else.

    using CodeBrix.Android.UI.Input;

    Gamepads.Start();                       // UI thread; idempotent
    foreach (Gamepad pad in Gamepads.ConnectedGamepads)   // poll each frame
    {
        if (pad.IsPressed(GamepadButtons.A)) { ... }
        GamepadStick left = pad.LeftStick;  // X, Y in -1..1, Y positive UP
        float lt = pad.LeftTrigger;         // 0..1
    }
    Gamepads.GamepadConnected / GamepadDisconnected / GamepadStateChanged
                                            // or listen instead of polling
    Gamepads.Stop();                        // hand the controller back to Android

  Gamepad        Id ("android:<device id>"), DeviceId, Name, Info (vendor and
                 product ids, Android's descriptor, the axis layout),
                 IsConnected, PressedButtons (names), IsPressed(name),
                 LeftStick, RightStick (GamepadStick), LeftTrigger,
                 RightTrigger. A snapshot: read it on the UI thread.
  GamepadButtons The 15 names of the standard layout: A, B, X, Y, Back, Guide,
                 Start, LeftStick, RightStick, LeftShoulder, RightShoulder,
                 DPadUp, DPadDown, DPadLeft, DPadRight - the same strings a
                 CodeBrix game binds against on the desktop heads, so a
                 binding table ports unchanged. The D-pad reads as pressed
                 whether the controller reports it as keys or as the hat
                 axes; a trigger reads 1 while its digital button (L2 / R2)
                 is down on a controller without analogue triggers. Guide is
                 reserved by Android on most phones and tablets and rarely
                 reaches an app.
  Axis layout    Controllers put the right stick on Z/RZ (most) or RX/RY, and
                 the triggers on LTRIGGER/RTRIGGER or BRAKE/GAS. The device's
                 own axis list decides, preferring the first pair of each;
                 each axis's dead zone (Android's "flat") centres small
                 readings. The connection is logged (Information, category
                 CodeBrix.Android.UI.Input.Gamepad) with the name, the vendor
                 and product ids and that layout - the line to read when a
                 controller behaves oddly, and what tells a controller's
                 firmware generation apart (an old firmware shows a
                 different layout and ids from an updated one). The layout
                 also depends on the Android version's key-layout files: one
                 controller on one firmware reports LTRIGGER/RTRIGGER on one
                 device and BRAKE/GAS on another - read a Gamepad's triggers,
                 never the axes.
  VirtualGamepad Gamepads.AddVirtual("name") adds a gamepad the app or a test
                 drives itself (Press / Release / Tap a button name,
                 SetLeftStick, SetRightStick, SetLeftTrigger, SetRightTrigger,
                 ReleaseAll, Disconnect). It is listed like a physical one,
                 whether or not Start() was called, so a PlayTest or a
                 host-free test (net10.0: the gamepad types have no Android
                 dependency) exercises the same state a game reads on a
                 device. GamepadTracker is the state machine itself, for a
                 test that feeds raw Android key codes and axis values
                 (AndroidGamepadCodes).
  Injected input "adb shell input gamepad keyevent KEYCODE_BUTTON_A" (and the
                 other BUTTON_* / DPAD_* codes) reaches a started app as a
                 gamepad with no device behind it (Origin InjectedInput, no
                 axis layout); sticks and triggers cannot be injected that
                 way - script them through a VirtualGamepad.
  Not done       Rumble / haptics on the controller, controller light bars,
                 touchpads, motion sensors, more than the standard layout's
                 15 buttons.


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
    WebGpu                     CodeBrix.Android.WebGpu.ApacheLicenseForever
                               (brings SkiaSharp.Views and CodeBrix.WebGpu.Skia, which
                               brings CodeBrix.WebGpu.Native and the wgpu engine's
                               Android natives)
    WebView                    CodeBrix.Android.WebView.ApacheLicenseForever

What each add-in gives a page, and the namespace to import it from (in XAML
xmlns:x1="using:<namespace>"; in C# a using). "Default" means the framework's
own XAML namespace (no import needed): the add-in only supplies the Android
side of a framework element.

    Add-in            Element(s) / type(s)                Namespace
    AdvancedTextEdit  AdvancedTextEdit                    CodeBrix.Platform.UI.AdvancedTextEdit
    AppSettings       (no element; a settings service)    -
    AudioPlayer       AudioPlayer, MidiPlayer             CodeBrix.Platform.UI.AudioPlayer.Skia
    CommandBar        ToolBar, ToolBarTray, ToolBarGroup, CodeBrix.Platform.UI.CommandBar
                      ToolButton, ToolToggleButton,
                      ToolDropDownButton, ToolBarSeparator,
                      ToolBarSpacer; icons SvgIconSource,
                      RasterIconSource
    FlexPanel         FlexPanel                           CodeBrix.Platform.UI.FlexPanel
    Graphics2DSK      SKCanvasElement (abstract: subclass CodeBrix.Platform.WinUI.Graphics2DSK
                      it, override RenderOverride)
    Graphics3DGL      GLCanvasElement (abstract: see the  CodeBrix.Platform.WinUI.Graphics3DGL
                      recipe below), SkiaGLCanvasElement; (GL: CodeBrix.Platform.OpenGL)
                      the GL type its overrides receive
    Lottie            AnimatedVisualPlayer                Default
                      LottieVisualSource,                 CommunityToolkit.WinUI.Lottie
                      ThemableLottieVisualSource
    MediaPlayer       MediaPlayerElement                  Default
    PlotterView       PlotterControl                      CodeBrix.Platform.UI.PlotterView
    SkiaSharp.Views   SKXamlCanvas                        SkiaSharp.Views.Windows
    Svg               SvgImageSource (the source of an    Default (Microsoft.UI.Xaml.Media.Imaging)
                      Image)
    TerminalView      TerminalControl                     CodeBrix.Platform.UI.TerminalView
    TextLayout        (no element) TextLayoutEngine, used CodeBrix.Platform.UI.TextLayout
                      from code; draw its layouts on an
                      SKXamlCanvas
    VideoPlayer       VideoPlayer                         CodeBrix.Platform.UI.VideoPlayer.Skia
    WebGpu            WebGpuRenderView; IWebGpuRenderer,  CodeBrix.Platform.UI.WebGpu
                      WebGpuFrame, WebGpuDeviceInfo,      (WebGPU types: CodeBrix.WebGpu.Native)
                      WebGpuRenderViewHandoff
    WebView           WebView2                            Default

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
  * WebView: a page packaged with the app is NOT opened with an ms-appx:///
    Source (the page fails to load, as on Windows). Map a host name to the
    app folder that holds it and navigate to that host:
        await web.EnsureCoreWebView2Async();
        web.CoreWebView2.SetVirtualHostNameToFolderMapping("app.local",
            "Assets/web", CoreWebView2HostResourceAccessKind.Allow);
        web.Source = new Uri("https://app.local/index.html");
    (CoreWebView2HostResourceAccessKind is in Microsoft.Web.WebView2.Core;
    the folder's files are Content items of the head, so they are the app's
    assets; relative links between the pages work). Messages from the page
    to the app: the page calls window.chrome.webview.postMessage(text) and
    the app receives WebView2.WebMessageReceived. Messages from the app to
    the page: CoreWebView2.PostWebMessageAsString(text) and
    PostWebMessageAsJson(json) reach the page's WebView2 listeners -
    window.chrome.webview.addEventListener('message', e => ...),
    removeEventListener and onmessage - with e.data the string, or the parsed
    JSON value (invalid JSON throws ArgumentException, as WebView2 does). A
    message posted before the page has registered a listener is dropped (as
    on every head), so have the page post "ready" first and reply to it.
    ExecuteScriptAsync remains the way to run script in the page and read its
    result. The page API is the same as on the Linux desktop head.
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
    view. Hardware keys (and adb input) reach it as on the desktop. When a
    finger or pen presses it the soft keyboard opens with a terminal layout (no
    suggestions or autocorrection; the digits row is shown): every key reaches
    InputEmitted at once, the keyboard's Enter is CR and its delete is DEL. A
    finger or pen on the terminal brings a dismissed keyboard back; a mouse
    does not. The keyboard covers the bottom rows of a terminal that fills the
    window (the control is not resized for it).
  * AdvancedTextEdit: the editor works as on the desktop heads (the document,
    highlighting, folding, line numbers, the search panel, completion windows,
    undo/redo, the caret and the drag selection of the editor itself); every
    surface (the text and each margin) is drawn on a native Skia view. Hardware
    keys (and adb input) reach it as on the desktop. When a finger or pen
    presses its text area the soft keyboard opens as a text editor (suggestions,
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
    Subclassing GLCanvasElement (the same on every head):
      - constructor: call base(null) (the window argument is used on WinUI
        only);
      - Init(GL gl): create your programs, buffers and vertex arrays; it may
        run more than once (each later call follows an OnDestroy);
      - RenderOverride(GL gl): draw; the viewport is already set. Leave the
        context as you found it: unbind your vertex array, program and
        buffers (bind 0) and switch off what you switched on (depth test,
        blending), because the framework draws with the same context;
      - OnDestroy(GL gl): delete what Init created;
      - call Invalidate() to draw again (a canvas draws when it loads and
        when it is invalidated).
    The usual GL calls that take a pointer (VertexAttribPointer with
    (void*)0, for example) need <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
    in the project that holds the subclass, and `unsafe` on the method.
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
    embedded:// resource, a file path or a stream instead. AutoPlay="True"
    may be written before or after Source="..." (set after the load has
    completed, it starts the loaded source then, as on the desktop heads).
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
    Android. AutoPlay="True" may be written before or after Source="..." (set
    after the clip has loaded, it starts the loaded clip then).
  * WebGpu: WebGpuRenderView is the CodeBrix.Platform WebGpu add-in's element
    with the SAME public API in the SAME namespace (CodeBrix.Platform.UI.WebGpu;
    in XAML xmlns:webGpu="using:CodeBrix.Platform.UI.WebGpu"), so pages and
    renderers written for the desktop compile and run unchanged:
      - properties FrameDriver (Continuous - the default - OnDemand, Manual),
        TargetFrameRate (default 60; 0 = as fast as possible), IsPaused,
        Renderer (an IWebGpuRenderer, settable from XAML), Stretch, ClearColor,
        DeviceOptions (CodeBrix.WebGpu.Skia's WebGpuSkiaDeviceOptions for the
        view's own device), RequireOwnedDevice; read-only RenderPath,
        IsDeviceReady, DeviceInfo (adapter, backend, enabled and available
        features and limits), PreferredFormat (BGRA8Unorm), PixelScale,
        TextureWidth / TextureHeight;
      - events DeviceReady, Render, Overlay (SkiaSharp HUD over each frame),
        DeviceLost, RenderPathChanged; the renderer is called before the event;
      - BeginFrame() / EndFrame() (Manual: any ONE thread at a time; BeginFrame
        returns null when the view cannot render - skip the frame), Invalidate()
        (OnDemand), GetDeviceHandoff() / GetViewHandoff() and
        WebGpuFrame.GetHandoff() for native (Rust, C, C++) modules;
      - the view is an IWebGpuDeviceSource and an IWebGpuTextureTarget, so
        CodeBrix.WebGpu.Graphics (your app's own optional reference, never the
        add-in's) draws straight into it.
    The CodeBrix.Platform package's AGENT-README (CodeBrix.Platform.WebGpu.
    ApacheLicenseForever) is the full reference - the lifetime rules, the
    native-module hand-off's C declaration and the module contract apply here
    as written. The view hand-off is the same 40-byte layout:
        typedef struct WGPUCodebrixRenderViewHandoff {
            uint32_t size;                                             /* 40 */
            uint32_t version;                                          /* 1  */
            void* context;                                             /* offset 8  */
            WGPUCodebrixFrameHandoff* (*begin_frame)(void* context);   /* offset 16 */
            void (*end_frame)(void* context);                          /* offset 24 */
            WGPUCodebrixDeviceHandoff const * device;                  /* offset 32 */
        } WGPUCodebrixRenderViewHandoff;
    What is Android-specific:
      - The view owns its WebGPU device, a Vulkan device (wgpu through
        CodeBrix.WebGpu.Skia). The engine's native library comes with
        CodeBrix.WebGpu.Native for android-arm64 and android-x64 and is packed
        into the APK's lib/<abi>/ folder by the build; the binding loads it
        from there - no code of yours is needed. Those natives require API 33,
        which is this package's minimum (SupportedOSPlatformVersion 33): the
        add-in adds no requirement of its own. An app that limits its
        RuntimeIdentifiers must keep the ABI of each target device.
      - RenderPath is Readback by default: each finished frame is read back
        once and drawn by a native Skia view (the SkiaSharp.Views add-in's
        canvas), so the view clips, overlaps and moves like any other element.
        Under the Continuous driver the copy is double-buffered: a frame's
        read-back is begun when it ends and completed when the NEXT frame
        ends, so the copy overlaps the next frame's recording and the picture
        on screen is ONE FRAME BEHIND the newest frame. OnDemand, Manual and
        native-module frames (and every frame ended through the public
        EndFrame) are read back and presented before EndFrame returns - one
        picture per frame, none left waiting. The frame in flight is presented
        when continuous ticking stops: FrameDriver leaves Continuous, IsPaused
        becomes true, or the view is unloaded. A frame still in flight when the
        view resizes or loses its device is dropped (it is never shown after a
        newer picture). The DirectSurface path (below) is unchanged: it
        presents every frame inside its own EndFrame on every driver.
      - Readback costs one GPU-to-processor copy of the whole texture per
        frame, which grows with the view's pixel size: on some phone GPUs a
        full-screen view at a high-density display's resolution renders only a
        few frames a second. Size the view to what it shows, use a lower
        TargetFrameRate / OnDemand, or opt in to the direct path.
      - DIRECT PATH (EXPERIMENTAL): an application whose MainApplication
        overrides CodeBrixApplication.UseDirectGpuRenderingMode => true gets
        RenderPath DirectSurface. The view's native view then hosts a
        SurfaceView UNDER its Skia canvas; each frame still renders into the
        same BGRA8Unorm texture the size of the element on the view's OWN
        device (so the frame protocol, the events, Renderer, DeviceOptions,
        HasUncapturedErrorHandler = true and the native-module hand-off are
        exactly Readback's), and is then drawn onto the SurfaceView's window
        surface by one GPU pass and presented (Fifo: synchronised with the
        display). Nothing is read back; the operating system composites the
        surface with the window.
          * Overlay: drawn by the canvas ABOVE the surface (over a transparent
            background) and composited by the OS; it is repainted after each
            presented frame while an Overlay handler or a Renderer is set.
          * The surface punches through the window: content BEHIND the view
            does not show through a transparent ClearColor (the surface is
            opaque), while elements drawn after the view stay above it.
          * Lifecycle: Android creates the window when the view is shown,
            changes it on resize / rotation (the next frame reconfigures) and
            destroys it when the activity goes to the background or the view
            leaves the window - the view then renders no frames (BeginFrame
            returns null, no DeviceLost) and continues on the same device when
            the window comes back. The WebGPU surface is released under the
            frame lock before Android's window is given back.
          * Fallback: anything that fails on the direct path (no window surface
            within 2 s of the view being loaded, a surface format or
            configuration the window does not offer, a frame that cannot be
            acquired) writes one log line, raises DeviceLost, removes the
            SurfaceView and continues on Readback; the direct path is tried
            again after the next Loaded. RequireOwnedDevice = true keeps the
            view on Readback with the latch on.
          * WebGpuRenderView.Surface captures (tests) hold no picture on this
            path; read the SurfaceView with PixelCopy instead.
      - The built-in drivers run on the main (user-interface) thread; the
        Overlay runs inside the native view's draw, also on the main thread,
        and must never touch WebGPU objects (the rule is the same on every
        head). The device is created on the first frame after the view is
        loaded and released when it is unloaded (DeviceLost "The view was
        unloaded."), so an activity that goes to the background keeps it only
        while the view stays in the visual tree.


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
    native views, as on the desktop heads, but the control gives up the
    Material widget's look, ripple and accessibility. That is a legitimate
    choice for an app that draws its own look (CBAND0001 records it as
    information, not a warning). To get the native control instead, leave the
    Template of Button, CheckBox, TextBox, ... unset and restyle it with
    properties and the Fluent lightweight-styling resource keys.


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
            samples/WebGpuDemo: the WebGpu add-in's four demo pages (the raw
            binding in Render, glTF models through CodeBrix.WebGpu.Graphics,
            an IWebGpuRenderer from XAML, frame drivers and a SkiaSharp HUD).


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
               CodeBrixActivity.HandledConfigurationChanges, LaunchMode =
               LaunchMode.SingleTask, Material3 theme); later launches arrive
               in CodeBrixApplication.OnLaunchIntent(Intent)
  Gamepads     CodeBrix.Android.UI.Input.Gamepads.Start(); poll
               ConnectedGamepads (GamepadButtons names, sticks Y-up, triggers
               0..1); AddVirtual() for tests and PlayTests (see GAMEPADS)
  Deploy       dotnet build -t:Install (Debug), Release APK for side-loading
  Warnings     CBANDnnnn: accepted-but-ignored constructs, never errors
               (see BUILD DIAGNOSTICS)
================================================================================
