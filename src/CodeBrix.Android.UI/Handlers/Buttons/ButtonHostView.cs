using CodeBrix.Android.UI.Platform;
using AContext = global::Android.Content.Context;
using AView = global::Android.Views.View;
using AViewGroup = global::Android.Views.ViewGroup;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>
/// The native view of a Button-family control: a <see cref="CodeBrixContentViewGroup"/> that shows
/// EITHER one native widget filling it (a MaterialButton for text content: <see cref="NativeChild"/>)
/// OR the control's Core content (element content, D-P11: the content's own native views, laid out
/// where Core put them, over a Material shape background with a ripple).
/// </summary>
internal sealed class ButtonHostView : CodeBrixContentViewGroup
{
    private AView _nativeChild;

    /// <summary>Creates the view.</summary>
    /// <param name="context">The context.</param>
    internal ButtonHostView(AContext context)
        : base(context)
    {
    }

    /// <summary>The native widget that fills the view (null in content mode).</summary>
    internal AView NativeChild
    {
        get => _nativeChild;
        set
        {
            if (ReferenceEquals(_nativeChild, value))
            {
                return;
            }

            if (_nativeChild != null && _nativeChild.Parent == this)
            {
                RemoveView(_nativeChild);
            }

            _nativeChild = value;
            if (value != null)
            {
                (value.Parent as AViewGroup)?.RemoveView(value);
                AddView(value);
            }

            RequestLayout();
        }
    }

    /// <inheritdoc />
    protected override void OnMeasure(int widthMeasureSpec, int heightMeasureSpec)
    {
        base.OnMeasure(widthMeasureSpec, heightMeasureSpec);
        _nativeChild?.Measure(MeasureSpecExtensions.Exactly(MeasuredWidth), MeasureSpecExtensions.Exactly(MeasuredHeight));
    }

    /// <inheritdoc />
    protected override void OnLaidOut(int width, int height)
    {
        base.OnLaidOut(width, height);
        _nativeChild?.Layout(0, 0, width, height);
    }
}
