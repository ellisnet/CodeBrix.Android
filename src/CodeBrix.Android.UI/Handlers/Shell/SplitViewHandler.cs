using CodeBrix.Android.UI.Platform;
using CodeBrix.Android.UI.Policy;
using CodeBrix.Platform.UI.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>
/// AP10-A: the handler of SplitView (plan 2.10 adaptive row SplitView; tsv row SplitView). The SplitView keeps its
/// Fluent template - pane and content are Core content, IsPaneOpen, light dismiss and the Pane* events stay Core's,
/// the view mirrors the template as the templated fallback does - and the adaptive table is applied on top: in a
/// Compact window an Inline pane is shown as an Overlay pane (a CompactInline one as CompactOverlay), so an open pane
/// covers the content with the light-dismiss layer instead of squeezing a phone-width content; the application's
/// DisplayMode is never changed and the declared form comes back when the window widens. A SplitView that is a part
/// of another control's template (NavigationView's) keeps the plain templated fallback: its owner decides its modes.
/// </summary>
internal sealed class SplitViewHandler : ViewGroupHandler<SplitView, CodeBrixContentViewGroup>
{
    /// <summary>SplitView's mapper.</summary>
    public static readonly PropertyMapper<SplitView, SplitViewHandler> Mapper = new(ViewMappers.ViewMapper)
    {
        [SplitView.DisplayModeProperty] = MapState,
        [SplitView.IsPaneOpenProperty] = MapState,
        [SplitView.PanePlacementProperty] = MapState,
    };

    private static readonly ILogger _log = CodeBrix.Android.UI.Hosting.HostLog.For("CodeBrix.Android.UI.Handlers.SplitView");
    private VisualStateGroup _group;
    private bool _applying;

    /// <summary>Creates the handler.</summary>
    public SplitViewHandler()
        : base(Mapper)
    {
    }

    /// <inheritdoc />
    public override ElementHandlerCapabilities Capabilities => ElementHandlerCapabilities.OwnsChildren;

    /// <summary>The display-mode state the template shows now (null before the template is applied).</summary>
    internal string CurrentState => _group?.CurrentState?.Name;

    /// <summary>The SplitView handler, or the templated fallback for a SplitView that is a template part.</summary>
    /// <param name="element">The SplitView.</param>
    /// <returns>The handler.</returns>
    internal static IAndroidElementHandler Create(UIElement element) =>
        element is SplitView split && split.GetTemplatedParent() == null ? new SplitViewHandler() : new TemplatedFallbackHandler();

    /// <summary>Maps the properties that decide the state: re-applies the adaptive form after Core's own update.</summary>
    public static void MapState(SplitViewHandler handler, SplitView element) => handler.PostApply();

    /// <inheritdoc />
    public override void OnChildAdded(UIElement child, int index)
    {
        base.OnChildAdded(child, index);
        Hook();
    }

    /// <inheritdoc />
    protected override CodeBrixContentViewGroup CreatePlatformView() => new(Context);

    /// <inheritdoc />
    protected override void OnConnected()
    {
        base.OnConnected();
        WindowSizeClassMonitor.Changed += OnSizeClassChanged;
        Hook();
    }

    /// <inheritdoc />
    protected override void DisconnectHandler(CodeBrixContentViewGroup platformView)
    {
        WindowSizeClassMonitor.Changed -= OnSizeClassChanged;
        if (_group != null)
        {
            _group.CurrentStateChanged -= OnStateChanged;
            _group = null;
        }

        base.DisconnectHandler(platformView);
    }

    private void Hook()
    {
        if (_group != null || Element is not SplitView split || VisualTreeHelper.GetChildrenCount(split) == 0
            || VisualTreeHelper.GetChild(split, 0) is not FrameworkElement root)
        {
            return;
        }

        foreach (var group in VisualStateManager.GetVisualStateGroups(root))
        {
            if (group.Name == "DisplayModeStates")
            {
                _group = group;
                group.CurrentStateChanged += OnStateChanged;
                PostApply();
                return;
            }
        }
    }

    private void OnStateChanged(object sender, VisualStateChangedEventArgs e) => Apply();

    private void OnSizeClassChanged(object sender, WindowSizeClassChangedEventArgs e)
    {
        if (Element is SplitView split && e.Concerns(WindowSizeClassMonitor.ActivityOf(split)))
        {
            Apply();
        }
    }

    private void PostApply() => NativeView?.Post(Apply);

    private void Apply()
    {
        if (_applying || Element is not SplitView split || _group == null)
        {
            return;
        }

        var wanted = SplitViewStates.Shown(WindowSizeClassMonitor.For(split), split.IsPaneOpen, split.DisplayMode, split.PanePlacement);
        if (_group.CurrentState?.Name == wanted)
        {
            return;
        }

        _applying = true;
        try
        {
            if (VisualStateManager.GoToState(split, wanted, false))
            {
                _log.LogDebug("SplitView {Name}: {State} (declared {Mode}).", split.Name, wanted, split.DisplayMode);
            }
        }
        finally
        {
            _applying = false;
        }
    }
}
