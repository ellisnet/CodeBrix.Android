using System;
using System.Collections.ObjectModel;
using System.IO;
using CodeBrix.Platform.UI.Core.UIReqs.Hosting;
using CodeBrix.Platform.UI.Core.UIReqs.Support;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using WColors = Microsoft.UI.Colors;

namespace CodeBrix.Android.UIReqs.Device.AndroidSteps;

/// <summary>AP10-B: the elements the AndroidElements10B scenarios show, by sample name (parts registered by name).</summary>
internal static class Samples10B
{
    /// <summary>The file the BitmapIcon samples show (a 40 x 20 two-colour PNG written at first use).</summary>
    internal const string IconUri = "ms-appdata:///local/ap10b-icon.png";

    /// <summary>Builds a sample (on the UI thread).</summary>
    /// <param name="sample">The sample name.</param>
    /// <param name="name">The element's registered name (parts get their own documented names).</param>
    /// <returns>The element.</returns>
    internal static FrameworkElement Create(string sample, string name) => sample switch
    {
        // status and feedback
        "info bar" => new InfoBar { Width = 400, Title = "Saved", Message = "The file is safe.", IsOpen = true },
        "error info bar" => new InfoBar { Width = 400, Title = "Failed", Message = "The file is gone.", Severity = InfoBarSeverity.Error, IsOpen = true },
        "closed info bar" => new InfoBar { Width = 400, Title = "Saved", Message = "The file is safe." },
        "info bar with an action" => new InfoBar { Width = 400, Title = "Update", Message = "A new version is ready.", IsOpen = true, ActionButton = Reg("action", new Button { Content = "Restart" }) },
        "info badge" => new InfoBadge { Value = 5 },
        "dot info badge" => new InfoBadge(),
        "icon info badge" => new InfoBadge { IconSource = new SymbolIconSource { Symbol = Symbol.Important } },
        "teaching tip" => new TeachingTip { Title = "Try this", Subtitle = "A tip about the page" },
        "progress ring" => new ProgressRing { Width = 64, Height = 64, IsActive = true },
        "determinate progress ring" => new ProgressRing { Width = 64, Height = 64, IsActive = true, IsIndeterminate = false, Value = 40 },
        "refresh container" => Refresh(),
        "refresh visualizer" => new RefreshVisualizer { Width = 60, Height = 60 },
        "swipe control" => Swipe(),
        "execute swipe control" => ExecuteSwipe(),
        "rating control" => new RatingControl(),
        "read-only rating control" => new RatingControl { Value = 2, IsReadOnly = true },
        "person picture" => new PersonPicture { DisplayName = "Ada Lovelace", Width = 96, Height = 96 },
        "person picture with initials and a badge" => new PersonPicture { Initials = "JE", BadgeNumber = 3, Width = 96, Height = 96 },
        "group person picture" => new PersonPicture { IsGroup = true, Width = 96, Height = 96 },
        "person picture with a profile picture" => new PersonPicture { DisplayName = "Ada Lovelace", Width = 96, Height = 96, ProfilePicture = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(IconUri)) },

        // buttons and icons
        "hyperlink button" => new HyperlinkButton { Content = "Docs" },
        "drop down button" => new DropDownButton { Content = "Sort", Flyout = Menu("By name", "By date") },
        "split button" => new SplitButton { Content = "Paste", Flyout = Menu("Paste as text", "Paste as image") },
        "split button with an icon" => new SplitButton { Content = new SymbolIcon(Symbol.Add), Flyout = Menu("Add file", "Add folder") },
        "toggle split button" => new ToggleSplitButton { Content = "Bullets", Flyout = Menu("Round", "Square") },
        "bitmap icon" => new BitmapIcon { Width = 40, Height = 20, UriSource = new Uri(IconUri), ShowAsMonochrome = false },
        "monochrome bitmap icon" => new BitmapIcon { Width = 40, Height = 20, UriSource = new Uri(IconUri), Foreground = new SolidColorBrush(WColors.Blue) },
        "path icon" => (PathIcon)Microsoft.UI.Xaml.Markup.XamlReader.Load(
            "<PathIcon xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' Width='40' Height='40' Foreground='Green' Data='M0,0 L40,0 L20,40 Z' />"),
        "animated icon" => new AnimatedIcon { Width = 40, Height = 40, FallbackIconSource = new SymbolIconSource { Symbol = Symbol.Accept } },

        // popups, scrolling, colour parts, shapes
        "popup" => new Microsoft.UI.Xaml.Controls.Primitives.Popup { Child = Reg("popup child", new Border { Width = 120, Height = 80, Background = new SolidColorBrush(WColors.Red) }) },
        "native popup base" => new NativePopupBase { Child = Reg("popup child", new Border { Width = 120, Height = 80, Background = new SolidColorBrush(WColors.Red) }) },
        "scroll view" => ScrollViewSample(),
        "color spectrum" => new ColorSpectrum { Width = 200, Height = 200, Color = WColors.Red },
        "color picker slider" => new ColorPickerSlider { Width = 240, ColorChannel = ColorPickerHsvChannel.Hue, Minimum = 0, Maximum = 360, Value = 120 },
        "line" => new Line { X1 = 0, Y1 = 0, X2 = 100, Y2 = 50, Stroke = new SolidColorBrush(WColors.Blue), StrokeThickness = 4 },
        "polygon" => new Polygon { Points = { new(0, 0), new(80, 0), new(40, 60) }, Fill = new SolidColorBrush(WColors.Lime) },
        "polyline" => new Polyline { Points = { new(0, 0), new(80, 0), new(40, 60) }, Stroke = new SolidColorBrush(WColors.Blue), StrokeThickness = 3 },
        // AP7-C: the AudioPlayer add-in's MidiPlayer (not in the AudioPlayer Core; the Android add-in's own port), turned down.
        "midi player" => new CodeBrix.Platform.UI.AudioPlayer.Skia.MidiPlayer { Volume = 0 },
        "rectangle" => new Rectangle { Width = 80, Height = 40, Fill = new SolidColorBrush(WColors.Orange), RadiusX = 8, RadiusY = 8 },
        _ => throw new ArgumentException("no AP10B sample " + sample),
    };

    /// <summary>Writes the BitmapIcon picture into the app's local folder (once).</summary>
    internal static void EnsureIconFile()
    {
        var path = System.IO.Path.Combine(Windows.Storage.ApplicationData.Current.LocalFolder.Path, "ap10b-icon.png");
        if (!File.Exists(path))
        {
            File.WriteAllBytes(path, TestImage.EncodePng(WColors.Red, WColors.Blue));
        }
    }

    private static T Reg<T>(string name, T element)
        where T : FrameworkElement
    {
        element.Name = name;
        ElementRegistry.Register(name, element);
        return element;
    }

    private static MenuFlyout Menu(params string[] items)
    {
        var menu = new MenuFlyout();
        foreach (var item in items)
        {
            menu.Items.Add(new MenuFlyoutItem { Text = item });
        }

        return menu;
    }

    private static RefreshContainer Refresh()
    {
        var list = new StackPanel();
        for (var i = 1; i <= 20; i++)
        {
            list.Children.Add(new TextBlock { Text = "Row " + i, Height = 40 });
        }

        return new RefreshContainer { Width = 300, Height = 300, Content = Reg("refresh scroller", new ScrollViewer { Content = list }) };
    }

    private static SwipeControl Swipe()
    {
        var swipe = new SwipeControl
        {
            Width = 320,
            Height = 64,
            Content = Reg("swipe content", new Border { Background = new SolidColorBrush(WColors.LightGray), Child = new TextBlock { Text = "Swipe me", Margin = new Thickness(12) } }),
        };
        swipe.LeftItems = new SwipeItems { Mode = SwipeMode.Reveal };
        swipe.LeftItems.Add(new SwipeItem { Text = "Flag", Background = new SolidColorBrush(WColors.Orange) });
        swipe.RightItems = new SwipeItems { Mode = SwipeMode.Reveal };
        swipe.RightItems.Add(new SwipeItem { Text = "Delete", Background = new SolidColorBrush(WColors.Red) });
        return swipe;
    }

    /// <summary>AP1.9: a SwipeControl whose right item executes (counted by <see cref="SwipeSteps"/>).</summary>
    private static SwipeControl ExecuteSwipe()
    {
        var swipe = new SwipeControl
        {
            Width = 320,
            Height = 64,
            Content = Reg("execute swipe content", new Border { Background = new SolidColorBrush(WColors.LightGray), Child = new TextBlock { Text = "Swipe to archive", Margin = new Thickness(12) } }),
        };
        swipe.RightItems = new SwipeItems { Mode = SwipeMode.Execute };
        var archive = new SwipeItem { Text = "Archive", Background = new SolidColorBrush(WColors.SeaGreen) };
        archive.Invoked += (_, _) => SwipeSteps.CountInvoked();
        swipe.RightItems.Add(archive);
        return swipe;
    }

    private static ScrollView ScrollViewSample()
    {
        var list = new StackPanel();
        for (var i = 1; i <= 30; i++)
        {
            list.Children.Add(new TextBlock { Text = "Line " + i, Height = 40 });
        }

        return new ScrollView { Width = 300, Height = 200, Content = list };
    }
}
