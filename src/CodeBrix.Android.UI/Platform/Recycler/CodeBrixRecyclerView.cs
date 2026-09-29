using System;
using CodeBrix.Android.UI.Input;
using Windows.Foundation;
using AContext = global::Android.Content.Context;
using AMotionEvent = global::Android.Views.MotionEvent;
using AMotionEventActions = global::Android.Views.MotionEventActions;
using ARecyclerView = global::AndroidX.RecyclerView.Widget.RecyclerView;

namespace CodeBrix.Android.UI.Platform.Recycler;

/// <summary>
/// The native list view of the RecyclerView-backed items controls (ListView, GridView, TreeView's list,
/// FlipView, ItemsRepeater). What it adds to RecyclerView:
/// <list type="bullet">
/// <item>the scroll position in pixels, followed from every scroll step (<see cref="Scrolled"/>), for the
/// Core ScrollViewer that mirrors it;</item>
/// <item>after the list comes to rest (and after programmatic scrolls), every item host re-arranges its Core
/// element where it now sits, so Core's rectangles follow the scroll;</item>
/// <item>the input contract of plan 2.15: a finger the list starts dragging is taken over from Core
/// (NativeInput.CancelPointer), and <see cref="IsNativeTouchActive"/> tells the Core-pointer bridge which
/// Core pointer streams are real touches the list already handles (the others - injected input, wheel -
/// are scrolled through <see cref="ScrollByPixels"/>).</item>
/// </list>
/// </summary>
internal sealed class CodeBrixRecyclerView : ARecyclerView
{
    private bool _nativeTouchActive;
    private int _lastState = ScrollStateIdle;

    /// <summary>Creates the view.</summary>
    /// <param name="context">The context.</param>
    internal CodeBrixRecyclerView(AContext context)
        : base(context)
    {
        // Core draws nothing outside an element's bounds for a list either: the viewport clips.
        SetClipToPadding(false);
        OverScrollMode = global::Android.Views.OverScrollMode.IfContentScrolls;
    }

    /// <inheritdoc />
    /// <remarks>
    /// AP8-S batch 1: the list draws nothing outside its own bounds. Its parents replay Core's layout and do not clip their
    /// children (CodeBrixViewGroup), so an item view partly scrolled out of the list was drawn over whatever sits above or
    /// below the list (seen in a pasted app: a row over a header). Clipping here also covers the item decorations and
    /// the overscroll edge effects drawn in Draw.
    /// </remarks>
    public override void Draw(global::Android.Graphics.Canvas canvas)
    {
        if (canvas == null)
        {
            base.Draw(canvas);
            return;
        }

        var save = canvas.Save();
        canvas.ClipRect(ScrollX, ScrollY, ScrollX + Width, ScrollY + Height);
        base.Draw(canvas);
        canvas.RestoreToCount(save);
    }

    /// <summary>
    /// Raised when the scroll position changed: (x, y) in pixels and whether more changes follow
    /// (dragging or settling).
    /// </summary>
    internal event Action<int, int, bool> Scrolled;

    /// <summary>Raised when the list came to rest (after a drag, a fling, a smooth scroll).</summary>
    internal event Action Settled;

    /// <summary>Raised after every layout pass (the item hosts are laid out).</summary>
    internal event Action LaidOut;

    /// <summary>The horizontal scroll position in pixels (sum of the scroll steps; 0 at the start).</summary>
    internal int OffsetX { get; private set; }

    /// <summary>The vertical scroll position in pixels.</summary>
    internal int OffsetY { get; private set; }

    /// <summary>True while a real touch stream (Android dispatch) is on the list.</summary>
    internal bool IsNativeTouchActive => _nativeTouchActive;

    /// <summary>The list's origin in window DIPs (for the item hosts' pixel rounding).</summary>
    internal Func<Point> AbsoluteDipOrigin { get; set; }

    /// <summary>Scrolls by a number of pixels (the Core-pointer bridge, programmatic scrolls).</summary>
    internal void ScrollByPixels(int dx, int dy)
    {
        if (dx != 0 || dy != 0)
        {
            ScrollBy(dx, dy);
        }
    }

    /// <summary>Resets the tracked position (the adapter was reset and the list is at its start).</summary>
    internal void ResetOffsets()
    {
        OffsetX = Math.Max(0, ComputeHorizontalScrollOffset());
        OffsetY = Math.Max(0, ComputeVerticalScrollOffset());
    }

    /// <summary>Re-arranges every attached item host's Core element where the host sits now.</summary>
    internal void SyncItemRects()
    {
        for (var i = 0; i < ChildCount; i++)
        {
            (GetChildAt(i) as RecyclerItemHost)?.SyncCoreRect();
        }
    }

    /// <inheritdoc />
    public override bool DispatchTouchEvent(AMotionEvent e)
    {
        if (e != null)
        {
            var action = e.ActionMasked;
            if (action == AMotionEventActions.Down)
            {
                _nativeTouchActive = true;
            }
            else if (action is AMotionEventActions.Up or AMotionEventActions.Cancel)
            {
                // Cleared after the dispatch below: Core sees the up after the native views.
                var result = base.DispatchTouchEvent(e);
                _nativeTouchActive = false;
                return result;
            }
        }

        return base.DispatchTouchEvent(e);
    }

    /// <summary>
    /// False when every item is shown (the list was measured unconstrained inside a ScrollViewer): the list then
    /// leaves drags to the scroll viewer around it instead of taking the finger for a scroll it cannot make.
    /// </summary>
    internal bool CanScrollAtAll =>
        CanScrollVertically(1) || CanScrollVertically(-1) || CanScrollHorizontally(1) || CanScrollHorizontally(-1);

    /// <inheritdoc />
    public override bool OnInterceptTouchEvent(AMotionEvent e) => Enabled && CanScrollAtAll && base.OnInterceptTouchEvent(e);

    /// <inheritdoc />
    public override bool OnTouchEvent(AMotionEvent e) => Enabled && CanScrollAtAll && base.OnTouchEvent(e);

    /// <inheritdoc />
    public override void OnScrollStateChanged(int state)
    {
        base.OnScrollStateChanged(state);
        if (state == ScrollStateDragging && _lastState != ScrollStateDragging && _nativeTouchActive)
        {
            // The finger now scrolls the list natively: Core's handling of the same finger stops (a cancel),
            // so an item does not also see a tap or a pointer that wandered off.
            NativeInput.CancelPointer(null, 0);
        }

        _lastState = state;
        if (state == ScrollStateIdle)
        {
            SyncItemRects();
            Scrolled?.Invoke(OffsetX, OffsetY, false);
            Settled?.Invoke();
        }
    }

    /// <inheritdoc />
    public override void OnScrolled(int dx, int dy)
    {
        base.OnScrolled(dx, dy);
        OffsetX = Math.Max(0, OffsetX + dx);
        OffsetY = Math.Max(0, OffsetY + dy);
        var idle = ScrollState == ScrollStateIdle;
        if (idle)
        {
            // A programmatic scroll (scrollBy, scrollToPosition): no settle callback follows.
            SyncItemRects();
        }

        Scrolled?.Invoke(OffsetX, OffsetY, !idle);
    }

    /// <inheritdoc />
    protected override void OnLayout(bool changed, int l, int t, int r, int b)
    {
        base.OnLayout(changed, l, t, r, b);
        LaidOut?.Invoke();
    }
}
