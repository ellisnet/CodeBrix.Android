using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace WebGpuDemo.Views;

public sealed partial class MainPage : Page
{
    private TriangleView _triangle;
    private ModelsView _models;
    private RendererView _renderer;
    private ControlsView _controls;

    public MainPage()
    {
        InitializeComponent();
        // Android sample: the start page comes from the launch intent (StartPages; the Triangle page by default).
        PageHost.Child = StartPages.Resolve(this, StartPages.Requested);
    }

    /// <summary>The page showing now.</summary>
    public UIElement CurrentPage => PageHost.Child;

    public TriangleView Triangle => _triangle ??= new TriangleView();

    public ModelsView Models => _models ??= new ModelsView();

    public RendererView Renderer => _renderer ??= new RendererView();

    public ControlsView Controls => _controls ??= new ControlsView();

    private void OnTriangle(object sender, RoutedEventArgs e) => PageHost.Child = Triangle;

    private void OnModels(object sender, RoutedEventArgs e) => PageHost.Child = Models;

    private void OnRenderer(object sender, RoutedEventArgs e) => PageHost.Child = Renderer;

    private void OnControls(object sender, RoutedEventArgs e) => PageHost.Child = Controls;
}
