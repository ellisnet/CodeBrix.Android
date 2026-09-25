using System;
using CodeBrix.Android.UI.Android;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using AComplexUnitType = global::Android.Util.ComplexUnitType;
using AContext = global::Android.Content.Context;
using AMaterialButton = Google.Android.Material.Button.MaterialButton;
using AMaterialSplitButton = Google.Android.Material.Button.MaterialSplitButton;
using AMotionEventActions = global::Android.Views.MotionEventActions;
using ASystemClock = global::Android.OS.SystemClock;
using AView = global::Android.Views.View;
using AViewGroup = global::Android.Views.ViewGroup;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>AP10-B: the native SplitButton: a Material 3 split button (tonal leading button + chevron icon button).</summary>
internal sealed class SplitButtonView : AMaterialSplitButton
{
    /// <summary>Creates the view.</summary>
    /// <param name="context">A Material 3 context.</param>
    internal SplitButtonView(AContext context)
        : base(context)
    {
        var density = context.Resources?.DisplayMetrics?.Density ?? 1f;
        Spacing = 0;
        SetPadding(0, 0, 0, 0);
        Leading = Button(context, "materialSplitButtonLeadingFilledTonalStyle");
        Trailing = Button(context, "materialSplitButtonIconFilledTonalStyle");
        Trailing.Icon = IconDrawables.Create(new FontIconSource { Glyph = DropDownButtonMapping.Chevron }, context, density, unchecked((int)0xFF1D1B20), 12);
        Trailing.IconGravity = AMaterialButton.IconGravityTextStart;
        Trailing.IconPadding = 0;
        Trailing.ContentDescription = "More options";
        AddView(Leading, new LayoutParams(AViewGroup.LayoutParams.WrapContent, AViewGroup.LayoutParams.MatchParent));
        AddView(Trailing, new LayoutParams(AViewGroup.LayoutParams.WrapContent, AViewGroup.LayoutParams.MatchParent));
    }

    /// <summary>The leading (primary) button.</summary>
    internal AMaterialButton Leading { get; }

    /// <summary>The trailing (drop-down) button.</summary>
    internal AMaterialButton Trailing { get; }

    /// <summary>Gives the halves exact pixel widths (those of the template's two buttons under them).</summary>
    internal void SetHalfWidths(int leading, int trailing)
    {
        SetWidth(Leading, leading);
        SetWidth(Trailing, trailing);
    }

    private static void SetWidth(AView view, int width)
    {
        if (view.LayoutParameters is { } lp && lp.Width != width)
        {
            lp.Width = width;
            view.LayoutParameters = lp;
        }
    }

    private static AMaterialButton Button(AContext context, string styleAttr)
    {
        var attr = MaterialWidgets.AttrId(context, styleAttr);
        var button = attr != 0 ? new AMaterialButton(context, null, attr) : new AMaterialButton(context);
        button.InsetTop = 0;
        button.InsetBottom = 0;
        button.SetMinWidth(0);
        button.SetMinHeight(0);
        button.SetMinimumWidth(0);
        button.SetMinimumHeight(0);
        button.SetAllCaps(false);
        button.SetIncludeFontPadding(false);
        button.SoundEffectsEnabled = false;
        button.Focusable = false;
        button.FocusableInTouchMode = false;
        button.ToggleCheckedStateOnClick = false;
        button.SetPadding(0, 0, 0, 0);
        return button;
    }
}

/// <summary>
/// AP10-B: the handler of SplitButton and ToggleSplitButton (tsv rows: "MaterialSplitButton (Material 1.13+) with
/// Flyout"). The control keeps its Fluent template - its PrimaryButton and SecondaryButton parts, Click, the Flyout
/// opening, ToggleSplitButton's IsChecked toggling and IsCheckedChanged all stay Core's - and is shown by a Material 3
/// split button over it (<see cref="SplitButtonView"/>), whose two halves are exactly as wide as the two template
/// buttons under them: a finger on a half shows the Material press and the same touch lands, through Core, on the
/// template button under it (a click that is not a touch - accessibility - runs that part's click). The leading half
/// shows text content; a ToggleSplitButton's checked state checks it; the chevron half is checked while the flyout is
/// open. Element content (an icon, a panel) keeps the template visible.
/// </summary>
internal sealed class SplitButtonHandler : TemplateOverlayHandler<SplitButton, SplitButtonView>
{
    /// <summary>SplitButton's mapper.</summary>
    public static readonly PropertyMapper<SplitButton, SplitButtonHandler> Mapper = new(ViewMappers.ViewMapper)
    {
        [ContentControl.ContentProperty] = MapOverlay,
        [ContentControl.ContentTemplateProperty] = MapOverlay,
        [SplitButton.FlyoutProperty] = MapFlyout,
        [ToggleSplitButton.IsCheckedProperty] = MapOverlay,
        [Control.IsEnabledProperty] = MapOverlay,
        [Control.FontFamilyProperty] = MapOverlay,
        [Control.FontSizeProperty] = MapOverlay,
    };

    private FlyoutBase _flyout;
    private long _lastTouchUp;

    /// <summary>Creates the handler.</summary>
    public SplitButtonHandler()
        : base(Mapper)
    {
    }

    /// <summary>The handler, or the templated fallback for a re-templated split button.</summary>
    /// <param name="element">The SplitButton or ToggleSplitButton.</param>
    /// <returns>The handler.</returns>
    internal static IAndroidElementHandler Create(UIElement element) => element switch
    {
        ToggleSplitButton toggle when NativeControlPolicy.IsNative(toggle, typeof(ToggleSplitButton), new[] { "DefaultToggleSplitButtonStyle" }, out _) => new SplitButtonHandler(),
        SplitButton split when split is not ToggleSplitButton && NativeControlPolicy.IsNative(split, typeof(SplitButton), new[] { "DefaultSplitButtonStyle" }, out _) => new SplitButtonHandler(),
        _ => new TemplatedFallbackHandler(),
    };

    /// <summary>Maps Flyout: the chevron half follows the flyout's open state.</summary>
    public static void MapFlyout(SplitButtonHandler handler, SplitButton element)
    {
        handler.Follow(element.Flyout);
        handler.PostRefresh();
    }

    /// <inheritdoc />
    protected override SplitButtonView CreateOverlay(AContext context)
    {
        var view = new SplitButtonView(context);
        view.Leading.Click += OnLeadingClick;
        view.Trailing.Click += OnTrailingClick;
        view.Leading.Touch += OnWidgetTouch;
        view.Trailing.Touch += OnWidgetTouch;
        return view;
    }

    /// <inheritdoc />
    protected override bool IsOverlayShown(SplitButton element) =>
        element.Content is null or string && element.ContentTemplate == null && Part(element, "PrimaryButton") != null && Part(element, "SecondaryButton") != null;

    /// <inheritdoc />
    protected override void UpdateOverlay(SplitButton element, SplitButtonView view)
    {
        var density = Density;
        var primary = Part(element, "PrimaryButton");
        var secondary = Part(element, "SecondaryButton");
        var total = MaterialWidgets.Px(ArrangedRect.Width, density);
        var leading = MaterialWidgets.Px(primary.ActualWidth, density);
        view.SetHalfWidths(leading, Math.Max(0, total - leading));
        view.Leading.Text = element.Content as string ?? string.Empty;
        view.Leading.Typeface = AndroidPlatformBootstrap.Fonts?.Resolve(element.FontFamily, element.FontWeight, element.FontStyle, element.FontStretch);
        view.Leading.SetTextSize(AComplexUnitType.Px, (float)(element.FontSize * density));
        view.Leading.Checkable = element is ToggleSplitButton;
        view.Leading.Checked = element is ToggleSplitButton { IsChecked: true };
        view.Trailing.Checkable = true;
        view.Trailing.Checked = element.Flyout?.IsOpen == true;
        view.Leading.Enabled = element.IsEnabled && primary.IsEnabled;
        view.Trailing.Enabled = element.IsEnabled && secondary.IsEnabled;
        view.Trailing.IconTint = view.Trailing.TextColors;
    }

    /// <inheritdoc />
    protected override void DisconnectHandler(TemplateOverlayHostView platformView)
    {
        if (platformView.NativeOverlay is SplitButtonView view)
        {
            view.Leading.Click -= OnLeadingClick;
            view.Trailing.Click -= OnTrailingClick;
            view.Leading.Touch -= OnWidgetTouch;
            view.Trailing.Touch -= OnWidgetTouch;
        }

        Follow(null);
        base.DisconnectHandler(platformView);
    }

    private static ButtonBase Part(SplitButton element, string name) => FindNamed(element, name) as ButtonBase;

    private void Follow(FlyoutBase flyout)
    {
        if (ReferenceEquals(flyout, _flyout))
        {
            return;
        }

        if (_flyout != null)
        {
            _flyout.Opened -= OnFlyoutChanged;
            _flyout.Closed -= OnFlyoutChanged;
        }

        _flyout = flyout;
        if (flyout != null)
        {
            flyout.Opened += OnFlyoutChanged;
            flyout.Closed += OnFlyoutChanged;
        }
    }

    private void OnFlyoutChanged(object sender, object e) => PostRefresh();

    private void OnWidgetTouch(object sender, AView.TouchEventArgs e)
    {
        // The half shows the press; the same touch reaches Core, where it lands on the template button under the half.
        e.Handled = false;
        if (e.Event is { } motion && (motion.ActionMasked == AMotionEventActions.Up || motion.ActionMasked == AMotionEventActions.Cancel))
        {
            _lastTouchUp = ASystemClock.UptimeMillis();
        }
    }

    private void OnLeadingClick(object sender, EventArgs e) => ClickPart("PrimaryButton");

    private void OnTrailingClick(object sender, EventArgs e) => ClickPart("SecondaryButton");

    private void ClickPart(string name)
    {
        // A click right after a touch is Core's (it saw the same touch); any other comes from accessibility.
        if (ASystemClock.UptimeMillis() - _lastTouchUp < 1000 || Element is not SplitButton element)
        {
            return;
        }

        Part(element, name)?.RaiseClickFromPlatform();
    }
}
