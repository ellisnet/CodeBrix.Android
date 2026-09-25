using System;
using CodeBrix.Android.UI.Input;
using AContext = global::Android.Content.Context;
using AMotionEvent = global::Android.Views.MotionEvent;
using AMotionEventActions = global::Android.Views.MotionEventActions;
using AOverScroller = global::Android.Widget.OverScroller;
using AVelocityTracker = global::Android.Views.VelocityTracker;
using AViewConfiguration = global::Android.Views.ViewConfiguration;

namespace CodeBrix.Android.UI.Platform;

/// <summary>
/// The native scrolling view of a ScrollViewer (the view of its ScrollContentPresenter, plan 2.5 / seam
/// OwnsScrolling): Core lays the content out at its full extent (the layout replay places the content
/// view at its Core rectangle, unscrolled) and this view scrolls it natively - View.scrollX/scrollY moves
/// the drawing and the touch mapping of its children, drags follow the finger (after the touch slop, on
/// the axes the ScrollViewer allows, never while Core has captured the pointer), flings decelerate with
/// Android's OverScroller, programmatic scrolls animate or jump. Every offset change is reported to Core
/// (<see cref="Scrolled"/>); a drag takes the pointer over from Core (Core gets a cancel, so Core's own
/// manipulation of the same finger does not scroll a second time).
/// </summary>
internal sealed class CodeBrixScrollView : CodeBrixContentViewGroup
{
    private readonly AOverScroller _scroller;
    private readonly int _touchSlop;
    private readonly int _minimumFlingVelocity;
    private readonly int _maximumFlingVelocity;
    private AVelocityTracker _velocityTracker;
    private bool _isBeingDragged;
    private bool _isAnimating;
    private int _activePointerId = -1;
    private float _downX;
    private float _downY;
    private float _lastX;
    private float _lastY;

    /// <summary>Creates the view.</summary>
    /// <param name="context">The context.</param>
    internal CodeBrixScrollView(AContext context)
        : base(context)
    {
        _scroller = new AOverScroller(context);
        var configuration = AViewConfiguration.Get(context);
        _touchSlop = configuration.ScaledTouchSlop;
        _minimumFlingVelocity = configuration.ScaledMinimumFlingVelocity;
        _maximumFlingVelocity = configuration.ScaledMaximumFlingVelocity;

        // The viewport clips its content (Core's ScrollContentPresenter clips to its bounds).
        SetClipChildren(true);
        SetClipToPadding(true);
        OverScrollMode = global::Android.Views.OverScrollMode.IfContentScrolls;
    }

    /// <summary>
    /// Raised when the scroll position changed: (x, y) in pixels, and whether more changes follow (a drag,
    /// a fling or an animation in progress).
    /// </summary>
    internal event Action<int, int, bool> Scrolled;

    /// <summary>Whether horizontal scrolling is allowed (ScrollViewer scroll mode / bar visibility).</summary>
    internal bool AllowsHorizontalScroll { get; set; }

    /// <summary>Whether vertical scrolling is allowed.</summary>
    internal bool AllowsVerticalScroll { get; set; } = true;

    /// <summary>The content's extent in pixels (Core's ExtentWidth/ExtentHeight at the view's density).</summary>
    internal (int Width, int Height) ExtentPx { get; set; }

    /// <summary>The largest horizontal scroll position in pixels.</summary>
    internal int MaxScrollX => Math.Max(0, ExtentPx.Width - Width);

    /// <summary>The largest vertical scroll position in pixels.</summary>
    internal int MaxScrollY => Math.Max(0, ExtentPx.Height - Height);

    /// <summary>True while a drag, fling or animated scroll is in progress.</summary>
    internal bool IsScrollingNatively => _isBeingDragged || _isAnimating;

    /// <summary>Scrolls to a position in pixels (clamped), animated or not; reports it to Core.</summary>
    internal void ScrollToPosition(int x, int y, bool animate)
    {
        x = Math.Clamp(x, 0, MaxScrollX);
        y = Math.Clamp(y, 0, MaxScrollY);
        if (!_scroller.IsFinished)
        {
            _scroller.AbortAnimation();
        }

        if (animate && (x != ScrollX || y != ScrollY))
        {
            _isAnimating = true;
            _scroller.StartScroll(ScrollX, ScrollY, x - ScrollX, y - ScrollY, 250);
            PostInvalidateOnAnimation();
            return;
        }

        _isAnimating = false;
        if (x != ScrollX || y != ScrollY)
        {
            ScrollTo(x, y);
        }

        Scrolled?.Invoke(ScrollX, ScrollY, false);
    }

    /// <summary>
    /// Moves to a position Core decided (Core's own manipulation, a wheel, a clamp after a layout change)
    /// without reporting it back.
    /// </summary>
    internal void FollowCore(int x, int y)
    {
        if (IsScrollingNatively)
        {
            return;
        }

        x = Math.Clamp(x, 0, Math.Max(MaxScrollX, x));
        y = Math.Clamp(y, 0, Math.Max(MaxScrollY, y));
        if (x != ScrollX || y != ScrollY)
        {
            _suppressReport = true;
            try
            {
                ScrollTo(x, y);
            }
            finally
            {
                _suppressReport = false;
            }
        }
    }

    private bool _suppressReport;

    /// <inheritdoc />
    public override bool OnInterceptTouchEvent(AMotionEvent ev)
    {
        var action = ev.Action & AMotionEventActions.Mask;
        if (action == AMotionEventActions.Move && _isBeingDragged)
        {
            return true;
        }

        switch (action)
        {
            case AMotionEventActions.Down:
                _activePointerId = ev.GetPointerId(0);
                _downX = _lastX = ev.GetX();
                _downY = _lastY = ev.GetY();
                if (!_scroller.IsFinished)
                {
                    // A finger on a flinging list stops it and drags it.
                    _scroller.AbortAnimation();
                    _isAnimating = false;
                    StartDrag(ev, 0);
                }

                break;

            case AMotionEventActions.Move:
                var index = ev.FindPointerIndex(_activePointerId);
                if (index < 0)
                {
                    break;
                }

                var x = ev.GetX(index);
                var y = ev.GetY(index);
                var dx = Math.Abs(x - _downX);
                var dy = Math.Abs(y - _downY);
                var vertical = AllowsVerticalScroll && MaxScrollY > 0 && dy > _touchSlop && dy >= dx;
                var horizontal = AllowsHorizontalScroll && MaxScrollX > 0 && dx > _touchSlop && dx > dy;
                if ((vertical || horizontal) && !NativeInput.IsCapturedByCore(ev, index))
                {
                    _lastX = x;
                    _lastY = y;
                    StartDrag(ev, index);
                }

                break;

            case AMotionEventActions.Up:
            case AMotionEventActions.Cancel:
                EndDrag();
                break;
        }

        return _isBeingDragged;
    }

    /// <inheritdoc />
    public override bool OnTouchEvent(AMotionEvent ev)
    {
        _velocityTracker ??= AVelocityTracker.Obtain();
        _velocityTracker.AddMovement(ev);
        var action = ev.Action & AMotionEventActions.Mask;
        switch (action)
        {
            case AMotionEventActions.Down:
                _activePointerId = ev.GetPointerId(0);
                _downX = _lastX = ev.GetX();
                _downY = _lastY = ev.GetY();
                if (!_scroller.IsFinished)
                {
                    _scroller.AbortAnimation();
                    _isAnimating = false;
                }

                // Keep the stream: a drag may start on an empty part of the content.
                return AllowsVerticalScroll || AllowsHorizontalScroll;

            case AMotionEventActions.Move:
            {
                var index = ev.FindPointerIndex(_activePointerId);
                if (index < 0)
                {
                    return false;
                }

                var x = ev.GetX(index);
                var y = ev.GetY(index);
                if (!_isBeingDragged)
                {
                    var dx = Math.Abs(x - _downX);
                    var dy = Math.Abs(y - _downY);
                    var vertical = AllowsVerticalScroll && MaxScrollY > 0 && dy > _touchSlop && dy >= dx;
                    var horizontal = AllowsHorizontalScroll && MaxScrollX > 0 && dx > _touchSlop && dx > dy;
                    if (!(vertical || horizontal) || NativeInput.IsCapturedByCore(ev, index))
                    {
                        return true;
                    }

                    _lastX = x;
                    _lastY = y;
                    StartDrag(ev, index);
                }

                var deltaX = AllowsHorizontalScroll ? (int)Math.Round(_lastX - x) : 0;
                var deltaY = AllowsVerticalScroll ? (int)Math.Round(_lastY - y) : 0;
                _lastX = x;
                _lastY = y;
                var targetX = Math.Clamp(ScrollX + deltaX, 0, MaxScrollX);
                var targetY = Math.Clamp(ScrollY + deltaY, 0, MaxScrollY);
                if (targetX != ScrollX || targetY != ScrollY)
                {
                    ScrollTo(targetX, targetY);
                }

                return true;
            }

            case AMotionEventActions.Up:
            {
                if (_isBeingDragged)
                {
                    _velocityTracker.ComputeCurrentVelocity(1000, _maximumFlingVelocity);
                    var vx = AllowsHorizontalScroll ? -(int)_velocityTracker.GetXVelocity(_activePointerId) : 0;
                    var vy = AllowsVerticalScroll ? -(int)_velocityTracker.GetYVelocity(_activePointerId) : 0;
                    if (Math.Abs(vx) > _minimumFlingVelocity || Math.Abs(vy) > _minimumFlingVelocity)
                    {
                        _isAnimating = true;
                        _scroller.Fling(ScrollX, ScrollY, vx, vy, 0, MaxScrollX, 0, MaxScrollY);
                        PostInvalidateOnAnimation();
                    }
                }

                EndDrag();
                return true;
            }

            case AMotionEventActions.Cancel:
                EndDrag();
                return true;
        }

        return true;
    }

    /// <inheritdoc />
    public override void ComputeScroll()
    {
        if (_scroller.ComputeScrollOffset())
        {
            var x = Math.Clamp(_scroller.CurrX, 0, MaxScrollX);
            var y = Math.Clamp(_scroller.CurrY, 0, MaxScrollY);
            if (x != ScrollX || y != ScrollY)
            {
                ScrollTo(x, y);
            }

            PostInvalidateOnAnimation();
            return;
        }

        if (_isAnimating)
        {
            _isAnimating = false;
            Scrolled?.Invoke(ScrollX, ScrollY, false);
        }
    }

    /// <inheritdoc />
    protected override void OnScrollChanged(int l, int t, int oldl, int oldt)
    {
        base.OnScrollChanged(l, t, oldl, oldt);
        if (!_suppressReport)
        {
            Scrolled?.Invoke(l, t, IsScrollingNatively);
        }
    }

    /// <inheritdoc />
    protected override void OnLaidOut(int width, int height)
    {
        base.OnLaidOut(width, height);

        // The extent or the viewport changed: keep the position within the new range.
        if (ScrollX > MaxScrollX || ScrollY > MaxScrollY)
        {
            ScrollTo(Math.Min(ScrollX, MaxScrollX), Math.Min(ScrollY, MaxScrollY));
        }
    }

    /// <inheritdoc />
    protected override int ComputeVerticalScrollRange() => Math.Max(Height, ExtentPx.Height);

    /// <inheritdoc />
    protected override int ComputeHorizontalScrollRange() => Math.Max(Width, ExtentPx.Width);

    private void StartDrag(AMotionEvent ev, int pointerIndex)
    {
        if (_isBeingDragged)
        {
            return;
        }

        _isBeingDragged = true;
        Parent?.RequestDisallowInterceptTouchEvent(true);

        // The finger now scrolls natively: Core's own manipulation of it must stop (a cancel).
        NativeInput.CancelPointer(ev, pointerIndex);
    }

    private void EndDrag()
    {
        var wasDragging = _isBeingDragged;
        _isBeingDragged = false;
        _activePointerId = -1;
        _velocityTracker?.Recycle();
        _velocityTracker = null;
        if (wasDragging && !_isAnimating)
        {
            Scrolled?.Invoke(ScrollX, ScrollY, false);
        }
    }
}
