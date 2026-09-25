using CodeBrix.Platform.Simple;
using Microsoft.UI.Xaml.Controls;
using PdfSideBySide.PdfRender;
using PdfSideBySide.Services;
using PdfSideBySide.ViewModels;
using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Windows.Storage.Pickers;

namespace PdfSideBySide.Views;

public sealed partial class MainPage : Page
{
    private MainViewModel _wiredViewModel;

    public MainPage()
    {
        DataContextChanged += (_, _) =>
        {
            //Give the view model's SimpleDialog helpers a XamlRoot to attach dialogs to
            (DataContext as IXamlRootGetter)?.SetXamlRootGetter(() => XamlRoot);

            //Give the view model the file dialog only a head can show
            if (DataContext is IPdfFileBridge fileBridge)
            {
                fileBridge.PickPdfPathAsync = PickPdfPathAsync;
            }

            WireViewModel();
        };

        //Anything the view model opens can fail, and an error dialog needs the XamlRoot that only
        //  a loaded page has, so the startup documents are opened from here
        Loaded += (_, _) =>
        {
            WireViewModel();
            ViewModel?.OnPageReady();
        };

        Unloaded += (_, _) => UnwireViewModel();

        this.InitializeComponent(); //Leave this line last

        LeftScroller.SizeChanged += (_, _) => ApplyView(DocumentSide.Left);
        RightScroller.SizeChanged += (_, _) => ApplyView(DocumentSide.Right);
    }

    private MainViewModel ViewModel => DataContext as MainViewModel;

    #region | Watching the view model |

    //One property to watch: the view model folds everything that moves the view - a zoom, a pan, a
    //  page change, a newly rendered image - into ViewVersion
    private void WireViewModel()
    {
        var viewModel = ViewModel;
        if (ReferenceEquals(viewModel, _wiredViewModel)) { return; }

        UnwireViewModel();
        if (viewModel == null) { return; }

        _wiredViewModel = viewModel;
        _wiredViewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void UnwireViewModel()
    {
        if (_wiredViewModel == null) { return; }

        _wiredViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        _wiredViewModel = null;
    }

    private void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(MainViewModel.ViewVersion)) { ApplyViews(); }
    }

    #endregion

    #region | Head-capability bridges |

    private static async Task<string> PickPdfPathAsync()
    {
        try
        {
            var picker = new FileOpenPicker
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
            };
            picker.FileTypeFilter.Add(".pdf");

            var file = await picker.PickSingleFileAsync();
            return file?.Path;
        }
        catch (NotSupportedException)
        {
            //A head with no windowing system registers no picker
            return null;
        }
    }

    #endregion

    private void ApplyViews()
    {
        ApplyView(DocumentSide.Left);
        ApplyView(DocumentSide.Right);
    }

    /// <summary>
    /// Tells the view model how big side's viewer is and applies what it works out: the size
    /// of the page image and the offset to scroll the viewer to.
    /// </summary>
    private void ApplyView(DocumentSide side)
    {
        var viewModel = ViewModel;
        if (viewModel == null) { return; }

        var pane = side == DocumentSide.Left ? viewModel.LeftPane : viewModel.RightPane;
        var scroller = side == DocumentSide.Left ? LeftScroller : RightScroller;
        var image = side == DocumentSide.Left ? LeftImage : RightImage;

        //Only the page knows how big the viewer is; the arithmetic belongs to the view model
        viewModel.SetViewportSize(side, scroller.ActualWidth, scroller.ActualHeight);

        image.Width = pane.ImageWidth;
        image.Height = pane.ImageHeight;
        if (double.IsNaN(pane.ImageWidth)) { return; }

        //Let the viewer measure the new extent before positioning it
        scroller.UpdateLayout();
        scroller.ChangeView(pane.ScrollOffsetX, pane.ScrollOffsetY, null, disableAnimation: true);
    }
}
