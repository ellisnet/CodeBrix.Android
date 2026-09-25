using System;
using System.Globalization;
using CodeBrix.Android.UI.Input;
using CodeBrix.Android.UI.Portable.Drawing;
using CodeBrix.Platform.UI.Contracts;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;
using AColor = global::Android.Graphics.Color;
using AColorStateList = global::Android.Content.Res.ColorStateList;
using AContext = global::Android.Content.Context;
using AEditText = global::Android.Widget.EditText;
using AImeAction = global::Android.Views.InputMethods.ImeAction;
using AInputTypes = global::Android.Text.InputTypes;
using ALinearLayout = global::Android.Widget.LinearLayout;
using AMaterialButton = Google.Android.Material.Button.MaterialButton;
using AMaterialSlider = Google.Android.Material.Slider.Slider;
using ATextInputEditText = Google.Android.Material.TextField.TextInputEditText;
using ATextInputLayout = Google.Android.Material.TextField.TextInputLayout;
using AView = global::Android.Views.View;
using AViewGroup = global::Android.Views.ViewGroup;
using AViewStates = global::Android.Views.ViewStates;
using WColor = Windows.UI.Color;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>
/// The native ColorPicker (plan 3 row ColorPicker; Pinta's ColorPickerDialog): a composition of Android views
/// instead of the Fluent template (whose spectrum is a composition-drawn bitmap and whose text boxes are templated
/// parts): a spectrum view drawing the two HSV channels of ColorSpectrumComponents (<see cref="ColorSpectrumView"/>,
/// android.graphics), a preview swatch, Material Sliders for the third channel and for alpha (over a gradient bar),
/// the R/G/B/A channel fields and the hex field (Material TextInputLayouts), and the More/Less button. Every
/// IsXxxVisible property shows or hides its part; the picker writes <see cref="ColorPicker.Color"/> (Core raises
/// ColorChanged) and follows it without echo, keeping the hue of greys and the channel the user is dragging.
/// </summary>
/// <remarks>
/// Not reproduced: ColorSpectrumShape.Ring (drawn as the Box), the Min/Max Hue/Saturation/Value clamps, the
/// RGB/HSV channel-mode ComboBox (channels are RGB) and Orientation.Horizontal (the parts stack vertically).
/// </remarks>
internal sealed class ColorPickerHandler : ViewHandler<ColorPicker, ColorPickerView>
{
    /// <summary>The ColorPicker's mapper.</summary>
    public static readonly PropertyMapper<ColorPicker, ColorPickerHandler> Mapper = new(ViewMappers.ViewMapper)
    {
        [ColorPicker.ColorProperty] = MapColor,
        [ColorPicker.PreviousColorProperty] = MapColor,
        [ColorPicker.ColorSpectrumComponentsProperty] = MapColor,
        [ColorPicker.IsAlphaEnabledProperty] = MapParts,
        [ColorPicker.IsAlphaSliderVisibleProperty] = MapParts,
        [ColorPicker.IsAlphaTextInputVisibleProperty] = MapParts,
        [ColorPicker.IsColorChannelTextInputVisibleProperty] = MapParts,
        [ColorPicker.IsColorPreviewVisibleProperty] = MapParts,
        [ColorPicker.IsColorSliderVisibleProperty] = MapParts,
        [ColorPicker.IsColorSpectrumVisibleProperty] = MapParts,
        [ColorPicker.IsHexInputVisibleProperty] = MapParts,
        [ColorPicker.IsMoreButtonVisibleProperty] = MapParts,
        [Control.IsEnabledProperty] = MapIsEnabled,
    };

    private HsvColor _hsv;
    private WColor _shown;
    private bool _hasShown;
    private bool _updating;
    private bool _moreOpen;

    /// <summary>Creates the handler.</summary>
    public ColorPickerHandler()
        : base(Mapper)
    {
    }

    /// <summary>The native view (null while disconnected).</summary>
    internal ColorPickerView Native => ((ElementHandler)this).PlatformView as ColorPickerView;

    /// <inheritdoc />
    public override ElementHandlerCapabilities Capabilities => ElementHandlerCapabilities.OwnsVisuals | ElementHandlerCapabilities.MeasuresNatively;

    /// <summary>The HSV colour the picker shows (keeps the hue of greys).</summary>
    internal HsvColor Hsv => _hsv;

    /// <summary>The native ColorPicker, or the templated fallback when a style replaces its template.</summary>
    /// <param name="element">The ColorPicker.</param>
    /// <returns>The handler.</returns>
    internal static IAndroidElementHandler Create(UIElement element) =>
        element is ColorPicker picker && NativeControlPolicy.IsNative(picker, typeof(ColorPicker), new[] { "DefaultColorPickerStyle" }, out _)
            ? new ColorPickerHandler()
            : new TemplatedFallbackHandler();

    /// <summary>Maps Color / PreviousColor / ColorSpectrumComponents to every part.</summary>
    public static void MapColor(ColorPickerHandler handler, ColorPicker element)
    {
        if (handler._updating)
        {
            return;
        }

        var color = element.Color;
        if (!handler._hasShown || color != handler._shown)
        {
            // A colour set from Core: re-derive HSV, but keep the hue (and saturation) where RGB loses them.
            var hsv = ColorPickerMath.ToHsv(color);
            if (handler._hasShown)
            {
                if (hsv.S <= 0 || hsv.V <= 0)
                {
                    hsv = hsv with { H = handler._hsv.H };
                }

                if (hsv.V <= 0)
                {
                    hsv = hsv with { S = handler._hsv.S };
                }
            }

            handler._hsv = hsv;
            handler._shown = color;
            handler._hasShown = true;
        }

        handler.Refresh(null);
    }

    /// <summary>Maps the IsXxxVisible / IsAlphaEnabled properties (which parts are shown).</summary>
    public static void MapParts(ColorPickerHandler handler, ColorPicker element)
    {
        if (handler.Native is not { } view)
        {
            return;
        }

        var alpha = element.IsAlphaEnabled;
        view.Spectrum.Visibility = Shown(element.IsColorSpectrumVisible);
        view.Preview.Visibility = Shown(element.IsColorPreviewVisible);
        view.ColorSlider.Visibility = Shown(element.IsColorSliderVisible);
        view.AlphaSlider.Visibility = Shown(alpha && element.IsAlphaSliderVisible);
        view.MoreButton.Visibility = Shown(element.IsMoreButtonVisible && (element.IsColorChannelTextInputVisible || element.IsHexInputVisible));
        var textShown = !element.IsMoreButtonVisible || handler._moreOpen;
        view.ChannelRow.Visibility = Shown(textShown && element.IsColorChannelTextInputVisible);
        view.AlphaField.Visibility = Shown(alpha && element.IsAlphaTextInputVisible);
        view.HexField.Visibility = Shown(textShown && element.IsHexInputVisible);
        view.MoreButton.Text = handler._moreOpen ? "Less" : "More";
        handler.Refresh(null);
        element.InvalidateMeasure();
    }

    /// <summary>Maps IsEnabled.</summary>
    public static void MapIsEnabled(ColorPickerHandler handler, ColorPicker element)
    {
        if (handler.Native is { } view)
        {
            view.SetEnabledDeep(element.IsEnabled);
        }
    }

    /// <inheritdoc />
    public override Size Measure(Size availableSize)
    {
        if (Native is not { } view)
        {
            return new Size(0, 0);
        }

        // Measure at the width the parts will really have: an unconstrained width first gives the natural width,
        // then the height is measured at exactly that width (text fields are taller once their width is fixed).
        var density = Density;
        var desired = ViewHandlerExtensions.GetDesiredSizeFromView(view, availableSize, density);

        // Core raises the desired width to MinWidth (or the explicit Width) after this measure; the spectrum's height
        // follows the width, so the height is measured at the width the control will be arranged to.
        var width = desired.Width;
        if (Element is FrameworkElement element)
        {
            width = double.IsNaN(element.Width) ? Math.Max(width, element.MinWidth) : element.Width;
            width = Math.Min(width, element.MaxWidth);
            if (!double.IsInfinity(availableSize.Width))
            {
                width = Math.Min(width, availableSize.Width);
            }
        }

        var widthPx = MaterialWidgets.Px(width, density);
        view.Measure(
            Platform.MeasureSpecExtensions.Exactly(widthPx),
            ViewHandlerExtensions.CreateMeasureSpec(availableSize.Height, density));
        return new Size(width, Portable.Layout.LayoutReplayMath.FromPixels(view.MeasuredHeight, density));
    }

    /// <inheritdoc />
    protected override ColorPickerView CreatePlatformView() => new(MaterialWidgets.Material3(Context));

    /// <inheritdoc />
    protected override void ConnectHandler(ColorPickerView platformView)
    {
        base.ConnectHandler(platformView);
        platformView.Spectrum.Picked += OnSpectrumPicked;
        platformView.ColorSlider.Slider.Change += OnColorSliderChanged;
        platformView.AlphaSlider.Slider.Change += OnAlphaSliderChanged;
        platformView.ColorSlider.Slider.Touch += OnWidgetTouch;
        platformView.AlphaSlider.Slider.Touch += OnWidgetTouch;
        platformView.MoreButton.Click += OnMoreClick;
        foreach (var field in platformView.ChannelEditors)
        {
            field.EditorAction += OnFieldAction;
            field.FocusChange += OnFieldFocusChange;
        }
    }

    /// <inheritdoc />
    protected override void DisconnectHandler(ColorPickerView platformView)
    {
        platformView.Spectrum.Picked -= OnSpectrumPicked;
        platformView.ColorSlider.Slider.Change -= OnColorSliderChanged;
        platformView.AlphaSlider.Slider.Change -= OnAlphaSliderChanged;
        platformView.ColorSlider.Slider.Touch -= OnWidgetTouch;
        platformView.AlphaSlider.Slider.Touch -= OnWidgetTouch;
        platformView.MoreButton.Click -= OnMoreClick;
        foreach (var field in platformView.ChannelEditors)
        {
            field.EditorAction -= OnFieldAction;
            field.FocusChange -= OnFieldFocusChange;
        }

        base.DisconnectHandler(platformView);
    }

    private static AViewStates Shown(bool shown) => shown ? AViewStates.Visible : AViewStates.Gone;

    /// <summary>Writes a colour the user made (the source part is not refreshed while it is being used).</summary>
    private void Commit(HsvColor hsv, byte alpha, AView source)
    {
        if (Element is not ColorPicker element)
        {
            return;
        }

        _hsv = hsv;
        var color = ColorPickerMath.ToColor(hsv, element.IsAlphaEnabled ? alpha : (byte)255);
        _shown = color;
        _hasShown = true;
        _updating = true;
        try
        {
            if (element.Color != color)
            {
                element.Color = color;
            }
        }
        finally
        {
            _updating = false;
        }

        Refresh(source);
    }

    /// <summary>Shows the current colour in every part except <paramref name="except"/>.</summary>
    private void Refresh(AView except)
    {
        if (Native is not { } view || Element is not ColorPicker element)
        {
            return;
        }

        var components = element.ColorSpectrumComponents;
        var channel = ColorPickerMath.SliderChannel(components);
        var color = _shown;
        view.Spectrum.SetColor(_hsv, components);
        view.Preview.SetColors(color, element.PreviousColor);

        _updating = true;
        try
        {
            if (!ReferenceEquals(except, view.ColorSlider.Slider))
            {
                view.ColorSlider.Slider.Value = (float)ColorPickerMath.Fraction(_hsv, channel);
            }

            view.ColorSlider.Bar.SetGradient(ChannelGradient(_hsv, channel), checkerboard: false);
            if (!ReferenceEquals(except, view.AlphaSlider.Slider))
            {
                view.AlphaSlider.Slider.Value = color.A / 255f;
            }

            var opaque = WColor.FromArgb(255, color.R, color.G, color.B);
            view.AlphaSlider.Bar.SetGradient(new[] { Argb(WColor.FromArgb(0, color.R, color.G, color.B)), Argb(opaque) }, checkerboard: true);
            SetText(view.RedField, color.R.ToString(CultureInfo.InvariantCulture), except);
            SetText(view.GreenField, color.G.ToString(CultureInfo.InvariantCulture), except);
            SetText(view.BlueField, color.B.ToString(CultureInfo.InvariantCulture), except);
            SetText(view.AlphaEditor, Math.Round(color.A * 100 / 255.0).ToString(CultureInfo.InvariantCulture), except);
            SetText(view.HexEditor, ColorPickerMath.ToHex(color, element.IsAlphaEnabled), except);
        }
        finally
        {
            _updating = false;
        }
    }

    private static void SetText(AEditText editor, string text, AView except)
    {
        if (ReferenceEquals(editor, except) || editor.HasFocus)
        {
            return;
        }

        if (!string.Equals(editor.Text, text, StringComparison.Ordinal))
        {
            editor.Text = text;
        }
    }

    private static int[] ChannelGradient(HsvColor hsv, ColorChannel channel)
    {
        const int Stops = 7;
        var colors = new int[Stops];
        for (var i = 0; i < Stops; i++)
        {
            colors[i] = Argb(ColorPickerMath.ToColor(ColorPickerMath.WithFraction(hsv, channel, i / (double)(Stops - 1))));
        }

        return colors;
    }

    private static int Argb(WColor color) => (color.A << 24) | (color.R << 16) | (color.G << 8) | color.B;

    private void OnSpectrumPicked(object sender, (double X, double Y) position)
    {
        if (Element is not ColorPicker element)
        {
            return;
        }

        var hsv = ColorPickerMath.AtSpectrumPosition(position.X, position.Y, element.ColorSpectrumComponents, _hsv);
        Commit(hsv, _shown.A, null);
    }

    private void OnColorSliderChanged(object sender, AMaterialSlider.ChangeEventArgs e)
    {
        if (_updating || !e.P2 || Element is not ColorPicker element)
        {
            return;
        }

        var channel = ColorPickerMath.SliderChannel(element.ColorSpectrumComponents);
        Commit(ColorPickerMath.WithFraction(_hsv, channel, e.P1), _shown.A, Native?.ColorSlider.Slider);
    }

    private void OnAlphaSliderChanged(object sender, AMaterialSlider.ChangeEventArgs e)
    {
        if (_updating || !e.P2)
        {
            return;
        }

        Commit(_hsv, (byte)Math.Clamp(Math.Round(e.P1 * 255), 0, 255), Native?.AlphaSlider.Slider);
    }

    private void OnWidgetTouch(object sender, AView.TouchEventArgs e)
    {
        // The slider acts on the finger; Core only routes it (no Core control reacts to the same touch).
        e.Handled = false;
        NativeInput.MarkHandled();
    }

    private void OnMoreClick(object sender, EventArgs e)
    {
        _moreOpen = !_moreOpen;
        if (Element is ColorPicker element)
        {
            MapParts(this, element);
        }
    }

    private void OnFieldAction(object sender, global::Android.Widget.TextView.EditorActionEventArgs e)
    {
        e.Handled = false;
        if (e.ActionId is AImeAction.Done or AImeAction.Next or AImeAction.Go || e.Event?.KeyCode == global::Android.Views.Keycode.Enter)
        {
            ApplyField(sender as AEditText);
        }
    }

    private void OnFieldFocusChange(object sender, AView.FocusChangeEventArgs e)
    {
        if (!e.HasFocus)
        {
            ApplyField(sender as AEditText);
        }
    }

    /// <summary>Applies the text of one field (invalid text is put back to the current value).</summary>
    internal void ApplyField(AEditText field)
    {
        if (_updating || field == null || Native is not { } view || Element is not ColorPicker element)
        {
            return;
        }

        var color = _shown;
        var text = field.Text ?? string.Empty;
        if (ReferenceEquals(field, view.HexEditor))
        {
            if (ColorPickerMath.TryParseHex(text, element.IsAlphaEnabled, out var parsed))
            {
                color = element.IsAlphaEnabled ? parsed : WColor.FromArgb(color.A, parsed.R, parsed.G, parsed.B);
            }
        }
        else if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
        {
            if (ReferenceEquals(field, view.AlphaEditor))
            {
                color.A = (byte)Math.Clamp(Math.Round(Math.Clamp(number, 0, 100) * 255 / 100.0), 0, 255);
            }
            else
            {
                var channel = (byte)Math.Clamp(number, 0, 255);
                if (ReferenceEquals(field, view.RedField))
                {
                    color.R = channel;
                }
                else if (ReferenceEquals(field, view.GreenField))
                {
                    color.G = channel;
                }
                else
                {
                    color.B = channel;
                }
            }
        }

        var hsv = ColorPickerMath.ToHsv(color);
        if (hsv.S <= 0 || hsv.V <= 0)
        {
            hsv = hsv with { H = _hsv.H };
        }

        // Force every field (this one included) to show the normalised value.
        Commit(hsv, color.A, null);
        _updating = true;
        try
        {
            field.Text = ReferenceEquals(field, view.HexEditor) ? ColorPickerMath.ToHex(_shown, element.IsAlphaEnabled)
                : ReferenceEquals(field, view.AlphaEditor) ? Math.Round(_shown.A * 100 / 255.0).ToString(CultureInfo.InvariantCulture)
                : ReferenceEquals(field, view.RedField) ? _shown.R.ToString(CultureInfo.InvariantCulture)
                : ReferenceEquals(field, view.GreenField) ? _shown.G.ToString(CultureInfo.InvariantCulture)
                : _shown.B.ToString(CultureInfo.InvariantCulture);
        }
        finally
        {
            _updating = false;
        }
    }
}

/// <summary>The native view of a ColorPicker: its parts stacked vertically (spectrum and preview in one row).</summary>
internal sealed class ColorPickerView : ALinearLayout
{
    /// <summary>Creates the view.</summary>
    /// <param name="context">A Material 3 context.</param>
    internal ColorPickerView(AContext context)
        : base(context)
    {
        Orientation = global::Android.Widget.Orientation.Vertical;
        var density = context.Resources?.DisplayMetrics?.Density ?? 1f;
        int Dp(double value) => (int)Math.Round(value * density);

        var top = new ALinearLayout(context) { Orientation = global::Android.Widget.Orientation.Horizontal };
        Spectrum = new ColorSpectrumView(context);
        Preview = new ColorPreviewView(context);
        top.AddView(Spectrum, new LayoutParams(0, AViewGroup.LayoutParams.WrapContent, 1f));
        var previewParams = new LayoutParams(Dp(44), AViewGroup.LayoutParams.MatchParent) { LeftMargin = Dp(12) };
        top.AddView(Preview, previewParams);
        AddView(top, new LayoutParams(AViewGroup.LayoutParams.MatchParent, AViewGroup.LayoutParams.WrapContent));

        ColorSlider = new ChannelSliderView(context);
        AlphaSlider = new ChannelSliderView(context);
        AddView(ColorSlider, new LayoutParams(AViewGroup.LayoutParams.MatchParent, Dp(40)) { TopMargin = Dp(12) });
        AddView(AlphaSlider, new LayoutParams(AViewGroup.LayoutParams.MatchParent, Dp(40)) { TopMargin = Dp(4) });

        MoreButton = new AMaterialButton(context, null, MaterialWidgets.AttrId(context, "borderlessButtonStyle")) { Text = "More" };
        AddView(MoreButton, new LayoutParams(AViewGroup.LayoutParams.WrapContent, AViewGroup.LayoutParams.WrapContent) { TopMargin = Dp(4) });

        ChannelRow = new ALinearLayout(context) { Orientation = global::Android.Widget.Orientation.Horizontal };
        RedField = AddField(ChannelRow, "R", Dp(4)).Editor;
        GreenField = AddField(ChannelRow, "G", Dp(4)).Editor;
        BlueField = AddField(ChannelRow, "B", Dp(4)).Editor;
        (AlphaField, AlphaEditor) = AddField(ChannelRow, "A %", 0);
        AddView(ChannelRow, new LayoutParams(AViewGroup.LayoutParams.MatchParent, AViewGroup.LayoutParams.WrapContent) { TopMargin = Dp(8) });

        var hexLayout = Field(context, "Hex");
        HexEditor = (AEditText)hexLayout.EditText;
        HexEditor.InputType = AInputTypes.ClassText | AInputTypes.TextFlagNoSuggestions | AInputTypes.TextFlagCapCharacters;
        HexField = hexLayout;
        AddView(hexLayout, new LayoutParams(AViewGroup.LayoutParams.MatchParent, AViewGroup.LayoutParams.WrapContent) { TopMargin = Dp(8) });

        ChannelEditors = new[] { RedField, GreenField, BlueField, AlphaEditor, HexEditor };
    }

    /// <summary>The spectrum.</summary>
    internal ColorSpectrumView Spectrum { get; }

    /// <summary>The preview swatch.</summary>
    internal ColorPreviewView Preview { get; }

    /// <summary>The third-channel slider.</summary>
    internal ChannelSliderView ColorSlider { get; }

    /// <summary>The alpha slider.</summary>
    internal ChannelSliderView AlphaSlider { get; }

    /// <summary>The More/Less button.</summary>
    internal AMaterialButton MoreButton { get; }

    /// <summary>The row of channel fields.</summary>
    internal ALinearLayout ChannelRow { get; }

    /// <summary>The red field's editor.</summary>
    internal AEditText RedField { get; }

    /// <summary>The green field's editor.</summary>
    internal AEditText GreenField { get; }

    /// <summary>The blue field's editor.</summary>
    internal AEditText BlueField { get; }

    /// <summary>The alpha (percent) field's editor.</summary>
    internal AEditText AlphaEditor { get; }

    /// <summary>The alpha field (its box).</summary>
    internal AView AlphaField { get; }

    /// <summary>The hex field (its box).</summary>
    internal AView HexField { get; }

    /// <summary>The hex field's editor.</summary>
    internal AEditText HexEditor { get; }

    /// <summary>Every text editor (channels and hex).</summary>
    internal AEditText[] ChannelEditors { get; }

    /// <summary>Enables or disables every part.</summary>
    /// <param name="enabled">True to enable.</param>
    internal void SetEnabledDeep(bool enabled)
    {
        Spectrum.Enabled = enabled;
        ColorSlider.Slider.Enabled = enabled;
        AlphaSlider.Slider.Enabled = enabled;
        MoreButton.Enabled = enabled;
        foreach (var editor in ChannelEditors)
        {
            editor.Enabled = enabled;
        }

        Alpha = enabled ? 1f : 0.6f;
    }

    private static ATextInputLayout Field(AContext context, string label)
    {
        var layout = new ATextInputLayout(context)
        {
            BoxBackgroundMode = ATextInputLayout.BoxBackgroundOutline,
            Hint = label,
        };
        var editor = new ATextInputEditText(layout.Context) { ImeOptions = AImeAction.Done };
        editor.SetSingleLine(true);
        layout.AddView(editor, new LayoutParams(AViewGroup.LayoutParams.MatchParent, AViewGroup.LayoutParams.WrapContent));
        return layout;
    }

    private static (AView Field, AEditText Editor) AddField(ALinearLayout row, string label, int endMargin)
    {
        var layout = Field(row.Context, label);
        var editor = (AEditText)layout.EditText;
        editor.InputType = AInputTypes.ClassNumber;
        row.AddView(layout, new LayoutParams(0, AViewGroup.LayoutParams.WrapContent, 1f) { RightMargin = endMargin });
        return (layout, editor);
    }
}

/// <summary>A channel slider: a Material Slider (0-1, transparent track) over a rounded gradient bar.</summary>
internal sealed class ChannelSliderView : global::Android.Widget.FrameLayout
{
    /// <summary>Creates the view.</summary>
    /// <param name="context">A Material 3 context.</param>
    internal ChannelSliderView(AContext context)
        : base(context)
    {
        Bar = new GradientBarView(context);
        Slider = new AMaterialSlider(context)
        {
            ValueFrom = 0f,
            ValueTo = 1f,
            StepSize = 0f,
            LabelBehavior = 2, // LABEL_GONE
            TrackActiveTintList = AColorStateList.ValueOf(AColor.Transparent),
            TrackInactiveTintList = AColorStateList.ValueOf(AColor.Transparent),
        };
        AddView(Bar, new LayoutParams(LayoutParams.MatchParent, LayoutParams.MatchParent));
        AddView(Slider, new LayoutParams(LayoutParams.MatchParent, LayoutParams.MatchParent));
    }

    /// <summary>The gradient bar.</summary>
    internal GradientBarView Bar { get; }

    /// <summary>The Material slider.</summary>
    internal AMaterialSlider Slider { get; }

    /// <inheritdoc />
    protected override void OnLayout(bool changed, int left, int top, int right, int bottom)
    {
        base.OnLayout(changed, left, top, right, bottom);
        Bar.Inset = Slider.TrackSidePadding;
    }
}

/// <summary>The gradient bar under a channel slider (a checkerboard under it for alpha).</summary>
internal sealed class GradientBarView : AView
{
    private readonly global::Android.Graphics.Paint _paint = new(global::Android.Graphics.PaintFlags.AntiAlias);
    private int[] _colors = { unchecked((int)0xFF000000), unchecked((int)0xFFFFFFFF) };
    private bool _checkerboard;
    private int _inset;

    /// <summary>Creates the view.</summary>
    /// <param name="context">The context.</param>
    internal GradientBarView(AContext context)
        : base(context)
    {
    }

    /// <summary>The horizontal inset of the bar (the slider's track side padding), in pixels.</summary>
    internal int Inset
    {
        get => _inset;
        set
        {
            if (_inset != value)
            {
                _inset = value;
                Invalidate();
            }
        }
    }

    /// <summary>Sets the gradient's colours (ARGB) left to right.</summary>
    /// <param name="colors">The colours.</param>
    /// <param name="checkerboard">True to draw a checkerboard under it (alpha).</param>
    internal void SetGradient(int[] colors, bool checkerboard)
    {
        _colors = colors;
        _checkerboard = checkerboard;
        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnDraw(global::Android.Graphics.Canvas canvas)
    {
        base.OnDraw(canvas);
        var density = Resources?.DisplayMetrics?.Density ?? 1f;
        var barHeight = 12 * density;
        var top = (Height - barHeight) / 2;
        using var rect = new global::Android.Graphics.RectF(_inset, top, Width - _inset, top + barHeight);
        if (rect.Width() <= 0)
        {
            return;
        }

        var radius = barHeight / 2;
        canvas.Save();
        using (var clip = new global::Android.Graphics.Path())
        {
            clip.AddRoundRect(rect, radius, radius, global::Android.Graphics.Path.Direction.Cw);
            canvas.ClipPath(clip);
        }

        if (_checkerboard)
        {
            Checkerboard.Draw(canvas, rect, 4 * density);
        }

        _paint.SetShader(new global::Android.Graphics.LinearGradient(rect.Left, 0, rect.Right, 0, _colors, null, global::Android.Graphics.Shader.TileMode.Clamp));
        canvas.DrawRect(rect, _paint);
        canvas.Restore();
    }
}

/// <summary>The checkerboard shown under transparent colours.</summary>
internal static class Checkerboard
{
    private static readonly global::Android.Graphics.Paint Light = new() { Color = new AColor(unchecked((int)0xFFFFFFFF)) };
    private static readonly global::Android.Graphics.Paint Dark = new() { Color = new AColor(unchecked((int)0xFFCCCCCC)) };

    /// <summary>Fills <paramref name="rect"/> with a checkerboard of <paramref name="cell"/>-pixel squares.</summary>
    internal static void Draw(global::Android.Graphics.Canvas canvas, global::Android.Graphics.RectF rect, float cell)
    {
        canvas.DrawRect(rect, Light);
        var row = 0;
        for (var y = rect.Top; y < rect.Bottom; y += cell, row++)
        {
            for (var x = rect.Left + (row % 2 * cell); x < rect.Right; x += cell * 2)
            {
                canvas.DrawRect(x, y, Math.Min(x + cell, rect.Right), Math.Min(y + cell, rect.Bottom), Dark);
            }
        }
    }
}

/// <summary>The preview swatch: the current colour (the previous one above it when PreviousColor is set).</summary>
internal sealed class ColorPreviewView : AView
{
    private readonly global::Android.Graphics.Paint _paint = new(global::Android.Graphics.PaintFlags.AntiAlias);
    private WColor _color;
    private WColor? _previous;

    /// <summary>Creates the view.</summary>
    /// <param name="context">The context.</param>
    internal ColorPreviewView(AContext context)
        : base(context)
    {
    }

    /// <summary>The colour shown (ARGB), for diagnostics and tests.</summary>
    internal int ShownColor => (_color.A << 24) | (_color.R << 16) | (_color.G << 8) | _color.B;

    /// <summary>Sets the colours.</summary>
    /// <param name="color">The current colour.</param>
    /// <param name="previous">The previous colour, or null.</param>
    internal void SetColors(WColor color, WColor? previous)
    {
        _color = color;
        _previous = previous;
        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnDraw(global::Android.Graphics.Canvas canvas)
    {
        base.OnDraw(canvas);
        var density = Resources?.DisplayMetrics?.Density ?? 1f;
        using var rect = new global::Android.Graphics.RectF(0, 0, Width, Height);
        var radius = 4 * density;
        canvas.Save();
        using (var clip = new global::Android.Graphics.Path())
        {
            clip.AddRoundRect(rect, radius, radius, global::Android.Graphics.Path.Direction.Cw);
            canvas.ClipPath(clip);
        }

        Checkerboard.Draw(canvas, rect, 6 * density);
        if (_previous is { } previous)
        {
            _paint.Color = new AColor(previous.R, previous.G, previous.B, previous.A);
            canvas.DrawRect(0, 0, Width, Height / 2f, _paint);
            _paint.Color = new AColor(_color.R, _color.G, _color.B, _color.A);
            canvas.DrawRect(0, Height / 2f, Width, Height, _paint);
        }
        else
        {
            _paint.Color = new AColor(_color.R, _color.G, _color.B, _color.A);
            canvas.DrawRect(rect, _paint);
        }

        canvas.Restore();
    }
}
