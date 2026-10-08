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
     the pinned Platform packages from nuget.org (the pin names a published
     Platform build). A local folder of .nupkg files is the exception, used only
     to test against a Platform build that is not on nuget.org yet.


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
                                build logic and packaging), Build/test-scripts,
                                Samples, Templates, Tests.
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
      nuget/                    the packaging: CodeBrix.Android.Pack.proj (the
                                pack driver), the framework package's nuspec,
                                CodeBrix.Android.PackInfo.targets, pack-shim/,
                                package-dependency-owners.txt (see PACKAGING /
                                PUBLISHING).
      pack.sh                   build, pack and gate every package (Linux).
      inrepo/                   CodeBrix.Android.InRepo.props / .targets: make
                                an in-repo app or app library consume the repo
                                exactly as a package consumer would (see
                                IN-REPO APPS).
      test-scripts/             paste-always-compile.sh, parity-score.sh,
                                device-smoke.sh (see TESTING).
    src/                        the CodeBrix.Android assemblies (one folder per
                                assembly; sub-folders per area; see SOURCE
                                LAYOUT). src/Directory.Build.props adds the
                                Core references, the intake build-order
                                reference and XML doc generation.
    tests/                      test projects; tests/PasteAlways/ holds the
                                paste-always compile heads (not in the slnx).
    tools/                      repository tools: UIReqsFrameCompare and
                                CodeBrix.Android.ParityScore (see TESTING).
    samples/                    HelloPaste (in the slnx); SimpleDebugApp_API_36/
                                _37 are standalone (see EXTRAS-README.txt).
    templates/                  the Android head of the CodeBrix.Platform
                                application template (AndroidHead/) and
                                TEMPLATE_INTEGRATION.md: what CodeBrix.Develop's
                                template and the application skill need.
    artifacts/                  build output of the intake, the paste-always
                                and device-smoke logs, the parity reports
                                (artifacts/parity/<pin>/<config>/; git-ignored).

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
     Sources: nuget.org - the normal case, the pin names a published Platform
     build. The exception, for testing an unpublished Platform build only: the
     folder feed named by the CODEBRIX_PLATFORM_FEED environment variable
     (MSBuild property CodeBrixPlatformFeed; default
     ~/ClaudeHome/android-feed/<version>/), used when that folder exists. Such a
     feed is an immutable copy of a Platform local feed; never write into it,
     and never pack a release from a pin that is not on nuget.org.
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
  1. Wait until the new Platform build is on nuget.org (check the v3 flat
     container for every package the intake takes). Only to test an
     unpublished build ahead of its publication, make its feed folder
     available as described under INTAKE (unset it again before packing).
  2. Change CodeBrixPlatformVersion (and, if it moved,
     CodeBrixPlatformSkiaSharpViewsVersion) in build/PlatformPin.props.
  3. Run the intake. Review every gate error: new add-in Core splits add
     lib/ and notices/ entries to gates/expected-files.txt; new Core
     references may need gates/allowed-references.txt (only for packages that
     are allowed and not produced by the Platform repo); new add-in grants go
     into gates/ivt-grants.txt; refresh build/platform-repo-package-ids.txt.
     A gate 5 error (a fingerprinted seam type or member is missing) means the
     Platform build changed a seam the Android code calls: never delete the
     line to pass the gate - adapt the Android code to the new seam (or have
     the Platform keep it) and change gates/seam-fingerprint.txt in the same
     change. A NEW seam the Android code starts to use gets its lines there
     too, so the next pin cannot drop it silently.
  4. Rebuild and test everything, Debug and Release.
  5. Re-run the device gates: paste-always, the HelloPaste smoke (Debug and
     trimmed Release), the full UIReqs suite in both orientations (strict 0
     against the baseline; re-baseline only frames a named Core change
     explains), and move every pending scenario the new build unblocks.
  6. Bump the dependency pins in Directory.Packages.props that the new
     Platform build moved (SkiaSharp, HarfBuzzSharp and the CodeBrix libraries
     the add-ins depend on), each to a version that exists on nuget.org, then
     pack (PACKAGING / PUBLISHING).


SOURCE LAYOUT
=============
One project per Core InternalsVisibleTo grant (the AssemblyName is the granted
name):
    CodeBrix.Android                 WinRT-surface contracts (application data,
                                     globalization, graphics imaging) and the
                                     WinRT registry extensions (analytics info,
                                     system theme, display information); the
                                     app-package files contract over the APK's
                                     AssetManager (AP1.12: ms-appx:/// reads;
                                     Portable/PackageFileLookup, host-free
                                     tested).
    CodeBrix.Android.UI.Dispatching  the Looper dispatcher pump; multi-targets
                                     net10.0 (HostFree/ only: a managed pump the
                                     host-free tests drain).
    CodeBrix.Android.UI.Composition  the INERT composition platform (Core keeps
                                     its Visual tree, nothing paints it) and the
                                     android.graphics.Path composition geometry;
                                     multi-targets net10.0 (the inert platform,
                                     Portable/ and HostFree/ only).
    CodeBrix.Android.UI.Toolkit      IElevationPlatform (stub until handlers);
                                     TriPaneViewEntryPoints: the door into the
                                     TriPaneView's internal seams - the
                                     divider's platform drag entry points and
                                     the display override (AP1.12; used by UI's
                                     Handlers/Toolkit).
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
                   Handlers/Overlays/ToolTipMapping), AutomationProperties.
                   Name / AutomationId (AP9-3: the content description / string
                   tag of the handler's AccessibilityView - the native view, or
                   the focusable widget a handler hosts: the Material button,
                   the text field's editor, the overlaid widget; applied at
                   connect after every mapper and at every change the Core seam
                   delivers; a cleared name clears it and OnAutomationNameCleared
                   lets a handler that labels its widget put its label back;
                   such labels use Views/Portable/AutomationText so the app's
                   name wins) - never Width/Height/
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
  Handlers/Toolkit/ (AP7-B) TriPaneViewHandler: the Toolkit TriPaneView keeps its
                   template and Core's engine; the handler applies the adaptive
                   form (AdaptivePolicy.TriPane: one pane / side + one stacked /
                   three panes) as the engine's DISPLAY OVERRIDE (AP1.12, WPE1-13
                   ITriPaneDisplayOverride) - Portable/TriPanePlan returns the
                   weights to display, zero for the regions the window has no room
                   for (the engine lays them out minimized with their restore
                   grips; a grip tap calls the plan's Restore, which switches
                   panes); the app's percent and IsMinimized properties are never
                   written - and takes a finger or stylus within the 48-dp touch
                   target of a divider natively (TriPaneViewLayout.
                   OnInterceptTouchEvent; Core gets a cancel), raising the
                   divider's RaiseDrag*FromPlatform entry points (AP1.12) through
                   CodeBrix.Android.UI.Toolkit's TriPaneViewEntryPoints (only the
                   Toolkit names get Toolkit.Core's internals; UI.Toolkit cannot
                   reference UI, so the handler lives here). A mouse stays Core's.
                   Switch: AdaptivePolicy.AdaptiveTriPaneView (AppContext
                   CodeBrix.Android.UI.AdaptiveTriPaneView). Host-free tests:
                   tests/CodeBrix.Android.UI.Toolkit.Tests (the plan against the
                   Core engine); device: the AndroidToolkit UIReqs group.
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
text box focused by touch, as WinUI's touch keyboard. SOFT-INPUT ADJUST (AP8-S
item K, MAUI's model): CodeBrixActivity.ApplySoftInputAdjust sets the window's
adjust bits from CodeBrixApplication.SoftInputAdjust (when the app set it), else
from the activity's declared adjust mode, else AdjustPan (Portable/Input/
SoftInputModePolicy, host-free tested); in adjustResize the window wrapper feeds
the settled IME inset to IRootElement.ContentBottomOcclusionInset (the Platform's
FrameBuffer keyboard seam: Core withholds it from every root but the popup root),
otherwise 0. Edge to edge, Android itself never resizes the window, so Resize is
Core's layout; Pan is the window manager's pan (GetLocationOnScreen includes it;
Core's coordinates do not). Fence: the Android-only UIReqs group AndroidSoftInput.
Pointer icons follow
Core's cursor (PointerIcons). Core drops a wheel event from a mouse it has not
seen hover: real mice always hover first.
CodeBrixRootLayout has DefaultFocusHighlightEnabled = false: it holds the Android focus for Core-focused elements,
and out of touch mode (after a navigation key through the system) Android would draw its grey focus highlight over
the whole window (AP7-B TerminalView fix; fence AndroidTerminal). The same holds for the root view group
TextBoxHandler.ParkFocus hands the focus to, and for any future view that takes the Android focus for Core.
Pointer positions are measured from the content layer's position relative to the
DECOR VIEW (the activity receives decor-view coordinates): with the soft keyboard up
on a window that shows the status bar, Android lays the decor view out below the
status bar inside the window (AP7-B TerminalView fix; fence AndroidTerminal).
CUSTOM TEXT-ENTRY CONTROLS (Input/TextInput/, AP7-B): the Core controls that are
typed into but are not a TextBox (TerminalView, AdvancedTextEdit) report their focus
through CodeBrix.Platform's SoftwareKeyboardFocus seam; CoreTextInputController (the
registered ITextInputFocusNotificationsSingleton) then opens a session on the
activity's CoreTextInputView (a 1x1 view in the root layout's FocusLayer that takes
the Android focus and is an editor while a session is open). It does NOT show the
keyboard for the focus (AP9-4): a finger or pen tap does - the TextBox's rule and
WinUI's (Portable/TextInput/SoftKeyboardPressRule, host-free tested). Press and release
handlers (handled events too) sit on the focused control AND on the window's root
element (AndroidXamlRootHost.AttachRootView -> WatchRoot, so the first tap of a window
is seen): a press on the focused control shows it; a focus that follows a finger/pen
release on that control within 500 ms shows it (TerminalControl focuses itself on
Tapped, after the release, with FocusState.Pointer - the state its GrabFocus() also
gives, so FocusState cannot tell a tap from a call); one show per tap; a mouse never.
InputPane.TryShow while a session
is open shows it on the session's view (CoreTextInputController.TryShowForInputPane).
No hardware-keyboard detection: the input method decides. OpenCount counts sessions
(the UIReqs IME-reset hook reads OpenCount + ShowCount).
Its CoreTextInputConnection (a BaseInputConnection over one editable the view owns)
turns committed text, a finished composition and "delete surrounding text" into
KEY PRESSES raised in Core through the window's keyboard source
(AndroidKeyboardInputSource.InjectSoftwareKey; Portable/TextInput/TextInputKeystrokes:
the Platform software keyboard's key table, one Enter per line break, Backspace/Delete
per deleted character); keys the IME sends as key events take the ordinary key path.
A composition stays in the editable until committed. An add-in registers its
control's CoreTextInputProfile (EditorInfo: suggestions or not, multi-line or not)
with CoreTextInput.RegisterProfile from its module initializer; the default profile is
a multi-line text editor with suggestions. Hardware keys never pass through the view
(Core gets every key first; the view is not a native editor for ActivityInputRouter).
CARET (AP8-S item L): Android's adjustPan brings the FOCUSED view's rectangle above the keyboard (and only a rectangle
inside that view's visible bounds), so while a session is open CoreTextInputView is laid out ON the control's caret
(FrameLayout margins in the focus layer: Portable/TextInput/CaretPlacement, host-free tested) and follows it (the
caret's Moved event, the element's LayoutUpdated; one placement per looper turn; OnLayout asks for the rectangle on
screen again so the pan follows a caret that moved while the keyboard is up). An add-in registers its control's
caret (CoreTextInput.RegisterCaret(type, control => ICoreTextInputCaret: Element, TryGetBounds in the element's DIPs,
Moved)); without one the view stays parked 1x1 at the origin (no pan). AdvancedTextEdit registers TextAreaCaret
(Caret.CalculateCaretRectangle minus the TextView's scroll offset); TerminalView registers Input/TerminalCaret (the
Core's internal platform seam TerminalControl.GetCaretRectForPlatform / CaretRectChangedForPlatform, WPE1-18: the
cursor cell in control DIPs, Rect.Empty while the program hides the cursor or its line is scrolled out; intake gate 5
fingerprints both). A control that reports its focus while it is not on the page gets no session: not loaded AND its
parents do not reach its XamlRoot's content (the live-tree rule - it also covers a control whose Unloaded the
controller never saw). Fence: AndroidSoftInput.
TEXT TARGET (AP7-B AdvancedTextEdit): a control whose text the keyboard may SEE registers a target factory
(CoreTextInput.RegisterTarget(type, control => ICoreTextInputTarget)); the session then gives
CoreTextInputView.TargetEditor (Portable/TextInput/TextInputTargetEditor, host-free tested) and the connection
works on the control's own text instead of its empty editable: getTextBefore/AfterCursor, getSelectedText,
getCursorCapsMode, commitText, setComposingText/Region, finishComposingText, deleteSurroundingText(InCodePoints),
setSelection, performContextMenuAction (select all/cut/copy/paste), batch edits. ICoreTextInputTarget (UTF-16
offsets): TextLength, SelectionStart/End, GetText, CanEdit (read-only ranges), Type (the control's own typing path,
returns the caret), Replace (raw), Select, ShowComposition (the underline), Perform, BeginBatch/EndBatch (one undo
group per input-method call, never held across looper turns), Changed(Selection|Text|Reset), IDisposable (the
session disposes it). The editor types a composition that grows at the caret (and a commit that replaces the
selection) through Type, so the control's text-input events fire; anything else is a raw Replace. Changes made by
anything else end a composition; the view reports to the input method on the NEXT looper turn, coalesced
(UpdateSelection with the composing region, or RestartInput for a whole new text: a restart makes the input method
call finishComposingText synchronously, which must not happen inside the control's change event).
CoreTextInputView.ReportsReachInputMethod = false is the UIReqs switch for scenarios that drive the connection
themselves (the device's real input method would end a composition it did not make). Without a target (the terminal)
the key-press path above is unchanged.
Unfocus (or the focused control's Unloaded) closes the session on the next looper turn unless another custom control
took the focus; focus never summons the keyboard (programmatic, keyboard, a page's first
focus, the same control focused again), a finger/pen press on the control does (a mouse
press does not). Fences: AndroidTerminal, AndroidAdvancedTextEdit, AndroidSoftInput.
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
IDeviceFamilyPlatform: DeviceFamily "Android.<form>" - decision D2, ruled by
Jeremy 2026-09-26: the form comes from the window width size class - Compact
"Android.Mobile", Medium "Android.Tablet", Expanded "Android.Desktop" (the
television / car / watch / VR-headset UI mode types keep those forms; desk mode
and the PC feature no longer force Desktop) - and is read at query time:
Portable/DeviceFormClassifier maps it, Android/AnalyticsInfoAndroidExtension
asks the size-class service (WindowSizeClassMonitor.CurrentWindowWidthDp,
override-aware, installed by the UI bootstrap) and falls back to
Configuration.ScreenWidthDp. AnalyticsInfo.DeviceForm is therefore live on a
docked phone. VersionInfo.DeviceFamily is live too since pin 1.0.270.342
(WPE1-11: Core reads it at query time while IDeviceFamilyPlatform is
registered); fence: AndroidPolicy "The device family names the form of the
window's current size class". CodeBrix.Mobile will use "AppleMobile.<form>" on Apple
devices; Android/DeviceFamilyAndroidPlatform) and sets the
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
Since pin 1.0.268.65 (WPE1-6) Core opens a Popup that was set open before it could
reach a XamlRoot (IsOpen="True" in XAML, opened and then added, or parentless with a
late XamlRoot) when it loads or when its XamlRoot is assigned; Opened is raised when
it is shown. No Android code re-toggles IsOpen, defers it to Loaded or sets XamlRoot
to force an open. A parentless Popup still needs its XamlRoot set (it stays closed
and logs one warning otherwise; D12 is the owner's). Fenced by the copied
Popups/Popup.feature scenarios 4-6.
OverlayPresentation switches the Material forms off (the UIReqs device app does so
for every scenario not tagged @native-overlays); PlatformOverlays lists what is shown
natively - INTERNAL (no new public API before D-O1), read by the UIReqs device app
through InternalsVisibleTo and by the HelloPaste sample's self-check by reflection
(no library grants InternalsVisibleTo to a sample app). AcrylicBrush (and every
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
ThemeKeyMap = the re-keyed corpus keys with their path (and which are honored);
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
InvalidateRender -> Poke); the per-frame decision (one raise per display frame,
next frame only while subscribers remain) is Platform/Animation/Portable/
RenderingFramePump, fenced host-free (HostFree/RenderingFramePumpTests) and on the
device (AndroidNative "CompositionTarget.Rendering ticks while subscribed and stops
when not"; AP10-C); MotionPolicy.AlwaysAnimate drives frozen native
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
    AP7-B: the engine itself needs nothing else from Android. Its first layout loads the
    device's ICU (API 31+: the NDK-stable libicu.so of the i18n module, whose exports are
    versioned, e.g. ICU 78 on the API 37 agent AVD) and binds bidi + line breaking to it;
    Android/TextEngineProbe (diagnostics, UIReqs fences) lays text out through the public
    TextLayoutEngine and reads the engine's ICU state (Portable/TextEngineIcu names the Core's
    private members; a host-free test pins them against the intake's TextLayout.Core). Fences:
    the copied TextLayout group (Scenarios/Features/TextLayout) and
    AndroidNative/EngineContracts "The text engine finds the device's ICU ...".
  Lottie (AP7-B): the Core (CodeBrix.Platform.UI.Lottie.Core) is the whole player - the
    animation sources, JSON loading, and the engine (Engine/LottiePlayer over Skottie: play
    state, stopwatch frame clock, segments/loop, stretch, colour theming). Since AP1.12 (WPE1-13)
    the ticks come from the Android frame clock, ILottieTickSourcePlatform
    (Android/ChoreographerTickSource: Choreographer frame callbacks, every display frame whose
    time is at least the engine's interval after the last tick - Portable/FrameTickGate,
    host-free tested); which animation frame is drawn stays the engine's stopwatch. The
    Android side is otherwise only the CANVAS SUPPLY, ILottieCanvasPlatform
    (Android/LottieCanvasAndroidPlatform), registered from the module initializer (Lottie.Core
    loads CodeBrix.Android.UI.Lottie by name): Android/LottieCanvasElement is a leaf element whose
    visual comes from the SkiaSharp.Views canvas-host factory (SKCanvasVisualBaseFactory, as the
    seam element does; no Graphics2DSK dependency), shown by that add-in's SkiaCanvasElementHandler
    (a native SkiaCanvasView that runs the Core's render callback on each draw); PaintCount is
    for fences. The animation-source provider (ILottieVisualSourceProvider) is NOT registered
    here: the Core declares it with an ApiExtension attribute and an app's generated App code
    registers it (a second registration throws); Android's ProgressRing is native and does not
    use it. Package: SkiaSharp.Skottie (native code in SkiaSharp's library). An ms-appx:///
    document loads from the APK's assets (since AP1.12: CodeBrix.Android's
    IApplicationPackageFilesPlatform over the AssetManager; fence AndroidNative/EngineContracts
    "A Lottie document named by an ms-appx URI loads from the app's assets"). Fences: the copied Lottie
    group (Scenarios/Features/Lottie; its two ProgressRing scenarios are pending: the native ring)
    and AndroidNative/EngineContracts "A Lottie animation is drawn on the Android canvas supply".
  TerminalView (AP7-B): the Core (CodeBrix.Platform.UI.TerminalView.Core) is the whole terminal -
    TerminalControl (its code-built template, scroll bar, context menu, timers, focus, clipboard)
    over the engine (Engine/TerminalRenderer: the CodeBrix.Terminal buffer, selection, palette,
    font, blink, Paint(SKCanvas), fit/measure, hit test, gestures; Engine/TerminalInputEncoder: the
    modifier tracking, the chords and the VT encoding). The Android side is the CANVAS SUPPLY,
    IRenderCanvasPlatform (Android/RenderCanvasAndroidPlatform), registered from the module
    initializer (TerminalView.Core loads CodeBrix.Android.UI.TerminalView by name), plus the
    terminal's soft-keyboard profile (CoreTextInputProfile.Terminal: visible-password layout, no
    suggestions, no full-screen editor; see INPUT, CUSTOM TEXT-ENTRY CONTROLS).
    Android/TerminalCanvasElement is a childless Canvas (the Skia heads' RenderCanvas is a Canvas
    and the copied steps find the surface by that type) shown by Android/TerminalCanvasHandler: a
    leaf SkiaCanvasView of the SkiaSharp.Views add-in that runs the paint handlers on each draw
    (one unit = one DIP, clipped), redrawn on Invalidate (any thread: PostInvalidate) and on every
    re-arrange; PaintCount/InvalidateCount are for fences. Packages: CodeBrix.Terminal and the
    RobotoMono fonts (the Platform package's versions); it references the TextLayout add-in (the
    cell is measured and the runs laid out with its engine). Fences: the copied TerminalView group
    (Scenarios/Features/TerminalView) and the Android-only AndroidTerminal group (the
    canvas supply, the soft-keyboard session and connection, the keyboard-up touch mapping).
  AdvancedTextEdit (AP7-B): the Core (CodeBrix.Platform.UI.AdvancedTextEdit.Core) is the whole editor -
    the control and its TextArea (caret, selection, input handlers, commands, undo), the document model,
    highlighting, folding, completion, search, and the renderer (TextView's visual lines laid out by the
    TextLayout engine, the margins). The Android side is the CANVAS SUPPLY, IRenderCanvasPlatform
    (Android/RenderCanvasAndroidPlatform: one Android/EditorCanvasElement per surface - the TextView's child 0
    and one per margin - shown by Android/EditorCanvasHandler, the same leaf SkiaCanvasView pattern as the
    TerminalView surface), registered from the module initializer (AdvancedTextEdit.Core loads
    CodeBrix.Android.UI.AdvancedTextEdit by name), plus the TextArea's soft-keyboard session: profile
    CoreTextInputProfile.Editor and the TEXT TARGET Editing/TextAreaInputTarget (see INPUT, TEXT TARGET) with
    Editing/CompositionUnderline (an IBackgroundRenderer on the TextView's selection layer, the Core's own
    extension point). Selection UI = the Core's own (drag selection, the selection brush); no native handles.
    Fences: the copied AdvancedTextEdit group (Scenarios/Features/AdvancedTextEdit) and the
    Android-only AndroidAdvancedTextEdit group (canvas supply, the session and its text target, composition,
    autocorrection, re-composition, delete/line break, selection, a hardware key during a composition, read-only,
    focus away, a real finger tap with the keyboard up, a real finger drag selection).
  PlotterView (AP7-C): the Core (CodeBrix.Platform.UI.PlotterView.Core) is the whole chart control - PlotterControl
    over Engine/PlotHost (model attach, painting of the model, zoom rectangle and tracker, CodeBrix.Plotter's controller
    for keys, mouse, wheel and touch: its default touch binding pans, pinch-zooms and tracks). The Android side is the
    CANVAS SUPPLY, IRenderCanvasPlatform (Android/RenderCanvasAndroidPlatform: one Android/PlotterCanvasElement - a
    childless Canvas - per chart, shown by Android/PlotterCanvasHandler, the TerminalView leaf SkiaCanvasView pattern),
    registered from the module initializer (PlotterView.Core loads CodeBrix.Android.UI.PlotterView by name). Fingers,
    the mouse and the wheel reach the control through Core's own pointer path (the input router); the typefaces come
    from IFontSourcePlatform<SKTypeface>, which the TextLayout add-in registers (PlotterView.Core loads that assembly
    by name too; the add-in references it). Fences: the copied PlotterView group and the Android-only
    AndroidPlotterView group (the canvas supply; a REAL one-finger pan, two-finger pinch, held finger and mouse wheel
    with the default binding). The host tests drive the engine's touch path (tests/..PlotterView.Tests/Engine).
  AudioPlayer (AP7-C): the Core (CodeBrix.Platform.UI.AudioPlayer.Core) holds the AudioPlayer element, its transport
    (Engine/AudioTransport), SoundEffect and the source resolver. The Android assembly registers, from its module
    initializer (after CodeBrixAndroidAudio.Initialize - CodeBrix.Audio.Android, the Android backend of CodeBrix.Audio
    with the same external API): IAudioPlayerPlatform (Android/AudioPlayerAndroidPlatform, an AudioFilePlayer per
    element), IAudioOutputPlatform (Android/AudioOutputAndroidPlatform: SoundEffectClip voices + the Opus failure
    explanation) and IAssetLocation (Android/AssetLocationAndroidPlatform: the ms-appx root is AndroidPackagedAssets'
    copy folder; Android/PackagedAudioAssets copies an asset out of the APK when a player opens it by path, with its
    folder for an SFZ / Decent Sampler preset - Portable/PackagedAssetPaths, host-tested). These are the Platform's
    Skia-side output files ported with the package swapped (provenance headers). MidiPlayer is NOT in the Core (its
    API is made of CodeBrix.Audio types): Public/MidiPlayer.cs re-provides it, ported (ANDROID PORT (1)-(3): ms-appx
    copy-out; UI.Core's internals are visible here, so three PropertyMetadata callbacks are typed and IsLoading's
    hiding is acknowledged). Known gap: SoundEffect (Core) reads an ms-appx source with File.ReadAllBytes at the
    resolved path before any Android code runs, so an ms-appx sound effect is found only once something copied it
    out (FIXLIST [AP7-C]; a Core change: read ms-appx through IApplicationPackageFilesPlatform). Fences: the copied
    AudioPlayer group (informational for frames) and AndroidAudioPlayer (the platforms, an ms-appx
    clip copied out and played, a sound effect, the add-in's MidiPlayer with an ms-appx SFZ); the AndroidElements10B
    MidiPlayer scenario (restated at AP7-C).
  VideoPlayer (AP7-C): the Core (CodeBrix.Platform.UI.VideoPlayer.Core) holds only the source resolver (over
    IAssetLocation), the rules and the failure args - the ELEMENT's public API names CodeBrix.VideoPlayback and
    SkiaSharp types, so the Platform keeps it in its Skia assembly. The Android assembly re-provides it: Public/
    (VideoPlayer, IVideoLayer, VideoComposingEventArgs, VideoPlayerRenderPathChangedEventArgs) and Internal/ (the
    presenter, the render driver, the YUV renderer, the surface element) are the Platform's Skia-side files ported
    with provenance headers; ANDROID PORT changes: an ms-appx source is copied out of the APK before it is opened
    (Android/PackagedVideoAssets), and the GPU-composed frame is read back as RGBA (OpenGL ES has no guaranteed BGRA
    read; the BGRA read failed with GL_INVALID_ENUM and presented nothing). The surface element paints through the
    SkiaSharp.Views canvas-host factory and is shown by that add-in's SkiaCanvasElementHandler (registered in the
    bootstrap); the GPU path is the Graphics3DGL add-in's SkiaGpuContext (EGL / OpenGL ES); the sound is
    CodeBrix.Audio's shared output on CodeBrix.Audio.Android (the bootstrap initialises it). One CodeBrix.Audio.Core
    line: CodeBrix.VideoPlayback 1.0.280.1149 and CodeBrix.Audio.Android 1.0.271.1201 both depend on
    CodeBrix.Audio.Core.MitLicenseForever 1.0.271.1165 (never the desktop CodeBrix.Audio package's own assemblies).
    Fences: the copied VideoPlayer group (informational for frames) and AndroidVideoPlayer.
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
  app): ListPickerFlyoutPresenter, PickerFlyoutPresenter. Since pin 1.0.270.342 TitleBar,
  ListBox, ListBoxItem (WPE1-10) and GroupItem (WPE1-8) are Core controls on their Core
  templates (no native mapping), and grouped ListView/GridView (Fluent template) show a
  ListViewHeaderItem / GridViewHeaderItem per group. UIReqs: AndroidFeatures/AndroidElements10A.

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
    Path / the fallback icon; Core never builds a visual from an AnimatedIcon's Source - the
    Lottie add-in does not change that).
  Scroller/: ScrollView keeps its template; its ScrollPresenter's view is CodeBrixScrollView;
    native scrolling is reported through ScrollPresenter.ScrollTo (no animation), Core
    scrolls (ViewChanged) move the view. ColorParts/: a stand-alone ColorSpectrum is AP6's
    ColorSpectrumView (a pick sets HsvColor; Core raises ColorChanged).
  Not mapped (NotImplemented in the Platform): RichTextBlock, RichTextBlockOverflow and
  RichEditBox (every member), Block / Paragraph / Glyphs / InlineUIContainer, Hub,
  HubSection, SemanticZoom, ParallaxView, AnnotatedScrollBar, MapControl, SwapChainPanel,
  SwapChainBackgroundPanel, the legacy WebView. Not in the packages: Popover, MapPresenter.
  MidiPlayer is the AudioPlayer add-in's (AP7-C). SKSwapChainPanel keeps the refusal
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
  * report the CBAND diagnostics (plan section 3.3, decision D-P12): the
    package's analyzer (analyzers/dotnet/cs/CodeBrix.Android.Analyzers.dll,
    src/CodeBrix.Android.Analyzers; netstandard2.0, Microsoft.CodeAnalysis.CSharp
    5.0.0 so every .NET 10 SDK loads it) reports, as WARNINGS, the constructs
    Android accepts and ignores: CbandCSharpAnalyzer in hand-written C#,
    CbandXamlAnalyzer in the Page / ApplicationDefinition XAML (the additional
    files the XAML generator's build logic passes to the compiler), at the XAML
    file and line. CBAND0001 template on a native control, 0002 template members
    on a native control, 0003 composition / backdrops / ThemeShadow, 0004 3-D
    projection, 0005 ScrollViewer zoom, 0006 PasswordChar not one character,
    0007 frame-buffer head options, 0008 acrylic / Mica. The ids are appended to
    WarningsNotAsErrors (CodeBrixAndroidCbandIds), so they never fail a build
    with TreatWarningsAsErrors; CodeBrixAndroidXamlScan=false turns the XAML scan
    off. The native-control list the rules use (AndroidSurface.NativeControls)
    follows the native registrations (CodeBrixHandlers.cs and the add-ins);
    keep them in step. In the repo, build/inrepo/CodeBrix.Android.InRepo.targets
    references the analyzer project (as the package's analyzers/ folder would);
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
        line per app; PASS = unchanged files, 0 warnings other than CBAND, 0
        errors. CBAND warnings are counted, never failures: the CBAND column,
        <log dir>/cband-counts.tsv (per app and id) and cband-findings.txt (file
        and line of each). Heads: every page of every Apache CodeBrix.Samples
        corpus app (one head per app; an app with a second main-page variant
        has a second head).
        The heads never write an XML doc file (tests/PasteAlways/
        Directory.Build.targets removes the doc item the Android SDK's
        Bindings.Core.targets adds after GenerateDocumentationFile=false), so
        verbatim sample sources are compiled as the desktop builds compile them. An add-in with an Android flavor is the real
        add-in in every head that uses it (CodeBrixAndroidInRepoAddIns); a
        Surface/ library that needs an add-in's Core sets the same property
        (a rendering library that needs Graphics3DGL, for example). Each
        head's Surface/SOURCES.txt says which of its files are verbatim and
        which are stubs.
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
        START-WAIT: a start that follows a stop by seconds first waits (up to
        60 s) until no emulator process of this AVD is alive and its ports are
        free - a start on the heels of a stop could hang in the cold boot.
        If a cold boot still hangs (boot timeout, exit 1), stop the AVD and
        start it ONCE more; a second hang is a failure to report, not to
        retry again.

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

    OTHER ADB DEVICES ATTACHED (a developer's phone or tablet): the device
        scripts address ONLY their own serial. android-uireqs-run.sh and
        device-smoke.sh export ANDROID_SERIAL=<serial> (adb without -s never
        reaches another device), read the AVD's ABI (ro.product.cpu.abi) and
        install with -p:RuntimeIdentifier=android-x64 (or android-arm64) next to
        -p:AdbTarget, then check `pm dump <package>` primaryCpuAbi equals it
        (exit 2 otherwise). Why: a Debug build WITHOUT AdbTarget (a plain
        solution build) asks the DEFAULT adb device for its ABI and packs only
        that ABI; the next -t:Install finds that APK up to date, and an
        arm64-only APK on the x86_64 AVD aborts at start ("No assemblies found
        ... Fast Deployment"). paste-always-compile.sh (compile only) exports
        ANDROID_SERIAL=emulator-5600 unless set. Run solution builds with
        ANDROID_SERIAL=emulator-5600 too.

    build/test-scripts/parity-score.sh [-c Debug|Release]
        The parity score, published per build (run it after the solution build):
        tools/CodeBrix.Android.ParityScore (written on CodeBrix.AssemblyTools;
        static IL reading, nothing is loaded) reads artifacts/intake/<pin>/lib/
        *.Core.dll and src/**/bin/<config>/net10.0-android36.1/CodeBrix.Android*.dll
        and writes artifacts/parity/<pin>/<config>/: parity-summary.txt (both
        scores on one page), parity-notimplemented(.tsv, -members.tsv) = (a) the
        NotImplemented members per public Core type (marked NotImplemented for
        the Core, or a body that throws NotImplementedException or raises
        ApiInformation.TryRaiseNotImplemented), parity-declined(.tsv,
        -properties.tsv) = (b) per native (element, handler): the public
        DependencyProperties declared on the element below FrameworkElement that
        the handler's mapper (with its chain, helpers and the policy layer's
        Append/Modify/ReplaceMapping calls) maps, that
        tools/CodeBrix.Android.ParityScore/declined-explained.tsv explains, and
        the rest = declined; the UIElement + FrameworkElement properties once, in a
        base row; parity-templated.tsv = registrations served only by the
        templated fallback or Core. Add an explained line only for a true,
        written policy; a newly mapped property simply counts as mapped.

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
      injection, hooks). Touch goes through Core's own InputInjector (since pin
      1.0.269.982 a point injected with TimeOffsetInMilliseconds 0 carries the
      real time, so an injected drag ends with its real release velocity and
      flings/flicks behave as a finger's do); keys are
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
      AndroidAccessibility (AP9-3, AndroidSteps/AccessibilitySteps.cs:
      AutomationProperties.Name / AutomationId on the native content
      description and tag, set before the tree and changed or cleared live),
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
      Rendering carry-over (AP1.10): after drawing a native month calendar, strict frames drawn later IN THE SAME APP SESSION can differ from
      the baseline by 1-5/255 on single edge rows/columns (state inside the
      emulator's rendering pipeline; no view, window or composition property
      differs; FIXLIST [AP1.10]). Stability triples therefore run PER GROUP
      (--group "<every group name>": the runner restarts the app for every
      group). The three native CalendarView scenarios now live in their own
      AndroidNativeCalendars group; the templated calendar and Material picker
      dialogs remain in AndroidElements10A. This prevents native calendar
      renderer state from reaching the other scenarios in that group.
      A single-session run is still useful for leak hunting.
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
      never deleted). A copied scenario whose claim runs in an Android-only
      group under the device settings it needs is listed in
      uireqs-rehomed.txt instead (same four columns; the third names the new
      home; skipped as RE-HOMED: nothing is owed, the claim is covered there).
      `dotnet test --solution` runs it too: with no scenario
      app answering on 127.0.0.1:47300 (no device/emulator reachable, nothing
      forwarded) every scenario is SKIPPED with that reason; the scenarios run
      when the runner script drives it (UIREQS_SERIAL set) or an app answers.
ANIMATIONS: the runner sets the system animation scales to 0 for every group
(deterministic frames; native Material indicators and UISettings.AnimationsEnabled
honour the animator duration scale - the ruled policy). The Android-only groups
named in the runner's animator_on_groups (today AndroidAnimatorOn) start with the
animator duration scale at 1 and get 0 back when the group ends (also on every
way out of the runner). Their scenarios begin with "Given the system animations
are on", so a run that did not set the scale fails by name; their frames are
informational (an animation is caught at a timing-dependent phase). A copied
claim that needs animations running (the ProgressRing "is animating" scenario)
is re-homed there through uireqs-rehomed.txt.
HARNESS RULES (what keeps the runs and the saved frames deterministic; the
keyboard mask and the host's request handling are fenced by host-free tests,
tests/CodeBrix.Android.UIReqs/HostFreeTests/ImeMaskTests.cs and
DeviceSessionTests.cs):
  * THE KEYBOARD MASK (tests/CodeBrix.Android.UIReqs/TestTarget/ImeMask.cs):
    the soft keyboard is system UI whose content (suggestion strip, labels,
    clipboard chip) is not deterministic. When the device asks for a capture
    while the IME is visible it sends the IME rectangle in screen pixels; the
    host fills that rectangle, plus ImeMask.ShadowBand rows above it (the
    keyboard's top shadow), with one flat grey in the copy it SAVES (frame
    files, archives). The pixels sent back to the device - which scenario
    assertions and the keyboard warm-up use - are never masked. A frame
    compare inside the masked rectangle is meaningless.
  * THE IME SETTLE WAIT (device TestTarget.WaitForSoftKeyboardSettledAsync):
    before every capture the device waits, bounded (2 s), while a soft-
    keyboard session is open but the keyboard is not yet visible, while a
    session just closed but the keyboard is still visible, or while an IME
    insets animation runs - then captures.
  * THE CARET DRAWABLE (TestTargetSession.InstallCaretHook, called from the
    scenario app's MainActivity): a global-layout listener gives every
    EditText a caret and insertion-handle drawable that draw NOTHING on its
    first layout, before the editor caches the real one (a native caret's
    blink phase cannot be observed at capture time). Never CursorVisible =
    false (the EditText then stops re-positioning its text after a theme
    switch). No scenario asserts a native caret.
  * "ime reset" AND THE KEYBOARD WARM-UP: a scenario that typed through a
    custom control's keyboard session asks the host for `ime reset` (a fresh
    input method, so its state cannot reach later scenarios). A reset input
    method cold-starts and briefly shows a different bottom row, so the
    harness then warms it up: a scratch 1x1 editor shows the keyboard, the
    harness waits until it is visible AND two captures 300 ms apart show the
    same keyboard (bounded), then hides it and waits until it has settled.
  * HOST SHELL WHITELIST: the host runs only `wm size`, `wm density`,
    `input tap X Y` and `ime reset` for a scenario (DeviceSession.
    IsAllowedShell).
  * HOST REQUEST SERIALIZATION: DeviceSession.RequestAsync lets ONE request
    be in flight per session (a SemaphoreSlim; an overlap is logged and
    waits) - the device's capture / save / shell requests are served inside
    the request loop, so overlapping requests would read each other's
    replies. DeviceSession.ReadScreencap reads each screencap into one fresh
    array of the capture's exact size (no shared or grown stream buffer).
  * INFORMATIONAL ENTRIES (build/test-scripts/uireqs-frame-compare.
    informational; its header is the reference): frames that are compared
    and reported but never fail. One entry per line: a whole group, a
    feature as <Group>/<feature>, or ONE frame as <Group>/<feature>/<frame>
    (the frame form only for one frame with a named cause); each covers both
    orientations unless prefixed Landscape/ or Portrait/. Every entry carries
    a comment with its cause. Everything not listed is strict.
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
samples/HelloPaste uses Svg (one of its pasted pages shows SVG icons).
A head's RootNamespace must differ from its app libraries' (the generator names
per-assembly types after it; the paste-always heads use <App>.PasteAlways).


PACKAGING / PUBLISHING
======================
    build/pack.sh [-c Release|Debug] [--no-build] [--version 1.x.y.z]

Builds CodeBrix.Android.slnx, then runs the pack driver
build/nuget/CodeBrix.Android.Pack.proj (`dotnet build
build/nuget/CodeBrix.Android.Pack.proj -c Release` after a solution build does
the same), which writes every package and the gate report to
artifacts/packages/<Configuration>/<version>/ (git-ignored). The driver is not
part of the solution build. On the Android track, set
CODEBRIX_ANDROID_BUILD_LOCK=~/ClaudeHome/android-buildout-work/build.lock so each
dotnet command runs under the track's build lock. Jeremy publishes; nothing in
the repository pushes a package.

PACKING ON ANOTHER MACHINE. A fresh clone packs with the .NET 10 SDK with the
"android" workload, and the Android SDK (with the JDK the workload builds with;
the projects target net10.0-android36.1; global.json pins no SDK version) - no
Android device, emulator or build/test-scripts are needed to pack. The intake
downloads the pinned Platform packages (build/PlatformPin.props) from nuget.org
by itself: the local feed folder (CodeBrixPlatformFeed, default
~/ClaudeHome/android-feed/<version>/) simply does not exist there and is then
not used. Two commands, from the repository
root (or build/pack.sh -c Release on Linux/macOS, which runs both):
    dotnet build CodeBrix.Android.slnx -c Release
    dotnet build build/nuget/CodeBrix.Android.Pack.proj -c Release
The packages and package-gates.txt land in
artifacts/packages/Release/<version>/, at the date-stamped version of that run;
that version is the one to publish and to tag. Nothing in the repository is
edited per pack.

THE PACKAGES. Every id carries the .ApacheLicenseForever suffix (decision D-O13
is Jeremy's; built to the recommendation: the suffix for every package,
MediaPlayer and SkiaSharp.Views included, whose CodeBrix.Platform counterparts
are LGPL / MIT; the MIT notice of the SkiaSharp.Views Core is kept in that
package's notices/ folder).
  CodeBrix.Android.ApacheLicenseForever (the framework), from the hand-written
  build/nuget/CodeBrix.Android.ApacheLicenseForever.nuspec:
    lib/net10.0-android36.1/  CodeBrix.Android, .UI (+ its .aar: the Android
                              resources), .UI.Composition, .UI.Dispatching,
                              .UI.Toolkit (dll, xml, pdb) and the re-shipped
                              framework Cores + CodeBrix.Platform.Xaml (every lib/
                              file the intake took from the CodeBrix.Platform
                              framework package)
    analyzers/dotnet/cs/      the re-shipped XAML source generator, its parser
                              and analyzers, and CodeBrix.Android.Analyzers (CBAND)
    buildTransitive/          CodeBrix.Android.ApacheLicenseForever.props/.targets
                              and, beside them, the re-shipped Platform build files
    notices/                  the CodeBrix.Platform package's
                              THIRD-PARTY-NOTICES.txt and the intake manifest
                              (the SHA-256 of every re-shipped file)
    README.md, AGENT-README.txt, THIRD-PARTY-NOTICES.txt, icon-codebrix-128.png
    dependencies: Material Components, AndroidX SwipeRefreshLayout, the
    Microsoft.Extensions packages and CodeBrix.ServiceLocator the Cores need,
    and the Fluent symbols font package.
  CodeBrix.Android.<AddIn>.ApacheLicenseForever, one per add-in project, from a
  nuspec the driver GENERATES from the project (never hand-edited): the add-in
  assembly (dll, xml, pdb, aar when present) and the Core(s) it owns = the Core
  references of the project, minus the framework Cores, minus the Cores its add-in
  dependencies ship (CommandBar ships CommandBar.Core; Svg ships Svg.Core;
  SkiaSharp.Views ships SkiaSharp.Views.Core), plus the CodeBrix.Platform notices
  of each owned Core. Dependencies: the framework package and every add-in
  project it references, at exactly the run's version ([v]); each
  PackageReference it passes on that the framework does not bring.
  <AddIn> is the project folder without "CodeBrix.Android." and a leading "UI." -
  the CodeBrix.Platform add-in package's name.
The net10.0 flavors of the multi-targeted projects are never packed (tests only).

VERSIONS. One date-stamped version per pack run, stamped on every package: the
family's canonical formula 1.<years since 2026>.<day of year>.<minute of day>
(UTC), computed once in the driver as in the CodeBrix.Platform pack driver
(-p:BuildVersion / --version re-packs an existing version). As CodeBrix.Platform
does, the package version is stamped at pack time only: every packed assembly
carries the stable Version 255.255.255.255 (src/Directory.Build.props and the
analyzer's Directory.Build.props) and the commit in its informational version
(255.255.255.255+<commit>), and the pack takes the built outputs unchanged.
Dependency versions are never written by hand: the nuspecs use
$dep_<Package_Id>$ tokens that the driver fills from Directory.Packages.props,
and the driver stops when the framework nuspec lacks a dependency the framework
projects reference.

PUBLISHING. Jeremy publishes the packages of one pack folder, all at its one
version, to nuget.org: the framework package first, then the add-ins in
dependency order (SkiaSharp.Views, TextLayout, Svg and Graphics3DGL before the
add-in packages that depend on them; package-gates.txt lists every package's
dependencies). Unshipped CBAND rows move to a new analyzer release line before
a published pack (ADDING A PACKAGE); before publishing, check that every external
dependency id and version in package-gates.txt exists on nuget.org. After
publishing: tag the repository with the package version and refresh the
template head's consumers (templates/TEMPLATE_INTEGRATION.md).

THE PACKAGE GATES (build/intake/CodeBrix.Android.IntakeGate, "packages" mode;
run by the driver after packing; any error fails the run; report
package-gates.txt beside the packages, which also lists every package's
dependencies and lib/ files for the dependency review):
  (a) CBAP0001  no dependency on an id in build/platform-repo-package-ids.txt
                (Constraint 1)
  (b) CBAP0002  no Skia twin of a Core in any package (no assembly named like a
                Core without ".Core", no non-Core CodeBrix.Platform assembly under
                lib/, no codebrix-platform-runtime/ folder, no lib/ assembly that
                references a twin); every lib/ file in lib/net10.0-android36.1/;
                every lib/ assembly in exactly one package
  (c) CBAP0003  every dependency id matches build/nuget/package-dependency-owners.txt
                (Microsoft / Xamarin / dotnetframework, CodeBrix.*, SQLitePCLRaw)
Tests: tests/CodeBrix.Android.IntakeGate.Tests/Packages/.

ADDING A PACKAGE
  * An add-in: create src/AddIns/CodeBrix.Android.<AddIn>/ as ADD-INS describes,
    then add ONE line to build/nuget/CodeBrix.Android.Pack.proj:
        <CodeBrixAndroidAddIn Include="CodeBrix.Android.<AddIn>" />
    (optional metadata: PackageName, Description). The driver stops on an add-in
    folder that is neither listed nor in CodeBrixAndroidAddInNotPacked (with a
    reason). Add the package to AGENT-README.txt (ADD-INS ON ANDROID).
  * A new dependency: pin it in Directory.Packages.props; the generated nuspecs
    pick it up; for the framework, add a <dependency> with its $dep_...$ token to
    the framework nuspec (the driver checks). An id outside the owner list needs
    Jeremy's approval before package-dependency-owners.txt gets a line.
  * A new framework assembly: add its files to the framework nuspec.
  * A new CBAND diagnostic: add its row to
    src/CodeBrix.Android.Analyzers/AnalyzerReleases.Unshipped.md (the Roslyn
    release-tracking format; the analyzer build reports RS2xxx otherwise).
    AnalyzerReleases.Shipped.md tracks the ANALYZER's own release line, independent
    of the date-stamped package versions: the first set shipped as "## Release 1.0";
    the next CBAND additions move from Unshipped.md to a new "## Release 1.1" heading
    (then 1.2, ...) before the pack that publishes them, leaving Unshipped.md with
    its header lines only. Never rename a heading to a package version - nothing in
    the repository is edited per pack.

APPLICATION TEMPLATE. templates/AndroidHead/ is the Android head of the
CodeBrix.Platform application template (token TemplateApp, as in the template
archive) and templates/TEMPLATE_INTEGRATION.md lists what CodeBrix.Develop's
template and the codebrix-create-new-application skill need to offer it; both
live outside this repository and are changed by their owner. Debugging (decision
D-O15, Jeremy's): no debugger is named as a requirement; CodeBrix.Develop debugs
.NET 11 Android apps, and a .NET 10 Android app is debugged as .NET 10 Android
apps are today.

HANDLER-AUTHORING API. CodeBrixHandlers.Register and the handler base classes
stay INTERNAL (decision D-O1, Jeremy's): apps cannot register native handlers in
this version.


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
  * The FIXLIST (the maintainer's fix list for this repository's Android
    track: every defect found, fixed-and-fenced items, pending decisions and
    the items for other libraries) is kept outside the repository, in
    ~/ClaudeHome/FIXLIST_codebrix_android_buildout_2026-09-23.txt. The
    "FIXLIST [<work package>]" references in this file and in source
    comments point at its entries. A defect found in another CodeBrix
    library is written there for its owner, never worked around here.

UIREQS ON AN EXISTING DEVICE WITHOUT CONFIGURATION CHANGES
=========================================================
build/test-scripts/android-uireqs-existing.py --serial SERIAL --group Harness
  --orientation both --out /absolute/output/directory

This runner uses an already connected device; it never starts or reconfigures
an AVD. It pins the device ABI for installation, restarts the scenario app for
each group, and asks only the app activity for its orientation. It verifies
the resulting screen orientation and fails if the request was not honoured.
The activity leaves system bars visible in this mode to avoid first-use fullscreen
tutorials that can obscure it. These captures therefore differ from fullscreen
emulator baselines. The clipboard fence requires the activity to own window focus.
Both build locks are held, Platform first. --group all discovers every group
and still restarts the app per group. --repeat 3 performs three runs.

UIREQS_PRESERVE_DEVICE_CONFIGURATION=1 travels in the host handshake. The host
rejects configuration-changing shell requests even if a scenario sends one.
Density-changing scenarios therefore fail; the custom keyboard cleanup omits
IME reset and system corner taps. IME-reset isolation remains UNVERIFIED in
this mode. The normal hook still hides the app's own soft keyboard. Existing
pixel baselines require the same density, display size and animation settings;
a physical-device run is not automatically a baseline validation. The runner
records the existing settings, without writing them. Uninstall the scratch
com.codebrix.uireqs app after a physical-device validation session.

Clipboard captures now wait for a quiet period after PrimaryClipChanged.
The harness does not read clipboard data, suppress the system overlay or
change a device setting. It waits ten seconds from the most recent copy,
extended by Android's recommended accessibility timeout, before requesting
the next frame. A new copy restarts the wait; ending the host session cancels
it. A capture fails after 30 seconds if the quiet period cannot finish; it never
shortens an accessibility timeout. AndroidClipboard/ClipboardCapture.feature
exercises a real copy and asserts that capture waited at least ten seconds.
The informational clipboard entries remain until device evidence supports
removing them and adopting the explicitly changed frames.
