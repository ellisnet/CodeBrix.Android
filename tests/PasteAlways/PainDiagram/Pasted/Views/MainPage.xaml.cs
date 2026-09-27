using CodeBrix.Imaging.Drawing;
using CodeBrix.Platform.Simple;
using Microsoft.UI.Xaml.Controls;
using PainDiagram.Services;
using PainDiagram.ViewModels;

// ReSharper disable once CheckNamespace
namespace PainDiagram.Views;

public sealed partial class MainPage : Page
{
    //I tend to like to declare/define private methods above the constructor, in C# classes
    private MainViewModel ViewModel => DataContext as MainViewModel;

    public MainPage()
    {
        //Doing this before InitializeComponent() - in case InitializeComponent()
        //  is the thing that sets the data context.
        DataContextChanged += (_, _) =>
        {
            (DataContext as IXamlRootGetter)?.SetXamlRootGetter(() => XamlRoot);

            if (DataContext is IFileSaveBridge fileSave)
            {
                //The save dialog is a registered service, so the page hands the view model a
                //  delegate without knowing how the dialog is built
                fileSave.PickSavePngPathAsync =
                    SimpleServiceResolver.Instance.GetService<IFileSavePicker>().PickSavePngPathAsync;
            }

            if (DataContext is ICanvasInvalidator invalidator)
            {
                invalidator.InvalidateCanvas = () => DrawCanvas?.Invalidate();
            }
        };

        InitializeComponent();

        //Paint, press, move, release and capture-lost all go straight to the drawing session
        DrawCanvas.BindToSession(() => ViewModel?.Session);
    }
}
