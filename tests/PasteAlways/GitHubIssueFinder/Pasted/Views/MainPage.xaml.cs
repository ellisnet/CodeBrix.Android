using CodeBrix.Platform.Simple;
using GitHubIssueFinder.Theming;
using GitHubIssueFinder.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.UI.ViewManagement;

namespace GitHubIssueFinder.Views;

public sealed partial class MainPage : Page, IColorSchemeApplier
{
    //Kept in a field on purpose: the platform holds only a weak reference to a UISettings, so a
    //local one would be collected and the operating system's theme changes would stop arriving.
    private readonly UISettings _systemColors = new UISettings();

    public MainPage()
    {
        DataContextChanged += (_, _) =>
        {
            //Give the view model's dialog helpers a XamlRoot to attach to, and hand it the page
            //as the thing that can paint a colour scheme.
            (DataContext as IXamlRootGetter)?.SetXamlRootGetter(() => XamlRoot);
            (DataContext as IManageColorScheme)?.AttachSchemeApplier(this, SystemPrefersDark());
        };

        _systemColors.ColorValuesChanged += (_, _) => DispatcherQueue.TryEnqueue(() =>
            (DataContext as IManageColorScheme)?.OnSystemThemeChanged(SystemPrefersDark()));

        this.InitializeComponent(); //Leave this line last
    }

    /// <summary>
    /// Paints a colour scheme: the element theme decides the chrome this application does not
    /// re-key, and every keyed brush the scheme drives is re-pointed in place, which repaints
    /// every consumer without a binding being raised.
    /// </summary>
    void IColorSchemeApplier.Apply(ColorSchemePalette palette, bool baseIsDark, bool followSystem)
    {
        if (palette == null) { return; }

        RootGrid.RequestedTheme = followSystem
            ? ElementTheme.Default
            : (baseIsDark ? ElementTheme.Dark : ElementTheme.Light);

        Repoint(Application.Current?.Resources, palette);
        Repoint(Resources, palette);
    }

    private static void Repoint(ResourceDictionary dictionary, ColorSchemePalette palette)
    {
        if (dictionary == null) { return; }

        foreach (var entry in SchemeBrushMap.Entries)
        {
            if (dictionary.TryGetValue(entry.Key, out var value) && value is SolidColorBrush brush)
            {
                PaletteBrushes.Repoint(brush, palette[entry.Value]);
            }
        }
    }

    //The operating system reports its preference as the colour it would paint a window with.
    //Reading it is the page's job; deciding what it means belongs with the scheme table.
    private bool SystemPrefersDark()
    {
        var background = _systemColors.GetColorValue(UIColorType.Background);
        return ColorSchemes.PrefersDark(background.R, background.G, background.B);
    }

    //Pressing Enter in either box runs Search, exactly as clicking the button does. The
    //CanExecute check matters: a key handler would otherwise walk past the disabled state that a
    //button honours.
    private void OnSearchBoxKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != Windows.System.VirtualKey.Enter) { return; }

        if (DataContext is MainViewModel { SearchCommand: var search }
            && search != null
            && search.CanExecute(null))
        {
            search.Execute(null);
            e.Handled = true;
        }
    }

    //Escape stops a running search from anywhere on the page.
    private void OnRootKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != Windows.System.VirtualKey.Escape) { return; }

        if (DataContext is MainViewModel { CancelCommand: var cancel }
            && cancel != null
            && cancel.CanExecute(null))
        {
            cancel.Execute(null);
            e.Handled = true;
        }
    }
}
