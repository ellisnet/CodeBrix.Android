using CodeBrix.Platform.Simple;
using CodeBrix.Platform.UI.FlexPanel;
using CodeBrix.Platform.WinUI.Graphics3DGL;
using Microsoft.UI.Xaml.Controls;
using PolyHavenBrowser.ViewModels;
using System.Threading.Tasks;

namespace PolyHavenBrowser.Views;

public sealed partial class MainPage : Page
{
    private MainViewModel ViewModel => DataContext as MainViewModel;

    //Whether the "3D preview unavailable" dialog has been shown already (once per app run).
    private bool _renderingUnavailableReported;

    public MainPage()
    {
        DataContextChanged += (_, _) =>
        {
            //Give the view model's SimpleDialog helpers a XamlRoot to attach dialogs to
            //(e.g. the "choose a download folder first" alert).
            (DataContext as IXamlRootGetter)?.SetXamlRootGetter(() => XamlRoot);

            if (DataContext is MainViewModel viewModel)
            {
                //Each bridge is filled in through the interface the view model implements
                //rather than through the concrete type, so what the page owes it is explicit.
                ICatalogGridBridge catalogBridge = viewModel;
                IModelViewBridge modelViewBridge = viewModel;

                //A new cell collection means the user re-searched or re-sorted: jump back
                //to the top.
                catalogBridge.ScrollCatalogToTop = () =>
                    CatalogScroll.ChangeView(null, 0, null, disableAnimation: true);

                //The user just opened the Model View: if the GL canvas already knows its
                //OpenGL initialization failed, tell them why the preview pane is empty.
                modelViewBridge.ModelViewOpened = () => _ = MaybeReportRenderingUnavailableAsync();
            }
        };

        InitializeComponent();

        //The canvas may only attempt its OpenGL initialization when it loads into the visual
        //tree, which can happen after IsModelViewActive is set - so check at both moments.
        ModelCanvas.Loaded += (_, _) => _ = MaybeReportRenderingUnavailableAsync();

        //The Model View's content panes: side-by-side while the window is landscape. When
        //the view model says the window is portrait the FlexPanel's main axis flips so the
        //3D viewer drops below the info panes, and the info panes trade their fixed-width
        //column (an explicit Width, so their content measures - and wraps - against it) for
        //a share of the height as a flex basis, still scrolling internally. Which way round
        //the window is, and the pane's width, basis and margin, are the view model's.
        SizeChanged += (_, args) =>
        {
            var viewModel = ViewModel;
            if (viewModel == null) { return; }

            viewModel.NotifyWindowSizeChanged(args.NewSize.Width, args.NewSize.Height);

            var stacked = viewModel.IsModelViewStacked;
            ModelContentFlex.Direction = stacked ? FlexDirection.Column : FlexDirection.Row;
            ModelInfoPane.Width = viewModel.ModelInfoPaneWidth;
            FlexPanel.SetBasis(ModelInfoPane, stacked
                ? new FlexBasis(viewModel.ModelInfoPaneStackedHeightBasis, isRelative: true)
                : FlexBasis.Auto);
            ModelInfoPane.Margin = viewModel.ModelInfoPaneMargin;
        };

        //Lazy catalog loading: the page reports where the grid has scrolled to, and the
        //view model's cell collection decides when to materialize its next batch.
        CatalogScroll.ViewChanged += (_, _) =>
            ViewModel?.NotifyCatalogScrolled(
                CatalogScroll.ExtentHeight, CatalogScroll.VerticalOffset, CatalogScroll.ViewportHeight);
    }

    //When the Model View is active and the preview canvas reports failed OpenGL initialization,
    //surface the failure (status + reason) in a dialog instead of leaving a silently empty pane.
    private async Task MaybeReportRenderingUnavailableAsync()
    {
        if (_renderingUnavailableReported || ViewModel is not { IsModelViewActive: true } viewModel)
        {
            return;
        }

        var state = ModelCanvas.GetGLInitializationState();
        if (state.Status == GLInitializationStatus.InitializationFailed)
        {
            _renderingUnavailableReported = true;
            await viewModel.ShowRenderingUnavailableAsync(state);
        }
    }
}
