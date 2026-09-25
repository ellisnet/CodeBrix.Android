using CodeBrix.Platform.Simple;
using JustBetweenUs.ViewModels;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;

namespace JustBetweenUs.Views;

public sealed partial class MainPage : Page
{
    public MainPage()
    {
        //Doing this before InitializeComponent() - in case InitializeComponent()
        //  is the thing that sets the data context.
        DataContextChanged += (sender, args) =>
        {
            (DataContext as IXamlRootGetter)?.SetXamlRootGetter(() => XamlRoot);

            if (DataContext is ICopyToClipboard copy)
            {
                copy.CopyTextToClipboard = (text) =>
                {
                    if (!string.IsNullOrEmpty(text))
                    {
                        var clipData = new DataPackage();
                        clipData.SetText(text);
                        Clipboard.SetContent(clipData);
                    }
                };
            }
        };

        //The view model waits for this before it shows its startup dialog: a dialog needs a XamlRoot,
        //  and the page does not have one until it is on screen.
        Loaded += (sender, args) => (DataContext as IPageReadyNotifier)?.NotifyPageReady();

        InitializeComponent();
    }
}
