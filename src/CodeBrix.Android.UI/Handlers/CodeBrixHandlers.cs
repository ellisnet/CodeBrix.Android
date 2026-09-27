using System;
using CodeBrix.Android.UI.Hosting;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>
/// The element-handler registrations of CodeBrix.Android: which native handler serves which
/// Core element type (most-derived match), and the templated fallback for everything else.
/// Apps and add-ins replace or add registrations with <see cref="Register{TElement}"/> before
/// the first element of that type goes live.
/// </summary>
/// <remarks>
/// Internal in v1: whether handler authoring is public API (MAUI-style) is Jeremy's decision
/// D-O1; the types are shaped so that making them public is mechanical.
/// </remarks>
internal static class CodeBrixHandlers
{
    private static readonly Lazy<ElementHandlerRegistry> _registry = new(CreateDefaultRegistry);
    private static ElementHandlerFactory _factory;

    /// <summary>The registrations in effect.</summary>
    internal static ElementHandlerRegistry Registry => _registry.Value;

    /// <summary>The factory the platform bootstrap registers with Core.</summary>
    internal static ElementHandlerFactory Factory => _factory ??= new ElementHandlerFactory(Registry, HostLog.For("CodeBrix.Android.UI.Handlers"));

    /// <summary>Registers (or replaces) the handler of <typeparamref name="TElement"/> and its derived types.</summary>
    /// <typeparam name="TElement">The element type.</typeparam>
    /// <typeparam name="THandler">The handler type (a new instance per element).</typeparam>
    internal static void Register<TElement, THandler>()
        where TElement : class
        where THandler : IAndroidElementHandler, new()
        => Registry.Register<TElement>(_ => new THandler());

    /// <summary>Registers (or replaces) a handler factory for <typeparamref name="TElement"/> and its derived types.</summary>
    /// <typeparam name="TElement">The element type.</typeparam>
    /// <param name="factory">Creates the handler (null = Core's own path for that element).</param>
    internal static void Register<TElement>(Func<UIElement, IAndroidElementHandler> factory)
        where TElement : class
        => Registry.Register<TElement>(factory);

    private static ElementHandlerRegistry CreateDefaultRegistry()
    {
        var registry = new ElementHandlerRegistry
        {
            Fallback = _ => new TemplatedFallbackHandler(),
        };

        registry.Register<Panel>(_ => new PanelHandler());
        registry.Register<Border>(_ => new BorderHandler());
        registry.Register<ContentPresenter>(_ => new ContentPresenterHandler());
        registry.Register<ScrollContentPresenter>(_ => new ScrollContentPresenterHandler());
        registry.Register<TextBlock>(_ => new TextBlockHandler());
        registry.Register<Microsoft.UI.Xaml.Shapes.Shape>(_ => new ShapeHandler());

        // Content controls whose content is hosted directly (HostsContent). A plain ContentControl keeps
        // the templated fallback when the app gave it a template or style; ContentControl
        // SUBCLASSES (Button, ToggleButton, ListViewItem, app controls) resolve to the ContentControl
        // registration too and get the fallback until their own handler exists.
        registry.Register<ContentControl>(e => e.GetType() == typeof(ContentControl) && ContentControlHandler.HasDefaultTemplate((Control)e)
            ? new ContentControlHandler()
            : new TemplatedFallbackHandler());
        registry.Register<UserControl>(_ => new UserControlHandler());
        registry.Register<Page>(_ => new PageHandler());
        registry.Register<Frame>(_ => new FrameHandler());
        registry.Register<ScrollViewer>(_ => new ScrollViewerHandler());

        // AP4 (navigation, overlays, dialogs, menus, services): NavigationView reaches the Android back
        // button. (The overlay presenter and the services' activity bridge are registered by
        // AndroidPlatformBootstrap before Core resolves its handler services - OverlayBootstrap.EnsureRegistered.)
        registry.Register<NavigationView>(_ => new NavigationViewHandler());

        // AP3a (sample controls tier 1: text, buttons, inputs, selection, icons, images). Each factory returns
        // the templated fallback for a control that does not look the way its default style says.
        registry.Register<Microsoft.UI.Xaml.Controls.Primitives.ButtonBase>(ButtonHandler.Create);
        registry.Register<CheckBox>(CheckBoxHandler.Create);
        registry.Register<RadioButton>(RadioButtonHandler.Create);
        registry.Register<ToggleSwitch>(ToggleSwitchHandler.Create);
        registry.Register<Slider>(SliderHandler.Create);
        registry.Register<ProgressBar>(ProgressBarHandler.Create);
        registry.Register<ProgressRing>(ProgressRingHandler.Create);
        registry.Register<TextBox>(TextBoxHandler.Create);
        registry.Register<PasswordBox>(PasswordBoxHandler.Create);
        registry.Register<NumberBox>(NumberBoxHandler.Create);
        registry.Register<AutoSuggestBox>(AutoSuggestBoxHandler.Create);
        registry.Register<ComboBox>(ComboBoxHandler.Create);
        registry.Register<Image>(_ => new ImageHandler());
        registry.Register<NativeTemplatePart>(_ => null);
        // END AP3a

        // AP3b (lists, items, tabs): ListView / GridView / TreeView's list, FlipView and ItemsRepeater over a
        // RecyclerView (the internal ListScrollViewer and RecyclerItemsPanel are that list's Core scroll viewer
        // and items panel). A list the RecyclerView cannot reproduce (a re-templated or grouped list, an
        // unknown items panel or repeater layout) keeps Core's template: the templated fallback.
        registry.Register<ListViewBase>(e => ListViewBaseHandler.CanHandle((ListViewBase)e) ? new ListViewBaseHandler() : new TemplatedFallbackHandler());
        registry.Register<FlipView>(e => FlipViewHandler.CanHandle((FlipView)e) ? new FlipViewHandler() : new TemplatedFallbackHandler());
        registry.Register<ItemsRepeater>(e => ItemsRepeaterHandler.CanHandle((ItemsRepeater)e) ? new ItemsRepeaterHandler() : new TemplatedFallbackHandler());
        registry.Register<ListScrollViewer>(_ => new ListScrollViewerHandler());
        registry.Register<Platform.Recycler.RecyclerItemsPanel>(_ => new RecyclerItemsPanelHandler());

        // AP3b tabs: TabView / Pivot / SelectorBar keep Core's template and selection; their tab row is a
        // Material TabLayout drawn over the template's own row.
        registry.Register<TabView>(e => TabViewHandler.CanHandle((TabView)e) ? new TabViewHandler() : new TemplatedFallbackHandler());
        registry.Register<Pivot>(e => PivotHandler.CanHandle((Pivot)e) ? new PivotHandler() : new TemplatedFallbackHandler());
        registry.Register<SelectorBar>(e => SelectorBarHandler.CanHandle((SelectorBar)e) ? new SelectorBarHandler() : new TemplatedFallbackHandler());
        // END AP3b

        // AP5 (presentation policy: size classes and the adaptive table, the theme bridge, the type scale, motion).
        // The layer installs its handler customisations (property-mapper appends) and starts the motion ticker
        // before the first handler is created.
        // NavigationView: the adaptive handler decides its container (bottom bar / rail / drawer / Fluent template)
        // when it is created, because Core reads the capabilities before it connects the handler.
        registry.Register<NavigationView>(e => new NavigationViewHandler((NavigationView)e));

        // ContentDialog that Core presents itself (XAML content): its template stays (the templated fallback, as
        // before); the policy follows it for the Compact full-screen form of the adaptive table.
        registry.Register<ContentDialog>(e =>
        {
            Policy.ContentDialogPolicy.Track((ContentDialog)e);
            return new TemplatedFallbackHandler();
        });
        Policy.PresentationPolicy.EnsureRegistered();
        // END AP5

        // AP6 (composed controls and shapes the samples use). Shapes keep the Shape registration above (the handler
        // now draws the WinUI stroke outline and owns the hit test). Expander and ColorPicker are native compositions;
        // each factory returns the templated fallback for a control its native form cannot show. DatePicker and
        // TimePicker keep their Fluent template; their picker flyouts are Material dialogs through Core's
        // native-picker provider contracts. Viewbox keeps the templated fallback: Core scales its inner Border with a
        // ScaleTransform, which the Border's view replays as its animation matrix.
        registry.Register<Expander>(ExpanderHandler.Create);
        registry.Register<ColorPicker>(ColorPickerHandler.Create);
        MaterialPickers.EnsureRegistered();
        // END AP6

        // AP10A (every remaining Platform element, first lane: navigation and shell, selection and paging, pickers and
        // calendars). CommandBar: a Material app bar (bottom app bar in a Compact window, top app bar otherwise; switch
        // CodeBrix.Android.UI.NativeCommandBar); a stand-alone AppBarButton / AppBarToggleButton is a Material icon
        // button; SplitView keeps its template with the adaptive Compact form. PipsPager, PagerControl and BreadcrumbBar
        // are native rows of Material parts (Handlers/Paging). CalendarView (single-selection month view) is the platform
        // calendar and CalendarDatePicker a Material text field opening a MaterialDatePicker (Handlers/Calendars).
        registry.Register<CommandBar>(CommandBarHandler.Create);
        registry.Register<AppBarButton>(AppBarButtonHandler.Create);
        registry.Register<AppBarToggleButton>(AppBarButtonHandler.Create);
        registry.Register<SplitView>(SplitViewHandler.Create);
        registry.Register<PipsPager>(PipsPagerHandler.Create);
        registry.Register<PagerControl>(PagerControlHandler.Create);
        registry.Register<BreadcrumbBar>(BreadcrumbBarHandler.Create);
        registry.Register<CalendarView>(CalendarViewHandler.Create);
        registry.Register<CalendarDatePicker>(CalendarDatePickerHandler.Create);
        // END AP10A

        // AP10B (every remaining Platform element, second lane: status and feedback, buttons and icons, the rest).
        // InfoBar, RatingControl, SplitButton / ToggleSplitButton: a native Material widget drawn over the Fluent
        // template, which keeps every behaviour and part (Handlers/TemplateOverlays). InfoBadge and PersonPicture are
        // native views; RefreshContainer keeps its template inside a native SwipeRefreshLayout (Handlers/Status).
        registry.Register<InfoBar>(InfoBarHandler.Create);
        registry.Register<InfoBadge>(InfoBadgeHandler.Create);
        registry.Register<RatingControl>(RatingControlHandler.Create);
        registry.Register<PersonPicture>(PersonPictureHandler.Create);
        registry.Register<RefreshContainer>(RefreshContainerHandler.Create);
        // DropDownButton: the Button handler's Material button with a trailing chevron (Handlers/MenuButtons); BitmapIcon:
        // Core's composition, its ImageView tinted when monochrome (Handlers/Icons).
        registry.Register<DropDownButton>(DropDownButtonMapping.Create);
        registry.Register<SplitButton>(SplitButtonHandler.Create);
        registry.Register<BitmapIcon>(_ => new BitmapIconHandler());
        // ScrollView keeps its template; its ScrollPresenter scrolls natively (the ScrollViewer's native scroll view,
        // Handlers/Scroller). A stand-alone ColorSpectrum is the native ColorPicker's spectrum view (Handlers/ColorParts).
        registry.Register<Microsoft.UI.Xaml.Controls.Primitives.ScrollPresenter>(_ => new ScrollPresenterHandler());
        registry.Register<Microsoft.UI.Xaml.Controls.Primitives.ColorSpectrum>(ColorSpectrumHandler.Create);
        // END AP10B

        // AP7-B (the engine-backed add-ins; TriPaneView lives in the framework's Toolkit): TriPaneView keeps its template
        // and Core's engine; its handler applies the adaptive form by window size class through the engine's weights and
        // takes finger drags on the dividers through the control's drag entry points (Handlers/Toolkit).
        registry.Register<CodeBrix.Platform.UI.Toolkit.TriPaneView>(TriPaneViewHandler.Create);
        // END AP7-B
        return registry;
    }
}
