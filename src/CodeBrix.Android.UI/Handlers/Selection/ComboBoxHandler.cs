// Technique from .NET MAUI, src/Core/src/Handlers/Picker/PickerHandler2.Android.cs and
// src/Core/src/Platform/Android/Material3Controls/MauiMaterialPicker.cs @ 828569a864 (a Material text field
// that shows the selected item and opens the choice list; the selection written back to the cross-platform
// control). Adapted to the Material 3 exposed drop-down (TextInputLayout + MaterialAutoCompleteTextView)
// instead of MAUI's dialog. Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License.
// See THIRD-PARTY-NOTICES.txt.

using System;
using System.Collections.Generic;
using System.Reflection;
using CodeBrix.Android.UI.Android;
using CodeBrix.Android.UI.Platform;
using CodeBrix.Platform.UI.Contracts;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Windows.Foundation.Collections;
using AColorDrawable = global::Android.Graphics.Drawables.ColorDrawable;
using AComplexUnitType = global::Android.Util.ComplexUnitType;
using AContext = global::Android.Content.Context;
using AImeAction = global::Android.Views.InputMethods.ImeAction;
using AInputTypes = global::Android.Text.InputTypes;
using ALinearLayoutParams = global::Android.Widget.LinearLayout.LayoutParams;
using AMaterialAutoCompleteTextView = Google.Android.Material.TextField.MaterialAutoCompleteTextView;
using AMeasureSpecMode = global::Android.Views.MeasureSpecMode;
using AResource = global::Android.Resource;
using ATextInputLayout = Google.Android.Material.TextField.TextInputLayout;
using ATextView = global::Android.Widget.TextView;
using AView = global::Android.Views.View;
using AViewGroup = global::Android.Views.ViewGroup;
using AViewStates = global::Android.Views.ViewStates;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>
/// The native view of a ComboBox: the Header above a Material exposed drop-down menu (a TextInputLayout
/// with the drop-down end icon around a MaterialAutoCompleteTextView that shows the selected item).
/// </summary>
internal sealed class ComboBoxView : AViewGroup
{
    /// <summary>Creates the view.</summary>
    /// <param name="context">A Material 3 context.</param>
    internal ComboBoxView(AContext context)
        : base(context)
    {
        Header = new ATextView(context) { Visibility = AViewStates.Gone };
        Header.SetIncludeFontPadding(false);
        Field = new ATextInputLayout(context)
        {
            HintEnabled = false,
            ErrorEnabled = false,
            HelperTextEnabled = false,
            BoxBackgroundMode = ATextInputLayout.BoxBackgroundOutline,
            EndIconMode = ATextInputLayout.EndIconDropdownMenu,
        };
        Editor = new AMaterialAutoCompleteTextView(Field.Context);
        Editor.SetIncludeFontPadding(false);
        Editor.SetMinHeight(0);
        Editor.SetMinimumHeight(0);
        Editor.SetSingleLine(true);
        Field.AddView(Editor, new ALinearLayoutParams(LayoutParams.MatchParent, LayoutParams.MatchParent));
        AddView(Header);
        AddView(Field);
    }

    /// <summary>The header text (Gone without a header).</summary>
    internal ATextView Header { get; }

    /// <summary>The Material text field (box, drop-down end icon).</summary>
    internal ATextInputLayout Field { get; }

    /// <summary>The text view showing the selected item (typing only when IsEditable).</summary>
    internal AMaterialAutoCompleteTextView Editor { get; }

    /// <summary>The gap below the header, in pixels.</summary>
    internal int HeaderGap { get; set; }

    /// <inheritdoc />
    protected override void OnMeasure(int widthMeasureSpec, int heightMeasureSpec)
    {
        var unspecified = AMeasureSpecMode.Unspecified.MakeMeasureSpec(0);
        var headerHeight = 0;
        var headerWidth = 0;
        if (Header.Visibility != AViewStates.Gone)
        {
            Header.Measure(unspecified, unspecified);
            headerHeight = Header.MeasuredHeight + HeaderGap;
            headerWidth = Header.MeasuredWidth;
        }

        var heightMode = heightMeasureSpec.GetMode();
        var fieldHeightSpec = heightMode == AMeasureSpecMode.Unspecified
            ? unspecified
            : heightMode.MakeMeasureSpec(Math.Max(0, heightMeasureSpec.GetSize() - headerHeight));
        Field.Measure(widthMeasureSpec, fieldHeightSpec);
        SetMeasuredDimension(
            ResolveSize(Math.Max(headerWidth, Field.MeasuredWidth), widthMeasureSpec),
            ResolveSize(headerHeight + Field.MeasuredHeight, heightMeasureSpec));
    }

    /// <inheritdoc />
    protected override void OnLayout(bool changed, int l, int t, int r, int b)
    {
        var width = r - l;
        var top = 0;
        if (Header.Visibility != AViewStates.Gone)
        {
            Header.Layout(0, 0, Math.Min(width, Header.MeasuredWidth), Header.MeasuredHeight);
            top = Header.MeasuredHeight + HeaderGap;
        }

        var fieldHeight = Math.Max(0, (b - t) - top);
        if (Field.MeasuredWidth != width || Field.MeasuredHeight != fieldHeight)
        {
            Field.Measure(MeasureSpecExtensions.Exactly(width), MeasureSpecExtensions.Exactly(fieldHeight));
        }

        Field.Layout(0, top, width, top + fieldHeight);
    }
}

/// <summary>
/// The ComboBox handler (plan 3 rows ComboBox, ComboBoxItem, Selector, SelectorItem): the Material 3
/// exposed drop-down menu. The rows are the items' texts - DisplayMemberPath, a ComboBoxItem's content, the
/// text of the ItemTemplate's first TextBlock with the item as its DataContext, or the item's ToString -
/// in Material's own row layout; choosing a row writes SelectedIndex (Core raises SelectionChanged and
/// updates SelectedItem and its bindings); Core's selection, items and IsDropDownOpen are shown natively.
/// IsEditable makes the text typeable: the text is submitted (Core's TextSubmitted, then selection by
/// text) with the IME action or when the box loses focus. Colours from the ComboBox keys.
/// </summary>
/// <remarks>
/// OFF by default (<see cref="Enabled"/>): the templated ComboBox (Core's popup, Fluent look) stays the default
/// until the native-vs-templated decision is taken ("AP5/AP8 decide native ComboBox", uireqs-pending.txt): the
/// copied UIReqs Items/ComboBox scenarios observe Core's own drop-down popup and its ComboBoxItem containers,
/// which a native drop-down does not create. The switch: the app's runtimeconfig / csproj
/// <c>&lt;RuntimeHostConfigurationOption Include="CodeBrix.Android.UI.NativeComboBox" Value="true" /&gt;</c>
/// (an AppContext switch, read once), or <see cref="Enabled"/> in-repo (the Android-only AndroidControls
/// feature turns it on per scenario).
/// </remarks>
internal sealed class ComboBoxHandler : ViewHandler<ComboBox, ComboBoxView>
{
    /// <summary>ComboBox's mapper.</summary>
    public static readonly PropertyMapper<ComboBox, ComboBoxHandler> Mapper = new(ViewMappers.ViewMapper)
    {
        [ItemsControl.ItemsSourceProperty] = MapItems,
        [ItemsControl.DisplayMemberPathProperty] = MapItems,
        [ItemsControl.ItemTemplateProperty] = MapItems,
        [Selector.SelectedIndexProperty] = MapSelection,
        [Selector.SelectedItemProperty] = MapSelection,
        [ComboBox.PlaceholderTextProperty] = MapPlaceholder,
        [ComboBox.HeaderProperty] = MapHeader,
        [ComboBox.IsEditableProperty] = MapEditable,
        [ComboBox.IsDropDownOpenProperty] = MapIsDropDownOpen,
        [Control.IsEnabledProperty] = MapIsEnabled,
        [Control.ForegroundProperty] = MapColors,
        [Control.BackgroundProperty] = MapColors,
        [Control.BorderBrushProperty] = MapColors,
        [FrameworkElement.StyleProperty] = MapColors,
        [Control.BorderThicknessProperty] = MapBox,
        [Control.CornerRadiusProperty] = MapBox,
        [Control.PaddingProperty] = MapPadding,
        [Control.FontFamilyProperty] = MapFont,
        [Control.FontSizeProperty] = MapFont,
        [Control.FontWeightProperty] = MapFont,
        [Control.FontStyleProperty] = MapFont,
    };

    private readonly CorePointerBridge _bridge;
    private readonly List<string> _texts = new();
    private IObservableVector<object> _items;
    private bool _updating;

    /// <summary>Creates the handler.</summary>
    public ComboBoxHandler()
        : base(Mapper)
    {
        _bridge = new CorePointerBridge(() => NativeView);
    }

    /// <summary>The AppContext switch that turns the native exposed drop-down on for an app.</summary>
    internal const string NativeComboBoxSwitch = "CodeBrix.Android.UI.NativeComboBox";

    /// <summary>
    /// True when ComboBoxes are shown by the native exposed drop-down (default: the
    /// <see cref="NativeComboBoxSwitch"/> AppContext switch, off when not set - the templated ComboBox with
    /// Core's popup; see the class remarks). Settable in-repo (tests).
    /// </summary>
    internal static bool Enabled { get; set; } = AppContext.TryGetSwitch(NativeComboBoxSwitch, out var on) && on;

    /// <inheritdoc />
    public override ElementHandlerCapabilities Capabilities =>
        ElementHandlerCapabilities.OwnsVisuals | ElementHandlerCapabilities.MeasuresNatively | ElementHandlerCapabilities.OwnsInput;

    /// <summary>The native handler when enabled and the ComboBox has its default look, else the templated fallback.</summary>
    internal static IAndroidElementHandler Create(UIElement element) =>
        Enabled && element is ComboBox combo && NativeControlPolicy.IsNative(combo, typeof(ComboBox), new[] { "DefaultComboBoxStyle" }, out _)
            ? new ComboBoxHandler()
            : new TemplatedFallbackHandler();

    /// <summary>The text a row shows for an item.</summary>
    /// <param name="combo">The ComboBox (DisplayMemberPath, ItemTemplate).</param>
    /// <param name="item">The item.</param>
    /// <returns>The row text.</returns>
    internal static string TextOf(ItemsControl combo, object item)
    {
        switch (item)
        {
            case null:
                return string.Empty;
            case ComboBoxItem container:
                return container.Content switch
                {
                    null => string.Empty,
                    string s => s,
                    TextBlock block => block.Text ?? string.Empty,
                    var other => other.ToString(),
                };
        }

        if (!string.IsNullOrEmpty(combo.DisplayMemberPath))
        {
            return Path(item, combo.DisplayMemberPath)?.ToString() ?? string.Empty;
        }

        if (combo.ItemTemplate is { } template)
        {
            try
            {
                if (template.LoadContent() is FrameworkElement root)
                {
                    root.DataContext = item;
                    if (FirstTextBlock(root) is { } block)
                    {
                        return block.Text ?? string.Empty;
                    }
                }
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                // Fall back to the item's own text.
            }
        }

        return item.ToString();
    }

    /// <summary>Maps ItemsSource / DisplayMemberPath / ItemTemplate (the rows).</summary>
    public static void MapItems(ComboBoxHandler handler, ComboBox element)
    {
        handler.WatchItems(element);
        handler.RebuildRows(element);
        MapSelection(handler, element);
    }

    /// <summary>Maps SelectedIndex / SelectedItem (the text shown in the box).</summary>
    public static void MapSelection(ComboBoxHandler handler, ComboBox element)
    {
        var editor = handler.PlatformView.Editor;
        var index = element.SelectedIndex;
        var text = index >= 0 && index < handler._texts.Count ? handler._texts[index] : (element.IsEditable ? editor.Text : string.Empty);
        if (!string.Equals(editor.Text, text, StringComparison.Ordinal))
        {
            handler._updating = true;
            try
            {
                editor.SetText(text, false);
            }
            finally
            {
                handler._updating = false;
            }
        }
    }

    /// <summary>Maps PlaceholderText (shown while nothing is selected).</summary>
    public static void MapPlaceholder(ComboBoxHandler handler, ComboBox element) =>
        handler.PlatformView.Editor.Hint = element.PlaceholderText ?? string.Empty;

    /// <summary>Maps Header.</summary>
    public static void MapHeader(ComboBoxHandler handler, ComboBox element)
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

    /// <summary>Maps IsEditable (typeable text vs a read-only choice).</summary>
    public static void MapEditable(ComboBoxHandler handler, ComboBox element)
    {
        var editor = handler.PlatformView.Editor;
        if (element.IsEditable)
        {
            editor.InputType = AInputTypes.ClassText;
            editor.ImeOptions = AImeAction.Done;
        }
        else
        {
            editor.InputType = AInputTypes.Null;
        }

        editor.Focusable = element.IsEditable;
        editor.FocusableInTouchMode = element.IsEditable;
    }

    /// <summary>Maps IsDropDownOpen (Core opening or closing the list).</summary>
    public static void MapIsDropDownOpen(ComboBoxHandler handler, ComboBox element)
    {
        if (handler._updating)
        {
            return;
        }

        var editor = handler.PlatformView.Editor;
        if (element.IsDropDownOpen && !editor.IsPopupShowing && editor.WindowToken != null)
        {
            editor.ShowDropDown();
        }
        else if (!element.IsDropDownOpen && editor.IsPopupShowing)
        {
            editor.DismissDropDown();
        }
    }

    /// <summary>Maps IsEnabled.</summary>
    public static void MapIsEnabled(ComboBoxHandler handler, ComboBox element)
    {
        var view = handler.PlatformView;
        view.Enabled = element.IsEnabled;
        view.Field.Enabled = element.IsEnabled;
        view.Editor.Enabled = element.IsEnabled;
    }

    /// <summary>Maps the colours (ComboBox keys unless the app set its own brushes).</summary>
    public static void MapColors(ComboBoxHandler handler, ComboBox element)
    {
        var view = handler.PlatformView;
        var black = unchecked((int)0xFF000000);
        var foreground = ThemeResources.ColorOf(element.Foreground) ?? ThemeResources.FindColor(element, "ComboBoxForeground") ?? black;
        view.Editor.SetTextColor(StateColors.FromKeys(element, "ComboBoxForeground", foreground).WithNormalEverywhere(foreground).ToColorStateList());
        view.Editor.SetHintTextColor(StateColors.Single(ThemeResources.FindColor(element, "ComboBoxPlaceHolderForeground") ?? unchecked((int)0xFF808080)));
        var background = StateColors.FromKeys(element, "ComboBoxBackground", 0);
        if (ThemeResources.ColorOf(element.Background) is { } actual && actual != background.Normal)
        {
            background = background.WithNormalEverywhere(actual);
        }

        view.Field.SetBoxBackgroundColorStateList(background.ToColorStateList());
        var border = StateColors.FromKeys(element, "ComboBoxBorderBrush", unchecked((int)0xFF808080));
        if (ThemeResources.ColorOf(element.BorderBrush) is { } stroke && stroke != border.Normal)
        {
            border = border.WithNormalEverywhere(stroke);
        }

        view.Field.SetBoxStrokeColorStateList(border.ToColorStateList());
        view.Field.SetEndIconTintList(StateColors.FromKeys(element, "ComboBoxDropDownGlyphForeground", foreground).ToColorStateList());
        if (ThemeResources.FindColor(element, "ComboBoxDropDownBackground") is { } dropDown)
        {
            view.Editor.SetDropDownBackgroundDrawable(new AColorDrawable(new global::Android.Graphics.Color(dropDown)));
        }

        view.Header.SetTextColor(StateColors.FromKeys(element, "ComboBoxHeaderForeground", black).ToColorStateList());
    }

    /// <summary>Maps BorderThickness and CornerRadius (the outlined box).</summary>
    public static void MapBox(ComboBoxHandler handler, ComboBox element)
    {
        var field = handler.PlatformView.Field;
        var density = handler.Density;
        var stroke = MaterialShapes.StrokeWidth(element.BorderThickness, density);
        field.BoxStrokeWidth = stroke;
        field.BoxStrokeWidthFocused = stroke > 0 ? Math.Max(stroke, MaterialWidgets.Px(2, density)) : 0;
        var radius = element.CornerRadius;
        field.SetBoxCornerRadii(
            (float)(radius.TopLeft * density),
            (float)(radius.TopRight * density),
            (float)(radius.BottomLeft * density),
            (float)(radius.BottomRight * density));
    }

    /// <summary>Maps Padding.</summary>
    public static void MapPadding(ComboBoxHandler handler, ComboBox element)
    {
        var density = handler.Density;
        var padding = element.Padding;
        handler.PlatformView.Editor.SetPadding(
            MaterialWidgets.Px(padding.Left, density),
            MaterialWidgets.Px(padding.Top, density),
            MaterialWidgets.Px(padding.Right, density),
            MaterialWidgets.Px(padding.Bottom, density));
        element.InvalidateMeasure();
    }

    /// <summary>Maps the font.</summary>
    public static void MapFont(ComboBoxHandler handler, ComboBox element)
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

    /// <inheritdoc />
    public override Size Measure(Size availableSize) =>
        ViewHandlerExtensions.GetDesiredSizeFromView(NativeView, availableSize, Density);

    /// <inheritdoc />
    protected override ComboBoxView CreatePlatformView() =>
        new(MaterialWidgets.Material3(Context)) { HeaderGap = MaterialWidgets.Px(8, Density) };

    /// <inheritdoc />
    protected override void ConnectHandler(ComboBoxView platformView)
    {
        base.ConnectHandler(platformView);
        platformView.Editor.ItemClick += OnItemClick;
        platformView.Editor.Dismiss += OnDismiss;
        platformView.Editor.EditorAction += OnEditorAction;
        platformView.Editor.FocusChange += OnFocusChange;
        platformView.Editor.Touch += OnTouch;
        platformView.Field.Touch += OnTouch;
        if (Element != null)
        {
            _bridge.Attach(Element);
        }
    }

    /// <inheritdoc />
    protected override void DisconnectHandler(ComboBoxView platformView)
    {
        _bridge.Detach();
        WatchItems(null);
        platformView.Editor.ItemClick -= OnItemClick;
        platformView.Editor.Dismiss -= OnDismiss;
        platformView.Editor.EditorAction -= OnEditorAction;
        platformView.Editor.FocusChange -= OnFocusChange;
        platformView.Editor.Touch -= OnTouch;
        platformView.Field.Touch -= OnTouch;
        if (platformView.Editor.IsPopupShowing)
        {
            platformView.Editor.DismissDropDown();
        }

        base.DisconnectHandler(platformView);
    }

    private static object Path(object item, string path)
    {
        var current = item;
        foreach (var part in path.Split('.'))
        {
            if (current == null)
            {
                return null;
            }

            var property = current.GetType().GetProperty(part, BindingFlags.Public | BindingFlags.Instance);
            if (property == null)
            {
                return null;
            }

            current = property.GetValue(current);
        }

        return current;
    }

    private static TextBlock FirstTextBlock(DependencyObject root)
    {
        if (root is TextBlock block)
        {
            return block;
        }

        switch (root)
        {
            case Panel panel:
                foreach (var child in panel.Children)
                {
                    if (FirstTextBlock(child) is { } found)
                    {
                        return found;
                    }
                }

                break;
            case Border { Child: { } child }:
                return FirstTextBlock(child);
            case ContentControl { Content: DependencyObject content }:
                return FirstTextBlock(content);
        }

        return null;
    }

    private void WatchItems(ComboBox element)
    {
        if (_items != null)
        {
            _items.VectorChanged -= OnItemsChanged;
        }

        _items = element?.Items;
        if (_items != null)
        {
            _items.VectorChanged += OnItemsChanged;
        }
    }

    private void OnItemsChanged(IObservableVector<object> sender, IVectorChangedEventArgs e)
    {
        if (Element is ComboBox element && PlatformView != null)
        {
            RebuildRows(element);
            MapSelection(this, element);
        }
    }

    private void RebuildRows(ComboBox element)
    {
        _texts.Clear();
        foreach (var item in element.Items)
        {
            _texts.Add(TextOf(element, item));
        }

        PlatformView.Editor.SetSimpleItems(_texts.ToArray());
    }

    private void OnItemClick(object sender, global::Android.Widget.AdapterView.ItemClickEventArgs e)
    {
        if (_updating || Element is not ComboBox element)
        {
            return;
        }

        var position = e.Position;
        if (position >= 0 && position < element.Items.Count && element.SelectedIndex != position)
        {
            element.SelectedIndex = position;
        }

        MapSelection(this, element);
        SetDropDownOpen(element, false);
    }

    private void OnDismiss(object sender, EventArgs e)
    {
        if (Element is ComboBox element)
        {
            SetDropDownOpen(element, false);
        }
    }

    private void SetDropDownOpen(ComboBox element, bool open)
    {
        if (element.IsDropDownOpen == open)
        {
            return;
        }

        _updating = true;
        try
        {
            element.IsDropDownOpen = open;
        }
        finally
        {
            _updating = false;
        }
    }

    private void OnEditorAction(object sender, ATextView.EditorActionEventArgs e)
    {
        e.Handled = false;
        if (Element is ComboBox { IsEditable: true } element)
        {
            element.RaiseTextSubmittedFromPlatform(PlatformView.Editor.Text ?? string.Empty);
        }
    }

    private void OnFocusChange(object sender, AView.FocusChangeEventArgs e)
    {
        if (!e.HasFocus && Element is ComboBox { IsEditable: true } element)
        {
            element.RaiseTextSubmittedFromPlatform(PlatformView.Editor.Text ?? string.Empty);
        }
    }

    private void OnTouch(object sender, AView.TouchEventArgs e)
    {
        e.Handled = false;
        _bridge.OnNativeTouch(sender as AView, e.Event);
        if (e.Event?.ActionMasked == global::Android.Views.MotionEventActions.Up && Element is ComboBox element && element.IsEnabled)
        {
            // The exposed drop-down opens on this touch: Core's IsDropDownOpen follows.
            PlatformView.Post(() =>
            {
                if (PlatformView?.Editor is { IsPopupShowing: true })
                {
                    SetDropDownOpen(element, true);
                }
            });
        }
    }
}
