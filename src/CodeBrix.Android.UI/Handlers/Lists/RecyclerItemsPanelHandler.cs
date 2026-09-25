using CodeBrix.Android.UI.Platform;
using CodeBrix.Android.UI.Platform.Recycler;
using CodeBrix.Platform.UI.Contracts;
using Microsoft.UI.Xaml;
using AView = global::Android.Views.View;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>
/// The handler of <see cref="RecyclerItemsPanel"/>. The panel has no view of its own on screen (its
/// children are shown by the RecyclerView's item hosts): the handler's view is an unattached placeholder,
/// and every child element whose native view appears (the child connected, or was added while the panel
/// is live) is handed to the item host that shows it.
/// </summary>
internal sealed class RecyclerItemsPanelHandler : ViewHandler<RecyclerItemsPanel, AView>, IViewGroupHandler
{
    /// <summary>The mapper (nothing of the panel is shown natively).</summary>
    public static readonly PropertyMapper<RecyclerItemsPanel, RecyclerItemsPanelHandler> Mapper = new(ElementHandler.ElementMapper);

    /// <summary>Creates the handler.</summary>
    public RecyclerItemsPanelHandler()
        : base(Mapper)
    {
    }

    /// <inheritdoc />
    public override ElementHandlerCapabilities Capabilities => ElementHandlerCapabilities.OwnsChildren;

    /// <inheritdoc />
    public CodeBrixViewGroup ViewGroup => null;

    /// <inheritdoc />
    public void AttachChild(UIElement child, AView view) => (Element as RecyclerItemsPanel)?.HostOf(child)?.AdoptElementView();

    /// <inheritdoc />
    public override void OnChildAdded(UIElement child, int index) => (Element as RecyclerItemsPanel)?.HostOf(child)?.AdoptElementView();

    /// <inheritdoc />
    public override void OnChildRemoved(UIElement child) => (Element as RecyclerItemsPanel)?.HostOf(child)?.ReleaseElementView();

    /// <inheritdoc />
    protected override AView CreatePlatformView() => new(Context);
}
