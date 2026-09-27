using CodeBrix.Platform.Simple;
using Microsoft.UI.Xaml.Controls;
using PicoScope.Brix.ViewModels;

namespace PicoScope.Brix.Views;

/// <summary>
/// The one page of the sample: hosts the chart control and wires the view
/// model's lifetime to the page's own.
/// </summary>
public sealed partial class MainPage : Page
{
    /// <summary>The page's view model, for the window-closed handler in <see cref="App"/>.</summary>
    internal MainViewModel ViewModel => DataContext as MainViewModel;

    /// <summary>
    /// Creates the page.
    /// </summary>
    public MainPage()
    {
        DataContextChanged += (_, _) =>
        {
            //Give the view model's SimpleDialog helpers a XamlRoot to attach dialogs to
            (DataContext as IXamlRootGetter)?.SetXamlRootGetter(() => XamlRoot);
        };

        Loaded += async (_, _) =>
        {
            if (ViewModel != null) { await ViewModel.InitializeAsync(); }
        };

        Unloaded += (_, _) =>
        {
            //Releasing the handle matters: a scope left open stays locked
            //  against every other process until this one exits.
            ViewModel?.Shutdown();
        };

        this.InitializeComponent(); //Leave this line last
    }
}
