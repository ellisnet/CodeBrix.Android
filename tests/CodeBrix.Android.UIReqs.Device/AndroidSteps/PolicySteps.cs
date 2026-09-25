#nullable disable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using CodeBrix.Android.UI.Diagnostics;
using CodeBrix.Android.UI.Handlers;
using CodeBrix.Android.UI.Hosting;
using CodeBrix.Android.UI.Overlay;
using CodeBrix.Android.UI.Platform.Animation;
using CodeBrix.Android.UI.Policy;
using CodeBrix.Android.UIReqs.Device.Runtime;
using CodeBrix.Platform.UI.Core.UIReqs.Hosting;
using CodeBrix.Platform.UI.Core.UIReqs.Support;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Reqnroll;
using SilverAssertions;
using Xunit;
using AAppCompatDelegate = AndroidX.AppCompat.App.AppCompatDelegate;
using AColorStateList = Android.Content.Res.ColorStateList;
using AImageButton = Android.Widget.ImageButton;
using AInsetDrawable = Android.Graphics.Drawables.InsetDrawable;
using AMaterialShapeDrawable = Google.Android.Material.Shape.MaterialShapeDrawable;
using AMenu = Android.Views.IMenu;
using AMenuItem = Android.Views.IMenuItem;
using AResource = Android.Resource;
using ATextView = Android.Widget.TextView;
using AUiMode = Android.Content.Res.UiMode;
using AView = Android.Views.View;
using AWindowCompat = AndroidX.Core.View.WindowCompat;
using AWindowInsetsCompat = AndroidX.Core.View.WindowInsetsCompat;
using AWindowInsetsControllerCompat = AndroidX.Core.View.WindowInsetsControllerCompat;
using AViewGroup = Android.Views.ViewGroup;
using WColor = Windows.UI.Color;

namespace CodeBrix.Android.UIReqs.Device.AndroidSteps;

/// <summary>
/// AP5: the harness settings of the presentation-policy layer. The copied scenarios are the Fluent specification,
/// so the whole run starts with the theme bridge's Material palette OFF (the framework's Fluent colours) and the
/// motion policy on <see cref="MotionMode.AlwaysAnimate"/> (the runner removes the system's animations for stable
/// frames; a native indeterminate indicator then keeps moving as the Fluent ProgressBar does). Scenarios tagged
/// <c>@material-policy</c> run with the Material palette on. After every scenario a REAL window resize (the host's
/// <c>wm density</c>) and shown system bars, the size-class override, the font scale override, the motion policy, the palette, the night mode and the brushes a scenario re-keyed are put back.
/// </summary>
[Binding]
public sealed class PolicyHooks
{
    /// <summary>The tag of the scenarios that run with the Material palette on.</summary>
    public const string MaterialPolicyTag = "material-policy";

    private readonly ScenarioContext _scenarioContext;

    /// <summary>Creates the hooks.</summary>
    public PolicyHooks(ScenarioContext scenarioContext) => _scenarioContext = scenarioContext;

    /// <summary>
    /// Sets the harness policy before the first scenario (after the app launched) - and before the copied harness reads
    /// the theme's colours (ThemeColors.RegisterAsync, Order 10: the "Accent" a feature names is the framework brush's
    /// colour read once per run). AP7-M: at Order 50 it ran after that read, so a run whose bridge had already written
    /// the Material palette at start-up registered the Material primary as "Accent" (a start-up race; seen in a
    /// single-session landscape run: every accent scenario failed with the Fluent #FF005A9E drawn).
    /// </summary>
    [BeforeTestRun(Order = 5)]
    public static async Task Use_the_harness_policy() =>
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            MotionPolicy.Mode = MotionMode.AlwaysAnimate;
            ThemeBridge.DynamicColor = false;
            ThemeBridge.MaterialPalette = false;
        }).ConfigureAwait(false);

    /// <summary>Chooses the palette of the scenario.</summary>
    [BeforeScenario(Order = -40)]
    public async Task Choose_the_palette()
    {
        var material = _scenarioContext.ScenarioInfo.CombinedTags.Contains(MaterialPolicyTag)
            || _scenarioContext.ScenarioInfo.Tags.Contains(MaterialPolicyTag);
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            ThemeBridge.DynamicColor = false;
            ThemeBridge.MaterialPalette = material;
        }).ConfigureAwait(false);
    }

    /// <summary>Puts every policy switch the scenario touched back.</summary>
    [AfterScenario(Order = -40)]
    public async Task Put_the_policy_back()
    {
        await PolicySteps.RestoreRealWindowAsync().ConfigureAwait(false);
        await PolicySteps.HideSystemBarsAgainAsync().ConfigureAwait(false);
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            PolicySteps.HideDialogs();
            if (AdaptivePolicy.Override != null)
            {
                WindowSizeClassMonitor.SetOverride(null);
            }

            if (TypeScale.FontScaleOverride != null)
            {
                TypeScale.FontScaleOverride = null;
                TypeScalePolicy.Refresh();
            }

            MotionPolicy.Mode = MotionMode.AlwaysAnimate;
            if (AAppCompatDelegate.DefaultNightMode != AAppCompatDelegate.ModeNightFollowSystem)
            {
                AAppCompatDelegate.DefaultNightMode = AAppCompatDelegate.ModeNightFollowSystem;
            }

            PolicySteps.RemoveRekeyedBrushes();
            ThemeBridge.DynamicColor = false;
            ThemeBridge.MaterialPalette = false;
            ThemeRefresh.RefreshAll();
        }).ConfigureAwait(false);
        await Task.Delay(200, TestContext.Current.CancellationToken).ConfigureAwait(false);
        await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
    }
}

/// <summary>The steps of the Android-only AndroidPolicy group (size classes and the adaptive table, theme keys and the theme bridge, type scale, motion).</summary>
[Binding]
public sealed class PolicySteps
{
    private const string RootName = "policyRoot";
    private const string ViewKey = "uireqs.policy.view.";
    private const string ColourKey = "uireqs.policy.colour.";
    private const string ActivityKey = "uireqs.policy.activity";
    private static readonly TimeSpan SettleDelay = TimeSpan.FromMilliseconds(400);
    private static readonly List<string> Rekeyed = new();
    private static readonly List<ContentDialog> Dialogs = new();
    private static readonly List<string> Invoked = new();
    private static int _selectionChanges;
    private static bool _settingsInvoked;
    private static int? _realDensityRestore;
    private static bool _systemBarsShown;

    private readonly ScenarioContext _scenarioContext;

    /// <summary>Creates the steps.</summary>
    public PolicySteps(ScenarioContext scenarioContext) => _scenarioContext = scenarioContext;

    /// <summary>Hides every ContentDialog a scenario showed (hook).</summary>
    internal static void HideDialogs()
    {
        foreach (var dialog in Dialogs)
        {
            dialog.Hide();
        }

        Dialogs.Clear();
    }

    /// <summary>Removes the brushes a scenario re-keyed in the application's dictionary (hook).</summary>
    internal static void RemoveRekeyedBrushes()
    {
        var resources = Application.Current?.Resources;
        foreach (var key in Rekeyed)
        {
            resources?.Remove(key);
        }

        Rekeyed.Clear();
    }

    // ------------------------------------------------------------ size classes

    /// <summary>Asserts the monitor's classes are the ones the window's metrics give.</summary>
    [Then("the window size classes are computed from the window's own size")]
    public async Task Then_the_size_classes_come_from_the_window()
    {
        var (current, computed, widthDp, heightDp) = await OnUIThreadAsync(() =>
        {
            var activity = Activity();
            var bounds = activity.WindowManager!.CurrentWindowMetrics.Bounds;
            var density = activity.Resources!.DisplayMetrics!.Density;
            return (WindowSizeClassMonitor.Current(activity), WindowSizeClassMonitor.Compute(activity), bounds.Width() / density, bounds.Height() / density);
        }).ConfigureAwait(false);

        computed.WidthDp.Should().BeApproximately(widthDp, 0.5, "the width class is computed from the window width in dp");
        computed.HeightDp.Should().BeApproximately(heightDp, 0.5, "the height class is computed from the window height in dp");
        current.SameClassesAs(computed).Should().BeTrue("the monitor follows the window: it reports {0}, the metrics give {1}", current, computed);
    }

    /// <summary>Asserts the window's width class.</summary>
    [Then("the window is {string} wide")]
    public async Task Then_the_window_is_wide(string widthClass)
    {
        var current = await OnUIThreadAsync(() => WindowSizeClassMonitor.Current(Activity())).ConfigureAwait(false);
        current.Width.ToString().Should().Be(widthClass, "the width class of the window ({0})", current);
    }

    /// <summary>Simulates a window size (the size-class override re-maps live, as a resize does).</summary>
    [Given("the window is simulated as {int} by {int} dp")]
    [When("the window is simulated as {int} by {int} dp")]
    public async Task When_the_window_is_simulated(int width, int height)
    {
        await TestTargetFixture.RunOnUIThreadAsync(() => WindowSizeClassMonitor.SetOverride(new WindowSizeClass(width, height, false))).ConfigureAwait(false);
        await SettleAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Resizes the REAL window to <paramref name="widthDp"/> dp wide: the host runs <c>wm density</c> (AP2-M2, the
    /// DeviceSession "shell" request) with the density that gives the window that width, and the step waits until
    /// the activity (which handles density changes without being re-created) and the size-class monitor follow.
    /// </summary>
    [Given("the real window is resized to {int} dp wide")]
    [When("the real window is resized to {int} dp wide")]
    public async Task When_the_real_window_is_resized(int widthDp)
    {
        var (widthPx, densityDpi) = await OnUIThreadAsync(() =>
        {
            var activity = Activity();
            return (activity.WindowManager!.CurrentWindowMetrics.Bounds.Width(), (int)activity.Resources!.DisplayMetrics!.DensityDpi);
        }).ConfigureAwait(false);
        _realDensityRestore ??= densityDpi;
        var dpi = (int)Math.Round(widthPx * 160.0 / widthDp);
        await SetRealDensityAsync(dpi).ConfigureAwait(false);
        var current = await OnUIThreadAsync(() => WindowSizeClassMonitor.Current(Activity())).ConfigureAwait(false);
        current.WidthDp.Should().BeApproximately(widthPx * 160.0 / dpi, 1.0, "the size-class monitor follows the real window ({0})", current);
    }

    /// <summary>Puts the real window's density back (the step form of the after-scenario hook).</summary>
    [When("the real window size is restored")]
    public async Task When_the_real_window_size_is_restored() => await RestoreRealWindowAsync().ConfigureAwait(false);

    /// <summary>Restores the density a real resize changed (hook; nothing when no real resize happened).</summary>
    internal static async Task RestoreRealWindowAsync()
    {
        if (_realDensityRestore is not { } dpi)
        {
            return;
        }

        _realDensityRestore = null;
        await SetRealDensityAsync(dpi).ConfigureAwait(false);
    }

    private static async Task SetRealDensityAsync(int dpi)
    {
        var (exit, output) = await HostChannel.ShellAsync("wm density " + dpi.ToString(CultureInfo.InvariantCulture), TestContext.Current.CancellationToken).ConfigureAwait(false);
        exit.Should().Be(0, "the host ran \"wm density {0}\" ({1})", dpi, output);
        for (var i = 0; i < 100; i++)
        {
            var (applied, followed) = await OnUIThreadAsync(() =>
            {
                var activity = Activity();
                return ((int)activity.Resources!.DisplayMetrics!.DensityDpi == dpi,
                    WindowSizeClassMonitor.Current(activity).SameClassesAs(WindowSizeClassMonitor.Compute(activity)));
            }).ConfigureAwait(false);
            if (applied && followed)
            {
                break;
            }

            await Task.Delay(100, TestContext.Current.CancellationToken).ConfigureAwait(false);
        }

        await SettleAsync().ConfigureAwait(false);
    }

    // ------------------------------------------------------------ system bars (AP2-M2, item 7)

    /// <summary>Shows the system bars the harness activity hides, so the window has a safe area to absorb.</summary>
    [Given("the system bars are shown")]
    public async Task Given_the_system_bars_are_shown()
    {
        _systemBarsShown = true;
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            var window = Activity().Window!;
            AWindowCompat.GetInsetsController(window, window.DecorView)?.Show(AWindowInsetsCompat.Type.SystemBars());
        }).ConfigureAwait(false);
        for (var i = 0; i < 50 && (await SystemBarInsetsPxAsync().ConfigureAwait(false)).Top == 0; i++)
        {
            await Task.Delay(100, TestContext.Current.CancellationToken).ConfigureAwait(false);
        }

        await SettleAsync().ConfigureAwait(false);
        (await SystemBarInsetsPxAsync().ConfigureAwait(false)).Top.Should().BeGreaterThan(0, "the status bar is shown");
    }

    /// <summary>Hides the system bars again (hook; nothing when a scenario did not show them).</summary>
    internal static async Task HideSystemBarsAgainAsync()
    {
        if (!_systemBarsShown)
        {
            return;
        }

        _systemBarsShown = false;
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            var window = Activity().Window!;
            var controller = AWindowCompat.GetInsetsController(window, window.DecorView);
            if (controller != null)
            {
                controller.Hide(AWindowInsetsCompat.Type.SystemBars());
                controller.SystemBarsBehavior = AWindowInsetsControllerCompat.BehaviorShowTransientBarsBySwipe;
            }
        }).ConfigureAwait(false);
        for (var i = 0; i < 50 && !IsEmpty(await SystemBarInsetsPxAsync().ConfigureAwait(false)); i++)
        {
            await Task.Delay(100, TestContext.Current.CancellationToken).ConfigureAwait(false);
        }

        await SettleAsync().ConfigureAwait(false);
    }

    /// <summary>Asserts that the chrome of a native NavigationView pads its destinations clear of the system bars it touches.</summary>
    [Then("the destinations of {string} are clear of the system bars")]
    public async Task Then_the_destinations_are_clear(string name)
    {
        var insets = await SystemBarInsetsPxAsync().ConfigureAwait(false);
        var (rect, padding, width, height, container) = await OnUIThreadAsync(() =>
        {
            var layout = Handler(name).Layout;
            var chrome = layout.Container == NavigationContainer.ModalDrawer ? (AView)layout.TopBar : layout.MenuView;
            var decor = Activity().Window!.DecorView;
            return (ScreenRect(chrome), new[] { chrome.PaddingLeft, chrome.PaddingTop, chrome.PaddingRight, chrome.PaddingBottom }, decor.Width, decor.Height, layout.Container.ToString());
        }).ConfigureAwait(false);

        insets.Top.Should().BeGreaterThan(0, "the scenario shows the status bar");
        var touched = 0;
        void Edge(bool touches, int pad, int inset, string edge)
        {
            if (touches && inset > 0)
            {
                touched++;
                pad.Should().BeGreaterThanOrEqualTo(inset - 1, "the {0} pads its {1} edge by the system bar inset ({2} px) - chrome {3}", container, edge, inset, rect);
            }
        }

        Edge(rect.Left <= 1, padding[0], insets.Left, "left");
        Edge(rect.Top <= 1, padding[1], insets.Top, "top");
        Edge(rect.Right >= width - 1, padding[2], insets.Right, "right");
        Edge(rect.Bottom >= height - 1, padding[3], insets.Bottom, "bottom");

        // A bar that touches no inset edge (a bottom bar on a device without a bottom navigation bar) needs no padding.
        if (touched == 0)
        {
            padding.Should().OnlyContain(p => p == 0, "the {0} touches no system bar ({1}), so it is not padded", container, rect);
        }
    }

    /// <summary>Asserts that every destination icon of a native container is drawn 24 dp at the window's current density.</summary>
    [Then("the destination icons of {string} are drawn for the window's density")]
    public async Task Then_the_icons_follow_the_density(string name)
    {
        var (sizes, expected) = await OnUIThreadAsync(() =>
        {
            var menu = Handler(name).Layout.Menu;
            var list = new List<(string Title, int Width, int Height)>();
            for (var i = 0; menu != null && i < menu.Size(); i++)
            {
                if (menu.GetItem(i) is { Icon: { } icon } item)
                {
                    list.Add((item.TitleFormatted?.ToString(), icon.IntrinsicWidth, icon.IntrinsicHeight));
                }
            }

            return (list, (int)Math.Round(24 * Density()));
        }).ConfigureAwait(false);

        sizes.Should().NotBeEmpty("the destinations have icons");
        foreach (var (title, width, height) in sizes)
        {
            width.Should().BeInRange(expected - 1, expected + 1, "the icon of \"{0}\" is 24 dp at the current density ({1} px)", title, expected);
            height.Should().BeInRange(expected - 1, expected + 1, "the icon of \"{0}\" is 24 dp at the current density ({1} px)", title, expected);
        }
    }

    /// <summary>Asserts that a view is laid out clear of the system bars.</summary>
    [Then("the page {string} is clear of the system bars")]
    public async Task Then_the_page_is_clear(string pageName)
    {
        var insets = await SystemBarInsetsPxAsync().ConfigureAwait(false);
        var (page, width, height) = await OnUIThreadAsync(() =>
        {
            var decor = Activity().Window!.DecorView;
            return (ScreenRect(NativeViewLocator.ViewOf(ElementRegistry.Resolve(pageName))), decor.Width, decor.Height);
        }).ConfigureAwait(false);

        insets.Top.Should().BeGreaterThan(0, "the scenario shows the status bar");
        page.Top.Should().BeGreaterThanOrEqualTo(insets.Top - 1, "the page starts under the status bar ({0})", page);
        page.Left.Should().BeGreaterThanOrEqualTo(insets.Left - 1, "the page is clear of a left bar ({0})", page);
        page.Right.Should().BeLessThanOrEqualTo(width - insets.Right + 1, "the page is clear of a right bar ({0})", page);
        page.Bottom.Should().BeLessThanOrEqualTo(height - insets.Bottom + 1, "the page ends above the navigation bar ({0})", page);
    }

    private static Task<global::Android.Graphics.Rect> SystemBarInsetsPxAsync() => OnUIThreadAsync(() =>
    {
        var decor = Activity().Window!.DecorView;
        var insets = decor.RootWindowInsets is { } raw
            ? AWindowInsetsCompat.ToWindowInsetsCompat(raw, decor).GetInsets(AWindowInsetsCompat.Type.SystemBars() | AWindowInsetsCompat.Type.DisplayCutout())
            : null;
        return insets != null ? new global::Android.Graphics.Rect(insets.Left, insets.Top, insets.Right, insets.Bottom) : new global::Android.Graphics.Rect();
    });

    private static bool IsEmpty(global::Android.Graphics.Rect insets) => insets.Left == 0 && insets.Top == 0 && insets.Right == 0 && insets.Bottom == 0;

    /// <summary>Goes back to the window's real size classes.</summary>
    [When("the window size classes are no longer simulated")]
    public async Task When_the_size_classes_are_no_longer_simulated()
    {
        await TestTargetFixture.RunOnUIThreadAsync(() => WindowSizeClassMonitor.SetOverride(null)).ConfigureAwait(false);
        await SettleAsync().ConfigureAwait(false);
    }

    // ---------------------------------------------------------- NavigationView

    /// <summary>Shows an Auto NavigationView with one item per label and a page of one colour as its content.</summary>
    [Given("the application shows an Auto NavigationView named {string} with the items {string} and a page named {string} painted {string}")]
    public async Task Given_an_Auto_NavigationView(string name, string items, string pageName, string colour)
    {
        ElementRegistry.Clear();
        Invoked.Clear();
        _selectionChanges = 0;
        _settingsInvoked = false;
        NavigationView view = null;
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            var page = new Border { Name = pageName, Background = new SolidColorBrush(Colors.Parse(colour)) };
            view = new NavigationView
            {
                Name = name,
                PaneDisplayMode = NavigationViewPaneDisplayMode.Auto,
                IsBackButtonVisible = NavigationViewBackButtonVisible.Collapsed,
                Content = page,
            };
            var symbols = new[] { Symbol.Home, Symbol.Mail, Symbol.Audio, Symbol.Calendar, Symbol.Camera, Symbol.Contact, Symbol.Favorite, Symbol.Globe };
            var i = 0;
            foreach (var label in Split(items))
            {
                var item = new NavigationViewItem { Name = label, Content = label, Icon = new SymbolIcon(symbols[i++ % symbols.Length]) };
                view.MenuItems.Add(item);
                ElementRegistry.Register(label, item);
            }

            view.ItemInvoked += (_, args) =>
            {
                Invoked.Add((args.InvokedItemContainer as FrameworkElement)?.Name ?? args.InvokedItem?.ToString() ?? string.Empty);
                _settingsInvoked |= args.IsSettingsInvoked;
            };
            view.SelectionChanged += (_, _) => _selectionChanges++;
            ElementRegistry.Register(name, view);
            ElementRegistry.Register(pageName, page);
        }).ConfigureAwait(false);
        await TestTargetFixture.SetContentAsync(view).ConfigureAwait(false);
        await SettleAsync().ConfigureAwait(false);
    }

    /// <summary>Gives a NavigationView a pane footer (which keeps its Fluent template).</summary>
    [When("the NavigationView {string} gets a PaneFooter")]
    public async Task When_the_NavigationView_gets_a_PaneFooter(string name)
    {
        await TestTargetFixture.RunOnUIThreadAsync(() => Navigation(name).PaneFooter = new TextBlock { Text = "Footer" }).ConfigureAwait(false);
        await SettleAsync().ConfigureAwait(false);
    }

    /// <summary>Asserts the container a NavigationView is shown in, and its destinations.</summary>
    [Then("the NavigationView {string} is shown as a {string} with the destinations {string}")]
    public async Task Then_the_NavigationView_is_shown_as(string name, string container, string destinations)
    {
        var (shown, titles) = await OnUIThreadAsync(() =>
        {
            var handler = Handler(name);
            return (handler.Layout.Container.ToString(), Titles(handler.Layout.Menu));
        }).ConfigureAwait(false);

        shown.Should().Be(container, "the Material container the adaptive table picks");
        titles.Should().Equal(Split(destinations), "the container lists the NavigationView's destinations, Settings last");
    }

    /// <summary>Asserts that a NavigationView keeps Core's Fluent template.</summary>
    [Then("the NavigationView {string} is shown with its Fluent template")]
    public async Task Then_the_NavigationView_keeps_its_template(string name)
    {
        var (container, native, children) = await OnUIThreadAsync(() =>
        {
            var view = Navigation(name);
            var handler = PolicyDiagnostics.HandlerOf(view) as NavigationViewHandler;
            return (handler?.Container.ToString() ?? "no handler", handler?.IsNative ?? false, VisualTreeHelper.GetChildrenCount(view));
        }).ConfigureAwait(false);

        container.Should().Be(nameof(NavigationContainer.Template), "an explicit PaneDisplayMode or a pane a Material container cannot show keeps the Fluent template");
        native.Should().BeFalse("no native container shows it");
        children.Should().BeGreaterThan(0, "the Fluent template is materialized");
    }

    /// <summary>Asserts that the page sits in the room the container's chrome leaves.</summary>
    [Then("the page {string} is laid out beside the {string} of {string}")]
    public async Task Then_the_page_is_laid_out_beside(string pageName, string container, string name)
    {
        var (page, chrome) = await OnUIThreadAsync(() =>
        {
            var layout = Handler(name).Layout;
            var chromeView = container == nameof(NavigationContainer.ModalDrawer) ? layout.TopBar : layout.MenuView;
            return (ScreenRect(NativeViewLocator.ViewOf(ElementRegistry.Resolve(pageName))), ScreenRect(chromeView));
        }).ConfigureAwait(false);

        page.Width().Should().BeGreaterThan(0, "the page has room");
        page.Height().Should().BeGreaterThan(0, "the page has room");
        switch (container)
        {
            case nameof(NavigationContainer.BottomBar):
                page.Bottom.Should().BeLessThanOrEqualTo(chrome.Top + 1, "the page ends where the bottom bar starts");
                break;
            case nameof(NavigationContainer.ModalDrawer):
                page.Top.Should().BeGreaterThanOrEqualTo(chrome.Bottom - 1, "the page starts under the top bar");
                break;
            default:
                page.Left.Should().BeGreaterThanOrEqualTo(chrome.Right - 1, "the page starts right of the {0}", container);
                break;
        }
    }

    /// <summary>Remembers an element's native view.</summary>
    [Given("the native view of {string} is remembered")]
    public async Task Given_the_native_view_is_remembered(string name) =>
        _scenarioContext[ViewKey + name] = await OnUIThreadAsync(() => NativeViewLocator.ViewOf(ElementRegistry.Resolve(name))).ConfigureAwait(false);

    /// <summary>Asserts that an element still has the native view it had (the page was not recreated).</summary>
    [Then("{string} still has the native view it had")]
    public async Task Then_the_native_view_is_the_same(string name)
    {
        var remembered = _scenarioContext[ViewKey + name] as AView;
        var now = await OnUIThreadAsync(() => NativeViewLocator.ViewOf(ElementRegistry.Resolve(name))).ConfigureAwait(false);
        ReferenceEquals(remembered, now).Should().BeTrue("the page keeps its native view across the re-mapping (nothing recreated)");
        var attached = await OnUIThreadAsync(() => now?.IsAttachedToWindow ?? false).ConfigureAwait(false);
        attached.Should().BeTrue("the page's view is still on the screen");
    }

    /// <summary>Asserts whether the modal drawer is open.</summary>
    [Then("the modal drawer of {string} is {word}")]
    public async Task Then_the_modal_drawer_is(string name, string state)
    {
        await SettleAsync().ConfigureAwait(false);
        var open = await OnUIThreadAsync(() => Handler(name).Layout.IsDrawerOpen).ConfigureAwait(false);
        open.Should().Be(state == "open", "the modal drawer of \"{0}\" must be {1}", name, state);
    }

    /// <summary>Presses the top bar's navigation (menu) button.</summary>
    [When("the navigation button of {string} is pressed")]
    public async Task When_the_navigation_button_is_pressed(string name)
    {
        var pressed = await OnUIThreadAsync(() =>
        {
            var bar = Handler(name).Layout.TopBar ?? throw new InvalidOperationException("The NavigationView has no top bar.");
            for (var i = 0; i < bar.ChildCount; i++)
            {
                if (bar.GetChildAt(i) is AImageButton button)
                {
                    return button.PerformClick();
                }
            }

            return false;
        }).ConfigureAwait(false);
        pressed.Should().BeTrue("the top bar has a navigation button");
        await SettleAsync().ConfigureAwait(false);
    }

    /// <summary>Picks a destination through the native menu (the path a tap takes).</summary>
    [When("the destination {string} of {string} is picked")]
    public async Task When_the_destination_is_picked(string destination, string name)
    {
        // The menu performs the item through the container's own selection path (a bar's reports "not consumed"
        // when its listener selects, so the return value says nothing; the Then steps check the outcome).
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            var menu = Handler(name).Layout.Menu ?? throw new InvalidOperationException("The NavigationView shows no native menu.");
            var item = Find(menu, destination) ?? throw new InvalidOperationException($"No destination \"{destination}\" in the menu.");
            menu.PerformIdentifierAction(item.ItemId, 0);
        }).ConfigureAwait(false);
        await SettleAsync().ConfigureAwait(false);
    }

    /// <summary>Asserts a destination is the checked one.</summary>
    [Then("the destination {string} of {string} is checked")]
    public async Task Then_the_destination_is_checked(string destination, string name)
    {
        var isChecked = await OnUIThreadAsync(() => Find(Handler(name).Layout.Menu, destination)?.IsChecked ?? false).ConfigureAwait(false);
        isChecked.Should().BeTrue("the native container shows \"{0}\" as the selected destination", destination);
    }

    /// <summary>Asserts ItemInvoked and SelectionChanged were raised for a destination.</summary>
    [Then("the NavigationView {string} reported ItemInvoked for {string} and a selection change")]
    public void Then_ItemInvoked_and_selection_change(string name, string destination)
    {
        Invoked.Should().Contain(destination, "ItemInvoked names the NavigationViewItem as its container (recorded: [{0}])", string.Join(", ", Invoked));
        _selectionChanges.Should().BeGreaterThan(0, "SelectionChanged is raised");
    }

    /// <summary>Asserts ItemInvoked was raised for Settings.</summary>
    [Then("the NavigationView {string} reported ItemInvoked for Settings")]
    public void Then_ItemInvoked_for_Settings(string name) =>
        _settingsInvoked.Should().BeTrue("picking Settings raises ItemInvoked with IsSettingsInvoked (recorded: [{0}])", string.Join(", ", Invoked));

    // ------------------------------------------------------------ ContentDialog

    /// <summary>Shows a ContentDialog with XAML content (Core presents it).</summary>
    [Given("the application shows a ContentDialog named {string} with XAML content")]
    public async Task Given_a_ContentDialog_with_XAML(string name)
    {
        var root = await ShowRootAsync().ConfigureAwait(false);
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            var panel = new StackPanel();
            panel.Children.Add(new TextBlock { Text = "Pick a colour:" });
            panel.Children.Add(new CheckBox { Content = "Red" });
            var dialog = new ContentDialog { Name = name, Title = "Colours", Content = panel, PrimaryButtonText = "OK", CloseButtonText = "Cancel", XamlRoot = root.XamlRoot };
            Dialogs.Add(dialog);
            ElementRegistry.Register(name, dialog);
            _ = dialog.ShowAsync();
        }).ConfigureAwait(false);
        await SettleAsync().ConfigureAwait(false);
    }

    /// <summary>Asserts a Core dialog is full screen.</summary>
    [Then("the ContentDialog {string} is shown full screen")]
    public async Task Then_the_ContentDialog_is_full_screen(string name)
    {
        var (form, fullSize, card, window) = await DialogStateAsync(name).ConfigureAwait(false);
        form.Should().Be(DialogForm.FullScreen, "a Compact window shows a dialog with XAML content full screen");
        fullSize.Should().BeTrue("the dialog is asked for its full size (FullSizeDesired)");
        if (card > 0)
        {
            card.Should().BeGreaterThanOrEqualTo(window * 0.8, "the dialog's card fills the window height");
        }
    }

    /// <summary>Asserts a Core dialog is a basic (centred) dialog.</summary>
    [Then("the ContentDialog {string} is shown as a basic dialog")]
    public async Task Then_the_ContentDialog_is_basic(string name)
    {
        var (form, fullSize, card, window) = await DialogStateAsync(name).ConfigureAwait(false);
        form.Should().Be(DialogForm.Basic, "a wide window shows a basic dialog");
        fullSize.Should().BeFalse("the policy took FullSizeDesired back");
        if (card > 0)
        {
            card.Should().BeLessThan(window * 0.8, "the basic card is only as tall as its content");
        }
    }

    // -------------------------------------------------------------- theme keys

    /// <summary>Re-keys Fluent control brushes in the application's dictionary (lightweight styling).</summary>
    [Given("the application re-keys the brushes:")]
    public Task Given_the_application_rekeys(DataTable table) => TestTargetFixture.RunOnUIThreadAsync(() =>
    {
        var resources = Application.Current.Resources;
        foreach (var row in table.Rows)
        {
            var key = row["Key"];
            resources[key] = new SolidColorBrush(Colors.Parse(row["Colour"]));
            if (!Rekeyed.Contains(key))
            {
                Rekeyed.Add(key);
            }
        }
    });

    /// <summary>Re-points a re-keyed brush at another colour (a live scheme switch).</summary>
    [When("the re-keyed brush {string} is re-pointed to {string}")]
    public async Task When_the_brush_is_repointed(string key, string colour)
    {
        await TestTargetFixture.RunOnUIThreadAsync(() => ((SolidColorBrush)Application.Current.Resources[key]).Color = Colors.Parse(colour)).ConfigureAwait(false);
        await SettleAsync().ConfigureAwait(false);
    }

    /// <summary>Shows one control in a fresh root.</summary>
    [Given("the application shows a native {word} named {string}")]
    public async Task Given_the_application_shows_a_control(string kind, string name)
    {
        ElementRegistry.Clear();
        var root = await ShowRootAsync().ConfigureAwait(false);
        await AddAsync(root, kind, name).ConfigureAwait(false);
    }

    /// <summary>Adds a control to the root.</summary>
    [Given("the application also shows a native {word} named {string}")]
    public async Task Given_the_application_also_shows_a_control(string kind, string name) =>
        await AddAsync((Panel)ElementRegistry.Resolve(RootName), kind, name).ConfigureAwait(false);

    /// <summary>Adds an accent Button to the root.</summary>
    [Given("the application also shows an accent Button named {string}")]
    public async Task Given_an_accent_Button(string name) =>
        await AddAsync((Panel)ElementRegistry.Resolve(RootName), "AccentButton", name).ConfigureAwait(false);

    /// <summary>Adds a ListView with its first item selected.</summary>
    [Given("the application also shows a native ListView named {string} with its first item selected")]
    public async Task Given_a_ListView(string name) =>
        await AddAsync((Panel)ElementRegistry.Resolve(RootName), "SelectedListView", name).ConfigureAwait(false);

    /// <summary>Asserts a native colour list's four states.</summary>
    [Then("the native {string} colours of {string} are {string} at rest, {string} hovered, {string} pressed and {string} disabled")]
    public Task Then_the_native_colours_are(string part, string name, string rest, string hovered, string pressed, string disabled) =>
        AssertStatesAsync(part, name, false, rest, hovered, pressed, disabled);

    /// <summary>Asserts a native colour list's four checked states.</summary>
    [Then("the native checked {string} colours of {string} are {string} at rest, {string} hovered, {string} pressed and {string} disabled")]
    public Task Then_the_native_checked_colours_are(string part, string name, string rest, string hovered, string pressed, string disabled) =>
        AssertStatesAsync(part, name, true, rest, hovered, pressed, disabled);

    /// <summary>Asserts a native colour at rest.</summary>
    [Then("the native {string} colour of {string} at rest is {string}")]
    public async Task Then_the_native_colour_at_rest(string part, string name, string colour) =>
        (await ColourAsync(part, name, false, "rest").ConfigureAwait(false)).Should().Be(Hex(colour), "the {0} of \"{1}\" at rest", part, name);

    /// <summary>Asserts a native checked colour at rest.</summary>
    [Then("the native checked {string} colour of {string} at rest is {string}")]
    public async Task Then_the_native_checked_colour_at_rest(string part, string name, string colour) =>
        (await ColourAsync(part, name, true, "rest").ConfigureAwait(false)).Should().Be(Hex(colour), "the checked {0} of \"{1}\" at rest", part, name);

    /// <summary>Asserts a native colour in one state.</summary>
    [Then("the native {string} colour of {string} {word} is {string}")]
    public async Task Then_the_native_colour_in_state(string part, string name, string state, string colour) =>
        (await ColourAsync(part, name, false, state).ConfigureAwait(false)).Should().Be(Hex(colour), "the {0} of \"{1}\" {2}", part, name, state);

    /// <summary>Asserts a native colour at rest is a Material role of the palette in use.</summary>
    [Then("the native {string} colour of {string} at rest is the Material role {string}")]
    public async Task Then_the_native_colour_is_the_role(string part, string name, string role)
    {
        var expected = await OnUIThreadAsync(() => Roles()[role]).ConfigureAwait(false);
        (await ColourAsync(part, name, false, "rest").ConfigureAwait(false)).Should().Be(Hex(expected), "the {0} of \"{1}\" is the Material role {2}", part, name, role);
    }

    /// <summary>Asserts that a part of a control (its template included) paints with a re-keyed brush.</summary>
    [Then("a part of {string} paints with the re-keyed brush {string}")]
    public async Task Then_a_part_paints_with_the_brush(string name, string key)
    {
        // A ScrollViewer's scroll bars show their thumb only while they indicate: scroll a little first.
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            if (ElementRegistry.Resolve(name) is ScrollViewer viewer)
            {
                viewer.ChangeView(null, 40, null, true);
            }
        }).ConfigureAwait(false);
        await SettleAsync().ConfigureAwait(false);
        var (found, seen) = await OnUIThreadAsync(() =>
        {
            var brush = (Brush)Application.Current.Resources[key];
            var element = ElementRegistry.Resolve(name);
            return (UsesBrush(element, brush), BrushesOf(element));
        }).ConfigureAwait(false);
        found.Should().NotBeNull("a template part of \"{0}\" holds the app's \"{1}\" brush (brushes seen: {2})", name, key, seen);
    }

    /// <summary>Shows a text ContentDialog (a Material dialog in @native-overlays scenarios).</summary>
    [Given("the application shows a text ContentDialog named {string} titled {string} saying {string}")]
    public async Task Given_a_text_ContentDialog(string name, string title, string message)
    {
        var root = await ShowRootAsync().ConfigureAwait(false);
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            var dialog = new ContentDialog { Name = name, Title = title, Content = message, CloseButtonText = "OK", XamlRoot = root.XamlRoot };
            Dialogs.Add(dialog);
            _ = dialog.ShowAsync();
        }).ConfigureAwait(false);
        await SettleAsync().ConfigureAwait(false);
    }

    /// <summary>Asserts the Material dialog's surface colour.</summary>
    [Then("the Material dialog's surface is {string}")]
    public async Task Then_the_Material_dialog_surface_is(string colour)
    {
        var fill = await OnUIThreadAsync(() =>
        {
            var background = TopDialog().Native.Window!.DecorView.Background;
            var shape = background is AInsetDrawable inset ? inset.Drawable as AMaterialShapeDrawable : background as AMaterialShapeDrawable;
            return shape?.FillColor?.DefaultColor ?? 0;
        }).ConfigureAwait(false);
        Hex(fill).Should().Be(Hex(colour), "ContentDialogBackground colours the Material dialog's surface");
    }

    /// <summary>Asserts the Material dialog's title and message colour.</summary>
    [Then("the Material dialog's title and message are drawn in {string}")]
    public async Task Then_the_Material_dialog_texts_are(string colour)
    {
        var (title, message) = await OnUIThreadAsync(() =>
        {
            var native = TopDialog().Native;
            var titleId = native.Context.Resources!.GetIdentifier("alertTitle", "id", native.Context.PackageName);
            var titleView = native.FindViewById<ATextView>(titleId);
            var messageView = native.FindViewById<ATextView>(AResource.Id.Message);
            return (titleView?.CurrentTextColor ?? 0, messageView?.CurrentTextColor ?? 0);
        }).ConfigureAwait(false);
        Hex(title).Should().Be(Hex(colour), "ContentDialogForeground colours the title");
        Hex(message).Should().Be(Hex(colour), "ContentDialogForeground colours the message");
    }

    /// <summary>Asserts the Material dialog's dim amount.</summary>
    [Then("the Material dialog dims the window behind it by {int} percent")]
    public async Task Then_the_Material_dialog_dims(int percent)
    {
        var dim = await OnUIThreadAsync(() => TopDialog().Native.Window!.Attributes!.DimAmount).ConfigureAwait(false);
        dim.Should().BeApproximately(percent / 100f, 0.02f, "ContentDialogSmokeFill's alpha is the scrim's dim amount");
    }

    /// <summary>Turns the Material palette on with dynamic colour off.</summary>
    [Given("the Material palette is on with dynamic colour off")]
    public async Task Given_the_Material_palette_without_dynamic_colour()
    {
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            ThemeBridge.DynamicColor = false;
            ThemeBridge.MaterialPalette = true;
        }).ConfigureAwait(false);
        await SettleAsync().ConfigureAwait(false);
    }

    /// <summary>Switches the Material palette on or off.</summary>
    [When("the Material palette is switched {word}")]
    public async Task When_the_Material_palette_is_switched(string state)
    {
        await TestTargetFixture.RunOnUIThreadAsync(() => ThemeBridge.MaterialPalette = state == "on").ConfigureAwait(false);
        await SettleAsync().ConfigureAwait(false);
    }

    /// <summary>Switches dynamic colour on.</summary>
    [When("dynamic colour is switched on")]
    public async Task When_dynamic_colour_is_switched_on()
    {
        await TestTargetFixture.RunOnUIThreadAsync(() => ThemeBridge.DynamicColor = true).ConfigureAwait(false);
        await SettleAsync().ConfigureAwait(false);
    }

    /// <summary>Asserts a key resolves to a Material role of the palette in use.</summary>
    [Then("the key {string} resolves to the Material role {string}")]
    public async Task Then_the_key_resolves_to_the_role(string key, string role)
    {
        var (actual, expected) = await OnUIThreadAsync(() => (ThemeResources.FindColor(null, key) ?? 0, Roles()[role])).ConfigureAwait(false);
        Hex(actual).Should().Be(Hex(expected), "the theme bridge writes {0} into {1}", role, key);
    }

    /// <summary>Asserts a key still resolves to the app's colour.</summary>
    [Then("the key {string} still resolves to {string}")]
    public async Task Then_the_key_still_resolves_to(string key, string colour)
    {
        var actual = await OnUIThreadAsync(() => ThemeResources.FindColor(null, key) ?? 0).ConfigureAwait(false);
        Hex(actual).Should().Be(Hex(colour), "a key the app defines is never written by the theme bridge");
    }

    /// <summary>Asserts a framework key is a baseline (non-dynamic) Material role.</summary>
    [Then("the framework's {string} is the baseline Material role {string}")]
    public async Task Then_the_framework_key_is_the_baseline_role(string key, string role)
    {
        var (actual, expected) = await OnUIThreadAsync(() => (ThemeResources.FindColor(null, key) ?? 0, ThemeBridge.ReadRole(role, IsDark(), dynamic: false) ?? 1)).ConfigureAwait(false);
        Hex(actual).Should().Be(Hex(expected), "with dynamic colour off the baseline Material palette is written into {0}", key);
    }

    /// <summary>Asserts a framework key is the role of the palette in use (dynamic when available).</summary>
    [Then("the framework's {string} is the Material role {string} of the palette in use")]
    public async Task Then_the_framework_key_is_the_role_in_use(string key, string role)
    {
        var (actual, expected, dynamic) = await OnUIThreadAsync(() =>
            (ThemeResources.FindColor(null, key) ?? 0, ThemeBridge.ReadRole(role, IsDark(), ThemeBridge.UsesDynamicColor) ?? 1, ThemeBridge.UsesDynamicColor)).ConfigureAwait(false);
        Hex(actual).Should().Be(Hex(expected), "the {0} palette's {1} is written into {2}", dynamic ? "dynamic" : "baseline (no dynamic colour on this device)", role, key);
    }

    /// <summary>Remembers the colour a framework key resolves to.</summary>
    [Given("the framework's colour of {string} is remembered")]
    public async Task Given_the_framework_colour_is_remembered(string key) =>
        _scenarioContext[ColourKey + key] = await OnUIThreadAsync(() => ThemeResources.FindColor(null, key) ?? 0).ConfigureAwait(false);

    /// <summary>Asserts a framework key's colour changed.</summary>
    [Then("the framework's colour of {string} changed")]
    public async Task Then_the_framework_colour_changed(string key)
    {
        var now = await OnUIThreadAsync(() => ThemeResources.FindColor(null, key) ?? 0).ConfigureAwait(false);
        Hex(now).Should().NotBe(Hex((int)_scenarioContext[ColourKey + key]), "the Material palette re-colours {0}", key);
    }

    /// <summary>Asserts a framework key's colour is the remembered one.</summary>
    [Then("the framework's colour of {string} is the remembered one")]
    public async Task Then_the_framework_colour_is_remembered(string key)
    {
        var now = await OnUIThreadAsync(() => ThemeResources.FindColor(null, key) ?? 0).ConfigureAwait(false);
        Hex(now).Should().Be(Hex((int)_scenarioContext[ColourKey + key]), "switching the palette off puts the Fluent colour of {0} back", key);
    }

    /// <summary>Remembers the running activity.</summary>
    [Given("the running activity is remembered")]
    public void Given_the_activity_is_remembered() => _scenarioContext[ActivityKey] = AppHost.Activity;

    /// <summary>Switches the app's night mode on (the native layer's day/night).</summary>
    [When("the app's night mode is switched on")]
    public async Task When_the_night_mode_is_switched_on()
    {
        await TestTargetFixture.RunOnUIThreadAsync(() => AAppCompatDelegate.DefaultNightMode = AAppCompatDelegate.ModeNightYes).ConfigureAwait(false);
        await SettleAsync().ConfigureAwait(false);
    }

    /// <summary>Lets the app's night mode follow the system again.</summary>
    [When("the app's night mode follows the system again")]
    public async Task When_the_night_mode_follows_the_system()
    {
        await TestTargetFixture.RunOnUIThreadAsync(() => AAppCompatDelegate.DefaultNightMode = AAppCompatDelegate.ModeNightFollowSystem).ConfigureAwait(false);
        await SettleAsync().ConfigureAwait(false);
    }

    /// <summary>Asserts the activity was not recreated.</summary>
    [Then("the running activity is the remembered one")]
    public async Task Then_the_activity_is_the_remembered_one()
    {
        var (same, destroyed) = await OnUIThreadAsync(() => (ReferenceEquals(AppHost.Activity, _scenarioContext[ActivityKey]), AppHost.Activity?.IsDestroyed ?? true)).ConfigureAwait(false);
        same.Should().BeTrue("a day/night flip does not recreate the activity (it handles uiMode changes)");
        destroyed.Should().BeFalse("the activity is alive");
    }

    /// <summary>Asserts the activity's configuration is in night mode.</summary>
    [Then("the activity's configuration is in night mode")]
    public async Task Then_the_activity_is_in_night_mode()
    {
        var night = await OnUIThreadAsync(() => Activity().Resources!.Configuration!.UiMode & AUiMode.NightMask).ConfigureAwait(false);
        night.Should().Be(AUiMode.NightYes, "the activity's configuration follows the app's night mode");
    }

    // ---------------------------------------------------------------- type scale

    /// <summary>Shows a TextBlock with a FontSize in a fresh root.</summary>
    [Given("the application shows a TextBlock named {string} with FontSize {int}")]
    public async Task Given_a_TextBlock(string name, int fontSize)
    {
        ElementRegistry.Clear();
        var root = await ShowRootAsync().ConfigureAwait(false);
        await AddTextBlockAsync(root, name, fontSize, true, null).ConfigureAwait(false);
    }

    /// <summary>Adds a TextBlock that ignores the font scale.</summary>
    [Given("the application also shows a TextBlock named {string} with FontSize {int} that ignores the font scale")]
    public async Task Given_a_fixed_TextBlock(string name, int fontSize) =>
        await AddTextBlockAsync((Panel)ElementRegistry.Resolve(RootName), name, fontSize, false, null).ConfigureAwait(false);

    /// <summary>Shows a TextBlock with a framework TextBlock style.</summary>
    [Given("the application shows a TextBlock named {string} styled {string}")]
    public async Task Given_a_styled_TextBlock(string name, string styleKey)
    {
        ElementRegistry.Clear();
        var root = await ShowRootAsync().ConfigureAwait(false);
        await AddTextBlockAsync(root, name, 0, true, styleKey).ConfigureAwait(false);
    }

    /// <summary>Sets the font scale (the policy's override: only the system settings change the real one).</summary>
    [When("the font scale is {float}")]
    public async Task When_the_font_scale_is(float scale)
    {
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            TypeScale.FontScaleOverride = scale;
            TypeScalePolicy.Refresh();
        }).ConfigureAwait(false);
        await SettleAsync().ConfigureAwait(false);
    }

    /// <summary>Goes back to the system's font scale.</summary>
    [When("the font scale is the system's again")]
    public async Task When_the_font_scale_is_the_systems()
    {
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            TypeScale.FontScaleOverride = null;
            TypeScalePolicy.Refresh();
        }).ConfigureAwait(false);
        await SettleAsync().ConfigureAwait(false);
    }

    /// <summary>Asserts the native text size in dp.</summary>
    [Then("the native text size of {string} is {int} dp")]
    public async Task Then_the_native_text_size_is(string name, int dp)
    {
        var size = await OnUIThreadAsync(() => ((ATextView)NativeViewLocator.ViewOf(ElementRegistry.Resolve(name))).TextSize / Density()).ConfigureAwait(false);
        size.Should().BeApproximately(dp, 0.5, "the native text size of \"{0}\" in dp", name);
    }

    /// <summary>Asserts the native line height in dp.</summary>
    [Then("the native line height of {string} is {int} dp")]
    public async Task Then_the_native_line_height_is(string name, int dp)
    {
        var height = await OnUIThreadAsync(() => ((ATextView)NativeViewLocator.ViewOf(ElementRegistry.Resolve(name))).LineHeight / Density()).ConfigureAwait(false);
        height.Should().BeApproximately(dp, 1.0, "the native line height of \"{0}\" in dp", name);
    }

    // -------------------------------------------------------------------- motion

    /// <summary>Asserts that Core animations are being ticked from the frame clock.</summary>
    [Then("Core animations are being ticked")]
    public async Task Then_Core_animations_are_ticked()
    {
        var before = await OnUIThreadAsync(() => CoreAnimationTicker.FrameCount).ConfigureAwait(false);
        await Task.Delay(150, TestContext.Current.CancellationToken).ConfigureAwait(false);
        var after = await OnUIThreadAsync(() => CoreAnimationTicker.FrameCount).ConfigureAwait(false);
        after.Should().BeGreaterThan(before, "a running Storyboard is advanced on display frames");
    }

    /// <summary>Asserts that no Core animation asks for frames any more.</summary>
    [Then("Core animations are no longer ticked")]
    public async Task Then_Core_animations_are_no_longer_ticked()
    {
        await SettleAsync().ConfigureAwait(false);
        var (ticking, subscribers) = await OnUIThreadAsync(() => (CoreAnimationTicker.IsTicking, CoreAnimationTicker.HasSubscribers())).ConfigureAwait(false);
        subscribers.Should().BeFalse("a finished Storyboard no longer subscribes to CompositionTarget.Rendering");
        ticking.Should().BeFalse("with nothing animating the ticker requests no frames");
    }

    /// <summary>Sets the motion policy.</summary>
    [Given("the motion policy is {string}")]
    public Task Given_the_motion_policy(string mode) => TestTargetFixture.RunOnUIThreadAsync(() => MotionPolicy.Mode = Enum.Parse<MotionMode>(mode));

    /// <summary>Shows an indeterminate ProgressBar.</summary>
    [Given("the application shows an indeterminate ProgressBar named {string}")]
    public async Task Given_an_indeterminate_ProgressBar(string name)
    {
        ElementRegistry.Clear();
        var root = await ShowRootAsync().ConfigureAwait(false);
        await AddAsync(root, "IndeterminateProgressBar", name).ConfigureAwait(false);
    }

    /// <summary>Asserts the CodeBrix motion clock drives the native indicator.</summary>
    [Then("the native indicator of {string} is driven by the CodeBrix motion clock")]
    public async Task Then_the_indicator_is_driven(string name)
    {
        var (driven, animators) = await OnUIThreadAsync(() => (IndeterminateProgressDriver.IsDriving(Indicator(name)), global::Android.Animation.ValueAnimator.AreAnimatorsEnabled())).ConfigureAwait(false);
        animators.Should().BeFalse("the UIReqs runner removes the system's animations");
        driven.Should().BeTrue("the always-animate policy drives the frozen indicator");
    }

    /// <summary>Asserts the CodeBrix motion clock does not drive the native indicator.</summary>
    [Then("the native indicator of {string} is not driven by the CodeBrix motion clock")]
    public async Task Then_the_indicator_is_not_driven(string name)
    {
        var driven = await OnUIThreadAsync(() => IndeterminateProgressDriver.IsDriving(Indicator(name))).ConfigureAwait(false);
        driven.Should().BeFalse("following the system leaves the frozen indicator still");
    }

    /// <summary>Asserts the native indicator moves.</summary>
    [Then("the native indicator of {string} moves within {int} milliseconds")]
    public async Task Then_the_indicator_moves(string name, int milliseconds)
    {
        var first = await OnUIThreadAsync(() => Indicator(name).Progress).ConfigureAwait(false);
        await Task.Delay(milliseconds, TestContext.Current.CancellationToken).ConfigureAwait(false);
        var second = await OnUIThreadAsync(() => Indicator(name).Progress).ConfigureAwait(false);
        second.Should().NotBe(first, "the driven indicator's segment changes from frame to frame");
    }

    // ------------------------------------------------------------------- helpers

    private static CodeBrixActivity Activity() => AppHost.Activity ?? throw new InvalidOperationException("No scenario activity.");

    private static double Density() => Activity().Resources!.DisplayMetrics!.Density;

    private static NavigationView Navigation(string name) => (NavigationView)ElementRegistry.Resolve(name);

    private static NavigationViewHandler Handler(string name) =>
        PolicyDiagnostics.HandlerOf(Navigation(name)) as NavigationViewHandler ?? throw new InvalidOperationException($"\"{name}\" has no NavigationView handler.");

    private static Google.Android.Material.ProgressIndicator.LinearProgressIndicator Indicator(string name) =>
        (Google.Android.Material.ProgressIndicator.LinearProgressIndicator)NativeViewLocator.ViewOf(ElementRegistry.Resolve(name));

    private static NativeContentDialog TopDialog() =>
        NativeOverlays.Open.OfType<NativeContentDialog>().LastOrDefault(d => d.IsShowing) ?? throw new InvalidOperationException("No Material dialog is showing.");

    private static bool IsDark() => Application.Current.RequestedTheme == ApplicationTheme.Dark;

    private static IReadOnlyDictionary<string, int> Roles() => IsDark() ? ThemeBridge.DarkRoles : ThemeBridge.LightRoles;

    private static string Hex(string colour)
    {
        var c = Colors.Parse(colour);
        return $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";
    }

    private static string Hex(int argb) => "#" + argb.ToString("X8", CultureInfo.InvariantCulture);

    private static IEnumerable<string> Split(string list) => list.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0);

    private static List<string> Titles(AMenu menu)
    {
        var titles = new List<string>();
        void Walk(AMenu m)
        {
            for (var i = 0; i < m.Size(); i++)
            {
                var item = m.GetItem(i)!;
                if (item.HasSubMenu && item.SubMenu is { } sub)
                {
                    Walk(sub);
                }
                else
                {
                    titles.Add(item.TitleFormatted?.ToString() ?? string.Empty);
                }
            }
        }

        if (menu != null)
        {
            Walk(menu);
        }

        return titles;
    }

    private static AMenuItem Find(AMenu menu, string title)
    {
        if (menu == null)
        {
            return null;
        }

        for (var i = 0; i < menu.Size(); i++)
        {
            var item = menu.GetItem(i)!;
            if (item.HasSubMenu && item.SubMenu is { } sub && Find(sub, title) is { } nested)
            {
                return nested;
            }

            if (string.Equals(item.TitleFormatted?.ToString(), title, StringComparison.Ordinal))
            {
                return item;
            }
        }

        return null;
    }

    private static global::Android.Graphics.Rect ScreenRect(AView view)
    {
        if (view == null)
        {
            return new global::Android.Graphics.Rect();
        }

        var location = new int[2];
        view.GetLocationInWindow(location);
        return new global::Android.Graphics.Rect(location[0], location[1], location[0] + view.Width, location[1] + view.Height);
    }

    private static async Task<StackPanel> ShowRootAsync()
    {
        StackPanel root = null;
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            root = new StackPanel { Name = RootName, Spacing = 16, Padding = new Thickness(24), Background = new SolidColorBrush(Colors.White) };
            ElementRegistry.Register(RootName, root);
        }).ConfigureAwait(false);
        await TestTargetFixture.SetContentAsync(root).ConfigureAwait(false);
        await SettleAsync().ConfigureAwait(false);
        return root;
    }

    private static async Task AddAsync(Panel root, string kind, string name)
    {
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            FrameworkElement element = kind switch
            {
                "Button" => new Button { Content = "Go", Width = 200, Height = 56 },
                "AccentButton" => new Button { Content = "Go", Width = 200, Height = 56, Style = ThemeResources.TryFind(null, "AccentButtonStyle", out var accent) ? accent as Style : null },
                "CheckBox" => new CheckBox { Content = "Agree" },
                "Slider" => new Slider { Width = 300, Minimum = 0, Maximum = 100, Value = 40 },
                "TextBox" => new TextBox { Width = 300, Text = "abc" },
                "ProgressBar" => new ProgressBar { Width = 300, Value = 60 },
                "IndeterminateProgressBar" => new ProgressBar { Width = 300, IsIndeterminate = true },
                "ComboBox" => ComboBox(),
                "SelectedListView" => ListView(),
                "ScrollViewer" => Scroller(),
                _ => throw new NotSupportedException($"The AndroidPolicy steps cannot show a \"{kind}\"."),
            };
            element.Name = name;
            root.Children.Add(element);
            ElementRegistry.Register(name, element);
        }).ConfigureAwait(false);
        await SettleAsync().ConfigureAwait(false);
    }

    private static async Task AddTextBlockAsync(Panel root, string name, int fontSize, bool scales, string styleKey)
    {
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            var block = new TextBlock { Name = name, Text = "Type scale", IsTextScaleFactorEnabled = scales };
            if (styleKey != null)
            {
                block.Style = ThemeResources.TryFind(null, styleKey, out var style) ? style as Style : throw new InvalidOperationException($"No framework style \"{styleKey}\".");
            }
            else
            {
                block.FontSize = fontSize;
            }

            root.Children.Add(block);
            ElementRegistry.Register(name, block);
        }).ConfigureAwait(false);
        await SettleAsync().ConfigureAwait(false);
    }

    private static ComboBox ComboBox()
    {
        var combo = new ComboBox { Width = 240 };
        combo.Items.Add("One");
        combo.Items.Add("Two");
        combo.SelectedIndex = 0;
        return combo;
    }

    private static ListView ListView()
    {
        var list = new ListView { Height = 160, SelectionMode = ListViewSelectionMode.Single };
        list.Items.Add("First");
        list.Items.Add("Second");
        list.Items.Add("Third");
        list.SelectedIndex = 0;
        return list;
    }

    private static ScrollViewer Scroller()
    {
        var content = new StackPanel();
        for (var i = 0; i < 30; i++)
        {
            content.Children.Add(new TextBlock { Text = "Line " + i.ToString(CultureInfo.InvariantCulture) });
        }

        return new ScrollViewer { Height = 120, Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Visible };
    }

    private static DependencyObject UsesBrush(DependencyObject root, Brush brush)
    {
        var stack = new Stack<DependencyObject>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            foreach (var property in current.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!typeof(Brush).IsAssignableFrom(property.PropertyType) || property.GetIndexParameters().Length > 0)
                {
                    continue;
                }

                object value;
                try
                {
                    value = property.GetValue(current);
                }
                catch (Exception exception) when (exception is not OutOfMemoryException)
                {
                    continue;
                }

                if (ReferenceEquals(value, brush))
                {
                    return current;
                }
            }

            var count = VisualTreeHelper.GetChildrenCount(current);
            for (var i = 0; i < count; i++)
            {
                stack.Push(VisualTreeHelper.GetChild(current, i));
            }
        }

        return null;
    }

    private static string BrushesOf(DependencyObject root)
    {
        var seen = new List<string>();
        var stack = new Stack<DependencyObject>();
        stack.Push(root);
        while (stack.Count > 0 && seen.Count < 40)
        {
            var current = stack.Pop();
            foreach (var property in current.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (typeof(Brush).IsAssignableFrom(property.PropertyType) && property.GetIndexParameters().Length == 0)
                {
                    try
                    {
                        if (property.GetValue(current) is SolidColorBrush solid)
                        {
                            seen.Add($"{current.GetType().Name}{((current as FrameworkElement)?.Name is { Length: > 0 } n ? "#" + n : string.Empty)}.{property.Name}={solid.Color}");
                        }
                    }
                    catch (Exception exception) when (exception is not OutOfMemoryException)
                    {
                        _ = exception;
                    }
                }
            }

            var count = VisualTreeHelper.GetChildrenCount(current);
            for (var i = 0; i < count; i++)
            {
                stack.Push(VisualTreeHelper.GetChild(current, i));
            }
        }

        return string.Join("; ", seen);
    }

    private static async Task<(DialogForm Form, bool FullSize, double Card, double Window)> DialogStateAsync(string name)
    {
        await SettleAsync().ConfigureAwait(false);
        return await OnUIThreadAsync(() =>
        {
            var dialog = (ContentDialog)ElementRegistry.Resolve(name);
            var card = Find(dialog, "BackgroundElement") as FrameworkElement;
            return (ContentDialogPolicy.FormOf(dialog), dialog.FullSizeDesired, card?.ActualHeight ?? 0, dialog.XamlRoot?.Size.Height ?? 0);
        }).ConfigureAwait(false);
    }

    private static DependencyObject Find(DependencyObject root, string name)
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is FrameworkElement { Name: var n } && n == name)
            {
                return child;
            }

            if (Find(child, name) is { } nested)
            {
                return nested;
            }
        }

        return null;
    }

    private async Task AssertStatesAsync(string part, string name, bool isChecked, string rest, string hovered, string pressed, string disabled)
    {
        var actual = new[]
        {
            await ColourAsync(part, name, isChecked, "rest").ConfigureAwait(false),
            await ColourAsync(part, name, isChecked, "hovered").ConfigureAwait(false),
            await ColourAsync(part, name, isChecked, "pressed").ConfigureAwait(false),
            await ColourAsync(part, name, isChecked, "disabled").ConfigureAwait(false),
        };
        actual.Should().Equal(new[] { Hex(rest), Hex(hovered), Hex(pressed), Hex(disabled) }, "the {0} colours of \"{1}\" at rest, hovered, pressed and disabled", part, name);
    }

    private static async Task<string> ColourAsync(string part, string name, bool isChecked, string state)
    {
        await SettleAsync().ConfigureAwait(false);
        var argb = await OnUIThreadAsync(() =>
        {
            var element = ElementRegistry.Resolve(name);
            var list = ListOf(element, part, out var single);
            if (list == null)
            {
                return single;
            }

            var states = new List<int>();
            if (state != "disabled")
            {
                states.Add(AResource.Attribute.StateEnabled);
            }

            if (isChecked)
            {
                states.Add(AResource.Attribute.StateChecked);
            }

            switch (state)
            {
                case "hovered":
                    states.Add(AResource.Attribute.StateHovered);
                    break;
                case "pressed":
                    states.Add(AResource.Attribute.StatePressed);
                    break;
                case "focused":
                    states.Add(AResource.Attribute.StateFocused);
                    break;
            }

            return list.GetColorForState(states.ToArray(), new global::Android.Graphics.Color(1));
        }).ConfigureAwait(false);
        return Hex(argb);
    }

    private static AColorStateList ListOf(UIElement element, string part, out int single)
    {
        single = 0;
        switch (PolicyDiagnostics.HandlerOf(element))
        {
            case ButtonHandler button when button.MaterialButton is { } material:
                return part switch
                {
                    "background" => material.BackgroundTintList,
                    "text" => material.TextColors,
                    _ => throw new NotSupportedException($"A Button has no \"{part}\" colour."),
                };
            case CheckBoxHandler checkBox:
                return part switch
                {
                    "box" => checkBox.PlatformView.ButtonTintList,
                    "glyph" => checkBox.PlatformView.ButtonIconTintList,
                    "text" => checkBox.PlatformView.TextColors,
                    _ => throw new NotSupportedException($"A CheckBox has no \"{part}\" colour."),
                };
            case SliderHandler slider:
                return part switch
                {
                    "active track" => slider.PlatformView.Slider.TrackActiveTintList,
                    "inactive track" => slider.PlatformView.Slider.TrackInactiveTintList,
                    "thumb" => slider.PlatformView.Slider.ThumbTintList,
                    _ => throw new NotSupportedException($"A Slider has no \"{part}\" colour."),
                };
            case TextBoxHandler text:
                switch (part)
                {
                    case "end icon":
                        return ThemeKeyAppliers.EndIconTintOf(text.PlatformView.Field);
                    case "stroke":
                        // TextInputLayout reports the stroke colour of its focused state.
                        single = text.PlatformView.Field.BoxStrokeColor;
                        return null;
                    case "box":
                        single = text.PlatformView.Field.BoxBackgroundColor;
                        return null;
                    case "highlight":
                        single = text.EditText.HighlightColor;
                        return null;
                    default:
                        throw new NotSupportedException($"A TextBox has no \"{part}\" colour.");
                }

            case ProgressBarHandler progress when part == "indicator":
                single = progress.PlatformView.GetIndicatorColor()![0];
                return null;
            default:
                throw new NotSupportedException($"No native \"{part}\" colour for {element.GetType().Name}.");
        }
    }

    private static async Task<T> OnUIThreadAsync<T>(Func<T> func)
    {
        T result = default;
        await TestTargetFixture.RunOnUIThreadAsync(() => result = func()).ConfigureAwait(false);
        return result;
    }

    private static async Task SettleAsync()
    {
        await Task.Delay(SettleDelay, TestContext.Current.CancellationToken).ConfigureAwait(false);
        await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
    }
}
