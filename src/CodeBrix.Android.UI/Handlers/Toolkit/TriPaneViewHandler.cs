using System;
using CodeBrix.Android.UI.Input;
using CodeBrix.Android.UI.Platform;
using CodeBrix.Android.UI.Policy;
using CodeBrix.Android.UI.Toolkit.Android;
using CodeBrix.Platform.UI.Contracts;
using CodeBrix.Platform.UI.Toolkit;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using AContext = global::Android.Content.Context;
using AMotionEvent = global::Android.Views.MotionEvent;
using AMotionEventActions = global::Android.Views.MotionEventActions;
using AMotionEventToolType = global::Android.Views.MotionEventToolType;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>
/// AP7-B: the handler of the Toolkit's TriPaneView (adaptive table row "TriPaneView"; plan AP7-B item 3). The control keeps
/// its template and its ENGINE (Core's TriPaneLayoutState computes every column, row, divider and grip; the view mirrors
/// the template exactly as the templated fallback does). On top of it:
/// <list type="bullet">
/// <item>The ADAPTIVE FORM by the window's width size class (<see cref="AdaptivePolicy.TriPane"/>, followed live -
/// a docked phone switching between phone and desktop mode): Compact = one pane, Medium = the side pane and one stacked
/// pane, Expanded = three panes. It is reached THROUGH the engine by weights (<see cref="TriPanePlan"/>): a region the
/// window has no room for gets weight 0, as if its divider had been dragged over, so the engine minimizes it and offers
/// its restore grip (RestoreGripMode Auto/Always); tapping the grip reopens that pane and the plan closes its sibling (the
/// grips switch panes); the weights the plan zeroed come back when the window widens. Switch:
/// <see cref="AdaptivePolicy.AdaptiveTriPaneView"/> (AppContext "CodeBrix.Android.UI.AdaptiveTriPaneView").</item>
/// <item>FINGER DRAGS on the dividers: a divider is a few DIPs thick, so a finger (or stylus) that lands within the 48-dp
/// Material touch target around a shown, enabled divider is taken natively - Core gets a cancel for that pointer - and
/// the drag is driven through the TriPaneView's drag entry points (<see cref="TriPaneViewEntryPoints"/>): the engine
/// resolves drag, tap-to-restore on a grip, drag-to-minimize and cancel exactly as for its own pointer handling. A mouse
/// keeps Core's own divider handling (hover state, resize cursor).</item>
/// </list>
/// </summary>
internal sealed class TriPaneViewHandler : ViewGroupHandler<TriPaneView, TriPaneViewLayout>
{
    /// <summary>The Material minimum touch target, in dp.</summary>
    internal const double TouchTargetDp = 48d;

    /// <summary>TriPaneView's mapper: a weight change (a drag, the app, the grips) re-checks the adaptive form.</summary>
    public static readonly PropertyMapper<TriPaneView, TriPaneViewHandler> Mapper = new(ViewMappers.ViewMapper)
    {
        [TriPaneView.SidePanePercentProperty] = MapWeights,
        [TriPaneView.StackPercentProperty] = MapWeights,
        [TriPaneView.UpperPanePercentProperty] = MapWeights,
        [TriPaneView.LowerPanePercentProperty] = MapWeights,
    };

    private static readonly ILogger _log = CodeBrix.Android.UI.Hosting.HostLog.For("CodeBrix.Android.UI.Handlers.TriPaneView");
    private readonly TriPanePlan _plan = new();
    private bool _applying;
    private bool _posted;
    private DividerGesture _gesture;

    /// <summary>Creates the handler.</summary>
    public TriPaneViewHandler()
        : base(Mapper)
    {
    }

    /// <inheritdoc />
    public override ElementHandlerCapabilities Capabilities => ElementHandlerCapabilities.OwnsChildren;

    /// <summary>The form applied last (null before the first application or when the adaptive form is off).</summary>
    internal TriPaneForm? Form => _plan.Form;

    /// <summary>The adaptive plan (diagnostics and the device fences).</summary>
    internal TriPanePlan Plan => _plan;

    /// <summary>The number of divider drags taken natively (a finger or stylus) since the handler connected.</summary>
    internal int NativeDragCount { get; private set; }

    /// <summary>Whether a divider drag taken natively is in progress.</summary>
    internal bool IsNativeDragActive => _gesture != null;

    /// <summary>The TriPaneView handler.</summary>
    /// <param name="element">The TriPaneView.</param>
    /// <returns>The handler.</returns>
    internal static IAndroidElementHandler Create(UIElement element) => new TriPaneViewHandler();

    /// <summary>A weight changed: re-check the form once the engine's pass is over (posted, coalesced).</summary>
    public static void MapWeights(TriPaneViewHandler handler, TriPaneView element) => handler.PostApply();

    /// <summary>Re-applies the adaptive form now (tests, diagnostics).</summary>
    internal void ApplyNow() => Apply();

    /// <inheritdoc />
    protected override TriPaneViewLayout CreatePlatformView() => new(Context) { Owner = this };

    /// <inheritdoc />
    protected override void OnConnected()
    {
        base.OnConnected();
        WindowSizeClassMonitor.Changed += OnSizeClassChanged;
        PostApply();
    }

    /// <inheritdoc />
    protected override void DisconnectHandler(TriPaneViewLayout platformView)
    {
        WindowSizeClassMonitor.Changed -= OnSizeClassChanged;
        if (_gesture != null && Element is TriPaneView view)
        {
            TriPaneViewEntryPoints.CompleteDrag(view, _gesture.Kind, 0d, canceled: true);
        }

        _gesture = null;
        platformView.Owner = null;
        base.DisconnectHandler(platformView);
    }

    /// <inheritdoc />
    protected override void OnArrangedSizeChanged(Windows.Foundation.Size size)
    {
        base.OnArrangedSizeChanged(size);
        PostApply();
    }

    private void OnSizeClassChanged(object sender, WindowSizeClassChangedEventArgs e)
    {
        if (Element is TriPaneView view && e.Concerns(WindowSizeClassMonitor.ActivityOf(view)))
        {
            Apply();
        }
    }

    private void PostApply()
    {
        if (_posted || NativeView is not { } native)
        {
            return;
        }

        _posted = true;
        native.Post(() =>
        {
            _posted = false;
            Apply();
        });
    }

    private void Apply()
    {
        if (_applying || _gesture != null || Element is not TriPaneView view || !AdaptivePolicy.AdaptiveTriPaneView)
        {
            return;
        }

        // Never in the middle of a divider drag (Core's pointer or ours): the form is re-checked when the drag has written
        // its last weights.
        if (TriPaneViewEntryPoints.Divider(view, TriPaneViewDividerKind.Side) is { IsDragging: true }
            || TriPaneViewEntryPoints.Divider(view, TriPaneViewDividerKind.Stack) is { IsDragging: true })
        {
            return;
        }

        var window = WindowSizeClassMonitor.For(view);
        if (window.WidthDp <= 0)
        {
            return;
        }

        var form = AdaptivePolicy.TriPane(window);
        var current = Weights(view);
        var target = _plan.Apply(form, current);
        if (target != current)
        {
            _applying = true;
            try
            {
                // Opened regions first, so a pair is never written with both weights at zero (the engine would read an
                // even split).
                Write(view, target, open: true);
                Write(view, target, open: false);
            }
            finally
            {
                _applying = false;
            }

            _log.LogDebug("TriPaneView {Name}: {Form} at {Width:0} dp: {Before} -> {After}.", view.Name, form, window.WidthDp, current, Weights(view));
        }

        _plan.Observe(Weights(view));
    }

    private static TriPaneWeights Weights(TriPaneView view) =>
        new(view.SidePanePercent, view.StackPercent, view.UpperPanePercent, view.LowerPanePercent);

    private static void Write(TriPaneView view, TriPaneWeights target, bool open)
    {
        void Set(double currentValue, double wanted, Action<double> setter)
        {
            if (!currentValue.Equals(wanted) && TriPaneWeights.IsClosed(wanted) != open)
            {
                setter(wanted);
            }
        }

        Set(view.SidePanePercent, target.Side, v => view.SidePanePercent = v);
        Set(view.StackPercent, target.Stack, v => view.StackPercent = v);
        Set(view.UpperPanePercent, target.Upper, v => view.UpperPanePercent = v);
        Set(view.LowerPanePercent, target.Lower, v => view.LowerPanePercent = v);
    }

    /// <summary>A touch reached the control's view group before its children: take it when it lands on a divider.</summary>
    /// <param name="e">The event.</param>
    /// <returns>True when the gesture is the divider's from now on.</returns>
    internal bool InterceptTouch(AMotionEvent e)
    {
        if (e == null || (e.Action & AMotionEventActions.Mask) != AMotionEventActions.Down || Element is not TriPaneView view)
        {
            return false;
        }

        var tool = e.GetToolType(0);
        if (tool != AMotionEventToolType.Finger && tool != AMotionEventToolType.Stylus)
        {
            return false;
        }

        if (NativeInput.IsCapturedByCore(e, 0))
        {
            return false;
        }

        var kind = DividerAt(view, e.GetX(), e.GetY());
        if (kind == null)
        {
            return false;
        }

        var density = Density > 0 ? Density : 1d;
        _gesture = new DividerGesture(kind.Value, e.GetPointerId(0), e.RawX, e.RawY, density);
        NativeInput.CancelPointer(e, 0);
        NativeInput.MarkHandled();
        NativeDragCount++;
        TriPaneViewEntryPoints.StartDrag(view, kind.Value);
        NativeView?.Parent?.RequestDisallowInterceptTouchEvent(true);
        return true;
    }

    /// <summary>The rest of a divider gesture this handler took.</summary>
    /// <param name="e">The event.</param>
    /// <returns>True when the event was the gesture's.</returns>
    internal bool OnTouch(AMotionEvent e)
    {
        if (_gesture is not { } gesture || e == null || Element is not TriPaneView view)
        {
            return false;
        }

        NativeInput.MarkHandled();
        var index = e.FindPointerIndex(gesture.PointerId);
        var action = e.Action & AMotionEventActions.Mask;
        if (action == AMotionEventActions.Down)
        {
            // Our own interception's DOWN, re-delivered to this view's OnTouchEvent: the drag has started already.
            return true;
        }

        if (index >= 0 && action is AMotionEventActions.Move or AMotionEventActions.Up)
        {
            // Raw (screen) coordinates: the view itself moves while the divider drags the columns around it.
            var raw = RawAt(e, index);
            var along = gesture.Kind == TriPaneViewDividerKind.Side ? raw.X - gesture.LastX : raw.Y - gesture.LastY;
            if (Math.Abs(along) > 0)
            {
                gesture.LastX = raw.X;
                gesture.LastY = raw.Y;
                TriPaneViewEntryPoints.UpdateDrag(view, gesture.Kind, along / gesture.Density);
            }
        }

        if (action == AMotionEventActions.Up || action == AMotionEventActions.Cancel
            || (action == AMotionEventActions.PointerUp && e.GetPointerId(e.ActionIndex) == gesture.PointerId))
        {
            _gesture = null;
            var travel = gesture.Kind == TriPaneViewDividerKind.Side ? gesture.LastX - gesture.StartX : gesture.LastY - gesture.StartY;
            TriPaneViewEntryPoints.CompleteDrag(view, gesture.Kind, travel / gesture.Density, canceled: action == AMotionEventActions.Cancel);
            PostApply();
        }

        return true;
    }

    private static (float X, float Y) RawAt(AMotionEvent e, int index) => (e.GetRawX(index), e.GetRawY(index));

    /// <summary>The divider whose touch target holds (x, y) (view pixels), or null. The nearer one wins.</summary>
    private TriPaneViewDividerKind? DividerAt(TriPaneView view, float x, float y)
    {
        var density = Density > 0 ? Density : 1d;
        TriPaneViewDividerKind? best = null;
        var bestDistance = double.MaxValue;
        foreach (var kind in new[] { TriPaneViewDividerKind.Side, TriPaneViewDividerKind.Stack })
        {
            if (!TriPaneViewEntryPoints.AcceptsTouch(view, kind) || TriPaneViewEntryPoints.Divider(view, kind) is not { } divider)
            {
                continue;
            }

            var bounds = divider.TransformToVisual(view).TransformBounds(new Windows.Foundation.Rect(0, 0, divider.ActualWidth, divider.ActualHeight));
            var target = TriPaneTouchTarget.Inflate(bounds, kind == TriPaneViewDividerKind.Side, TouchTargetDp);
            var px = x / density;
            var py = y / density;
            if (!target.Contains(new Windows.Foundation.Point(px, py)))
            {
                continue;
            }

            var distance = kind == TriPaneViewDividerKind.Side
                ? Math.Abs(px - (bounds.X + (bounds.Width / 2)))
                : Math.Abs(py - (bounds.Y + (bounds.Height / 2)));
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = kind;
            }
        }

        return best;
    }

    private sealed class DividerGesture
    {
        internal DividerGesture(TriPaneViewDividerKind kind, int pointerId, float x, float y, double density)
        {
            Kind = kind;
            PointerId = pointerId;
            StartX = LastX = x;
            StartY = LastY = y;
            Density = density;
        }

        internal TriPaneViewDividerKind Kind { get; }

        internal int PointerId { get; }

        internal float StartX { get; }

        internal float StartY { get; }

        internal float LastX { get; set; }

        internal float LastY { get; set; }

        internal double Density { get; }
    }
}

/// <summary>The view group of a TriPaneView: it mirrors the template and gives its handler the first look at a touch.</summary>
internal sealed class TriPaneViewLayout : CodeBrixContentViewGroup
{
    /// <summary>Creates the view group.</summary>
    /// <param name="context">The context.</param>
    internal TriPaneViewLayout(AContext context)
        : base(context)
    {
    }

    /// <summary>The handler (null once disconnected).</summary>
    internal TriPaneViewHandler Owner { get; set; }

    /// <inheritdoc />
    public override bool OnInterceptTouchEvent(AMotionEvent ev) => Owner?.InterceptTouch(ev) == true || base.OnInterceptTouchEvent(ev);

    /// <inheritdoc />
    public override bool OnTouchEvent(AMotionEvent e) => Owner?.OnTouch(e) == true || base.OnTouchEvent(e);
}
