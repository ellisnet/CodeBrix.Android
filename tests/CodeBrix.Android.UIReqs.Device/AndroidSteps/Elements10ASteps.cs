using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CodeBrix.Android.UI.Diagnostics;
using CodeBrix.Android.UI.Handlers;
using CodeBrix.Android.UIReqs.Device.Runtime;
using CodeBrix.Platform.UI.Core.UIReqs.Hosting;
using CodeBrix.Platform.UI.Core.UIReqs.Support;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Reqnroll;
using SilverAssertions;
using AInputSourceType = Android.Views.InputSourceType;
using AMotionEvent = Android.Views.MotionEvent;
using AMotionEventActions = Android.Views.MotionEventActions;
using ASystemClock = Android.OS.SystemClock;
using ATextView = Android.Widget.TextView;
using AToolbar = AndroidX.AppCompat.Widget.Toolbar;
using AView = Android.Views.View;
using AViewGroup = Android.Views.ViewGroup;
using AViewStates = Android.Views.ViewStates;
using WColors = Microsoft.UI.Colors;

namespace CodeBrix.Android.UIReqs.Device.AndroidSteps;

/// <summary>
/// AP10-A: the steps of the Android-only group AndroidElements10A (every remaining Platform element, first lane: the
/// CommandBar / AppBar family, NavigationView items, SplitView, TitleBar, TwoPaneView, the Pivot / SelectorBar /
/// FlipView families, PipsPager, PagerControl, BreadcrumbBar, ListBox, ItemsView, grouped lists, RadioButtons, the
/// calendars and the picker presenters). Elements are built by sample name (<see cref="Samples"/>).
/// </summary>
[Binding]
public sealed class Elements10ASteps
{
    private static readonly Dictionary<string, int> Counts = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, object> Values = new(StringComparer.Ordinal);
    private static CalendarDatePickerHandler? _calendarDialog;

    /// <summary>Follows the CalendarDatePicker dialogs (for the whole run).</summary>
    [BeforeTestRun(Order = 61)]
    public static void Follow_the_calendar_dialogs() => CalendarDatePickerHandler.DialogShown += (_, handler) => _calendarDialog = handler;

    /// <summary>Forgets the counters and puts the native CommandBar switch back after every scenario.</summary>
    [AfterScenario]
    public static void Forget_the_AP10A_state()
    {
        CommandBarHandler.Enabled = true;
        Counts.Clear();
        Values.Clear();
        _calendarDialog = null;
    }

    // ------------------------------------------------------------------ building

    /// <summary>Shows one of the AP10-A samples, registered by name (its parts too, by their own names).</summary>
    [Given("the application shows the AP10 sample {string} named {string}")]
    public async Task Given_the_sample(string sample, string name)
    {
        FrameworkElement element = null!;
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            element = Samples.Create(sample, name);
            element.Name = name;
            if (element.ReadLocalValue(FrameworkElement.HorizontalAlignmentProperty) == DependencyProperty.UnsetValue)
            {
                element.HorizontalAlignment = HorizontalAlignment.Left;
            }

            element.VerticalAlignment = VerticalAlignment.Top;
            ElementRegistry.Register(name, element);
        }).ConfigureAwait(false);
        await TestTargetFixture.SetContentAsync(element).ConfigureAwait(false);
        await SettleAsync().ConfigureAwait(false);
    }

    /// <summary>Turns the native CommandBar off (AppContext switch CodeBrix.Android.UI.NativeCommandBar = false).</summary>
    [Given("the native CommandBar is switched off")]
    public static void Given_the_native_CommandBar_is_switched_off() => CommandBarHandler.Enabled = false;

    /// <summary>Counts an event of an element (Click, SelectedIndexChanged, ItemClicked, DateChanged, ...).</summary>
    [Given("the {word} events of {string} are counted")]
    public async Task Given_the_events_are_counted(string eventName, string name)
    {
        Counts[name + "." + eventName] = 0;
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            var element = ElementRegistry.Resolve(name);
            void Count() => Counts[name + "." + eventName] = Counts.GetValueOrDefault(name + "." + eventName) + 1;
            switch (element, eventName)
            {
                case (ButtonBase button, "Click"):
                    button.Click += (_, _) => Count();
                    break;
                case (AppBar bar, "Opened"):
                    bar.Opened += (_, _) => Count();
                    break;
                case (AppBar bar, "Closed"):
                    bar.Closed += (_, _) => Count();
                    break;
                case (PipsPager pips, "SelectedIndexChanged"):
                    pips.SelectedIndexChanged += (_, _) => Count();
                    break;
                case (PagerControl pager, "SelectedIndexChanged"):
                    pager.SelectedIndexChanged += (_, _) => Count();
                    break;
                case (BreadcrumbBar bar, "ItemClicked"):
                    bar.ItemClicked += (_, args) =>
                    {
                        Count();
                        Values[name + ".ItemClicked"] = args.Index + ":" + args.Item;
                    };
                    break;
                case (CalendarView calendar, "SelectedDatesChanged"):
                    calendar.SelectedDatesChanged += (_, _) => Count();
                    break;
                case (CalendarDatePicker picker, "DateChanged"):
                    picker.DateChanged += (_, _) => Count();
                    break;
                case (CalendarDatePicker picker, "Opened"):
                    picker.Opened += (_, _) => Count();
                    break;
                case (CalendarDatePicker picker, "Closed"):
                    picker.Closed += (_, _) => Count();
                    break;
                case (SplitView split, "PaneClosed"):
                    split.PaneClosed += (_, _) => Count();
                    break;
                case (RadioButtons radios, "SelectionChanged"):
                    radios.SelectionChanged += (_, _) => Count();
                    break;
                default:
                    throw new InvalidOperationException($"no counter for {eventName} of {element.GetType().Name}");
            }
        }).ConfigureAwait(false);
    }

    /// <summary>Asserts how many times a counted event was raised.</summary>
    [Then("{string} raised {word} {int} times")]
    public async Task Then_raised(string name, string eventName, int times)
    {
        await SettleAsync().ConfigureAwait(false);
        Counts.GetValueOrDefault(name + "." + eventName).Should().Be(times, "{0} must have raised {1} {2} times", name, eventName, times);
    }

    // --------------------------------------------------------------- generic trees

    /// <summary>Asserts that the element's Core visual tree holds an element of a type.</summary>
    [Then("the Core tree of {string} holds a {word}")]
    public async Task Then_the_Core_tree_holds(string name, string typeName)
    {
        var found = await OnUIThreadAsync(() => CoreTypes(ElementRegistry.Resolve(name)).ToList()).ConfigureAwait(false);
        found.Should().Contain(typeName, "the Core tree of \"{0}\" must hold a {1}; it holds [{2}]", name, typeName, string.Join(", ", found.Distinct()));
    }

    /// <summary>Asserts that the element's Core visual tree holds no element of a type.</summary>
    [Then("the Core tree of {string} holds no {word}")]
    public async Task Then_the_Core_tree_holds_no(string name, string typeName)
    {
        var found = await OnUIThreadAsync(() => CoreTypes(ElementRegistry.Resolve(name)).ToList()).ConfigureAwait(false);
        found.Should().NotContain(typeName);
    }

    /// <summary>Asserts the size Core gave an element (DIPs).</summary>
    [Then("{string} takes no room")]
    public async Task Then_takes_no_room(string name)
    {
        var size = await OnUIThreadAsync(() => (ElementRegistry.Resolve(name).ActualWidth, ElementRegistry.Resolve(name).ActualHeight)).ConfigureAwait(false);
        (size.ActualWidth * size.ActualHeight).Should().Be(0);
    }

    /// <summary>Asserts that a public element type of the Platform carries its NotImplemented marker.</summary>
    [Then("the Platform marks {word} as not implemented")]
    public void Then_the_Platform_marks_not_implemented(string typeName)
    {
        var type = FindPlatformType(typeName);
        type.Should().NotBeNull("the Platform must have a type {0}", typeName);
        type!.GetCustomAttributes(false).Select(a => a.GetType().Name).Should().Contain("NotImplementedAttribute");
    }

    /// <summary>AP1.12: asserts that a public element type of the Platform is implemented (no NotImplemented marker).</summary>
    [Then("the Platform implements {word}")]
    public void Then_the_Platform_implements(string typeName)
    {
        var type = FindPlatformType(typeName);
        type.Should().NotBeNull("the Platform must have a type {0}", typeName);
        type!.GetCustomAttributes(false).Select(a => a.GetType().Name).Should().NotContain("NotImplementedAttribute");
    }

    /// <summary>Asserts that the Platform has no public element type of that name.</summary>
    [Then("the Platform has no public element named {word}")]
    public void Then_the_Platform_has_no_element(string typeName) => FindPlatformType(typeName).Should().BeNull();

    /// <summary>Asserts that a native view inside an element is left of (or above) another element's native view.</summary>
    [Then("the native view of {string} is left of the native view of {string}")]
    public async Task Then_left_of(string left, string right)
    {
        var (a, b) = await OnUIThreadAsync(() => (WindowRect(NativeViewLocator.ViewOf(ElementRegistry.Resolve(left))!), WindowRect(NativeViewLocator.ViewOf(ElementRegistry.Resolve(right))!))).ConfigureAwait(false);
        a.Right.Should().BeLessThanOrEqualTo(b.Left + 1, "\"{0}\" must be left of \"{1}\"", left, right);
    }

    // --------------------------------------------------------------- CommandBar

    /// <summary>Asserts the Material app bar a CommandBar is shown as.</summary>
    [Then("the CommandBar {string} is a Material {word} titled {string}")]
    public async Task Then_the_CommandBar_is(string name, string widget, string title)
    {
        var (type, shown) = await OnUIThreadAsync(() =>
        {
            var bar = HandlerLookup.Of<CommandBarHandler>(ElementRegistry.Resolve(name))!.Bar!;
            return (bar.GetType().Name, bar.Title ?? string.Empty);
        }).ConfigureAwait(false);
        type.Should().Be(widget);
        shown.Should().Be(title);
    }

    /// <summary>Asserts the actions (shown on the bar) and overflow items of a native CommandBar.</summary>
    [Then("the CommandBar {string} has the actions {string} and the overflow items {string}")]
    public async Task Then_the_CommandBar_has(string name, string actions, string overflow)
    {
        var (shownActions, overflowItems) = await OnUIThreadAsync(() =>
        {
            var bar = HandlerLookup.Of<CommandBarHandler>(ElementRegistry.Resolve(name))!.Bar!;
            var onBar = Descendants(bar).Where(v => v.GetType().Name == "ActionMenuItemView").Select(v => v.ContentDescription ?? string.Empty).ToList();
            var all = new List<string>();
            for (var i = 0; i < bar.Menu!.Size(); i++)
            {
                all.Add(bar.Menu.GetItem(i)!.TitleFormatted?.ToString() ?? string.Empty);
            }

            return (onBar, all.Except(onBar).ToList());
        }).ConfigureAwait(false);
        string.Join(", ", shownActions).Should().Be(actions);
        string.Join(", ", overflowItems).Should().Be(overflow);
    }

    /// <summary>Asserts how many menu groups (divided in the overflow) the native bar has.</summary>
    [Then("the overflow of the CommandBar {string} has {int} groups")]
    public async Task Then_the_overflow_has_groups(string name, int groups)
    {
        var count = await OnUIThreadAsync(() =>
        {
            var menu = HandlerLookup.Of<CommandBarHandler>(ElementRegistry.Resolve(name))!.Bar!.Menu!;
            var ids = new HashSet<int>();
            for (var i = 0; i < menu.Size(); i++)
            {
                if (menu.GetItem(i) is { GroupId: > 0 } item)
                {
                    ids.Add(item.GroupId);
                }
            }

            return ids.Count;
        }).ConfigureAwait(false);
        count.Should().Be(groups);
    }

    /// <summary>A real finger taps the action of a native CommandBar that carries a label.</summary>
    [When("a real finger taps the action {string} of the CommandBar {string}")]
    public async Task When_a_real_finger_taps_the_action(string label, string name)
    {
        var center = await OnUIThreadAsync(() =>
        {
            var bar = HandlerLookup.Of<CommandBarHandler>(ElementRegistry.Resolve(name))!.Bar!;
            var view = Descendants(bar).First(v => v.GetType().Name == "ActionMenuItemView" && v.ContentDescription == label);
            return Center(view);
        }).ConfigureAwait(false);
        await RealTapAsync(center.X, center.Y).ConfigureAwait(false);
    }

    /// <summary>Invokes an overflow item of a native CommandBar as the overflow popup does.</summary>
    [When("the overflow item {string} of the CommandBar {string} is chosen")]
    public async Task When_the_overflow_item_is_chosen(string label, string name)
    {
        await OnUIThreadAsync(() =>
        {
            var bar = HandlerLookup.Of<CommandBarHandler>(ElementRegistry.Resolve(name))!.Bar!;
            var menu = bar.Menu!;
            for (var i = 0; i < menu.Size(); i++)
            {
                if (menu.GetItem(i) is { } item && item.TitleFormatted?.ToString() == label)
                {
                    menu.PerformIdentifierAction(item.ItemId, 0);
                    bar.HideOverflowMenu();
                    return true;
                }
            }

            throw new InvalidOperationException("no overflow item " + label);
        }).ConfigureAwait(false);
        await SettleAsync().ConfigureAwait(false);
    }

    /// <summary>Sets IsOpen of an AppBar from Core.</summary>
    [When("IsOpen of {string} is set to {word}")]
    public async Task When_IsOpen_is_set(string name, string value)
    {
        await OnUIThreadAsync(() => ((AppBar)ElementRegistry.Resolve(name)).IsOpen = bool.Parse(value)).ConfigureAwait(false);
        await SettleAsync(600).ConfigureAwait(false);
    }

    /// <summary>Asserts whether the native overflow menu of a CommandBar is showing, and Core's IsOpen with it.</summary>
    [Then("the overflow menu of the CommandBar {string} is {word}")]
    public async Task Then_the_overflow_menu_is(string name, string state)
    {
        var open = state == "showing";
        var ok = await PollAsync(() =>
        {
            var bar = (CommandBar)ElementRegistry.Resolve(name);
            return HandlerLookup.Of<CommandBarHandler>(bar)!.Bar!.IsOverflowMenuShowing == open && bar.IsOpen == open;
        }).ConfigureAwait(false);
        ok.Should().BeTrue("the overflow menu of \"{0}\" must be {1} and IsOpen must follow it", name, state);
    }

    /// <summary>Asserts the enabled state of a native bar's menu item.</summary>
    [Then("the menu item {string} of the CommandBar {string} is {word}")]
    public async Task Then_the_menu_item_is(string label, string name, string state)
    {
        var item = await OnUIThreadAsync(() => MenuItem(name, label)).ConfigureAwait(false);
        (state switch
        {
            "enabled" => item.IsEnabled,
            "disabled" => !item.IsEnabled,
            "checked" => item.IsChecked,
            "unchecked" => !item.IsChecked,
            _ => throw new ArgumentException(state),
        }).Should().BeTrue("the menu item {0} must be {1}", label, state);
    }

    /// <summary>Asserts that a native bar has a menu item with a title.</summary>
    [Then("the CommandBar {string} has a menu item {string}")]
    public async Task Then_the_CommandBar_has_a_menu_item(string name, string label)
    {
        var found = await PollAsync(() => TryMenuItem(name, label) != null).ConfigureAwait(false);
        found.Should().BeTrue("the CommandBar \"{0}\" must have the menu item {1}", name, label);
    }

    /// <summary>Changes a Core property of an element (Label, IsEnabled, IsChecked, NumberOfPages, ...).</summary>
    [When("the Core property {word} of {string} becomes {string}")]
    public async Task When_a_property_is_set(string property, string name, string value)
    {
        await OnUIThreadAsync(() =>
        {
            var element = ElementRegistry.Resolve(name);
            var info = element.GetType().GetProperty(property)!;
            var type = Nullable.GetUnderlyingType(info.PropertyType) ?? info.PropertyType;
            object converted = type.IsEnum ? Enum.Parse(type, value) : type == typeof(DateTimeOffset) ? DateTimeOffset.Parse(value, System.Globalization.CultureInfo.InvariantCulture) : Convert.ChangeType(value, type, System.Globalization.CultureInfo.InvariantCulture);
            info.SetValue(element, converted);
            return true;
        }).ConfigureAwait(false);
        await SettleAsync(400).ConfigureAwait(false);
    }

    /// <summary>Asserts a Core property value (by its string form).</summary>
    [Then("the Core property {word} of {string} is {string}")]
    public async Task Then_a_property_is(string property, string name, string value)
    {
        await SettleAsync().ConfigureAwait(false);
        var actual = await OnUIThreadAsync(() =>
        {
            var element = ElementRegistry.Resolve(name);
            var raw = element.GetType().GetProperty(property)!.GetValue(element);
            return raw switch
            {
                null => "null",
                DateTimeOffset d => d.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                bool b => b ? "True" : "False",
                _ => Convert.ToString(raw, System.Globalization.CultureInfo.InvariantCulture) ?? "null",
            };
        }).ConfigureAwait(false);
        actual.Should().Be(value);
    }

    /// <summary>A real finger taps a point of an element's native view given as percentages of its size.</summary>
    [When("a real finger taps {string} at {int} percent across and {int} percent down")]
    public async Task When_a_real_finger_taps_at(string name, int across, int down)
    {
        var point = await OnUIThreadAsync(() =>
        {
            var view = NativeViewLocator.ViewOf(ElementRegistry.Resolve(name))!;
            var r = WindowRect(view);
            return (X: r.Left + (int)Math.Round(r.Width() * across / 100.0), Y: r.Top + (int)Math.Round(r.Height() * down / 100.0));
        }).ConfigureAwait(false);
        await RealTapAsync(point.X, point.Y).ConfigureAwait(false);
    }

    /// <summary>A real finger taps the native view inside an element that shows a text or content description.</summary>
    [When("a real finger taps the native {string} of {string}")]
    public async Task When_a_real_finger_taps_the_native(string text, string name)
    {
        var center = await OnUIThreadAsync(() =>
        {
            var view = Descendants(NativeViewLocator.ViewOf(ElementRegistry.Resolve(name))!)
                .Where(v => v.Visibility == AViewStates.Visible)
                .First(v => (v is ATextView t && t.Text == text) || v.ContentDescription == text);
            return Center(view);
        }).ConfigureAwait(false);
        await RealTapAsync(center.X, center.Y).ConfigureAwait(false);
    }

    /// <summary>Asserts that a native view inside an element shows a text or description, visible or not.</summary>
    [Then("{string} has a {word} native {string}")]
    public async Task Then_has_a_native(string name, string visibility, string text)
    {
        var state = await OnUIThreadAsync(() =>
        {
            var view = Descendants(NativeViewLocator.ViewOf(ElementRegistry.Resolve(name))!)
                .FirstOrDefault(v => (v is ATextView t && t.Text == text) || v.ContentDescription == text);
            return view == null ? "missing" : view.Visibility == AViewStates.Visible ? (view.Enabled ? "visible" : "disabled") : "hidden";
        }).ConfigureAwait(false);
        state.Should().Be(visibility, "the native \"{0}\" of \"{1}\"", text, name);
    }

    // --------------------------------------------------------------- SplitView

    /// <summary>Asserts the display-mode state a SplitView's template shows.</summary>
    [Then("the SplitView {string} shows the state {word}")]
    public async Task Then_the_SplitView_shows_the_state(string name, string state)
    {
        var shown = "";
        var ok = await PollAsync(() => (shown = HandlerLookup.Of<SplitViewHandler>(ElementRegistry.Resolve(name))?.CurrentState ?? "none") == state).ConfigureAwait(false);
        ok.Should().BeTrue("the SplitView \"{0}\" must show {1}; it shows {2}", name, state, shown);
    }

    // ------------------------------------------------------------ paging

    /// <summary>Asserts the pips a native PipsPager shows (first page number and count; the selected page).</summary>
    [Then("the PipsPager {string} shows {int} pips from page {int} with page {int} selected")]
    public async Task Then_the_PipsPager_shows(string name, int count, int firstPage, int selectedPage)
    {
        var (pips, first, selected) = await OnUIThreadAsync(() =>
        {
            var handler = HandlerLookup.Of<PipsPagerHandler>(ElementRegistry.Resolve(name))!;
            var index = handler.Pips.Select((p, i) => (p, i)).FirstOrDefault(t => t.p.Selected).i;
            return (handler.Pips.Count, handler.FirstPip + 1, handler.FirstPip + index + 1);
        }).ConfigureAwait(false);
        pips.Should().Be(count);
        first.Should().Be(firstPage);
        selected.Should().Be(selectedPage);
    }

    /// <summary>A real finger taps a pip (numbered from 1 among the pips shown).</summary>
    [When("a real finger taps pip {int} of the PipsPager {string}")]
    public async Task When_a_real_finger_taps_pip(int pip, string name)
    {
        var center = await OnUIThreadAsync(() => Center(HandlerLookup.Of<PipsPagerHandler>(ElementRegistry.Resolve(name))!.Pips[pip - 1])).ConfigureAwait(false);
        await RealTapAsync(center.X, center.Y).ConfigureAwait(false);
    }

    /// <summary>Asserts the selector a native PagerControl shows.</summary>
    [Then("the PagerControl {string} shows a {word} selector showing {string}")]
    public async Task Then_the_PagerControl_shows(string name, string kind, string text)
    {
        var (shownKind, shownText) = await OnUIThreadAsync(() =>
        {
            var handler = HandlerLookup.Of<PagerControlHandler>(ElementRegistry.Resolve(name))!;
            return (handler.SelectorKind?.ToString() ?? "none", handler.DropDown?.Text ?? handler.Field?.Text ?? string.Empty);
        }).ConfigureAwait(false);
        shownKind.Should().Be(kind);
        if (kind != "ButtonPanel")
        {
            shownText.Should().Be(text);
        }
    }

    /// <summary>Chooses a page from the drop-down menu of a native PagerControl (opened by a real finger).</summary>
    [When("page {int} is chosen from the drop-down of the PagerControl {string}")]
    public async Task When_page_is_chosen_from_the_drop_down(int page, string name)
    {
        var center = await OnUIThreadAsync(() => Center(HandlerLookup.Of<PagerControlHandler>(ElementRegistry.Resolve(name))!.DropDown!)).ConfigureAwait(false);
        await RealTapAsync(center.X, center.Y).ConfigureAwait(false);
        await SettleAsync(400).ConfigureAwait(false);
        await OnUIThreadAsync(() =>
        {
            var menu = HandlerLookup.Of<PagerControlHandler>(ElementRegistry.Resolve(name))!.LastMenu!;
            menu.Menu.PerformIdentifierAction(page - 1, 0);
            menu.Dismiss();
            return true;
        }).ConfigureAwait(false);
        await SettleAsync().ConfigureAwait(false);
    }

    /// <summary>Types a page number into the number field of a native PagerControl and commits it (IME Done).</summary>
    [When("{string} is typed into the number field of the PagerControl {string}")]
    public async Task When_typed_into_the_number_field(string text, string name)
    {
        await OnUIThreadAsync(() =>
        {
            var handler = HandlerLookup.Of<PagerControlHandler>(ElementRegistry.Resolve(name))!;
            handler.Field!.Text = text;
            handler.Field.OnEditorAction(global::Android.Views.InputMethods.ImeAction.Done);
            return true;
        }).ConfigureAwait(false);
        await SettleAsync().ConfigureAwait(false);
    }

    /// <summary>Asserts the crumbs of a native BreadcrumbBar.</summary>
    [Then("the BreadcrumbBar {string} shows the crumbs {string} with {int} buttons")]
    public async Task Then_the_BreadcrumbBar_shows(string name, string crumbs, int buttons)
    {
        var (texts, count) = await OnUIThreadAsync(() =>
        {
            var handler = HandlerLookup.Of<BreadcrumbBarHandler>(ElementRegistry.Resolve(name))!;
            return (string.Join(", ", handler.Texts), handler.Buttons.Count);
        }).ConfigureAwait(false);
        texts.Should().Be(crumbs);
        count.Should().Be(buttons);
    }

    /// <summary>Asserts the last ItemClicked a BreadcrumbBar raised ("index:item").</summary>
    [Then("the last ItemClicked of {string} was {string}")]
    public void Then_the_last_ItemClicked_was(string name, string value) =>
        (Values.GetValueOrDefault(name + ".ItemClicked") as string).Should().Be(value);

    /// <summary>Adds an item to the sample's observable ItemsSource.</summary>
    [When("the crumb {string} is added to {string}")]
    public async Task When_the_crumb_is_added(string crumb, string name)
    {
        await OnUIThreadAsync(() =>
        {
            ((ObservableCollection<string>)((BreadcrumbBar)ElementRegistry.Resolve(name)).ItemsSource!).Add(crumb);
            return true;
        }).ConfigureAwait(false);
        await SettleAsync(400).ConfigureAwait(false);
    }

    // ------------------------------------------------------------ calendars

    /// <summary>Asserts the day the native calendar shows as selected.</summary>
    [Then("the native calendar of {string} shows {string}")]
    public async Task Then_the_native_calendar_shows(string name, string day)
    {
        var shown = await OnUIThreadAsync(() =>
        {
            var view = (global::Android.Widget.CalendarView)NativeViewLocator.ViewOf(ElementRegistry.Resolve(name))!;
            var calendar = Java.Util.Calendar.Instance;
            calendar.TimeInMillis = view.Date;
            return $"{calendar.Get(Java.Util.CalendarField.Year):D4}-{calendar.Get(Java.Util.CalendarField.Month) + 1:D2}-{calendar.Get(Java.Util.CalendarField.DayOfMonth):D2}";
        }).ConfigureAwait(false);
        shown.Should().Be(day);
    }

    /// <summary>Asserts the native calendar's bounds.</summary>
    [Then("the native calendar of {string} runs from {string} to {string}")]
    public async Task Then_the_native_calendar_runs(string name, string from, string to)
    {
        var (min, max) = await OnUIThreadAsync(() =>
        {
            var view = (global::Android.Widget.CalendarView)NativeViewLocator.ViewOf(ElementRegistry.Resolve(name))!;
            return (Day(view.MinDate), Day(view.MaxDate));
        }).ConfigureAwait(false);
        min.Should().Be(from);
        max.Should().Be(to);
    }

    /// <summary>A day is picked on the native calendar (the path of its date-change listener).</summary>
    [When("the day {string} is picked on the native calendar of {string}")]
    public async Task When_the_day_is_picked(string day, string name)
    {
        var date = DateTime.Parse(day, System.Globalization.CultureInfo.InvariantCulture);
        await OnUIThreadAsync(() =>
        {
            HandlerLookup.Of<CalendarViewHandler>(ElementRegistry.Resolve(name))!.PickDay(date.Year, date.Month, date.Day);
            return true;
        }).ConfigureAwait(false);
        await SettleAsync().ConfigureAwait(false);
    }

    /// <summary>Sets the one selected date of a CalendarView from Core.</summary>
    [When("the selected date of {string} is set to {string}")]
    public async Task When_the_selected_date_is_set(string name, string day)
    {
        var date = DateTimeOffset.Parse(day + "T12:00:00+00:00", System.Globalization.CultureInfo.InvariantCulture);
        await OnUIThreadAsync(() =>
        {
            var dates = ((CalendarView)ElementRegistry.Resolve(name)).SelectedDates;
            dates.Clear();
            dates.Add(date);
            return true;
        }).ConfigureAwait(false);
        await SettleAsync().ConfigureAwait(false);
    }

    /// <summary>Asserts the selected dates of a CalendarView.</summary>
    [Then("the selected dates of {string} are {string}")]
    public async Task Then_the_selected_dates_are(string name, string days)
    {
        await SettleAsync().ConfigureAwait(false);
        var shown = await OnUIThreadAsync(() => string.Join(", ", ((CalendarView)ElementRegistry.Resolve(name)).SelectedDates.Select(d => d.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)))).ConfigureAwait(false);
        shown.Should().Be(days);
    }

    /// <summary>Asserts the text of a native CalendarDatePicker field.</summary>
    [Then("the field of the CalendarDatePicker {string} shows {string} labelled {string}")]
    public async Task Then_the_field_shows(string name, string text, string hint)
    {
        var (shown, label) = await OnUIThreadAsync(() =>
        {
            var handler = HandlerLookup.Of<CalendarDatePickerHandler>(ElementRegistry.Resolve(name))!;
            return (handler.Text, handler.PlatformView.Hint ?? string.Empty);
        }).ConfigureAwait(false);
        shown.Should().Be(text);
        label.Should().Be(hint);
    }

    /// <summary>Asserts that the CalendarDatePicker's Material date picker is showing.</summary>
    [Then("the Material date picker of the CalendarDatePicker is showing")]
    public async Task Then_the_calendar_dialog_is_showing()
    {
        var showing = await PollAsync(() => _calendarDialog?.Dialog is { IsAdded: true, Dialog.IsShowing: true }).ConfigureAwait(false);
        showing.Should().BeTrue("a MaterialDatePicker dialog must be showing");
    }

    /// <summary>Chooses a day in the CalendarDatePicker's dialog as its OK does, and closes it.</summary>
    [When("the CalendarDatePicker's Material date picker chooses {string}")]
    public async Task When_the_calendar_dialog_chooses(string day)
    {
        var date = new DateTimeOffset(DateTime.Parse(day, System.Globalization.CultureInfo.InvariantCulture), TimeSpan.Zero);
        await OnUIThreadAsync(() =>
        {
            _calendarDialog!.Pick(date.ToUnixTimeMilliseconds());
            _calendarDialog.Dialog!.Dismiss();
            return true;
        }).ConfigureAwait(false);
        await SettleAsync(600).ConfigureAwait(false);
    }

    /// <summary>Asserts that no CalendarDatePicker dialog is showing.</summary>
    [Then("no Material date picker of a CalendarDatePicker is showing")]
    public async Task Then_no_calendar_dialog()
    {
        var closed = await PollAsync(() => _calendarDialog == null || _calendarDialog.Dialog == null).ConfigureAwait(false);
        closed.Should().BeTrue();
    }

    /// <summary>Asserts that no Core popup holds a presenter of a type (the Material dialog replaced it).</summary>
    [Then("no open Core popup over {string} holds a {word}")]
    public async Task Then_no_open_popup_holds(string name, string typeName)
    {
        var found = await OnUIThreadAsync(() =>
        {
            var names = new List<string>();
            var root = ElementRegistry.Resolve(name).XamlRoot;
            foreach (var popup in VisualTreeHelper.GetOpenPopupsForXamlRoot(root))
            {
                if (popup.Child is { } child)
                {
                    names.Add(child.GetType().Name);
                    names.AddRange(CoreTypes(child));
                }
            }

            return names;
        }).ConfigureAwait(false);
        found.Should().NotContain(typeName);
    }

    // ------------------------------------------------------------- helpers

    private static Type? FindPlatformType(string typeName)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (!assembly.GetName().Name!.StartsWith("CodeBrix.Platform", StringComparison.Ordinal))
            {
                continue;
            }

            foreach (var ns in new[] { "Microsoft.UI.Xaml.Controls.", "Microsoft.UI.Xaml.Controls.Primitives." })
            {
                if (assembly.GetType(ns + typeName) is { IsPublic: true } type)
                {
                    return type;
                }
            }
        }

        return null;
    }

    private static string Day(long localMillis)
    {
        var calendar = Java.Util.Calendar.Instance;
        calendar.TimeInMillis = localMillis;
        return $"{calendar.Get(Java.Util.CalendarField.Year):D4}-{calendar.Get(Java.Util.CalendarField.Month) + 1:D2}-{calendar.Get(Java.Util.CalendarField.DayOfMonth):D2}";
    }

    private static global::Android.Views.IMenuItem MenuItem(string name, string label) =>
        TryMenuItem(name, label) ?? throw new InvalidOperationException("no menu item " + label);

    private static global::Android.Views.IMenuItem? TryMenuItem(string name, string label)
    {
        var menu = HandlerLookup.Of<CommandBarHandler>(ElementRegistry.Resolve(name))?.Bar?.Menu;
        if (menu == null)
        {
            return null;
        }

        for (var i = 0; i < menu.Size(); i++)
        {
            if (menu.GetItem(i) is { } item && item.TitleFormatted?.ToString() == label)
            {
                return item;
            }
        }

        return null;
    }

    private static IEnumerable<string> CoreTypes(DependencyObject root)
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child.GetType().Name;
            foreach (var nested in CoreTypes(child))
            {
                yield return nested;
            }
        }
    }

    private static IEnumerable<AView> Descendants(AView root)
    {
        var pending = new Stack<AView>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var view = pending.Pop();
            yield return view;
            if (view is AViewGroup group)
            {
                for (var i = group.ChildCount - 1; i >= 0; i--)
                {
                    if (group.GetChildAt(i) is { } child)
                    {
                        pending.Push(child);
                    }
                }
            }
        }
    }

    private static global::Android.Graphics.Rect WindowRect(AView view)
    {
        var location = new int[2];
        view.GetLocationInWindow(location);
        return new global::Android.Graphics.Rect(location[0], location[1], location[0] + view.Width, location[1] + view.Height);
    }

    private static (int X, int Y) Center(AView view)
    {
        var r = WindowRect(view);
        return (r.CenterX(), r.CenterY());
    }

    private static async Task RealTapAsync(int x, int y)
    {
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            var down = ASystemClock.UptimeMillis();
            Dispatch(down, down, AMotionEventActions.Down, x, y);
            Dispatch(down, down + 50, AMotionEventActions.Up, x, y);
        }).ConfigureAwait(false);
        await SettleAsync(300).ConfigureAwait(false);
    }

    private static void Dispatch(long downTime, long eventTime, AMotionEventActions action, int x, int y)
    {
        var e = AMotionEvent.Obtain(downTime, eventTime, action, x, y, 0)!;
        e.SetSource(AInputSourceType.Touchscreen);
        AppHost.Activity!.DispatchTouchEvent(e);
        e.Recycle();
    }

    private static async Task<T> OnUIThreadAsync<T>(Func<T> func)
    {
        T result = default!;
        await TestTargetFixture.RunOnUIThreadAsync(() => result = func()).ConfigureAwait(false);
        return result;
    }

    private static async Task<bool> PollAsync(Func<bool> condition)
    {
        for (var i = 0; i < 40; i++)
        {
            if (await OnUIThreadAsync(condition).ConfigureAwait(false))
            {
                return true;
            }

            await Task.Delay(100).ConfigureAwait(false);
        }

        return false;
    }

    private static async Task SettleAsync(int milliseconds = 200)
    {
        await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
        await Task.Delay(milliseconds).ConfigureAwait(false);
        await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
    }
}
