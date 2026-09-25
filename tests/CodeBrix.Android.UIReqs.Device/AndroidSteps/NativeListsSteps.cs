using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using CodeBrix.Android.UI.Diagnostics;
using CodeBrix.Android.UIReqs.Device.Runtime;
using CodeBrix.Platform.UI.Core.UIReqs.Canvas;
using CodeBrix.Platform.UI.Core.UIReqs.Hosting;
using CodeBrix.Platform.UI.Core.UIReqs.Support;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Reqnroll;
using SilverAssertions;
using AInputSourceType = Android.Views.InputSourceType;
using AMotionEvent = Android.Views.MotionEvent;
using AMotionEventActions = Android.Views.MotionEventActions;
using AMotionEventToolType = Android.Views.MotionEventToolType;
using ASystemClock = Android.OS.SystemClock;
using Colors = CodeBrix.Platform.UI.Core.UIReqs.Support.Colors;

namespace CodeBrix.Android.UIReqs.Device.AndroidSteps;

/// <summary>
/// The steps of the Android-only "Native lists" feature (AndroidFeatures/AndroidItems): RecyclerView-backed
/// ListView / ItemsRepeater built from an observable collection, real finger input dispatched to the
/// activity, collection changes, and what the native list realised.
/// </summary>
[Binding]
public sealed class NativeListsSteps
{
    private static ObservableCollection<string> _rows = new();
    private static int _itemClicks;
    private static string _lastClicked = string.Empty;

    /// <summary>A ListView bound to an ObservableCollection of "Row n" strings.</summary>
    [Given("the application shows a native test ListView named {string} {int} by {int} with {int} rows")]
    public async Task Given_a_list(string name, int width, int height, int rows) =>
        await ShowListAsync(name, width, height, rows, null, null, false).ConfigureAwait(false);

    /// <summary>A ListView whose ItemContainerStyle sets the containers' Background.</summary>
    [Given("the application shows a native test ListView named {string} {int} by {int} with {int} rows whose containers are {string}")]
    public async Task Given_a_styled_list(string name, int width, int height, int rows, string color) =>
        await ShowListAsync(name, width, height, rows, color, null, false).ConfigureAwait(false);

    /// <summary>A ListView with a coloured Header element.</summary>
    [Given("the application shows a native test ListView named {string} {int} by {int} with {int} rows and a {string} header named {string}")]
    public async Task Given_a_list_with_header(string name, int width, int height, int rows, string color, string headerName) =>
        await ShowListAsync(name, width, height, rows, null, (color, headerName), false).ConfigureAwait(false);

    /// <summary>A ListView with item clicks enabled and no selection.</summary>
    [Given("the application shows a native test ListView named {string} {int} by {int} with {int} rows that raise ItemClick")]
    public async Task Given_a_clickable_list(string name, int width, int height, int rows) =>
        await ShowListAsync(name, width, height, rows, null, null, true).ConfigureAwait(false);

    /// <summary>An ItemsRepeater with a UniformGridLayout over coloured tiles.</summary>
    [Given("the application shows a native test ItemsRepeater named {string} {int} by {int} with {int} tiles {int} wide in a uniform grid")]
    public async Task Given_a_repeater(string name, int width, int height, int tiles, int tileWidth)
    {
        FrameworkElement repeater = null!;
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            var items = new ObservableCollection<object>();
            for (var i = 1; i <= tiles; i++)
            {
                var tile = new Border
                {
                    Name = string.Create(CultureInfo.InvariantCulture, $"{name} tile {i}"),
                    Width = tileWidth,
                    Height = 60,
                    Background = new SolidColorBrush(Colors.Parse(i % 2 == 0 ? "Navy" : "Lime")),
                };
                ElementRegistry.Register(tile.Name, tile);
                items.Add(tile);
            }

            repeater = new ItemsRepeater
            {
                Name = name,
                Width = width,
                Height = height,
                ItemsSource = items,
                Layout = new UniformGridLayout { MinItemWidth = tileWidth, MinItemHeight = 60, MinColumnSpacing = 0, MinRowSpacing = 0 },
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            ElementRegistry.Register(name, repeater);
        }).ConfigureAwait(false);
        await TestTargetFixture.SetContentAsync(repeater).ConfigureAwait(false);
        await SettleAsync().ConfigureAwait(false);
    }

    /// <summary>The element's native view is a RecyclerView.</summary>
    [Then("the native view of {string} is a RecyclerView")]
    public async Task Then_native_view_is_recycler(string name)
    {
        var isRecycler = false;
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            var element = ElementRegistry.Resolve(name);
            isRecycler = Recycler(element) != null;
        }).ConfigureAwait(false);
        isRecycler.Should().BeTrue("\"{0}\" must be shown by a native RecyclerView", name);
    }

    /// <summary>How many containers are in Core's tree (the list's items panel).</summary>
    [Then("the ListView {string} has fewer than {int} realised containers")]
    public async Task Then_realised_fewer_than(string name, int maximum)
    {
        var count = -1;
        await TestTargetFixture.RunOnUIThreadAsync(() =>
            count = ((ListViewBase)ElementRegistry.Resolve(name)).ItemsPanelRoot?.Children.Count ?? -1).ConfigureAwait(false);
        count.Should().BeGreaterThanOrEqualTo(0, "\"{0}\" must have an items panel", name);
        count.Should().BeLessThan(maximum, "only the rows on screen of \"{0}\" have containers", name);
    }

    /// <summary>How many view holders the native list ever created (recycling keeps this small).</summary>
    [Then("the ListView {string} created fewer than {int} containers")]
    public async Task Then_created_fewer_than(string name, int maximum)
    {
        var created = -1;
        await TestTargetFixture.RunOnUIThreadAsync(() =>
            created = ((Recycler(ElementRegistry.Resolve(name)) as global::AndroidX.RecyclerView.Widget.RecyclerView)?.GetAdapter() as CodeBrix.Android.UI.Platform.Recycler.CoreItemsAdapter)?.CreatedHolders ?? -1).ConfigureAwait(false);
        created.Should().BeGreaterThan(0, "\"{0}\" must be a native list", name);
        created.Should().BeLessThan(maximum, "the native list \"{0}\" must reuse its containers as it scrolls", name);
    }

    /// <summary>No container exists for an item (it scrolled out and was recycled).</summary>
    [Then("the ListView {string} has no container for item {int}")]
    public async Task Then_no_container(string name, int index)
    {
        DependencyObject? container = null;
        await TestTargetFixture.RunOnUIThreadAsync(() =>
            container = ((ListViewBase)ElementRegistry.Resolve(name)).ContainerFromIndex(index - 1)).ConfigureAwait(false);
        container.Should().BeNull("item {0} of \"{1}\" scrolled out of view", index, name);
    }

    /// <summary>
    /// Calls ListViewBase.ScrollIntoView for an item (1-based). Since pin 1.0.268.12 Core sends it to the list's items
    /// host (ScrollIntoViewRequest), which scrolls the RecyclerView (AP1.9).
    /// </summary>
    [When("item {int} of the ListView {string} is scrolled into view")]
    public async Task When_item_is_scrolled_into_view(int index, string name)
    {
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            var list = (ListViewBase)ElementRegistry.Resolve(name);
            list.ScrollIntoView(list.Items[index - 1]);
        }).ConfigureAwait(false);
        await SettleAsync().ConfigureAwait(false);
        await SettleAsync().ConfigureAwait(false);
    }

    /// <summary>A container exists for an item (1-based): the item is realised in the native list.</summary>
    [Then("the ListView {string} has a container for item {int}")]
    public async Task Then_a_container(string name, int index)
    {
        DependencyObject? container = null;
        await TestTargetFixture.RunOnUIThreadAsync(() =>
            container = ((ListViewBase)ElementRegistry.Resolve(name)).ContainerFromIndex(index - 1)).ConfigureAwait(false);
        container.Should().NotBeNull("item {0} of \"{1}\" must have been brought into view", index, name);
    }

    /// <summary>A real finger drags the middle of a list upwards (slowly: no fling).</summary>
    [When("a real finger drags the list {string} {int} pixels up")]
    public async Task When_real_drag(string name, int distance)
    {
        var (x, startY) = (await DeviceRect.OfAsync(ElementRegistry.Resolve(name)).ConfigureAwait(false)).Center;
        const int steps = 15;
        var down = ASystemClock.UptimeMillis();
        await TestTargetFixture.RunOnUIThreadAsync(() => Touch(down, down, AMotionEventActions.Down, x, startY)).ConfigureAwait(false);
        for (var step = 1; step <= steps; step++)
        {
            var y = startY - (int)Math.Round(distance * (step / (double)steps));
            var time = down + (step * 30);
            await TestTargetFixture.RunOnUIThreadAsync(() => Touch(down, time, AMotionEventActions.Move, x, y)).ConfigureAwait(false);
            await Task.Delay(16).ConfigureAwait(false);
        }

        var end = down + (steps * 30) + 300;
        await TestTargetFixture.RunOnUIThreadAsync(() => Touch(down, end - 100, AMotionEventActions.Move, x, startY - distance)).ConfigureAwait(false);
        await TestTargetFixture.RunOnUIThreadAsync(() => Touch(down, end, AMotionEventActions.Up, x, startY - distance)).ConfigureAwait(false);
        await Task.Delay(400).ConfigureAwait(false);
        await SettleAsync().ConfigureAwait(false);
    }

    /// <summary>A real finger taps the middle of an item's container.</summary>
    [When("a real finger taps item {int} of the list {string}")]
    public async Task When_real_tap(int index, string name)
    {
        FrameworkElement container = null!;
        await TestTargetFixture.RunOnUIThreadAsync(() =>
            container = (FrameworkElement)((ListViewBase)ElementRegistry.Resolve(name)).ContainerFromIndex(index - 1)).ConfigureAwait(false);
        var (x, y) = (await DeviceRect.OfAsync(container).ConfigureAwait(false)).Center;
        var down = ASystemClock.UptimeMillis();
        await TestTargetFixture.RunOnUIThreadAsync(() => Touch(down, down, AMotionEventActions.Down, x, y)).ConfigureAwait(false);
        await Task.Delay(60).ConfigureAwait(false);
        await TestTargetFixture.RunOnUIThreadAsync(() => Touch(down, down + 60, AMotionEventActions.Up, x, y)).ConfigureAwait(false);
        await Task.Delay(300).ConfigureAwait(false);
        await SettleAsync().ConfigureAwait(false);
    }

    /// <summary>Inserts a row into the list's collection (1-based position).</summary>
    [When("the row {string} is inserted at position {int} of the list {string}")]
    public async Task When_row_inserted(string text, int position, string name)
    {
        await TestTargetFixture.RunOnUIThreadAsync(() => _rows.Insert(position - 1, text)).ConfigureAwait(false);
        await SettleAsync().ConfigureAwait(false);
    }

    /// <summary>Removes a row from the list's collection (1-based position).</summary>
    [When("the row at position {int} of the list {string} is removed")]
    public async Task When_row_removed(int position, string name)
    {
        await TestTargetFixture.RunOnUIThreadAsync(() => _rows.RemoveAt(position - 1)).ConfigureAwait(false);
        await SettleAsync().ConfigureAwait(false);
    }

    /// <summary>Clears the list's collection.</summary>
    [When("the collection of the list {string} is cleared")]
    public async Task When_cleared(string name)
    {
        await TestTargetFixture.RunOnUIThreadAsync(() => _rows.Clear()).ConfigureAwait(false);
        await SettleAsync().ConfigureAwait(false);
    }

    /// <summary>Adds rows "Row 1".."Row n" to the list's collection.</summary>
    [When("{int} rows are added to the list {string}")]
    public async Task When_rows_added(int count, string name)
    {
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            for (var i = 1; i <= count; i++)
            {
                _rows.Add(string.Create(CultureInfo.InvariantCulture, $"Row {i}"));
            }
        }).ConfigureAwait(false);
        await SettleAsync().ConfigureAwait(false);
    }

    /// <summary>The header element sits above the first item.</summary>
    [Then("the header {string} of the list {string} is above item {int}")]
    public async Task Then_header_above(string header, string name, int index)
    {
        FrameworkElement container = null!;
        await TestTargetFixture.RunOnUIThreadAsync(() =>
            container = (FrameworkElement)((ListViewBase)ElementRegistry.Resolve(name)).ContainerFromIndex(index - 1)).ConfigureAwait(false);
        var headerRect = await DeviceRect.OfAsync(ElementRegistry.Resolve(header), 0).ConfigureAwait(false);
        var itemRect = await DeviceRect.OfAsync(container, 0).ConfigureAwait(false);
        (headerRect.Y + headerRect.Height).Should().BeLessThanOrEqualTo(itemRect.Y + 1,
            "the header must end above item {0}: header {1}, item {2}", index, headerRect, itemRect);
    }

    /// <summary>ItemClick count and the last clicked item.</summary>
    [Then("the list {string} raised ItemClick {int} times, last with {string}")]
    public void Then_item_click(string name, int times, string item)
    {
        _itemClicks.Should().Be(times, "ItemClick of \"{0}\" was counted", name);
        _lastClicked.Should().Be(item, "the clicked item of \"{0}\" was asserted", name);
    }

    /// <summary>A tile of a repeater sits to the right of another, on the same row.</summary>
    [Then("tile {int} of the repeater {string} is to the right of tile {int}")]
    public async Task Then_tile_right(int index, string name, int other)
    {
        var rect = await DeviceRect.OfAsync(ElementRegistry.Resolve(Tile(name, index)), 0).ConfigureAwait(false);
        var otherRect = await DeviceRect.OfAsync(ElementRegistry.Resolve(Tile(name, other)), 0).ConfigureAwait(false);
        rect.X.Should().BeGreaterThan(otherRect.X, "tile {0} must be right of tile {1}: {2} vs {3}", index, other, rect, otherRect);
        rect.Y.Should().Be(otherRect.Y, "tile {0} must be on the row of tile {1}: {2} vs {3}", index, other, rect, otherRect);
    }

    /// <summary>A tile of a repeater sits on a later row than another.</summary>
    [Then("tile {int} of the repeater {string} is below tile {int}")]
    public async Task Then_tile_below(int index, string name, int other)
    {
        var rect = await DeviceRect.OfAsync(ElementRegistry.Resolve(Tile(name, index)), 0).ConfigureAwait(false);
        var otherRect = await DeviceRect.OfAsync(ElementRegistry.Resolve(Tile(name, other)), 0).ConfigureAwait(false);
        rect.Y.Should().BeGreaterThanOrEqualTo(otherRect.Y + otherRect.Height - 1, "tile {0} must be below tile {1}: {2} vs {3}", index, other, rect, otherRect);
    }

    /// <summary>A WrapPanel of coloured blocks named "{name} block n" (odd Lime, even Navy... the last one Navy).</summary>
    [Given("the application shows a native test WrapPanel named {string} {int} wide holding {int} blocks {int} by {int}")]
    public async Task Given_a_wrap_panel(string name, int width, int count, int blockWidth, int blockHeight)
    {
        FrameworkElement panel = null!;
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            var wrap = new WrapPanel { Name = name, Width = width, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            for (var i = 1; i <= count; i++)
            {
                var block = new Border
                {
                    Name = string.Create(CultureInfo.InvariantCulture, $"{name} block {i}"),
                    Width = blockWidth,
                    Height = blockHeight,
                    Background = new SolidColorBrush(Colors.Parse(i % 2 == 1 && i != count ? "Lime" : "Navy")),
                };
                ElementRegistry.Register(block.Name, block);
                wrap.Children.Add(block);
            }

            ElementRegistry.Register(name, wrap);
            panel = wrap;
        }).ConfigureAwait(false);
        await TestTargetFixture.SetContentAsync(panel).ConfigureAwait(false);
        await SettleAsync().ConfigureAwait(false);
    }

    /// <summary>A block of a panel sits below another.</summary>
    [Then("block {int} of the panel {string} is below block {int}")]
    public async Task Then_block_below(int index, string name, int other)
    {
        var rect = await DeviceRect.OfAsync(ElementRegistry.Resolve(Block(name, index)), 0).ConfigureAwait(false);
        var otherRect = await DeviceRect.OfAsync(ElementRegistry.Resolve(Block(name, other)), 0).ConfigureAwait(false);
        rect.Y.Should().BeGreaterThanOrEqualTo(otherRect.Y + otherRect.Height - 1, "block {0} must be below block {1}: {2} vs {3}", index, other, rect, otherRect);
    }

    /// <summary>A block of a panel sits to the right of another on its row.</summary>
    [Then("block {int} of the panel {string} is to the right of block {int}")]
    public async Task Then_block_right(int index, string name, int other)
    {
        var rect = await DeviceRect.OfAsync(ElementRegistry.Resolve(Block(name, index)), 0).ConfigureAwait(false);
        var otherRect = await DeviceRect.OfAsync(ElementRegistry.Resolve(Block(name, other)), 0).ConfigureAwait(false);
        rect.X.Should().BeGreaterThan(otherRect.X, "block {0} must be right of block {1}", index, other);
        rect.Y.Should().Be(otherRect.Y, "block {0} must be on the row of block {1}", index, other);
    }

    /// <summary>A TabView whose tabs carry Lime / Navy / Orange pages.</summary>
    [Given("the application shows a native test TabView named {string} with {int} tabs")]
    public async Task Given_a_tab_view(string name, int count)
    {
        FrameworkElement element = null!;
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            var tabs = new TabView { Name = name, Width = 600, Height = 400, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            for (var i = 0; i < count; i++)
            {
                tabs.TabItems.Add(new TabViewItem { Header = string.Create(CultureInfo.InvariantCulture, $"Tab {i + 1}"), IsClosable = false, Content = SectionPanel(i) });
            }

            tabs.SelectionChanged += (s, _) => EventRecorder.Record(((FrameworkElement)s).Name, ItemsElements.SelectionChangedEvent);
            ElementRegistry.Register(name, tabs);
            element = tabs;
        }).ConfigureAwait(false);
        await TestTargetFixture.SetContentAsync(element).ConfigureAwait(false);
        await SettleAsync().ConfigureAwait(false);
        EventRecorder.Clear();
    }

    /// <summary>A Pivot whose sections carry Lime / Navy / Orange pages.</summary>
    [Given("the application shows a native test Pivot named {string} with {int} sections")]
    public async Task Given_a_pivot(string name, int count)
    {
        FrameworkElement element = null!;
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            var pivot = new Pivot { Name = name, Width = 600, Height = 400, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            for (var i = 0; i < count; i++)
            {
                pivot.Items.Add(new PivotItem { Header = string.Create(CultureInfo.InvariantCulture, $"Section {i + 1}"), Content = SectionPanel(i) });
            }

            pivot.SelectionChanged += (s, _) => EventRecorder.Record(((FrameworkElement)s).Name, ItemsElements.SelectionChangedEvent);
            ElementRegistry.Register(name, pivot);
            element = pivot;
        }).ConfigureAwait(false);
        await TestTargetFixture.SetContentAsync(element).ConfigureAwait(false);
        await SettleAsync().ConfigureAwait(false);
        EventRecorder.Clear();
    }

    /// <summary>The element shows a Material TabLayout with a number of tabs.</summary>
    [Then("the element {string} shows a native tab strip with {int} tabs")]
    public async Task Then_native_tab_strip(string name, int count)
    {
        var tabs = -1;
        await TestTargetFixture.RunOnUIThreadAsync(() => tabs = TabLayoutOf(ElementRegistry.Resolve(name))?.TabCount ?? -1).ConfigureAwait(false);
        tabs.Should().Be(count, "\"{0}\" must show a native TabLayout with {1} tabs", name, count);
    }

    /// <summary>A real finger taps one tab of the element's native tab strip.</summary>
    [When("a real finger taps native tab {int} of {string}")]
    public async Task When_real_tap_tab(int index, string name)
    {
        var (x, y) = (0, 0);
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            var layout = TabLayoutOf(ElementRegistry.Resolve(name)) ?? throw new InvalidOperationException($"\"{name}\" has no native tab strip.");
            var tab = layout.GetTabAt(index - 1)?.View ?? throw new InvalidOperationException($"\"{name}\" has no tab {index}.");
            var location = new int[2];
            tab.GetLocationInWindow(location);
            (x, y) = (location[0] + (tab.Width / 2), location[1] + (tab.Height / 2));
        }).ConfigureAwait(false);
        var down = ASystemClock.UptimeMillis();
        await TestTargetFixture.RunOnUIThreadAsync(() => Touch(down, down, AMotionEventActions.Down, x, y)).ConfigureAwait(false);
        await Task.Delay(60).ConfigureAwait(false);
        await TestTargetFixture.RunOnUIThreadAsync(() => Touch(down, down + 60, AMotionEventActions.Up, x, y)).ConfigureAwait(false);
        await Task.Delay(400).ConfigureAwait(false);
        await SettleAsync().ConfigureAwait(false);
    }

    private static global::Google.Android.Material.Tabs.TabLayout? TabLayoutOf(FrameworkElement element) =>
        Find<global::Google.Android.Material.Tabs.TabLayout>(NativeViewLocator.ViewOf(element));

    private static T? Find<T>(global::Android.Views.View? root)
        where T : global::Android.Views.View
    {
        if (root is T match)
        {
            return match;
        }

        if (root is global::Android.Views.ViewGroup group)
        {
            for (var i = 0; i < group.ChildCount; i++)
            {
                if (Find<T>(group.GetChildAt(i)) is { } found)
                {
                    return found;
                }
            }
        }

        return null;
    }

    private static Border SectionPanel(int index) => new()
    {
        Width = 400,
        Height = 200,
        Background = new SolidColorBrush(Colors.Parse(index switch { 0 => "Lime", 1 => "Navy", _ => "Orange" })),
    };

    private static string Block(string name, int index) => string.Create(CultureInfo.InvariantCulture, $"{name} block {index}");

    private static string Tile(string name, int index) => string.Create(CultureInfo.InvariantCulture, $"{name} tile {index}");

    private static async Task ShowListAsync(string name, int width, int height, int rows, string? containerColor, (string Color, string Name)? header, bool clicks)
    {
        FrameworkElement list = null!;
        await TestTargetFixture.RunOnUIThreadAsync(() =>
        {
            _rows = new ObservableCollection<string>(Enumerable.Range(1, rows).Select(i => string.Create(CultureInfo.InvariantCulture, $"Row {i}")));
            _itemClicks = 0;
            _lastClicked = string.Empty;
            var view = new ListView
            {
                Name = name,
                Width = width,
                Height = height,
                ItemsSource = _rows,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            if (containerColor != null)
            {
                var style = new Style(typeof(ListViewItem));
                style.Setters.Add(new Setter(Control.BackgroundProperty, new SolidColorBrush(Colors.Parse(containerColor))));
                view.ItemContainerStyle = style;
            }

            if (header is { } h)
            {
                var head = new Border { Name = h.Name, Height = 40, Background = new SolidColorBrush(Colors.Parse(h.Color)) };
                ElementRegistry.Register(h.Name, head);
                view.Header = head;
            }

            if (clicks)
            {
                view.SelectionMode = ListViewSelectionMode.None;
                view.IsItemClickEnabled = true;
                view.ItemClick += (_, e) =>
                {
                    _itemClicks++;
                    _lastClicked = e.ClickedItem?.ToString() ?? string.Empty;
                };
            }

            ItemsElements.RecordSelectionChanges(view);
            ElementRegistry.Register(name, view);
            list = view;
        }).ConfigureAwait(false);
        await TestTargetFixture.SetContentAsync(list).ConfigureAwait(false);
        await SettleAsync().ConfigureAwait(false);
        EventRecorder.Clear();
    }

    private static global::Android.Views.View? Recycler(FrameworkElement element)
    {
        var view = NativeViewLocator.ViewOf(element);
        if (view is global::AndroidX.RecyclerView.Widget.RecyclerView direct)
        {
            return direct;
        }

        // A list's view holds its native list (the view of its internal scroll viewer).
        if (view is global::Android.Views.ViewGroup group)
        {
            for (var i = 0; i < group.ChildCount; i++)
            {
                if (group.GetChildAt(i) is global::AndroidX.RecyclerView.Widget.RecyclerView child)
                {
                    return child;
                }
            }
        }

        return null;
    }

    private static async Task SettleAsync()
    {
        // The native list lays out on the next Android frame after a change; give it two, then drain Core.
        await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
        await Task.Delay(100).ConfigureAwait(false);
        await TestTargetFixture.RunOnUIThreadAsync(() => VirtualApplication.Running?.Root.UpdateLayout()).ConfigureAwait(false);
        await TestTargetFixture.WaitForIdleAsync().ConfigureAwait(false);
    }

    private static void Touch(long downTime, long eventTime, AMotionEventActions action, int x, int y)
    {
        var properties = new AMotionEvent.PointerProperties { Id = 0, ToolType = AMotionEventToolType.Finger };
        var coords = new AMotionEvent.PointerCoords { X = x, Y = y, Pressure = action == AMotionEventActions.Up ? 0f : 1f, Size = 1f };
        var e = AMotionEvent.Obtain(downTime, eventTime, action, 1, new[] { properties }, new[] { coords }, 0, 0, 1f, 1f, 1, 0, AInputSourceType.Touchscreen, 0)!;
        AppHost.Activity!.DispatchTouchEvent(e);
        e.Recycle();
    }
}
