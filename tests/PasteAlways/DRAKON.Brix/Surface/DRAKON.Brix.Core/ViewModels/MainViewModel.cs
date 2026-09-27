using CodeBrix.Platform.Simple;
using System.Diagnostics;

namespace DRAKON.Brix.ViewModels;

[Microsoft.UI.Xaml.Data.Bindable]
public class MainViewModel : SimpleViewModel
{
    public MainViewModel()
    {
        if (IsDesignMode(true)) { return; } //Leave as the first line of constructor

        Debug.WriteLine("Main view model startup.");
    }

    #region | Bindable properties |

    //The DRAKON Editor draws its own entire UI through the Tk host, so there is
    //  nothing on this page for the view model to bind to yet.

    #endregion

    #region | Commands and their implementations |

    //No commands yet...

    #endregion
}
