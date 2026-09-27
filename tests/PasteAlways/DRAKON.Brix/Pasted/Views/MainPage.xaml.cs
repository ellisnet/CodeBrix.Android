using CodeBrix.Platform.Simple;
using DRAKON.Brix.Drakon;
using Microsoft.UI.Xaml.Controls;

namespace DRAKON.Brix.Views;

public sealed partial class MainPage : Page
{
    private readonly RuntimeHost _runtimeHost = new RuntimeHost();

    public MainPage()
    {
        DataContextChanged += (_, _) =>
        {
            //Give the view model's SimpleDialog helpers a XamlRoot to attach dialogs to
            (DataContext as IXamlRootGetter)?.SetXamlRootGetter(() => XamlRoot);
        };

        //The DRAKON Editor Tcl builds the whole UI; this page just starts the
        //  Tcl runtime once the Tk host (and its dispatcher) is live, and disposes
        //  it on unload. RuntimeHost keeps this page decoupled from DrakonRuntime.
        Loaded += (s, e) => _runtimeHost.Start(TkHost);
        Unloaded += (s, e) => _runtimeHost.Dispose();

        this.InitializeComponent(); //Leave this line last
    }
}
