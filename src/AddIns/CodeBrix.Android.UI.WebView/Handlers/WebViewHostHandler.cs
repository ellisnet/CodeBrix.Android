using CodeBrix.Android.UI.Handlers;
using CodeBrix.Android.UI.WebView.Hosting;
using CodeBrix.Platform.UI.Contracts;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using AView = global::Android.Views.View;
using AWebView = global::Android.Webkit.WebView;

namespace CodeBrix.Android.UI.WebView.Handlers;

/// <summary>
/// The handler of a <see cref="WebViewHostElement"/>: its native view IS the page's android.webkit.WebView (created by
/// the element's native web view with this handler's activity context), laid out at the element's Core rectangle. A
/// real touch goes to the page natively (marked handled for Core); a pointer that exists only in Core (injected input)
/// is forwarded to the page as the equivalent MotionEvents (the framework's CorePointerBridge).
/// </summary>
internal sealed class WebViewHostHandler : ViewHandler<WebViewHostElement, AWebView>
{
    /// <summary>The host element's mapper (only what every view maps).</summary>
    public static readonly PropertyMapper<WebViewHostElement, WebViewHostHandler> Mapper = new(ViewMappers.ViewMapper);

    private readonly CorePointerBridge _bridge;
    private Microsoft.UI.Xaml.Controls.WebView2 _owner;

    /// <summary>Creates the handler.</summary>
    public WebViewHostHandler()
        : base(Mapper)
    {
        _bridge = new CorePointerBridge(() => NativeView);
    }

    /// <inheritdoc />
    public override ElementHandlerCapabilities Capabilities => ElementHandlerCapabilities.OwnsVisuals | ElementHandlerCapabilities.OwnsInput;

    /// <inheritdoc />
    protected override AWebView CreatePlatformView() => VirtualElement.NativeWebView.GetOrCreateView(Context);

    /// <inheritdoc />
    protected override void ConnectHandler(AWebView platformView)
    {
        base.ConnectHandler(platformView);
        platformView.Touch += OnTouch;
        _bridge.Attach(Element);

        // Core's keyboard focus on the WebView2 (Focus(), Tab) gives the page's view the Android focus, so the keys a
        // person types reach the page (the activity sends keys to a focused web view first).
        _owner = FindOwner(Element);
        if (_owner != null)
        {
            _owner.GotFocus += OnOwnerGotFocus;
        }
    }

    /// <inheritdoc />
    protected override void DisconnectHandler(AWebView platformView)
    {
        _bridge.Detach();
        platformView.Touch -= OnTouch;
        if (_owner != null)
        {
            _owner.GotFocus -= OnOwnerGotFocus;
            _owner = null;
        }

        base.DisconnectHandler(platformView);
    }

    private static Microsoft.UI.Xaml.Controls.WebView2 FindOwner(UIElement element)
    {
        for (DependencyObject current = element; current != null; current = VisualTreeHelper.GetParent(current))
        {
            if (current is Microsoft.UI.Xaml.Controls.WebView2 owner)
            {
                return owner;
            }
        }

        return null;
    }

    private void OnOwnerGotFocus(object sender, RoutedEventArgs e)
    {
        if (NativeView is { IsFocused: false } view)
        {
            view.RequestFocus();
        }
    }

    private void OnTouch(object sender, AView.TouchEventArgs e)
    {
        // A finger on the page gives the page the keyboard focus (also a Core-injected pointer the bridge forwards).
        if (e.Event?.ActionMasked == global::Android.Views.MotionEventActions.Down && sender is AView { IsFocused: false } view)
        {
            view.RequestFocus();
        }

        _bridge.OnNativeTouch(sender as AView, e.Event);

        // The page handles the touch itself (its own onTouchEvent).
        e.Handled = false;
    }
}
