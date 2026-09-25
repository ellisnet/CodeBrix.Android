================================================================================
MAINTAINER-README: CodeBrix.Android
Notes for people and agents MAINTAINING this repository - not for package
consumers
================================================================================

If you are CONSUMING the NuGet package, stop reading and open AGENT-README.txt
instead. Everything below is about the repository itself: how it is laid out,
how it builds, how it is tested, how it is packaged, and the conventions the
source follows.


PURPOSE AND SCOPE
=================
CodeBrix.Android runs the unchanged CodeBrix.Platform object model (the
platform-neutral *.Core.dll assemblies of CodeBrix.Platform) on Android and
gives every element a native Android / Material Components view.

    PackageId:  CodeBrix.Android.ApacheLicenseForever (+ one
                CodeBrix.Android.<AddIn>.ApacheLicenseForever per add-in)
    License:    Apache License 2.0
    Consumer documentation: AGENT-README.txt (repo root)

Two hard constraints shape the build:
  1. No CodeBrix.Android package declares a dependency on any package the
     CodeBrix.Platform repository produces (build/platform-repo-package-ids.txt).
     The Core assemblies are extracted from a pinned Platform build and
     re-shipped inside the CodeBrix.Android packages.
  2. The Android build never reads the CodeBrix.Platform repository. It reads
     Platform packages from nuget.org, or from a local folder of .nupkg files
     while the pinned build is unpublished.


REPOSITORY LAYOUT
=================
    CodeBrix.Android.slnx       the solution. Solution Items: .gitignore,
                                AGENT-README.txt, Directory.Build.props,
                                Directory.Packages.props, EXTRAS-README.txt,
                                global.json, icon-codebrix-128.png, LICENSE,
                                MAINTAINER-README.txt, README-INDEX.txt,
                                README.md, THIRD-PARTY-NOTICES.txt. Folders:
                                Build (the intake), Build/inrepo (in-repo
                                consumer wiring), Build/nuget (consumer
                                build logic), Build/test-scripts, Samples,
                                Tests.
    global.json                 selects the Microsoft.Testing.Platform test
                                runner. Does NOT pin an SDK version.
    Directory.Build.props       family settings (NRT off, no implicit usings,
                                TreatWarningsAsErrors, Deterministic), imports
                                build/PlatformPin.props, defines
                                CodeBrixPlatformIntakeDir / ...IntakeLibDir and
                                CodeBrixAndroidMinimumApiLevel (33), and
                                documents the global::Android convention.
    Directory.Packages.props    Central Package Management: every package
                                version in one reviewed file.
    build/
      PlatformPin.props         CodeBrixPlatformVersion - the ONLY place the
                                pinned Platform build is named.
      platform-repo-package-ids.txt
                                the package ids the Platform repo produces
                                (input of the package dependency gate).
      intake/                   the intake (see INTAKE).
      nuget/buildTransitive/    CodeBrix.Android.ApacheLicenseForever.props /
                                .targets: the consumer build logic shipped in
                                the package (see CONSUMER BUILD LOGIC).
      inrepo/                   CodeBrix.Android.InRepo.props / .targets: make
                                an in-repo app or app library consume the repo
                                exactly as a package consumer would (see
                                IN-REPO APPS).
      test-scripts/             paste-always-compile.sh, device-smoke.sh (see
                                TESTING).
    src/                        the CodeBrix.Android assemblies (one folder per
                                assembly; sub-folders per area; see SOURCE
                                LAYOUT). src/Directory.Build.props adds the
                                Core references, the intake build-order
                                reference and XML doc generation.
    tests/                      test projects; tests/PasteAlways/ holds the
                                paste-always compile heads (not in the slnx).
    samples/                    HelloPaste (in the slnx); SimpleDebugApp_API_36/
                                _37 are standalone (see EXTRAS-README.txt).
    artifacts/                  build output of the intake, the paste-always
                                and device-smoke logs (git-ignored).

The two standalone samples are excluded from Directory.Build.props and
Directory.Packages.props by a path test at the top of Directory.Build.props
(_CodeBrixAndroidStandaloneSample); keep it that way.


BUILDING
========
    dotnet build CodeBrix.Android.slnx -c Debug
    dotnet build CodeBrix.Android.slnx -c Release

Both must end with 0 Warning(s) and 0 Error(s). The solution build runs the
intake first (every project that consumes the extracted assemblies references
build/intake/CodeBrix.Android.Intake.proj for build order).

Android projects target net10.0-android36.1 with SupportedOSPlatformVersion 33
($(CodeBrixAndroidMinimumApiLevel)); portable parts and tests target net10.0;
the analyzer project (when it exists) netstandard2.0.


INTAKE
======
    dotnet build build/intake/CodeBrix.Android.Intake.proj

What it does:
  1. <PackageDownload> of the framework package and every add-in package of
     the pinned build (heads are not taken) into the isolated folder
     artifacts/intake/.packages/ - no package dependency is recorded, and a
     same-version copy in the global NuGet cache is never used.
     Sources: nuget.org, plus the folder feed named by the CODEBRIX_PLATFORM_FEED
     environment variable (MSBuild property CodeBrixPlatformFeed; default
     ~/ClaudeHome/android-feed/<version>/) when that folder exists. The feed is
     an immutable copy of a Platform local feed; never write into it.
     A package already in artifacts/intake/.packages/ is compared (SHA-256) with
     the feed's copy of the same id and version: a version re-packed with
     different content (SkiaSharp.Views keeps its literal version across
     Platform builds) fails the build with CBAI0011 instead of being extracted
     silently - delete that package's folder and build again.
  2. Extraction to artifacts/intake/<version>/:
       lib/                   every *.Core.dll (+ .xml, .pdb) of every package,
                              and CodeBrix.Platform.Xaml.dll (+ .pdb)
       analyzers/dotnet/cs/   the XAML source generator, analyzers, parser
       buildTransitive/       Platform.UI.SourceGenerators.props,
                              Platform.UI.Tasks.targets,
                              platform.ui.tasks.assets.targets,
                              CodeBrix.Platform.UI.Tasks/, Descriptors/
       notices/<package-id>/  THIRD-PARTY-NOTICES.txt of each package that
                              contributes files
     The Skia twins, the Platform head/runtime-replace targets and the
     codebrix-platform-runtime folder are never extracted.
  3. intake-manifest.tsv: per downloaded package its nupkg SHA-256; per file
     its path, source package, SHA-256, size, assembly version and whether the
     assembly is marked trimmable.
  4. The five gates (build/intake/CodeBrix.Android.IntakeGate, which uses
     CodeBrix.AssemblyTools); the report is intake-gates.txt, and any error
     fails the build (codes CBAI0001-CBAI0005):
       1 expected file set       == gates/expected-files.txt exactly
       2 no Skia in Core         no framework *.Core.dll references SkiaSharp*
                                 or HarfBuzzSharp*; add-in Cores only when the
                                 package is in gates/skia-canvas-addins.txt;
                                 no Core references a Skia twin
       3 IVT grants              every row of gates/ivt-grants.txt holds, and
                                 every CodeBrix.Android.* project under src/
                                 has a row
       4 references resolve      every reference of every lib/ assembly is in
                                 the intake, the .NET reference pack, or
                                 gates/allowed-references.txt
       5 seam fingerprint        gates/seam-fingerprint.txt present: each
                                 "Type" / "Type::Member" line in
                                 CodeBrix.Platform.UI.Core, a "[Assembly] ..."
                                 line in that intake assembly (the Foundation,
                                 WinRT and Composition Core contracts). ON since
                                 AP1.8 (-p:CodeBrixIntakeSeamGate=false reports
                                 it only).

ASSEMBLY NAMES ARE FIXED BY THE CORE GRANTS. Core assemblies grant
InternalsVisibleTo to CodeBrix.Android.{Y}, and several Cores find their
platform implementation by loading CodeBrix.Android.{Y} BY NAME and running its
module initializer. A CodeBrix.Android assembly must carry exactly the name its
Core grants; gate 3 enforces the list.

PIN BUMP (moving to a newer Platform build)
  1. Make the new feed folder available (or wait for nuget.org).
  2. Change CodeBrixPlatformVersion (and, if it moved,
     CodeBrixPlatformSkiaSharpViewsVersion) in build/PlatformPin.props.
  3. Run the intake. Review every gate error: new add-in Core splits add
     lib/ and notices/ entries to gates/expected-files.txt; new Core
     references may need gates/allowed-references.txt (only for packages that
     are allowed and not produced by the Platform repo); new add-in grants go
     into gates/ivt-grants.txt; refresh build/platform-repo-package-ids.txt.
  4. Rebuild and test everything, Debug and Release.
  5. Re-run the device gates: paste-always, the HelloPaste smoke (Debug and
     trimmed Release), the full UIReqs suite in both orientations three times
     (frames against the baseline; re-baseline only frames a named Core change
     explains), and move every pending scenario the new build unblocks.


SOURCE LAYOUT
=============
One project per Core InternalsVisibleTo grant (the AssemblyName is the granted
name):
    CodeBrix.Android                 WinRT-surface contracts (application data,
                                     globalization, graphics imaging) and the
                                     WinRT registry extensions (analytics info,
                                     system theme, display information).
    CodeBrix.Android.UI.Dispatching  the Looper dispatcher pump; multi-targets
                                     net10.0 (HostFree/ only: a managed pump the
                                     host-free tests drain).
    CodeBrix.Android.UI.Composition  the INERT composition platform (Core keeps
                                     its Visual tree, nothing paints it) and the
                                     android.graphics.Path composition geometry;
                                     multi-targets net10.0 (the inert platform,
                                     Portable/ and HostFree/ only).
    CodeBrix.Android.UI.Toolkit      IElevationPlatform (stub until handlers).
    CodeBrix.Android.UI              the bootstrap chain, hosting, logging, the
                                     UI contracts, the ELEMENT HANDLERS, the
                                     (diagnostic) projection viewer;
                                     multi-targets net10.0 (Portable/,
                                     Diagnostics/, Handlers/Core/,
                                     Overlay/Portable/ and the lanes' portable
                                     helpers Platform/Recycler/Portable/ and
                                     Handlers/<Family>/Portable/ - pure C#, no
                                     Android type - for host-free tests) and
                                     net10.0-android36.1 (everything).
The net10.0 flavors exist for tests only (nothing ships them in an app); their
HostFree/ registrations are internal and granted to CodeBrix.Android.UI.Tests.
Folders inside a project: Android/ = Android implementations of Core contracts
(`<Concern>AndroidPlatform`, namespace `<root>.Android`, mirroring the Skia
twins' `<root>.Skia`) plus the assembly's AndroidPlatformBootstrap; Portable/ =
pure logic with no Android API (compiled into the net10.0 test assembly, see
TESTING); Hosting/, Logging/, Diagnostics/, Projection/, Handlers/, Platform/, Input/
in CodeBrix.Android.UI.

THE ELEMENT HANDLERS (the Core per-control handler seam, WPH1; plan 2.5 / 2.6).
The platform bootstrap registers CodeBrixHandlers.Factory as Core's
IElementHandlerFactoryPlatform, so every element entering a live tree gets a
handler (parent first) and its native view:
  Handlers/Core/   (portable, host-free tested) ports of MAUI's PropertyMapper,
                   CommandMapper (+ Append/Prepend/Modify/Replace extensions),
                   ElementHandlerState, RegisteredHandlerServiceTypeSet
                   (-> RegisteredHandlerTypeSet: most-derived type resolution),
                   ElementHandler / ElementHandler<TElement,TPlatformView>
                   (Connect -> create view once -> map EVERY key in stable
                   order; UpdateValue(DependencyProperty) -> that key's mapper;
                   Arrange(rect) records Core's rectangle); the registry and
                   the factory; BrushWatcher (re-pointed brush colours repaint).
                   Mapper keys are DependencyProperty instances; mappers read
                   the EFFECTIVE value from the element.
  Handlers/Views/  (Android) ViewHandler<TElement,TView> + ViewMappers (the
                   UIElement/FrameworkElement mapper: Visibility, Opacity,
                   RenderTransform(+Origin) as the view's animation matrix,
                   Clip (with Core's layout clip, Platform/ClipReplay),
                   Canvas.ZIndex, FlowDirection, ToolTipService.ToolTip (text
                   tooltips of native-input widgets -> TooltipCompat,
                   Handlers/Overlays/ToolTipMapping) - never Width/Height/
                   Margin/alignment, Core layout owns them); ViewGroupHandler
                   (the view shows the element's visual children, synced from
                   the seam's child notifications and from each child's own
                   connect); TemplatedFallbackHandler (every element without a
                   registration: Core expands its template, the view mirrors
                   it); ControlViewHandler + ControlMappers (IsEnabled,
                   Foreground, background/border drawable, Padding, font) for
                   native controls (OwnsVisuals); ViewHandlerExtensions (the
                   leaf measure: MeasureSpec from Core's available size).
  Handlers/Panels/ Panel / Border / ContentPresenter (BorderDrawable
                   background; Border clips its child to its rounded corners).
  Handlers/Content/ the content hosts (HostsContent|OwnsVisuals|MeasuresNatively):
                   ContentControl (exactly that type, no app Template/Style;
                   subclasses such as Button keep the templated fallback until
                   their own handler exists), UserControl, Page - the content
                   is the control's only visual child and the handler lays it
                   out (ContentHostMath: border + padding + absorbed insets,
                   content alignments). Page draws its Background and ABSORBS
                   the window's safe-area insets (SafeAreaMath: only the part
                   of the system bars/cutout it overlaps, from its margin box;
                   re-checked after every layout tick, SafeAreaAbsorbers).
                   Frame keeps its template (Core swaps the page inside the
                   template's presenter; HostsContent loses the page GoBack
                   creates) and adds the Android back button/gesture
                   (FrameBackNavigation: an OnBackPressedCallback enabled only
                   while a live Frame can go back; BackRequested first).
  Handlers/Scrolling/ ScrollViewer (OwnsScrolling: ChangeView -> the native
                   view; Core-made offset changes -> the native view follows)
                   and ScrollContentPresenter, whose view is
                   Platform/CodeBrixScrollView (native drag after the touch
                   slop on the allowed axes, OverScroller fling, animated or
                   instant ChangeView; every position is reported through the
                   presenter's own Set -> ScrollViewer.OnPresenterScrolled; a
                   drag cancels the pointer in Core; the content's composition
                   Visual.AnchorPoint = -offset so Core's hit testing and
                   TransformToVisual see the scrolled content).
  Handlers/Text/   TextBlock (MeasuresNatively): text, font, colour, wrapping,
                   trimming, alignment, padding, TextDecorations, LineHeight +
                   LineStackingStrategy, IsTextSelectionEnabled, and Inlines
                   (Run/Span/Bold/Italic/Underline/LineBreak/Hyperlink) as one
                   spannable with each run's effective foreground, size, font
                   and decorations (re-read at every native measure).
  Handlers/Shapes/ Shape (AP6; draws Core's own stretched geometry - the
                   sprite's android.graphics.Path, transform and Offset - the
                   WinUI way, and hit-tests it) - see SHAPES AND COMPOSED
                   CONTROLS below.
  Platform/        CodeBrixViewGroup (the LAYOUT REPLAY: OnMeasure measures
                   each child EXACTLY at its Core size, OnLayout places it at its
                   Core rectangle; rectangles are rounded in window pixels
                   (Portable/Layout/LayoutReplayMath) so nothing accumulates;
                   after placing a child it replays the child's clip:
                   ClipReplay = UIElement.Clip intersected with Core's LAYOUT
                   clip - ContainerVisual.LayoutClip, what FrameworkElement's
                   arrange clips to the layout slot and to Width/Height/Max*;
                   Panels' clip is in parent coordinates), CodeBrixContentViewGroup
                   (rounded child clip), CoreSubtreeHost (the one place Android
                   drives Core layout: dialog content, sheets; unsealed - a
                   subclass overrides ArrangeOrigin to arrange its content where
                   it sits in a Core parent; the list item host
                   Platform/Recycler/RecyclerItemHost is a sibling with its own
                   measure: fixed cell sizes, panel bookkeeping), HandlerContext (context and the
                   density PER XAMLROOT: XamlRoot.RasterizationScale), MeasureSpec
                   helpers, Drawables/ (BorderDrawable: per-side thickness,
                   per-corner radius, BackgroundSizing; BrushShader: solid,
                   linear, radial incl. an off-centre GradientOrigin).
  Handlers/Buttons/, Inputs/, Selection/, Media/, Text/ (AP3a: sample controls
                   tier 1) Button family (MaterialButton; element content ->
                   the Core content hosted in a Material shape + ripple),
                   CheckBox / RadioButton, ToggleSwitch, Slider, ProgressBar /
                   ProgressRing, NumberBox, AutoSuggestBox, ComboBox (native
                   exposed drop-down, OFF by default - see NATIVE VS TEMPLATE),
                   Image (an ImageView showing the bitmap Core decoded),
                   FontIcon/SymbolIcon drawables for native icon slots, TextBox /
                   PasswordBox (TextInputLayout + Platform/Text/CodeBrixEditText
                   behind Core's ITextBoxPlatform with IsManagedEditing false).
                   Inputs/Support/: NativeControlPolicy, ThemeResources /
                   StateColors (a template's colours per visual state),
                   CorePointerBridge, NativeTemplateParts, Material helpers.
  Handlers/Lists/, Handlers/Tabs/, Platform/Recycler/ (AP3b) ListView,
                   GridView, TreeView's list, FlipView and ItemsRepeater over a
                   RecyclerView whose containers are CORE's (Core makes and
                   prepares them; a container is in Core's tree exactly while
                   its ViewHolder is attached; recycled with its template);
                   the list's Core children are an internal ListScrollViewer
                   (offsets follow the native list) and RecyclerItemsPanel (the
                   ItemsPanelRoot, the Core parent of the attached containers).
                   Since pin 1.0.268.12 the list handler is Core's ITEMS HOST
                   (OwnsItemsHost + IItemsHostHandler, WPE1-5): Core generates
                   no containers into a panel, reads the realised containers
                   from the handler, takes the visible range from the layout
                   manager and sends ListViewBase.ScrollIntoView to the handler
                   (ScrollIntoViewRequest -> RecyclerView scroll); containers are
                   bound / released through ItemsControl's items-host entry
                   points. TabView / Pivot / SelectorBar keep Core's template
                   and selection; a Material TabLayout is drawn over the
                   template's tab row (a native tab choice goes through
                   TabView.RaiseSelectionChangedFromPlatform). ItemsControl
                   stays on Core's path (non-virtualizing: panel handlers draw
                   its containers).
  Handlers/Navigation/, Handlers/Overlays/, Overlay/ (AP4) - see OVERLAYS,
                   NAVIGATION AND SERVICES below.
  Policy/, Platform/Animation/ (AP5; Handlers/Navigation/ since AP5) - the
                   presentation-policy layer: see PRESENTATION POLICY below.
                   Policy/Portable/ compiles for net10.0 too.
  Handlers/Composed/ (AP6) Expander, ColorPicker, the Material date and time
                   pickers - see SHAPES AND COMPOSED CONTROLS below.
The window's root element (XamlIslandRoot) gets no handler (Core connects it as
the visual root); AndroidXamlRootHost owns the root view group that shows its
children's views and keeps it in the activity's content layer.

INPUT MODELS OF NATIVE CONTROLS (AP3a/AP3b). Two, chosen per control family:
  - Core acts (ButtonBase family: Button, ToggleButton, RepeatButton,
    HyperlinkButton, CheckBox, RadioButton; list items and tabs): the behaviour
    stays Core's for real and injected pointers (ClickMode, IsPressed, Command,
    the RepeatButton timer, three-state, GroupName, selection, ItemClick,
    expand/collapse); the native widget gives the look, the ripple and
    accessibility; a native click that no touch caused (accessibility) is raised
    through the seam (RaiseClickFromPlatform).
  - The widget acts (OwnsInput: ToggleSwitch, Slider, TextBox, PasswordBox,
    NumberBox, AutoSuggestBox, native ComboBox, a list's scrolling): real touches
    are the widget's and are marked handled (NativeInput.MarkHandled - Core still
    routes them with Handled set, so Core's own control logic does not react a
    second time); Core-only pointers (Core's InputInjector, the mouse wheel Core
    receives) are forwarded to the widget as MotionEvents (CorePointerBridge,
    CorePointerScrollBridge); the widget reports its result through the seam's
    raise entry points (SetTextFromPlatform, RaiseToggledFromPlatform, ...).
  Keys: a focused native text editor gets its keys before Core (see INPUT).

NATIVE VS TEMPLATE POLICY (Handlers/Inputs/Support/NativeControlPolicy). A
control is shown by its native widget only when it looks the way its default
style says: its DefaultStyleKey is the family's (an app subclass with
DefaultStyleKey = typeof(Button) qualifies; AppBarButton, DropDownButton do
not), it has no local Template, no Style in its chain sets a Template other than
the Fluent styles the widget reproduces (DefaultButtonStyle, AccentButtonStyle,
...), and it is not a part of another control's template (a ScrollBar's
RepeatButtons, a SplitButton's halves). Everything else keeps the templated
fallback (Core expands the Fluent template, native views mirror it). A list is
native only with a supported items panel and no re-template or grouping.
Colours follow D-O4 "honor": the effective brushes and the lightweight-styling
keys per visual state (StateColors); which Fluent keys become Material roles is
the AP5 theme bridge. The native ComboBox (exposed drop-down) is behind the
AppContext switch CodeBrix.Android.UI.NativeComboBox (off: the templated
ComboBox with Core's popup stays the default until "AP5/AP8 decide native
ComboBox", recorded in uireqs-pending.txt).

TEST-ONLY TEMPLATE-PART STAND-INS (Handlers/Inputs/Support/NativeTemplateParts,
OFF in apps). The copied UIReqs scenarios measure Fluent template parts by name
(NormalRectangle, SwitchKnob, HorizontalThumb, DeterminateProgressBarIndicator,
...), which a native widget does not create. The UIReqs device app turns the
stand-ins on (AndroidSteps/NativeControlsSteps' [BeforeTestRun] hook): each
native handler declares its parts at connect and places them during Core's
arrange where the widget draws them (refined after the widget draws). Never
turn them on in an app.

INSETS (Platform/Insets/). Content is always laid out edge to edge. One
WindowInsetsListener (port of MAUI's MauiWindowInsetListener, IME-animation gate
kept) sits on each activity's root layout and reports the settled safe area
(per edge max of system bars and display cutout) and the keyboard inset to the
window wrapper, which sets Bounds/VisibleBounds (DIPs), raises SafeAreaChanged
(Pages re-check what they absorb) and sets InputPane.OccludedRect (Core raises
Showing/Hiding and brings the focused element into view);
InputPaneAndroidExtension shows/hides the soft keyboard. The status bar is 24 dp
at every density, so VisibleBounds.Top is 24 DIPs everywhere (63 px at 2.625).

INPUT (Input/, plan 2.15). Per window, one ICodeBrixCorePointerInputSource and
one ICodeBrixKeyboardInputSource (ports of the upstream Skia head's sources;
created by Core's input manager with the window's AndroidXamlRootHost).
CodeBrixActivity.DispatchTouchEvent dispatches to the native views FIRST, then
feeds the event to Core (ActivityInputRouter): a native widget may mark it
handled (NativeInput.MarkHandled: Core still routes it, Handled set) and a
native scroll view that starts dragging takes the pointer over
(NativeInput.CancelPointer: Core gets PointerCancelled and no more of that
pointer); a pointer Core captured is never intercepted
(NativeInput.IsCapturedByCore). DispatchGenericMotionEvent feeds hover and the
mouse wheel (ACTION_SCROLL, 120 per notch) to Core; DispatchKeyEvent gives a
key to Core before the focused widget (VirtualKeyHelper: the upstream table plus
Alt = Menu, keyboard arrows = Up/Down/Left/Right, OEM keys) - EXCEPT while a
native text editor (an EditText of a TextBox, PasswordBox, NumberBox,
AutoSuggestBox) has the Android focus: then the editor gets the key first
(Window.SuperDispatchKeyEvent) and Core only a key the editor did not use (Tab,
Escape, accelerators, an arrow at the end of the text); a key-up follows its
key-down. This is the native-head order: caret keys move the caret natively
before Core decides whether the key was used, and Enter reaches the editor's IME
action (the handlers turn it into Core's Enter / QuerySubmitted) instead of a
Core-focused control elsewhere. System keys (back, volume) never go native-first.
The soft keyboard is hidden at start (CodeBrixActivity sets WindowSoftInputMode
StateHidden unless the app's activity declares a visibility): it shows for a
text box focused by touch, as WinUI's touch keyboard. Pointer icons follow
Core's cursor (PointerIcons). Core drops a wheel event from a mouse it has not
seen hover: real mice always hover first.
Register or replace a handler with CodeBrixHandlers.Register<TElement, THandler>()
before the first element of that type goes live (internal in v1: D-O1).

THE PROJECTION VIEWER (Projection/, pure parts in Portable/Projection/). The AP1
stand-in for element handlers: after each Core layout tick
(AndroidXamlRootHost.LayoutUpdated, raised from IRenderingPlatform.
OnRenderFrameOpportunity) it walks the window's visual tree and mirrors it into
CodeBrixActivity.RootLayout.ContentLayer (one ProjectionLayer child that places
views at Core's layout slots, density-aware, in tree order): TextBlock text,
TextBox text and FontIcon glyphs as TextViews (same typeface, size, colour,
alignment, wrapping as the Core measure), Border/Panel/ContentPresenter/Page
backgrounds and borders and Rectangle/Ellipse fills as ProjectionBoxViews, and a
labelled placeholder box (type name + key properties; also the view's
ContentDescription) for every control / image the page declares (template parts
draw only their own backgrounds and texts). Views are reused per element and
pooled; unchanged elements allocate nothing per tick. Not handled: render
transforms, scroll offsets, input. CodeBrixApplication.UseProjectionViewer
(default FALSE since AP2: the element handlers show the tree natively) is the
one switch; turned on, it draws its diagnostic views over the handlers' views.

FONTS. FontAndroidPlatform follows the family rule "never fall back to a system
font" (Portable/FontFallbackPolicy): a FontFamily that names a font file loads
it (the .ttf.manifest picks weight / style / stretch faces); a family NAME
(including Core's default "Segoe UI") resolves to the app's
FeatureConfiguration.Font.DefaultTextFontFamily file; only an app with no default
font file gets Typeface.Default, with one warning. The text ENGINE of the
TextLayout Core (and PlotterView's Core) takes its SkiaSharp typefaces from the
same rule through IFontSourcePlatform<SKTypeface>, registered by the TextLayout
add-in (see ADD-INS).

THE BOOTSTRAP CHAIN. Each assembly has an internal, idempotent (lock + flag)
AndroidPlatformBootstrap.EnsureRegistered() that registers its contracts with
ApiExtensibility.Register and also runs as a [ModuleInitializer]. The
CodeBrix.Android.UI bootstrap first calls the Dispatching, WinRT, Composition
and Toolkit bootstraps (reached through each assembly's InternalsVisibleTo
grant to CodeBrix.Android.UI), then registers the UI contracts (since pin
1.0.268.12 also IAnimationSettingsPlatform: UISettings.AnimationsEnabled
follows the animator duration scale; the WinRT bootstrap registers
IDeviceFamilyPlatform: DeviceFamily "Android.<form>", PROVISIONAL - decision
D2, one constant in Android/DeviceFamilyAndroidPlatform) and sets the
FeatureConfiguration platform defaults (Popup.ConstrainByVisibleBounds,
Frame.UseWinUIBehavior, ToolTip.UseToolTips = true). CodeBrixApplication.OnCreate
calls it explicitly: the framework Cores resolve contracts through
ApiExtensibility only (they do not load CodeBrix.Android.* by name), so the
registrations must exist before the first XAML type is used.
ApiExtensibility.Register throws on a duplicate registration - never register
a contract outside the bootstraps.

CROSS-ASSEMBLY GRANTS (since pin 1.0.266.1160). Every framework Core grants
InternalsVisibleTo to the Android name of each assembly that needs it, not only
to ITS twin: CodeBrix.Platform.UI.Composition.Core grants CodeBrix.Android.UI
(IImagingPlatform names PlatformCompositionSurface; ImagingAndroidPlatform is
registered) and CodeBrix.Platform.Core grants CodeBrix.Android.UI.Composition
(the Direct2D segment structs of composition path commands - Bezier, quadratic
Bezier and arc are drawn). Gate 3 (build/intake/gates/ivt-grants.txt) lists
each grant a src/ assembly relies on.

OVERLAYS, NAVIGATION AND SERVICES (AP4). The IOverlayPresenterPlatform
(src/CodeBrix.Android.UI/Overlay/, seam hooks H11/H12) shows a ContentDialog whose
Title and Content are text (a string, or a TextBlock of Runs - SimpleViewModel's
ShowInfo/ShowError/ConfirmDialog) as a Material 3 dialog, and a MenuFlyout of the
standard item kinds as a Material popup menu anchored at its target (a bottom sheet
in a Compact window, < 600 dp); every button / item still runs Core's path
(RaiseButtonFromPlatform, MenuFlyoutItem.Invoke) and the platform window closes only
when Core closes the overlay. Everything else (XAML dialog content, Flyout content,
Popup, TeachingTip, rich ToolTips) stays in Core's PopupRoot (tier 1).
OverlayPresentation switches the Material forms off (the UIReqs device app does so
for every scenario not tagged @native-overlays); PlatformOverlays lists what is shown
natively - INTERNAL (no new public API before D-O1), read by the UIReqs device app and
HelloPaste's self-check through InternalsVisibleTo. AcrylicBrush (and every
XamlCompositionBrushBase) paints its FallbackColor (no blur on Android - what WinUI
shows with transparency effects off; Portable/Drawing/BrushPaint). The presenter, the
activity bridge and the lifecycle listener are registered by AndroidPlatformBootstrap
(OverlayBootstrap.EnsureRegistered) just before it asks Core to resolve its
element-handler services, so nothing depends on Core's resolution order.
CodeBrixActivity.OnProvideKeyboardShortcuts lists the app's KeyboardAccelerators in
Android's keyboard shortcuts helper (Meta + /; Handlers/Navigation/KeyboardShortcuts).
Back (Handlers/Navigation/BackNavigation.cs): one AndroidX OnBackPressedCallback per
activity, enabled only while the app takes the back - the topmost Core overlay first,
then SystemNavigationManager.BackRequested, then a NavigationView (pane / BackRequested),
then the innermost Frame that can go back (predictive back previews the page being
left); otherwise the system's back (and back-to-home animation) applies. Frame page
changes play Material motion (FrameTransitions); NavigationStateKeeper keeps the root
frame's navigation state across process death.
WinRT services (src/CodeBrix.Android/Services/, registered by the WinRT bootstrap):
clipboard (text, links, HTML, bitmaps through the CodeBrix FileProvider declared by
Overlay/SharedFileProvider.cs), launcher, share sheet, connectivity (needs
ACCESS_NETWORK_STATE in the app manifest), haptics, FileOpenPicker / FileSavePicker over
the Storage Access Framework and FolderPicker over a document tree: since pin 1.0.268.12
the results are StorageFile / StorageFolder.FromImplementation over the content URI
(Services/ContentDocuments.cs: streams, name, content type, size, listing, create and
delete go to the provider; nothing is copied). A file's Path is a compatibility path
made on its FIRST read only, for code written against a desktop head that opens
file.Path with System.IO: a cache copy of an opened document, or for a created one a
cache file written back to the document each time it is closed. A folder's Path is
empty. Grants are kept (persistable) where the picker offers them. Application
view, badge (no-op), contact picker (not supported). They reach the activity through
IAndroidActivityBridge (installed by Overlay/SystemUiBridge.cs; results come back
through the ActivityResultRegistry).

PRESENTATION POLICY (AP5; src/CodeBrix.Android.UI/Policy/, Platform/Animation/,
Handlers/Navigation/). The replaceable policy layer, installed by the AP5 block of
CreateDefaultRegistry (PresentationPolicy.EnsureRegistered, POSTED: the registry is
built inside UIElement's static initializer, so nothing may touch handler/DP statics
there). Size classes: WindowSizeClassMonitor (WindowManager.CurrentWindowMetrics in
dp - what AndroidX Window's calculator returns on API 30+ - per activity, recomputed
on configuration changes and decor layout changes; fine pointer = a mouse/touchpad
input device; AdaptivePolicy.Override simulates classes). The adaptive table:
Policy/Portable/AdaptivePolicy (versioned "Material 3 Expressive as of 2026-09-23";
Rows documents where each row is applied). NavigationView (Auto) ->
BottomNavigationView / NavigationRailView / persistent Material drawer / top bar +
modal drawer, re-mapped live keeping the page view (Handlers/Navigation/
NavigationViewHandler + AdaptiveNavigationLayout); explicit PaneDisplayMode or pane
content a Material container cannot show keeps the Fluent template. A native
container ABSORBS the window's safe-area insets it overlaps, as a Page does
(NavigationViewHandler is an ISafeAreaAbsorber; Handlers/Navigation/Portable/
NavigationChromeInsets): the chrome runs edge to edge and pads its destinations clear
of the system bars on the edges it touches (the Material views' own inset listeners
are replaced), the content is laid out clear of them on the other edges too - a Page
content absorbs its own overlap instead (its background still runs under the bars).
A density change of the window (the wrapper's size report with a new
RasterizationScale) re-fills the container's menu so its icons follow the density.
ContentDialog with XAML content: full screen in Compact (ContentDialogPolicy).
Theme: ThemeBridge writes Material 3 roles (baseline or dynamic) into the curated
Fluent keys (MaterialRoleMap) as brushes of its own per theme dictionary and
re-resolves ThemeResources (Application.OnRequestedThemeChanged); app keys always
win; AppCompat night mode follows an explicit Application.RequestedTheme.
ThemeKeyMap = the 173 re-keyed corpus keys with their path (161 honored);
ThemeKeyAppliers complete the CheckBox/Slider/TextControl families on the AP3a
handlers: CheckBoxHandler.ApplyColors, SliderHandler.MapColors and TextBoxHandler's
recolour (and its editing refresh, which re-sets the end icon) end with the family's
applier, so a recolour made outside the mapper (an in-place Foreground re-point, a
PasswordBox RefreshFromCore) keeps the full state lists; the appliers act once the
layer is installed (ThemeKeyAppliers.Install). ThemeRefresh re-maps native handlers
when an app re-points a brush. A Material dialog is re-coloured from the app's
ContentDialog* keys right after it shows (NativeContentDialog.Show ->
ContentDialogPolicy.Recolor; the window-focus observer stays as the fallback).
TypeScalePolicy: FontSize in sp while IsTextScaleFactorEnabled; framework TextBlock
styles -> Material type roles (app-authored TextBlocks only). Motion:
CoreAnimationTicker raises CompositionTarget.Rendering from Choreographer while Core
has subscribers (Storyboards, VisualState transitions, TeachingTip); it looks at every
looper idle and at each of Core's own frame requests (AndroidXamlRootHost.
InvalidateRender -> Poke); MotionPolicy.AlwaysAnimate drives frozen native
indeterminate indicators (ProgressBarHandler.MapProgress ->
IndeterminateProgressDriver); AnimatorScaleMonitor follows the system scale. Frame
page changes under a native NavigationView fade through.

SHAPES AND COMPOSED CONTROLS (AP6; src/CodeBrix.Android.UI/Handlers/Shapes/,
Handlers/Composed/). Shapes: one ShapeView per Shape draws the geometry Core built
(composition sprite path x CombinedTransformMatrix + Offset); the Fill is mapped over
the fill geometry's bounds, the Stroke is an outline (ShapeStroke: Android stroker +
the WinUI start/end/Triangle caps, dashed end points, Triangle dash caps, miter-clip
on API 34+) filled over the outline's bounds - what the Skia heads paint. The Shape
handler OwnsVisuals so Core's hit test asks it (fill with a Fill, outline with a
Stroke). Viewbox and Border need nothing new (Viewbox: templated fallback, Core's
ScaleTransform on its inner Border replayed as the view's animation matrix; Border:
AP2's BorderDrawable). Composed: ColorPicker (ColorPickerHandler: spectrum view,
Material sliders and text fields, the IsXxxVisible parts; RGB channels only, Ring
drawn as Box, Min/Max clamps not applied), Expander (ExpanderHandler: card surface,
native header row, Core content hosted; ON by default since pin 1.0.268.12, whose
Core keeps hosted content across a Template change (WPE1-1 C0d) - AppContext switch
CodeBrix.Android.UI.NativeExpander=false keeps the Fluent template; the UIReqs app
keeps the copied Expander scenarios on the template), DatePicker / TimePicker
(MaterialPickers: Core's ISkiaNative{Date,Time}PickerProviderExtension ->
MaterialDatePicker / MaterialTimePicker dialogs, text input first in Expanded or with
a fine pointer; the controls keep their Fluent template).

ADD-INS (AP7-A; src/AddIns/CodeBrix.Android.<AddIn>/, one project per add-in, in the
slnx under /src/AddIns/, host-free tests tests/CodeBrix.Android.<AddIn>.Tests). Each
add-in's Core (re-shipped by the intake) loads its Android assembly BY NAME
(PlatformContract) - so the assembly name is exactly the one the Core grants
InternalsVisibleTo to - and the framework also loads every name in
Portable/AddInAssemblyNames at start-up (Android/AddInLoader), because some
contracts are only ever resolved through ApiExtensibility. Each add-in's module
initializer (Android/AndroidPlatformBootstrap) registers its contracts and handlers.
  SkiaSharp.Views: SKXamlCanvas = a view group painting a kept Skia buffer (an
    android.graphics.Bitmap shared with an SKSurface) under its XAML children,
    repainted only on Invalidate(); the canvas-host factory (SKCanvasVisualBaseFactory)
    every Skia-drawing add-in paints through (SKCanvasHostElement -> a leaf Skia view;
    inside an Image -> ImageHandler.ShowChildContent); SKSwapChainPanel keeps the
    Platform contract (refused). Software drawing; SkiaSharp + NativeAssets.Android.
  Svg: ISvgProvider -> the Core's SvgProvider (CodeBrix.SkiaSvg). Graphics2DSK and
    FlexPanel: their Cores are the whole add-in (SKCanvasElement -> the canvas-element
    handler; FlexPanel -> the panel handler). CommandBar: IIconRasterizationPlatform
    (SvgImageSource rasterized at the icon size); the bar keeps its Core template.
  AppSettings: IAppSettingsStoragePlatform -> Portable/SqliteAppSettingsStorage (the
    Platform storage's lifecycle, ported: settings.sqlite, adopt settings_incoming,
    quarantine settings_corrupt_*, restore settings_auto_backup_*, export/import
    validation) through CodeBrix.Sqlite; the folder is Context.FilesDir/CodeBrix/<app>/
    settings, staging under CacheDir.
  WebView: INativeWebViewProvider over android.webkit.WebView (navigation announce and
    cancel, ms-appx host-to-folder mapping to file:///android_asset/, NavigateToString,
    ExecuteScriptAsync, WebMessageReceived through an AndroidX WebMessageListener +
    document-start script, downloads with progress); the WebView lays out MATCH_PARENT
    (WRAP_CONTENT gives a page a zero-height viewport).
  MediaPlayer: IMediaPlayerExtension over Media3 ExoPlayer (main looper; a 100 ms
    position ticker that never writes Position back - Core writes Position during
    PositionChanged), IMediaPlayerPresenterExtension = a TextureView fitted by Stretch
    (Portable/VideoFit). ms-appx -> asset:///, ms-appdata -> the app data folders.
  Graphics3DGL: INativeOpenGLWrapper = an EGL context (OpenGL ES 3, 1x1 pbuffer; libEGL /
    libGLESv3 by P/Invoke, no package); GL entry points from libGLESv3, extensions from
    eglGetProcAddress (the driver's own glReadPixels: since pin 1.0.268.12 the Core picks
    BGRA or RGBA + swap per context itself, WPE1-1 C0f; the AP7-A read-back shim is gone).
    GLCanvasHandler follows the Core's back buffer (the WriteableBitmap in its Background
    ImageBrush), copies each picture flipped into an android.graphics.Bitmap and draws it
    under the children; the view's pre-draw listener calls OnVisualPainting (the Skia
    heads' compositor moment). The flavor-only public types of the Platform add-in
    (OffscreenGLContext, SkiaGLCanvasElement, SkiaGLPaintSurfaceEventArgs,
    SkiaGpuContext; namespace CodeBrix.Platform.WinUI.Graphics3DGL) are re-provided in
    Public/ (SkiaGLCanvasElement: Skia GPU surface on the EGL context, read back into a
    bitmap). Shaders must be GLSL ES (#version 300 es).
  TextLayout (AP1.9): the add-in is Core-only since the engine pass (the whole text
    engine is in CodeBrix.Platform.UI.TextLayout.Core); its Android side is only the
    engine's FONT SOURCE, IFontSourcePlatform<SKTypeface> (Android/FontSourceAndroidPlatform:
    SkiaSharp typefaces from the APK assets by the font rule of FONTS above; process-wide
    cache), registered from its module initializer (TextLayout.Core and PlotterView.Core
    load CodeBrix.Android.UI.TextLayout by name). SkiaSharp + HarfBuzzSharp native assets.
  UIReqs: each add-in's group is copied (tests/CodeBrix.Android.UIReqs.Device/PORTING.txt,
  ADD-IN GROUPS).

REMAINING ELEMENTS, FIRST LANE (AP10-A; src/CodeBrix.Android.UI/Handlers/AppBars/,
Shell/, Paging/, Calendars/, each with Portable/ helpers tested host-free).
  AppBars/: CommandBar = a Material app bar laid out where Core puts it (adaptive row
    "CommandBar (WinUI)": BottomAppBar in a Compact window, MaterialToolbar with the
    Content text as title otherwise, re-mapped live); PrimaryCommands are actions (IfRoom
    while IsDynamicOverflowEnabled), SecondaryCommands the native overflow menu (an
    AppBarSeparator there starts a menu group), an AppBarButton whose Flyout is a plain
    MenuFlyout a sub-menu; actions run Core's path (RaiseClickFromPlatform); IsOpen follows
    the overflow popup (window-focus change). A bar with element Content, an
    AppBarElementContainer, an app template, or a CommandBarFlyout's command row keeps the
    Fluent template; AppContext switch CodeBrix.Android.UI.NativeCommandBar=false turns
    it off. A stand-alone AppBarButton / AppBarToggleButton is a Material text button
    with its icon above its label (Core acts on touches, as the Button family).
  Shell/: SplitView keeps its template; its handler re-applies the adaptive Compact form
    (Inline shown as Overlay, CompactInline as CompactOverlay) by visual state, never
    changing DisplayMode (a SplitView inside another template is untouched).
  Paging/: PipsPager, PagerControl, BreadcrumbBar are native rows of Material parts
    (pips / icon buttons / text buttons / drop-down menu / number field); choices set
    SelectedPageIndex, ItemClicked through BreadcrumbBar.RaiseItemClickedEvent.
  Calendars/: a single-selection month CalendarView is android.widget.CalendarView (days
    in the device time zone); CalendarDatePicker is an outlined TextInputLayout opening a
    MaterialDatePicker (Opened/Closed raised through CalendarDatePicker.RaiseOpened/
    ClosedFromPlatform since pin 1.0.268.12).
  Not mapped because the Platform marks them NotImplemented (compile error Uno0001 in an
  app): TitleBar, ListBox, ListBoxItem, GroupItem, ListPickerFlyoutPresenter,
  PickerFlyoutPresenter. Grouped ListView/GridView keep the template; the pinned Core
  realises no group headers (FIXLIST AP10-A). UIReqs: AndroidFeatures/AndroidElements10A.

REMAINING ELEMENTS, SECOND LANE (AP10-B; src/CodeBrix.Android.UI/Handlers/TemplateOverlays/,
Status/, MenuButtons/, Icons/, Scroller/, ColorParts/; Status/Portable/ tested host-free).
  TemplateOverlays/: TemplateOverlayHandler - a control KEEPS its Fluent template (parts,
    visual states, events, Core input and hit tests stay Core's) and a native Material
    widget is drawn over it (the template parts it replaces are laid out, not drawn).
    A real finger on the widget shows the Material press and the SAME touch reaches Core,
    where it lands on the template part under the widget (the widget's halves / close
    button are placed exactly over those parts); a native click that is not a touch
    (accessibility) runs the part's own path (RaiseClickFromPlatform). Used by InfoBar
    (Material card: severity fill/border, icon, title + message, close; an InfoBar with an
    ActionButton or Content keeps its template visible), RatingControl (AppCompatRatingBar
    whose tiles are the template's own star glyphs) and SplitButton / ToggleSplitButton
    (MaterialSplitButton, tonal; element content keeps the template).
  Status/: InfoBadge (Material 3 badge view: dot / number / icon, colorError unless the app
    sets Background/Foreground), PersonPicture (circle + initials / contact / group glyph,
    ProfilePicture = the bitmap Core opens through ImageSource.Subscribe, badge),
    RefreshContainer (the template inside a SwipeRefreshLayout: a pull runs
    RequestRefresh, the RefreshVisualizer's state drives the native indicator both ways;
    only TopToBottom pulls). Package reference Xamarin.AndroidX.SwipeRefreshLayout.
  MenuButtons/: DropDownButton = the Button handler plus a trailing chevron (an
    AppendToMapping on ButtonHandler.Mapper that touches only DropDownButtons).
  Icons/: BitmapIcon mirrors Core's Grid+Image and tints the ImageView (SrcIn) with
    Foreground when ShowAsMonochrome. PathIcon / AnimatedIcon need nothing (Core composes a
    Path / the fallback icon; an AnimatedIcon's animation source waits for the Lottie add-in).
  Scroller/: ScrollView keeps its template; its ScrollPresenter's view is CodeBrixScrollView;
    native scrolling is reported through ScrollPresenter.ScrollTo (no animation), Core
    scrolls (ViewChanged) move the view. ColorParts/: a stand-alone ColorSpectrum is AP6's
    ColorSpectrumView (a pick sets HsvColor; Core raises ColorChanged).
  Not mapped (NotImplemented in the Platform): RichTextBlock, RichTextBlockOverflow and
  RichEditBox (every member), Block / Paragraph / Glyphs / InlineUIContainer, Hub,
  HubSection, SemanticZoom, ParallaxView, AnnotatedScrollBar, MapControl, SwapChainPanel,
  SwapChainBackgroundPanel, the legacy WebView. Not in the packages: Popover, MapPresenter.
  MidiPlayer is the AudioPlayer add-in's (AP7-B). SKSwapChainPanel keeps the refusal
  contract (AP7-A). UIReqs: AndroidFeatures/AndroidElements10B.

LANE RULES (parallel work packages in this repository). Lanes own DISJOINT
folders (the handler families above: AP3a Handlers/Buttons|Inputs|Selection|
Media|Text + Platform/Text; AP3b Handlers/Lists|Tabs + Platform/Recycler; AP4
Handlers/Overlays + Overlay/ + src/CodeBrix.Android/Services; AP5 Policy/,
Platform/Animation/, Handlers/Navigation/ (from AP4); AP6 Handlers/Shapes/ (from
AP2), Handlers/Composed/; AP10-A Handlers/AppBars|Shell|Paging|Calendars/; AP10-B Handlers/TemplateOverlays|Status|
MenuButtons|Icons|Scroller|ColorParts/; add-ins src/AddIns/<AddIn>/). Coordinator-owned (a lane writes the exact change it needs
into its report's "COORDINATOR CHANGES NEEDED" and works around it locally):
Handlers/Core/, Handlers/Views/ (incl. ViewMappers.cs - a lane adds a
UIElement-level mapping as AppendToMapping on its own handler's mapper and the
coordinator lifts it), Handlers/Panels|Scrolling|Content/, Platform/ outside the
lanes' sub-folders (CodeBrixViewGroup, CoreSubtreeHost, ClipReplay, Insets/),
Input/, Hosting/, Portable/, build/test-scripts/, the UIReqs runner and scenario
app harness, the csproj files and the docs. Handlers/CodeBrixHandlers.cs is
APPEND-ONLY: each lane registers in its own commented block at the end of
CreateDefaultRegistry and replaces a registration only by adding a more-derived
one. Test files: one per handler family under tests/CodeBrix.Android.UI.Tests;
copied UIReqs feature files are never edited - Android-only scenarios go under
AndroidFeatures/<Group>/. uireqs-pending.txt: never delete a scenario line except
to move it to passing when its control lands; "#" lines are comments (also used
to record a pending DECISION). Every build, test and emulator start/stop runs
under the Android track's build lock, one at a time.


CONSUMER BUILD LOGIC
====================
build/nuget/buildTransitive/CodeBrix.Android.ApacheLicenseForever.props and
.targets ship in the package next to the re-shipped Platform build files. They:
  * import the re-shipped Platform.UI.SourceGenerators.props and
    Platform.UI.Tasks.targets (via CodeBrixAndroidPlatformBuildPath, which is
    the package's buildTransitive folder; in-repo builds point it at
    artifacts/intake/<version>/buildTransitive/);
  * give the XAML generator exactly the desktop dialect: heads get
    CodeBrixRuntimeIdentifier=Skia (as a desktop head), and after the imports
    XamarinProjectType / _IsNetStdRef / _IsAndroidSkia /
    _CodeBrixUnderlyingPlatform are reset to the values a desktop head or
    library computes, and AndroidApplication is hidden from the generator
    (otherwise it emits an Android drawable resolver against a helper the
    platform-neutral Core does not have);
  * define the family constants minus the Skia ones, plus HAS_CODEBRIX_ANDROID
    and __CODEBRIX_ANDROID__;
  * add every app Content item and every library asset (the output of the
    re-shipped _CodeBrixAddLibraryAssets) as an AndroidAsset whose LogicalName
    is its ms-appx path, set EnableDefaultAndroidAssetItems=false and
    SupportsFontManifest=true;
  * root the head assembly and its project references for the trimmer, and
    every CodeBrix.Android.* platform assembly the app references (the Cores
    load them by name; each also embeds an ILLink descriptor rooting itself
    behind the feature switch CodeBrix.Android.RootPlatformAssemblies,
    src/Directory.Build.targets);
  * set CodeBrix.Platform.RootSkiaPlatformAssemblies=false (the Core descriptors
    would root the Skia twins an Android app does not ship: IL2007); XAML
    resource trimming stays off (decision D10);
  * never import the Platform head, runtime-replace, single-project,
    cross-runtime or WinAppSDK targets.


TESTING
=======
    dotnet test --solution CodeBrix.Android.slnx

THE TEST RUNNER IS Microsoft.Testing.Platform (MTP), selected by global.json
at the repo root. That file does NOT pin an SDK version; keep it committed -
without it `dotnet test` falls back to the VSTest bridge, which fails on the
.NET 10 SDK. MTP output ends in a "Test run summary:" block. If a solution run
reports zero tests, run a test assembly directly:

    dotnet tests/CodeBrix.Android.IntakeGate.Tests/bin/Debug/net10.0/CodeBrix.Android.IntakeGate.Tests.dll

tests/CodeBrix.Android.UI.Tests (net10.0, assembly name granted by UI.Core)
tests host-free: the portable logic of CodeBrix.Android.UI (its net10.0 build;
UI/, Projection/, Handlers/ (mappers, registry, the seam fences of the native
control families, stroke maths, navigation menu and chrome insets), Layout/,
Drawing/, Overlay/, Lists/, Policy/ (size classes, the adaptive table, theme keys,
type scale, motion), Composed/ (colour maths, Expander layout)), the extracted Core run without a device (HostFree/: the
net10.0 flavors of .UI.Dispatching and .UI.Composition give Core a managed pump
and the inert composition platform; test doubles stand in for the text, font,
rendering, focus and application contracts; Frame navigation, bindings and
layout run for real), the intake's acceptance (Intake/), and the Portable/
folder of CodeBrix.Android compiled in as linked source. The HostFree tests
share Core's static UI state and run in one non-parallel xUnit collection.

Two scripts cover what needs an Android build or device (both honour
CODEBRIX_ANDROID_BUILD_LOCK: when set, builds and emulator start/stop run under
`flock -o <lock>`):

    build/test-scripts/paste-always-compile.sh [-c Debug|Release] [App ...]
        The paste-always compile test: builds each tests/PasteAlways/<App> head
        (the corpus page's App.xaml, App.xaml.cs, MainPage.xaml(.cs) and page
        partials, VERBATIM, checked against <App>/pasted-files.sha256; what they
        reference comes from <App>/Surface/, verbatim or stub) and reports one
        line per page; PASS = unchanged files, 0 warnings, 0 errors. Heads: the
        eight corpus pages, PdfSideBySide (HelloPaste's second page), and
        WikipediaPublisher (the WebView add-in) and PolyHavenBrowser (the
        Graphics3DGL add-in). An add-in with an Android flavor is the real
        add-in in every head that uses it (CodeBrixAndroidInRepoAddIns); a
        Surface/ library that needs an add-in's Core sets the same property
        (e.g. KenneyAssetBrowser.Rendering / PolyHavenBrowser.Rendering:
        Graphics3DGL). WikipediaPublisher, PolyHavenBrowser and
        KenneyAssetBrowser have no stubs left (Surface/SOURCES.txt).
        The heads define nothing extra: since pin 1.0.266.1160 the re-shipped
        generator emits the Core template root (UIElement) for an Android
        consumer, so the pages compile exactly as a shipped consumer's would.

    build/test-scripts/android-uireqs-avd.sh [create|start|stop|status] [--port N]
        Creates the test AVD CodeBrix_Agent_15inch (15-inch profile, 1080x1920,
        density 1, API 37 x86_64 google_apis) if it is missing; never touches
        another AVD. start: cold boot, headless, on port 5600 (5554 refused), with
        file descriptors 3-9 closed (the emulator never inherits a caller's lock
        fd), every boot-wait adb call bounded by `timeout 10`, and an EXIT trap
        that stops the emulator it started if the boot does not complete.
        stop: stops only this AVD's emulator on the port.

    build/test-scripts/device-smoke.sh [-c Debug|Release] [--avd NAME] [--port N] [--keep-emulator]
        Starts CodeBrix_Agent_15inch headless on port 5600 (serial emulator-5600;
        port 5554 is refused), deploys samples/HelloPaste with -t:Install,
        runs both pages, waits for "SELFCHECK SUMMARY pass=N fail=M", captures
        screenshots and tree dumps in portrait and landscape (--landscape rotate,
        the default: the display really rotates, `cmd window user-rotation lock 1`;
        --landscape size: `wm size <h>x<w>` instead), stops the app and the
        emulator it started (never one it did not start; a running emulator on
        the port is used only if it runs the requested AVD); exits non-zero on
        any failed check or crash.

    build/test-scripts/android-uireqs-run.sh [--group "Harness Layout"|all] [--orientation portrait|landscape|both] [--out DIR] [--keep-emulator] [--density DPI]
        THE UIREQS SUITE ON ANDROID (see UIREQS below). Takes the build lock for
        the whole run, starts CodeBrix_Agent_15inch (port 5600), fixes the global
        settings (animation scales 0, font scale 1.0, night mode off, touches
        hidden, stay awake, rotation locked, default size/density), installs the
        scenario app (Debug, -t:Install), builds the host runner, and per
        orientation: rotates the emulator (`cmd window user-rotation lock <r>`:
        the API 37 AVD ignores `settings put system user_rotation` alone) and
        checks the display really is 1080x1920 / 1920x1080 (a screencap's size;
        a rotation that did not apply is a setup failure, exit 2), restarts the
        app, adb-forwards tcp:47300 and runs the runner for each group; saves
        <out>/<Orientation>-<Group>.log (per-scenario results), .logcat.txt,
        frames/ (every captured frame, CODEBRIX_UIREQS_FRAME_SAVE) and run.log;
        stops the emulator (unless --keep-emulator). Exit status = the test
        result (0 = every scenario passed or was skipped).
        Lock hygiene: the runner holds the lock on fd 9 and starts every
        long-lived child (the emulator, the adb server, logcat, the dotnet builds)
        with fd 9 closed, so nothing outlives it holding the lock; an EXIT trap
        (also on INT/TERM/HUP) removes the forward, unlocks the rotation and
        stops the emulator (unless --keep-emulator) on every way out, a failed
        device build included. Callers that already hold the lock pass --no-lock.
        --density DPI (wm density, e.g. 420 = 2.625, 560 = 3.5) runs at another
        density: scenarios that state device-pixel sizes fail there by design;
        what it proves is the geometry audit (logcat tag UIReqs.Geometry, and no
        "Layout replay:" failure).

    build/test-scripts/compare-uireqs-frames.sh <baseline-frames> <current-frames> [--report FILE]
        The copied Platform frame-compare tool (tools/UIReqsFrameCompare, managed
        PNG codec instead of SkiaSharp): every frame byte/pixel compared, diff
        images under <current>/_diff; entries in
        build/test-scripts/uireqs-frame-compare.informational (copied from
        Platform) are reported but never fail. Pixel parity with the Platform's
        frames is never asserted: an Android run compares with an Android
        baseline (~/ClaudeHome/android-buildout-work/uireqs-baseline/).

Test projects use xUnit v3 and SilverAssertions (no coverage collector), file
names <Class>Tests.cs, snake_case test names, //Arrange //Act //Assert.


UIREQS (the CodeBrix.Platform UIReqs suite on an Android emulator)
======
The scenarios are COPIED, not rewritten (tests/CodeBrix.Android.UIReqs.Device/
PORTING.txt: source commit, every adaptation). Two halves:
  tests/CodeBrix.Android.UIReqs.Device   (net10.0-android36.1, com.codebrix.uireqs)
      The scenario app: its XAML application is the copied VirtualApplication;
      Scenarios/ holds the copied feature files (the ONE copy), steps, hooks,
      element factory and canvas vocabulary; Runtime/StepServer listens on
      localhost:47300 and runs each step the host sends IN the app (matched
      against the copied step definitions: cucumber expressions, context
      injection, hooks). Touch goes through Core's own InputInjector; keys are
      REAL Android KeyEvents dispatched to the activity (Core's keyboard
      injection is a stub in the pinned build); a captured frame is requested
      from the host; after every frame the GEOMETRY AUDIT checks each named
      element's native view rectangle against its Core rectangle (within 1 px;
      logcat tag UIReqs.Geometry). Full screen (system bars hidden).
      AndroidFeatures/<Group>/ + AndroidSteps/ are ANDROID-ONLY groups (not
      copies): AndroidInput (real MotionEvents/KeyEvents: a Grid with a
      Background gets pointer events and Tapped, right click, wheel, hover,
      keys and an Alt accelerator on a page), AndroidNative (native
      ScrollViewer scrolling: real drags, ChangeView, wheel, offsets reported),
      AndroidControls (AP3a: the native widget types, real touches and keys on
      them, Core values without echo, the native ComboBox and Image),
      AndroidItems (AP3b: RecyclerView realisation, recycling, native drag,
      selection, ObservableCollection changes, native tab strips) and
      AndroidOverlays (AP4: Material dialogs and menus, bottom sheets, back
      and predictive back; the copied Popups group runs with the Material
      forms OFF, scenarios tagged @native-overlays turn them on),
      AndroidPolicy (AP5, AndroidSteps/PolicySteps.cs: size classes, the
      adaptive NavigationView and ContentDialog, theme keys, palette, night
      mode, type scale, motion; a [BeforeTestRun] hook runs the whole suite
      with the Material palette OFF and MotionMode.AlwaysAnimate, @material-policy
      scenarios turn the palette on; Compact/Medium come from REAL resizes of
      the window - "the real window is resized to N dp wide" asks the host for
      `wm density`, put back after the scenario - one scenario checks the
      size-class override; the NavigationView safe-area scenarios show the
      system bars and hide them again afterwards), AndroidShapes and
      AndroidComposed (AP6, AndroidSteps/ShapesComposedSteps.cs: every Shape
      kind, caps, dashes, Stretch, hit tests, Viewbox, Border; the native
      Expander - "Given native Expanders are used" turns ExpanderHandler.Enabled
      on for one scenario, an [AfterScenario] hook turns it off - ColorPicker
      and the Material date and time pickers), AndroidElements10A (AP10-A,
      AndroidSteps/Elements10ASteps.cs + Elements10ASamples.cs: the remaining
      elements of the first AP10 lane, built by sample name; an [AfterScenario]
      hook puts the native CommandBar switch back on), AndroidElements10B (AP10-B,
      AndroidSteps/Elements10BSteps.cs + Elements10BSamples.cs: the second AP10
      lane, built by sample name; the AndroidElements10A generic steps are shared;
      @native-overlays scenarios let flyouts be Material menus).
      Isolation hook for a single-session run (android-uireqs-run.sh --group
      all: every feature in one app process): AndroidSteps/SoftKeyboardHooks.cs
      hides a soft keyboard a scenario left up (logcat UIReqs.Keyboard). (The
      AP7-M FrameworkBrushGuard is gone: pin 1.0.268.12 fixes the shared-brush
      animation leak in Core, fenced by the copied ProgressBar scenario.)
      The harness waits until the content a step shows IsLoaded before the
      next step (VirtualApplication.SetContentAsync, PORTING.txt (8)).
  tests/CodeBrix.Android.UIReqs          (net10.0, xunit.v3 + Reqnroll, MTP)
      The host runner: Reqnroll over the same feature files (linked); every
      step is forwarded to the device (Steps/ForwardingSteps); while a step
      runs it serves the device's capture requests (adb exec-out screencap,
      raw RGBA, cropped to the app window), "save frame" requests (the
      copied FrameArchive / FrameReview run here) and "shell" requests - a
      whitelist of `wm size` / `wm density` (DeviceSession.IsAllowedShell),
      so a scenario can resize the REAL window; a scenario that resizes
      captures its frames only at the original size (the crop is the window
      size the device reported at hello). A failure's output carries
      the canvas report and the screencap path, as on Platform. Scenarios a
      later phase owns are listed in uireqs-pending.txt (skipped as PENDING,
      never deleted). `dotnet test --solution` runs it too: with no scenario
      app answering on 127.0.0.1:47300 (no device/emulator reachable, nothing
      forwarded) every scenario is SKIPPED with that reason; the scenarios run
      when the runner script drives it (UIREQS_SERIAL set) or an app answers.
Baselines per orientation/group:
~/ClaudeHome/android-buildout-work/uireqs-baseline/<Orientation>/<Group>/.
Tests never reach outside the repository.


IN-REPO APPS
============
samples/HelloPaste, tests/PasteAlways and tests/CodeBrix.Android.UIReqs.Device consume the repository through
build/inrepo/CodeBrix.Android.InRepo.props / .targets, imported by their
folder's Directory.Build.props / .targets with CodeBrixAndroidInRepoConsumer=true.
The pair simulates the package: the consumer buildTransitive files, the
re-shipped Platform build files, Core assemblies and generator from the intake,
CodeBrix.Android.UI as a project reference (Android heads) and the Fluent font
package. Only Android target frameworks get the generator; the net10.0 flavor of
a multi-targeted app library compiles against the same Core assemblies. Add-in
Cores: CodeBrixAndroidInRepoAddIns="FlexPanel;AudioPlayer;CommandBar;AppSettings;TextLayout" (the names are listed in
build/inrepo/CodeBrix.Android.InRepo.targets; an Android head also gets each named add-in's Android assembly).
samples/HelloPaste uses Svg (the JustBetweenUs page's SVG icons).
A head's RootNamespace must differ from its app libraries' (the generator names
per-assembly types after it; the paste-always heads use <App>.PasteAlways).


PACKAGING / PUBLISHING
======================
Every package id carries the .ApacheLicenseForever suffix. Packages are packed
from nuspecs (a csproj pack would turn project references into package
dependencies). Package gates: no dependency on an id in
build/platform-repo-package-ids.txt; every assembly in the TFM folder an
Android app selects; dependency diff reviewed. The packable projects carry the
family's date-stamped version block. Jeremy publishes.


CODING CONVENTIONS
==================
  * .cs layout: no leading blank line; (ported files only) the provenance
    header; the using block (System.* first, alphabetical, aliases last, no
    global usings); file-scoped namespace; no `?` on reference types; no `!`.
  * Mono.Android types inside `namespace CodeBrix.Android.*` are written
    `global::Android.…` or through a using alias.
  * XML doc comments on every public member (CS1591 fixed at source).
  * InternalsVisibleTo.cs at each library root, granting its .Tests project.
  * Source organized into sub-folders.


PROVENANCE / VENDORED SOURCES
=============================
Ported .NET MAUI files (MIT) start with
  // Derived from .NET MAUI, <path> @ 828569a864. Copyright (c) .NET Foundation and Contributors.
  // Licensed under the MIT License. See THIRD-PARTY-NOTICES.txt.
Files derived from the upstream project of CodeBrix.Platform (Apache 2.0) get
an Apache provenance header naming the source path and tag, and the
"//was previously:" marker on renamed namespace/type lines. Each derived file
is listed in THIRD-PARTY-NOTICES.txt. The re-shipped Platform binaries carry
the Platform packages' own notices (artifacts/intake/<version>/notices/).


NOTES
=====
  * The intake's isolated packages folder and extraction folder live under
    artifacts/ (git-ignored). Deleting artifacts/ is always safe; the next
    build re-creates it from the feed / nuget.org.
