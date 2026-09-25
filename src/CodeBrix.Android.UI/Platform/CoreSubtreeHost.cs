using System;
using CodeBrix.Android.UI.Handlers;
using CodeBrix.Android.UI.Portable.Layout;
using Microsoft.UI.Xaml;
using Windows.Foundation;
using AContext = global::Android.Content.Context;
using AMeasureSpecMode = global::Android.Views.MeasureSpecMode;
using AView = global::Android.Views.View;

namespace CodeBrix.Android.UI.Platform;

/// <summary>
/// The one place where ANDROID drives Core layout (plan 2.6): a native container (a
/// RecyclerView item, dialog content, a bottom sheet) hosts a Core element subtree that is not
/// laid out by a window's layout tick. <see cref="OnMeasure"/> measures the element with the
/// spec's constraint (MAUI ItemContentView pattern: Unspecified = infinite), <see cref="OnLayout"/>
/// arranges it at the view's size and then replays the subtree's rectangles like every
/// <see cref="CodeBrixViewGroup"/>.
/// </summary>
/// <remarks>
/// A subclass whose content has a Core parent (a list item in its list's panel) overrides
/// <see cref="ArrangeOrigin"/>: the content is then arranged where the host sits in that parent, so Core's own
/// rectangles (hit testing, TransformToVisual) match the screen, while the content view still fills the host.
/// </remarks>
internal class CoreSubtreeHost : CodeBrixViewGroup
{
    private UIElement _content;
    private AView _contentView;

    /// <summary>Creates the host.</summary>
    /// <param name="context">The context.</param>
    internal CoreSubtreeHost(AContext context)
        : base(context)
    {
    }

    /// <summary>
    /// Where the content is arranged, in DIPs of its Core parent's coordinates (default: the origin - content with
    /// no Core parent, such as dialog content or a bottom sheet's).
    /// </summary>
    protected virtual Point ArrangeOrigin => default;

    /// <summary>The hosted Core element (its handler's view becomes this view's only child).</summary>
    internal UIElement Content
    {
        get => _content;
        set
        {
            if (ReferenceEquals(_content, value))
            {
                return;
            }

            if (_content != null)
            {
                RemoveElementChild(_content);
            }

            _content = value;
            _contentView = null;
            AttachContentView();
            RequestLayout();
        }
    }

    /// <summary>Attaches the content's native view once its handler exists (after it entered a live tree).</summary>
    internal void AttachContentView()
    {
        if (_content?.Handler is IAndroidElementHandler { PlatformView: AView view } && !ReferenceEquals(view, _contentView))
        {
            _contentView = view;
            AddElementChild(_content, view, 0);
        }
    }

    /// <inheritdoc />
    protected override void OnMeasure(int widthMeasureSpec, int heightMeasureSpec)
    {
        AttachContentView();
        if (_content == null)
        {
            SetMeasuredDimension(0, 0);
            return;
        }

        var density = HandlerContext.Density(_content);
        var available = new Size(widthMeasureSpec.ToDips(density), heightMeasureSpec.ToDips(density));
        _content.Measure(available);
        var desired = _content.DesiredSize;

        var width = widthMeasureSpec.GetMode() == AMeasureSpecMode.Exactly ? widthMeasureSpec.GetSize() : LayoutReplayMath.ToPixels(desired.Width, density);
        var height = heightMeasureSpec.GetMode() == AMeasureSpecMode.Exactly ? heightMeasureSpec.GetSize() : LayoutReplayMath.ToPixels(desired.Height, density);
        if (widthMeasureSpec.GetMode() == AMeasureSpecMode.AtMost)
        {
            width = Math.Min(width, widthMeasureSpec.GetSize());
        }

        if (heightMeasureSpec.GetMode() == AMeasureSpecMode.AtMost)
        {
            height = Math.Min(height, heightMeasureSpec.GetSize());
        }

        SetMeasuredDimension(width, height);
    }

    /// <inheritdoc />
    protected override void OnLayout(bool changed, int l, int t, int r, int b)
    {
        if (_content != null)
        {
            var density = HandlerContext.Density(_content);
            var origin = ArrangeOrigin;
            _content.Arrange(new Rect(origin.X, origin.Y, LayoutReplayMath.FromPixels(r - l, density), LayoutReplayMath.FromPixels(b - t, density)));

            // The replay places the content view at (arranged position - origin): the host's top-left.
            AbsoluteDipOrigin = new Point(-origin.X, -origin.Y);
            AttachContentView();
        }

        base.OnLayout(changed, l, t, r, b);
    }
}
