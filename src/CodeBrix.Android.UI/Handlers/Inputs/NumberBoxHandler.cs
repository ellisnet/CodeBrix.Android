// Technique from .NET MAUI, src/Core/src/Handlers/Entry/EntryHandler2.Android.cs (numeric keyboard, text written
// back with a reentrancy guard) and src/Core/src/Handlers/Stepper/StepperHandler.Android.cs (two step
// buttons changing the value by the interval) @ 828569a864. Copyright (c) .NET Foundation and Contributors.
// Licensed under the MIT License. See THIRD-PARTY-NOTICES.txt.

using System;
using CodeBrix.Android.UI.Android;
using CodeBrix.Platform.UI.Contracts;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;
using AComplexUnitType = global::Android.Util.ComplexUnitType;
using AImeAction = global::Android.Views.InputMethods.ImeAction;
using AInputTypes = global::Android.Text.InputTypes;
using ATextInputLayout = Google.Android.Material.TextField.TextInputLayout;
using ATextView = global::Android.Widget.TextView;
using AView = global::Android.Views.View;
using AViewStates = global::Android.Views.ViewStates;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>
/// The NumberBox handler (plan 3 row NumberBox): the TextBox view (Header, outlined box, editor) with a
/// numeric keyboard (text for AcceptsExpression), and, unless SpinButtonPlacementMode is Hidden, a minus
/// start icon and a plus end icon that step Value by SmallChange (wrapping when IsWrapEnabled). The typed
/// text is committed when the IME action runs or the box loses focus, through
/// <c>NumberBox.CommitTextFromPlatform</c> (Core's own validation: NumberFormatter, AcceptsExpression,
/// ValidationMode -> Value, and the formatted Text, which Core writes to Text for the handler to show).
/// </summary>
internal sealed class NumberBoxHandler : ViewHandler<NumberBox, TextBoxView>
{
    /// <summary>NumberBox's mapper.</summary>
    public static readonly PropertyMapper<NumberBox, NumberBoxHandler> Mapper = new(ViewMappers.ViewMapper)
    {
        [NumberBox.TextProperty] = MapText,
        [NumberBox.ValueProperty] = MapText,
        [NumberBox.HeaderProperty] = MapHeader,
        [NumberBox.PlaceholderTextProperty] = MapPlaceholder,
        [NumberBox.AcceptsExpressionProperty] = MapInput,
        [NumberBox.SpinButtonPlacementModeProperty] = MapSpinButtons,
        [NumberBox.TextAlignmentProperty] = MapInput,
        [Control.IsEnabledProperty] = MapIsEnabled,
        [Control.ForegroundProperty] = MapColors,
        [Control.BackgroundProperty] = MapColors,
        [Control.BorderBrushProperty] = MapColors,
        [Control.BorderThicknessProperty] = MapBox,
        [Control.CornerRadiusProperty] = MapBox,
        [Control.FontFamilyProperty] = MapFont,
        [Control.FontSizeProperty] = MapFont,
    };

    private readonly CorePointerBridge _bridge;
    private bool _updating;
    private int _iconRoom = -1;

    /// <summary>Creates the handler.</summary>
    public NumberBoxHandler()
        : base(Mapper)
    {
        _bridge = new CorePointerBridge(() => NativeView);
    }

    /// <inheritdoc />
    public override ElementHandlerCapabilities Capabilities =>
        ElementHandlerCapabilities.OwnsVisuals | ElementHandlerCapabilities.MeasuresNatively | ElementHandlerCapabilities.OwnsInput;

    /// <summary>The NumberBox handler, or the templated fallback for one with a template of its own.</summary>
    internal static IAndroidElementHandler Create(UIElement element) =>
        element is NumberBox box && NativeControlPolicy.IsNative(box, typeof(NumberBox), new[] { "DefaultNumberBoxStyle" }, out _)
            ? new NumberBoxHandler()
            : new TemplatedFallbackHandler();

    /// <summary>Maps Text and Value (the formatted value Core wrote to Text; the value when Text is empty).</summary>
    public static void MapText(NumberBoxHandler handler, NumberBox element)
    {
        var editor = handler.PlatformView.Editor;
        if (editor.HasFocus && handler._updating)
        {
            return;
        }

        var text = element.Text;
        if (string.IsNullOrEmpty(text) && !double.IsNaN(element.Value))
        {
            text = element.Value.ToString(System.Globalization.CultureInfo.CurrentCulture);
        }

        text ??= string.Empty;
        if (!string.Equals(editor.Text, text, StringComparison.Ordinal))
        {
            handler._updating = true;
            try
            {
                editor.Text = text;
                editor.SetSelection(editor.Length());
            }
            finally
            {
                handler._updating = false;
            }
        }
    }

    /// <summary>Maps Header.</summary>
    public static void MapHeader(NumberBoxHandler handler, NumberBox element)
    {
        var header = handler.PlatformView.Header;
        var text = element.Header switch
        {
            null => null,
            string s => s,
            TextBlock block => block.Text,
            UIElement => null,
            var other => other.ToString(),
        };
        header.Text = text ?? string.Empty;
        header.Visibility = string.IsNullOrEmpty(text) ? AViewStates.Gone : AViewStates.Visible;
        element.InvalidateMeasure();
    }

    /// <summary>Maps PlaceholderText.</summary>
    public static void MapPlaceholder(NumberBoxHandler handler, NumberBox element) =>
        handler.PlatformView.Editor.Hint = element.PlaceholderText ?? string.Empty;

    /// <summary>Maps the keyboard (numeric, or text for expressions) and the text alignment.</summary>
    public static void MapInput(NumberBoxHandler handler, NumberBox element)
    {
        var editor = handler.PlatformView.Editor;
        editor.SetSingleLine(true);
        editor.SetRawInputType(element.AcceptsExpression
            ? AInputTypes.ClassText | AInputTypes.TextFlagNoSuggestions
            : AInputTypes.ClassNumber | AInputTypes.NumberFlagDecimal | AInputTypes.NumberFlagSigned);
        editor.ImeOptions = AImeAction.Done;
        editor.Gravity = (element.TextAlignment switch
        {
            TextAlignment.Center => global::Android.Views.GravityFlags.CenterHorizontal,
            TextAlignment.Right or TextAlignment.End => global::Android.Views.GravityFlags.End,
            _ => global::Android.Views.GravityFlags.Start,
        }) | global::Android.Views.GravityFlags.CenterVertical;
    }

    /// <summary>Maps SpinButtonPlacementMode (step icons at the start and end of the box, or none).</summary>
    public static void MapSpinButtons(NumberBoxHandler handler, NumberBox element)
    {
        var field = handler.PlatformView.Field;
        if (element.SpinButtonPlacementMode == NumberBoxSpinButtonPlacementMode.Hidden)
        {
            field.StartIconDrawable = null;
            field.EndIconMode = ATextInputLayout.EndIconNone;
            return;
        }

        var density = handler.Density;
        var color = ThemeResources.ColorOf(element.Foreground) ?? unchecked((int)0xFF000000);
        field.StartIconDrawable = IconDrawables.Create(new SymbolIcon(Symbol.Remove), field.Context, density, color, 16);
        field.SetStartIconOnClickListener(new StepClick(handler, -1));
        field.EndIconMode = ATextInputLayout.EndIconCustom;
        field.EndIconDrawable = IconDrawables.Create(new SymbolIcon(Symbol.Add), field.Context, density, color, 16);
        field.SetEndIconOnClickListener(new StepClick(handler, 1));
    }

    /// <summary>Maps IsEnabled.</summary>
    public static void MapIsEnabled(NumberBoxHandler handler, NumberBox element)
    {
        var view = handler.PlatformView;
        view.Enabled = element.IsEnabled;
        view.Field.Enabled = element.IsEnabled;
        view.Editor.Enabled = element.IsEnabled;
    }

    /// <summary>Maps the colours (the TextControl keys, as a TextBox).</summary>
    public static void MapColors(NumberBoxHandler handler, NumberBox element)
    {
        var view = handler.PlatformView;
        var black = unchecked((int)0xFF000000);
        var foreground = ThemeResources.ColorOf(element.Foreground) ?? ThemeResources.FindColor(element, "TextControlForeground") ?? black;
        view.Editor.SetTextColor(StateColors.Single(foreground));
        var background = ThemeResources.ColorOf(element.Background) ?? ThemeResources.FindColor(element, "TextControlBackground") ?? 0;
        view.Field.SetBoxBackgroundColorStateList(StateColors.Single(background));
        var border = ThemeResources.ColorOf(element.BorderBrush) ?? ThemeResources.FindColor(element, "TextControlBorderBrush") ?? unchecked((int)0xFF808080);
        view.Field.SetBoxStrokeColorStateList(StateColors.Single(border));
        view.Header.SetTextColor(StateColors.FromKeys(element, "TextControlHeaderForeground", black).ToColorStateList());
    }

    /// <summary>Maps BorderThickness and CornerRadius.</summary>
    public static void MapBox(NumberBoxHandler handler, NumberBox element)
    {
        var field = handler.PlatformView.Field;
        var density = handler.Density;
        var stroke = MaterialShapes.StrokeWidth(element.BorderThickness, density);
        field.BoxStrokeWidth = stroke;
        field.BoxStrokeWidthFocused = stroke > 0 ? Math.Max(stroke, MaterialWidgets.Px(2, density)) : 0;
        var radius = element.CornerRadius;
        field.SetBoxCornerRadii((float)(radius.TopLeft * density), (float)(radius.TopRight * density), (float)(radius.BottomLeft * density), (float)(radius.BottomRight * density));
    }

    /// <summary>Maps the font.</summary>
    public static void MapFont(NumberBoxHandler handler, NumberBox element)
    {
        var view = handler.PlatformView;
        if (AndroidPlatformBootstrap.Fonts?.Resolve(element.FontFamily, element.FontWeight, element.FontStyle, element.FontStretch) is { } typeface)
        {
            view.Editor.Typeface = typeface;
            view.Header.Typeface = typeface;
        }

        var size = (float)(element.FontSize * handler.Density);
        view.Editor.SetTextSize(AComplexUnitType.Px, size);
        view.Header.SetTextSize(AComplexUnitType.Px, size);
        element.InvalidateMeasure();
    }

    // [AP8-S batch 3] The Material text field makes room for its start and end (minus / plus) icons by giving its editor
    // dummy compound drawables AFTER a measure, and lays itself out again later: a NumberBox without a width of its own
    // (a pasted app's tool bar - a horizontal StackPanel) kept the size of the first measure and drew its value under the
    // icons. When the room the editor keeps for them changes, Core measures the box again (the new measure includes it).
    private void OnEditorLayoutChange(object sender, AView.LayoutChangeEventArgs e)
    {
        if (PlatformView?.Editor is not { } editor)
        {
            return;
        }

        var room = editor.TotalPaddingLeft + editor.TotalPaddingRight;
        if (_iconRoom < 0 || room == _iconRoom)
        {
            return;
        }

        // Measured with less room than the editor keeps now: measure again (Measure records the new room).
        _iconRoom = room;
        Element?.InvalidateMeasure();
    }

    /// <inheritdoc />
    public override Size Measure(Size availableSize)
    {
        var size = ViewHandlerExtensions.GetDesiredSizeFromView(NativeView, availableSize, Density);
        if (PlatformView?.Editor is { } editor)
        {
            _iconRoom = editor.TotalPaddingLeft + editor.TotalPaddingRight;
        }

        return size;
    }

    /// <inheritdoc />
    protected override TextBoxView CreatePlatformView() => new(MaterialWidgets.Material3(Context)) { HeaderGap = MaterialWidgets.Px(8, Density) };

    /// <inheritdoc />
    protected override void ConnectHandler(TextBoxView platformView)
    {
        base.ConnectHandler(platformView);
        platformView.Editor.EditorAction += OnEditorAction;
        platformView.Editor.FocusChange += OnFocusChange;
        platformView.Editor.Touch += OnTouch;
        platformView.Field.Touch += OnTouch;
        platformView.Editor.LayoutChange += OnEditorLayoutChange;
        if (Element != null)
        {
            _bridge.Attach(Element);
        }
    }

    /// <inheritdoc />
    protected override void DisconnectHandler(TextBoxView platformView)
    {
        _bridge.Detach();
        platformView.Editor.EditorAction -= OnEditorAction;
        platformView.Editor.FocusChange -= OnFocusChange;
        platformView.Editor.Touch -= OnTouch;
        platformView.Field.Touch -= OnTouch;
        platformView.Editor.LayoutChange -= OnEditorLayoutChange;
        _iconRoom = -1;
        base.DisconnectHandler(platformView);
    }

    private void Commit()
    {
        if (Element is NumberBox element && PlatformView is { } view)
        {
            _updating = true;
            try
            {
                element.CommitTextFromPlatform(view.Editor.Text ?? string.Empty);
            }
            finally
            {
                _updating = false;
            }

            MapText(this, element);
        }
    }

    private void Step(int direction)
    {
        if (Element is not NumberBox element || !element.IsEnabled)
        {
            return;
        }

        Commit();
        var value = double.IsNaN(element.Value) ? 0 : element.Value;
        var next = value + (direction * element.SmallChange);
        if (next > element.Maximum)
        {
            next = element.IsWrapEnabled ? element.Minimum : element.Maximum;
        }
        else if (next < element.Minimum)
        {
            next = element.IsWrapEnabled ? element.Maximum : element.Minimum;
        }

        element.Value = next;
        MapText(this, element);
    }

    private void OnEditorAction(object sender, ATextView.EditorActionEventArgs e)
    {
        e.Handled = false;
        Commit();
    }

    private void OnFocusChange(object sender, AView.FocusChangeEventArgs e)
    {
        if (e.HasFocus)
        {
            if (_bridge.IsTouchActive && Element is NumberBox { FocusState: FocusState.Unfocused } element)
            {
                element.Focus(FocusState.Pointer);
            }
        }
        else
        {
            Commit();
        }
    }

    private void OnTouch(object sender, AView.TouchEventArgs e)
    {
        e.Handled = false;
        _bridge.OnNativeTouch(sender as AView, e.Event);
    }

    private sealed class StepClick : Java.Lang.Object, AView.IOnClickListener
    {
        private readonly WeakReference<NumberBoxHandler> _handler;
        private readonly int _direction;

        internal StepClick(NumberBoxHandler handler, int direction)
        {
            _handler = new WeakReference<NumberBoxHandler>(handler);
            _direction = direction;
        }

        public void OnClick(AView v)
        {
            if (_handler.TryGetTarget(out var handler))
            {
                handler.Step(_direction);
            }
        }
    }
}
