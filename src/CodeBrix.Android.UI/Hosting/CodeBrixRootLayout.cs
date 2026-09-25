using System;
using AContext = global::Android.Content.Context;
using AFrameLayout = global::Android.Widget.FrameLayout;
using AViewGroup = global::Android.Views.ViewGroup;

namespace CodeBrix.Android.UI.Hosting;

/// <summary>
/// The content view of a <see cref="CodeBrixActivity"/>: a FrameLayout with three layers,
/// bottom to top - <see cref="ContentLayer"/> (the native view of the window's root
/// element), <see cref="PopupLayer"/> (the XAML PopupRoot overlay) and
/// <see cref="FocusLayer"/> (focus and drag visuals). There is no app bar, coordinator or
/// fragment chrome at the root: in WinUI the chrome belongs to the page content.
/// </summary>
public sealed class CodeBrixRootLayout : AFrameLayout
{
    /// <summary>Creates the root layout and its three layers.</summary>
    /// <param name="context">The activity context.</param>
    public CodeBrixRootLayout(AContext context)
        : base(context)
    {
        LayoutParameters = new AViewGroup.LayoutParams(AViewGroup.LayoutParams.MatchParent, AViewGroup.LayoutParams.MatchParent);
        Focusable = true;
        FocusableInTouchMode = true;

        ContentLayer = CreateLayer(context);
        PopupLayer = CreateLayer(context);
        FocusLayer = CreateLayer(context);
        AddView(ContentLayer);
        AddView(PopupLayer);
        AddView(FocusLayer);
    }

    /// <summary>The layer that holds the native view of the window's root element.</summary>
    public AFrameLayout ContentLayer { get; }

    /// <summary>The layer that holds popups, flyouts and dialogs shown over the content.</summary>
    public AFrameLayout PopupLayer { get; }

    /// <summary>The layer that holds focus and drag visuals.</summary>
    public AFrameLayout FocusLayer { get; }

    /// <summary>Raised when the layout's size changes (width, height in physical pixels).</summary>
    internal event EventHandler SizeChanged;

    /// <inheritdoc />
    protected override void OnSizeChanged(int w, int h, int oldw, int oldh)
    {
        base.OnSizeChanged(w, h, oldw, oldh);
        SizeChanged?.Invoke(this, EventArgs.Empty);
    }

    private static AFrameLayout CreateLayer(AContext context) => new(context)
    {
        LayoutParameters = new AViewGroup.LayoutParams(AViewGroup.LayoutParams.MatchParent, AViewGroup.LayoutParams.MatchParent),
    };
}
