using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CodeBrix.Platform.UI.Core.UIReqs.Hosting;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using WColors = Microsoft.UI.Colors;

namespace CodeBrix.Android.UIReqs.Device.AndroidSteps;

/// <summary>AP10-A: the elements the AndroidElements10A scenarios show, by sample name (parts registered by name).</summary>
internal static class Samples
{
    /// <summary>Builds a sample (on the UI thread).</summary>
    /// <param name="sample">The sample name.</param>
    /// <param name="name">The element's registered name (parts get their own documented names).</param>
    /// <returns>The element.</returns>
    internal static FrameworkElement Create(string sample, string name) => sample switch
    {
        "top command bar" => TopCommandBar(),
        "command bar with an element container" => ContainerCommandBar(),
        "command bar flyout row" => FlyoutRow(),
        "app bar" => new AppBar { Width = 300, Content = new TextBlock { Text = "Plain app bar" } },
        "app bar button" => new AppBarButton { Label = "Add", Icon = new SymbolIcon(Symbol.Add) },
        "app bar toggle button" => new AppBarToggleButton { Label = "Bold", Icon = new SymbolIcon(Symbol.Bold) },
        "navigation view" => Navigation(),
        "inline split view" => Split(),
        "title bar" => TitleBarSample(),
        "two pane view" => new TwoPaneView { Width = 900, Height = 300, Pane1 = Reg("pane1", Box(WColors.Red)), Pane2 = Reg("pane2", Box(WColors.Blue)) },
        "pivot" => PivotSample(),
        "pivot panel" => new PivotPanel { Width = 200, Height = 100 },
        "native pivot presenter" => new NativePivotPresenter { Width = 300, Height = 100 },
        "selector bar" => SelectorBarSample(),
        "flip view" => FlipSample(),
        "pips pager" => new PipsPager { NumberOfPages = 5 },
        "long pips pager" => new PipsPager { NumberOfPages = 12, MaxVisiblePips = 5, SelectedPageIndex = 6 },
        "wrapping pips pager" => new PipsPager { NumberOfPages = 3, WrapMode = PipsPagerWrapMode.Wrap, PreviousButtonVisibility = PipsPagerButtonVisibility.Visible, NextButtonVisibility = PipsPagerButtonVisibility.Visible },
        "pager control" => new PagerControl { NumberOfPages = 5 },
        "number box pager" => new PagerControl { NumberOfPages = 20, DisplayMode = PagerControlDisplayMode.NumberBox },
        "button panel pager" => new PagerControl { NumberOfPages = 5, DisplayMode = PagerControlDisplayMode.ButtonPanel },
        "breadcrumb bar" => new BreadcrumbBar { ItemsSource = new ObservableCollection<string> { "Home", "Documents", "Design" } },
        "list box" => ListBoxSample(),
        "items view" => new ItemsView { Width = 300, Height = 200, ItemsSource = new[] { "Red", "Green", "Blue" } },
        "grouped list view" => Grouped(new ListView()),
        "grouped grid view" => Grouped(new GridView()),
        "radio buttons" => RadioSample(),
        "carousel panel" => new CarouselPanel { Width = 200, Height = 100 },
        "calendar view" => new CalendarView
        {
            Width = 360,
            Height = 360,
            MinDate = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            MaxDate = new DateTimeOffset(2026, 12, 31, 0, 0, 0, TimeSpan.Zero),
            SelectionMode = CalendarViewSelectionMode.Single,
        },
        // AP1.10 (D-P1.10-1): the captured calendar frame must not depend on the day the suite runs. The platform
        // calendar always has a selected day - today when Core selects none - so this sample selects a fixed day and
        // the native calendar shows March 2026 whatever today is.
        "calendar view on a fixed day" => FixedDayCalendar(),
        "multiple selection calendar view" => new CalendarView { Width = 360, Height = 360, SelectionMode = CalendarViewSelectionMode.Multiple },
        "calendar date picker" => new CalendarDatePicker { Width = 280, PlaceholderText = "Pick a date", Header = "Due" },
        "date picker" => new DatePicker { Date = new DateTimeOffset(2026, 9, 24, 12, 0, 0, TimeSpan.Zero) },
        "time picker" => new TimePicker { Time = new TimeSpan(9, 30, 0) },
        _ => throw new ArgumentException("no AP10 sample " + sample),
    };

    private static CalendarView FixedDayCalendar()
    {
        var calendar = new CalendarView
        {
            Width = 360,
            Height = 360,
            MinDate = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            MaxDate = new DateTimeOffset(2026, 12, 31, 0, 0, 0, TimeSpan.Zero),
            SelectionMode = CalendarViewSelectionMode.Single,
        };
        calendar.SelectedDates.Add(new DateTimeOffset(2026, 3, 14, 12, 0, 0, TimeSpan.Zero));
        return calendar;
    }

    private static T Reg<T>(string name, T element)
        where T : DependencyObject
    {
        if (element is FrameworkElement fe)
        {
            fe.Name = name;
        }

        ElementRegistry.Register(name, (FrameworkElement)(object)element);
        return element;
    }

    private static Border Box(Windows.UI.Color color) => new() { Background = new SolidColorBrush(color) };

    private static CommandBar TopCommandBar()
    {
        var bar = new CommandBar { Width = 600, Content = "Mail", IsDynamicOverflowEnabled = false };
        bar.PrimaryCommands.Add(Reg("add", new AppBarButton { Label = "Add", Icon = new SymbolIcon(Symbol.Add) }));
        bar.PrimaryCommands.Add(Reg("bold", new AppBarToggleButton { Label = "Bold", Icon = new SymbolIcon(Symbol.Bold) }));
        bar.PrimaryCommands.Add(new AppBarSeparator());
        bar.PrimaryCommands.Add(Reg("delete", new AppBarButton { Label = "Delete", Icon = new SymbolIcon(Symbol.Delete) }));
        bar.SecondaryCommands.Add(Reg("settings", new AppBarButton { Label = "Settings" }));
        bar.SecondaryCommands.Add(new AppBarSeparator());
        bar.SecondaryCommands.Add(Reg("about", new AppBarButton { Label = "About" }));
        return bar;
    }

    private static CommandBar ContainerCommandBar()
    {
        var bar = new CommandBar { Width = 400 };
        bar.PrimaryCommands.Add(new AppBarButton { Label = "Add", Icon = new SymbolIcon(Symbol.Add) });
        bar.PrimaryCommands.Add(new AppBarElementContainer { Content = new TextBlock { Text = "Hosted" } });
        return bar;
    }

    private static CommandBar FlyoutRow()
    {
        var bar = new CommandBarFlyoutCommandBar { Width = 300 };
        bar.PrimaryCommands.Add(new AppBarButton { Label = "Cut", Icon = new SymbolIcon(Symbol.Cut) });
        return bar;
    }

    private static NavigationView Navigation()
    {
        var view = new NavigationView { Width = 900, Height = 400, IsSettingsVisible = false };
        view.MenuItems.Add(new NavigationViewItemHeader { Content = "Mail" });
        view.MenuItems.Add(Reg("inbox", new NavigationViewItem { Content = "Inbox", Icon = new SymbolIcon(Symbol.Mail) }));
        view.MenuItems.Add(new NavigationViewItemSeparator());
        view.MenuItems.Add(Reg("sent", new NavigationViewItem { Content = "Sent", Icon = new SymbolIcon(Symbol.Send) }));
        view.Content = new TextBlock { Text = "Page" };
        return view;
    }

    private static SplitView Split() => new()
    {
        Width = 600,
        Height = 300,
        DisplayMode = SplitViewDisplayMode.Inline,
        IsPaneOpen = true,
        OpenPaneLength = 200,
        Pane = Reg("pane", Box(WColors.Red)),
        Content = Reg("content", Box(WColors.Blue)),
    };

#pragma warning disable Uno0001 // TitleBar / ListBox are NotImplemented in the Platform: the scenarios prove exactly that.
    private static FrameworkElement TitleBarSample() => new TitleBar { Title = "Title" };

    private static FrameworkElement ListBoxSample()
    {
        var list = new ListBox { Width = 300, Height = 200 };
        list.Items.Add("a");
        list.Items.Add("b");
        return list;
    }
#pragma warning restore Uno0001

    private static Pivot PivotSample()
    {
        var pivot = new Pivot { Width = 500, Height = 300 };
        pivot.Items.Add(new PivotItem { Header = "One", Content = Reg("p1", new TextBlock { Text = "first" }) });
        pivot.Items.Add(new PivotItem { Header = "Two", Content = Reg("p2", new TextBlock { Text = "second" }) });
        pivot.Items.Add(new PivotItem { Header = "Three", Content = Reg("p3", new TextBlock { Text = "third" }) });
        return pivot;
    }

    private static SelectorBar SelectorBarSample()
    {
        var bar = new SelectorBar();
        bar.Items.Add(Reg("recent", new SelectorBarItem { Text = "Recent" }));
        bar.Items.Add(Reg("shared", new SelectorBarItem { Text = "Shared" }));
        bar.Items.Add(Reg("favorites", new SelectorBarItem { Text = "Favorites" }));
        return bar;
    }

    private static FlipView FlipSample()
    {
        var flip = new FlipView { Width = 300, Height = 200 };
        flip.Items.Add("one");
        flip.Items.Add("two");
        flip.Items.Add("three");
        return flip;
    }

    private static RadioButtons RadioSample()
    {
        var radios = new RadioButtons { Header = "Size" };
        radios.Items.Add("Small");
        radios.Items.Add("Medium");
        radios.Items.Add("Large");
        return radios;
    }

    private static ListViewBase Grouped(ListViewBase list)
    {
        var groups = new List<Group> { new("Fruit", new[] { "Apple", "Pear" }), new("Vegetables", new[] { "Leek" }) };
        var source = new CollectionViewSource { IsSourceGrouped = true, Source = groups };
        list.Width = 300;
        list.Height = 300;
        list.ItemsSource = source.View;
        var header = (DataTemplate)Microsoft.UI.Xaml.Markup.XamlReader.Load(
            "<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'><TextBlock Text='{Binding Key}' /></DataTemplate>");
        list.GroupStyle.Add(new GroupStyle { HeaderTemplate = header });
        return list;
    }

    /// <summary>A group of the grouped-list samples.</summary>
    public sealed class Group : List<string>
    {
        /// <summary>Creates the group.</summary>
        /// <param name="key">The key (the header).</param>
        /// <param name="items">The items.</param>
        public Group(string key, IEnumerable<string> items)
            : base(items) => Key = key;

        /// <summary>The key (the header).</summary>
        public string Key { get; }

        /// <inheritdoc />
        public override string ToString() => Key;
    }
}
