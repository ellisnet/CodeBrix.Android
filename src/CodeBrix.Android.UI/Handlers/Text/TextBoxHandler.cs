// Technique from .NET MAUI, src/Core/src/Handlers/Entry/EntryHandler2.Android.cs,
// src/Core/src/Handlers/Editor/EditorHandler2.Android.cs and src/Core/src/Platform/Android/EditTextExtensions.cs
// @ 828569a864 (TextInputLayout outlined box + TextInputEditText; text written back with a reentrancy guard and
// the cursor kept in range; MaxLength as a LengthFilter; IsReadOnly without losing focusability; the IME
// action; no fullscreen IME). Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License.
// See THIRD-PARTY-NOTICES.txt.

using System;
using CodeBrix.Android.UI.Android;
using CodeBrix.Android.UI.Hosting;
using CodeBrix.Android.UI.Input;
using CodeBrix.Android.UI.Platform;
using CodeBrix.Android.UI.Platform.Text;
using CodeBrix.Platform.UI.Contracts;
using CodeBrix.Platform.UI.Hosting;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using AColorStateList = global::Android.Content.Res.ColorStateList;
using AComplexUnitType = global::Android.Util.ComplexUnitType;
using AContext = global::Android.Content.Context;
using AGravityFlags = global::Android.Views.GravityFlags;
using AImeAction = global::Android.Views.InputMethods.ImeAction;
using AImeFlags = global::Android.Views.InputMethods.ImeFlags;
using AInputMethodManager = global::Android.Views.InputMethods.InputMethodManager;
using AInputFilter = global::Android.Text.IInputFilter;
using AInputTypes = global::Android.Text.InputTypes;
using AJustificationMode = global::Android.Text.JustificationMode;
using AKeyEvent = global::Android.Views.KeyEvent;
using AKeyEventActions = global::Android.Views.KeyEventActions;
using AKeycode = global::Android.Views.Keycode;
using ALengthFilter = global::Android.Text.InputFilterLengthFilter;
using AResource = global::Android.Resource;
using ASpanned = global::Android.Text.ISpanned;
using ASystemClock = global::Android.OS.SystemClock;
using ATextInputLayout = Google.Android.Material.TextField.TextInputLayout;
using AView = global::Android.Views.View;
using AViewStates = global::Android.Views.ViewStates;
using ICharSequence = Java.Lang.ICharSequence;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>
/// The TextBox handler (plan 3 row TextBox, 2.14): a Material TextInputLayout (outlined) around a
/// <see cref="CodeBrixEditText"/>; editing, caret, selection handles, the IME and the clipboard gestures
/// are the EDITOR's. Typed text goes to Core through <c>TextBox.ApplyTextFromPlatform</c> (BeforeTextChanging,
/// TextChanging, Text, TextChanged, SelectionChanged), whose effective text is written back only when
/// Core changed it (MaxLength, a cancelled change, single-line); Core's own writes reach the editor
/// through the TextBox platform adapter (<see cref="TextBoxAndroidPlatform"/>), which this handler
/// connects, without echo. Header -> a label above the box, PlaceholderText -> the editor's hint,
/// InputScope / AcceptsReturn / IsSpellCheckEnabled / IsTextPredictionEnabled -> the input type, the IME
/// action -> a Core KeyDown Enter, IsReadOnly -> a focusable editor that refuses edits. Colours from the
/// TextControl lightweight-styling keys (TextControlBackground*, TextControlBorderBrush*,
/// TextControlForeground*, TextControlPlaceholderForeground*) unless the app set its own brushes.
/// </summary>
internal class TextBoxHandler : ViewHandler<TextBox, TextBoxView>, INativeTextEditor
{
    /// <summary>TextBox's mapper.</summary>
    public static readonly PropertyMapper<TextBox, TextBoxHandler> Mapper = new(ViewMappers.ViewMapper)
    {
        [TextBox.TextProperty] = (h, e) => h.MapTextCore(e),
        [TextBox.PlaceholderTextProperty] = MapPlaceholder,
        [TextBox.HeaderProperty] = MapHeader,
        [TextBox.IsReadOnlyProperty] = MapEditing,
        [TextBox.AcceptsReturnProperty] = MapEditing,
        [TextBox.TextWrappingProperty] = MapEditing,
        [TextBox.InputScopeProperty] = MapEditing,
        [TextBox.IsSpellCheckEnabledProperty] = MapEditing,
        [TextBox.IsTextPredictionEnabledProperty] = MapEditing,
        [TextBox.MaxLengthProperty] = MapEditing,
        [TextBox.TextAlignmentProperty] = MapAlignment,
        [Control.VerticalContentAlignmentProperty] = MapAlignment,
        [Control.IsEnabledProperty] = MapIsEnabled,
        [Control.ForegroundProperty] = MapColors,
        [Control.BackgroundProperty] = MapColors,
        [Control.BorderBrushProperty] = MapColors,
        [TextBox.PlaceholderForegroundProperty] = MapColors,
        [TextBox.SelectionHighlightColorProperty] = MapColors,
        [FrameworkElement.StyleProperty] = MapColors,
        [Control.BorderThicknessProperty] = MapBox,
        [Control.CornerRadiusProperty] = MapBox,
        [Control.PaddingProperty] = MapPadding,
        [Control.FontFamilyProperty] = MapFont,
        [Control.FontSizeProperty] = MapFont,
        [Control.FontWeightProperty] = MapFont,
        [Control.FontStyleProperty] = MapFont,
        [Control.FontStretchProperty] = MapFont,
        [Control.CharacterSpacingProperty] = MapFont,
    };

    private readonly CorePointerBridge _bridge;
    private readonly BrushWatcher _foregroundWatcher;
    private readonly BrushWatcher _backgroundWatcher;
    private readonly BrushWatcher _borderWatcher;
    private readonly ReadOnlyFilter _readOnlyFilter = new();
    private TextBoxAndroidPlatform _adapter;
    private bool _writingText;
    private bool _selecting;
    private bool _nativeEditing;
    private bool _focusClaimPending;

    /// <summary>Creates the handler.</summary>
    /// <param name="mapper">The mapper (a subclass passes its own).</param>
    protected TextBoxHandler(IPropertyMapper mapper)
        : base(mapper)
    {
        _bridge = new CorePointerBridge(() => NativeView);
        _foregroundWatcher = new BrushWatcher(Recolor);
        _backgroundWatcher = new BrushWatcher(Recolor);
        _borderWatcher = new BrushWatcher(Recolor);
    }

    /// <summary>Creates the handler.</summary>
    public TextBoxHandler()
        : this(Mapper)
    {
    }

    /// <inheritdoc />
    public override ElementHandlerCapabilities Capabilities =>
        ElementHandlerCapabilities.OwnsVisuals | ElementHandlerCapabilities.MeasuresNatively | ElementHandlerCapabilities.OwnsInput;

    /// <summary>The native editor.</summary>
    internal CodeBrixEditText EditText => PlatformView?.Editor;

    /// <summary>AP9-3: the native editor (the focusable field) carries the automation name, not its layout.</summary>
    public override AView AccessibilityView => (AView)(NativeView as TextBoxView)?.Editor ?? NativeView;

    /// <inheritdoc />
    public int SelectionStart => EditText is { } e ? Math.Min(e.SelectionStart, e.SelectionEnd) : 0;

    /// <inheritdoc />
    public int SelectionLength => EditText is { } e ? Math.Abs(e.SelectionEnd - e.SelectionStart) : 0;

    /// <inheritdoc />
    public bool IsBackwardSelection => EditText is { } e && e.SelectionEnd < e.SelectionStart;

    /// <summary>The TextBox handler, or the templated fallback for a TextBox with a template of its own.</summary>
    internal static IAndroidElementHandler Create(UIElement element) =>
        element is TextBox box && box is not PasswordBox && NativeControlPolicy.IsNative(box, typeof(TextBox), new[] { "DefaultTextBoxStyle" }, out _)
            ? new TextBoxHandler()
            : new TemplatedFallbackHandler();

    /// <summary>Maps PlaceholderText (the editor's hint, shown while empty).</summary>
    public static void MapPlaceholder(TextBoxHandler handler, TextBox element) =>
        handler.EditText.Hint = element.PlaceholderText ?? string.Empty;

    /// <summary>Maps Header (a label above the box).</summary>
    public static void MapHeader(TextBoxHandler handler, TextBox element)
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

    /// <summary>Maps the editing behaviour: input type, single/multi-line, wrapping, MaxLength, IsReadOnly.</summary>
    public static void MapEditing(TextBoxHandler handler, TextBox element) => handler.ApplyEditing(element);

    /// <summary>Maps TextAlignment and VerticalContentAlignment (the text's gravity in the box).</summary>
    public static void MapAlignment(TextBoxHandler handler, TextBox element)
    {
        var editor = handler.EditText;
        var horizontal = element.TextAlignment switch
        {
            TextAlignment.Center => AGravityFlags.CenterHorizontal,
            TextAlignment.Right or TextAlignment.End => AGravityFlags.End,
            _ => AGravityFlags.Start,
        };
        var vertical = element.VerticalContentAlignment switch
        {
            VerticalAlignment.Center => AGravityFlags.CenterVertical,
            VerticalAlignment.Bottom => AGravityFlags.Bottom,
            _ => AGravityFlags.Top,
        };
        editor.Gravity = horizontal | vertical;
        editor.JustificationMode = element.TextAlignment == TextAlignment.Justify ? AJustificationMode.InterWord : AJustificationMode.None;
    }

    /// <summary>Maps IsEnabled.</summary>
    public static void MapIsEnabled(TextBoxHandler handler, TextBox element)
    {
        var view = handler.PlatformView;
        view.Enabled = element.IsEnabled;
        view.Field.Enabled = element.IsEnabled;
        view.Editor.Enabled = element.IsEnabled;
        view.Header.Enabled = element.IsEnabled;
    }

    /// <summary>Maps the colours.</summary>
    public static void MapColors(TextBoxHandler handler, TextBox element) => handler.Recolor();

    /// <summary>Maps BorderThickness and CornerRadius (the outlined box).</summary>
    public static void MapBox(TextBoxHandler handler, TextBox element)
    {
        var layout = handler.PlatformView.Field;
        var density = handler.Density;
        var stroke = MaterialShapes.StrokeWidth(element.BorderThickness, density);
        layout.BoxStrokeWidth = stroke;
        layout.BoxStrokeWidthFocused = stroke > 0 ? Math.Max(stroke, MaterialWidgets.Px(2, density)) : 0;
        var radius = element.CornerRadius;
        layout.SetBoxCornerRadii(
            (float)(radius.TopLeft * density),
            (float)(radius.TopRight * density),
            (float)(radius.BottomLeft * density),
            (float)(radius.BottomRight * density));
        handler.Recolor();
    }

    /// <summary>Maps Padding (around the text inside the box).</summary>
    public static void MapPadding(TextBoxHandler handler, TextBox element)
    {
        var density = handler.Density;
        var padding = element.Padding;
        var border = element.BorderThickness;
        handler.EditText.SetPadding(
            MaterialWidgets.Px(padding.Left + border.Left, density),
            MaterialWidgets.Px(padding.Top + border.Top, density),
            MaterialWidgets.Px(padding.Right + border.Right, density),
            MaterialWidgets.Px(padding.Bottom + border.Bottom, density));
        element.InvalidateMeasure();
    }

    /// <summary>Maps the font (the editor's and the header's).</summary>
    public static void MapFont(TextBoxHandler handler, TextBox element)
    {
        var view = handler.PlatformView;
        var typeface = AndroidPlatformBootstrap.Fonts?.Resolve(element.FontFamily, element.FontWeight, element.FontStyle, element.FontStretch);
        if (typeface != null)
        {
            view.Editor.Typeface = typeface;
            view.Header.Typeface = typeface;
        }

        var size = (float)(element.FontSize * handler.Density);
        view.Editor.SetTextSize(AComplexUnitType.Px, size);
        view.Header.SetTextSize(AComplexUnitType.Px, size);
        view.Editor.LetterSpacing = element.CharacterSpacing / 1000f;
        element.InvalidateMeasure();
    }

    /// <inheritdoc />
    public override Size Measure(Size availableSize) =>
        ViewHandlerExtensions.GetDesiredSizeFromView(NativeView, availableSize, Density);

    /// <inheritdoc />
    public void SetTextFromCore(string text)
    {
        var editor = EditText;
        if (editor == null || _nativeEditing)
        {
            return;
        }

        text ??= string.Empty;
        if (string.Equals(editor.Text, text, StringComparison.Ordinal))
        {
            return;
        }

        var start = editor.SelectionStart;
        var end = editor.SelectionEnd;
        _writingText = true;
        _readOnlyFilter.Suspended = true;
        try
        {
            editor.Text = text;
            var length = editor.Length();
            editor.SetSelection(Math.Clamp(start, 0, length), Math.Clamp(end, 0, length));
        }
        finally
        {
            _readOnlyFilter.Suspended = false;
            _writingText = false;
        }
    }

    /// <inheritdoc />
    public void Select(int start, int length)
    {
        var editor = EditText;
        if (editor == null)
        {
            return;
        }

        var textLength = editor.Length();
        var from = Math.Clamp(start, 0, textLength);
        var to = Math.Clamp(start + Math.Max(0, length), from, textLength);
        if (editor.SelectionStart == from && editor.SelectionEnd == to)
        {
            return;
        }

        _selecting = true;
        try
        {
            editor.SetSelection(from, to);
        }
        finally
        {
            _selecting = false;
        }
    }

    /// <inheritdoc />
    public void OnCoreFocusStateChanged(FocusState state)
    {
        var editor = EditText;
        if (editor == null)
        {
            return;
        }

        if (state != FocusState.Unfocused)
        {
            ClaimNativeFocus(editor, state);

            if (state == FocusState.Keyboard || state == FocusState.Programmatic)
            {
                // WinUI shows no touch keyboard for programmatic or keyboard focus (a page's initial focus);
                // Android may still raise the IME for a newly focused editor when the window gains focus.
                editor.Post(() =>
                {
                    if (Element is TextBox { FocusState: FocusState.Keyboard or FocusState.Programmatic })
                    {
                        HideSoftInput(editor);
                    }
                });
                return;
            }

            if (!_bridge.IsTouchActive)
            {
                // [AP8-S batch 2] Pointer focus that did not come from a finger on THIS box - Core moved the focus on
                // (the focused element was collapsed: WinUI keeps the pointer focus state for the next element) - raises
                // no touch keyboard, as on WinUI; a finger on the box raises it (OnTouch). a pasted app: the
                // viewer's Back button collapsed and the keyboard came up for the search box.
                return;
            }

            ShowSoftInput(editor);
            return;
        }

        SetFocusClaimPending(editor, false);
        if (editor.HasFocus)
        {
            HideSoftInput(editor);
            ParkFocus(editor);
        }
    }

    /// <inheritdoc />
    public void Undo() => EditText?.OnTextContextMenuItem(AResource.Id.Undo);

    /// <inheritdoc />
    public void Redo() => EditText?.OnTextContextMenuItem(AResource.Id.Redo);

    /// <inheritdoc />
    public void RefreshFromCore()
    {
        if (Element is TextBox element && PlatformView != null)
        {
            ApplyEditing(element);
            MapAlignment(this, element);
            MapFont(this, element);
            Recolor();
        }
    }

    /// <summary>Maps Text (a PasswordBox maps Password instead).</summary>
    /// <param name="element">The element.</param>
    protected virtual void MapTextCore(TextBox element) => SetTextFromCore(element.Text);

    /// <summary>Hands the editor's text to Core; returns Core's effective text.</summary>
    /// <param name="element">The element.</param>
    /// <param name="text">The editor's text.</param>
    /// <param name="selectionStart">The selection start.</param>
    /// <param name="selectionLength">The selection length.</param>
    /// <returns>The effective text.</returns>
    protected virtual string ApplyToCore(TextBox element, string text, int selectionStart, int selectionLength) =>
        element.ApplyTextFromPlatform(text, selectionStart, selectionLength) ?? text;

    /// <summary>The input type of the editor.</summary>
    /// <param name="element">The element.</param>
    /// <returns>The input type.</returns>
    protected virtual AInputTypes InputType(TextBox element) =>
        // A read-only box shows no spelling marks (nothing can be corrected in it).
        InputScopeMapping.ForTextBox(element.InputScope, element.AcceptsReturn, element.IsSpellCheckEnabled && !element.IsReadOnly, element.IsTextPredictionEnabled && !element.IsReadOnly);

    /// <summary>True when the text is shown on several lines (AcceptsReturn, or wrapping).</summary>
    /// <param name="element">The element.</param>
    /// <returns>True for multi-line display.</returns>
    protected virtual bool IsMultiLine(TextBox element) => element.AcceptsReturn || element.TextWrapping != TextWrapping.NoWrap;

    /// <summary>Applies the display transformation (none for a TextBox; the mask for a PasswordBox).</summary>
    /// <param name="element">The element.</param>
    protected virtual void ApplyTransformation(TextBox element)
    {
    }

    /// <summary>Called after the editor's text changed (the PasswordBox shows its reveal button).</summary>
    protected virtual void OnEditorTextChanged()
    {
    }

    /// <summary>The end icon of the field (a clear button for a single-line TextBox, as the Fluent DeleteButton).</summary>
    /// <param name="element">The element.</param>
    protected virtual void ApplyEndIcon(TextBox element) =>
        PlatformView.Field.EndIconMode = !element.AcceptsReturn && !element.IsReadOnly
            ? ATextInputLayout.EndIconClearText
            : ATextInputLayout.EndIconNone;

    /// <inheritdoc />
    protected override TextBoxView CreatePlatformView()
    {
        var view = new TextBoxView(MaterialWidgets.Material3(Context))
        {
            HeaderGap = MaterialWidgets.Px(8, Density),
        };
        view.Editor.ImeOptions = AImeAction.Done | (AImeAction)AImeFlags.NoExtractUi;
        view.Editor.SaveEnabled = false;
        return view;
    }

    /// <inheritdoc />
    protected override void ConnectHandler(TextBoxView platformView)
    {
        base.ConnectHandler(platformView);
        var editor = platformView.Editor;
        editor.AfterTextChanged += OnAfterTextChanged;
        editor.BeforeTextChanged += OnBeforeTextChanged;
        editor.FocusChange += OnFocusChange;
        editor.EditorAction += OnEditorAction;
        editor.Touch += OnTouch;
        platformView.Field.Touch += OnTouch;
        editor.SelectionChangedCallback = OnSelectionChanged;
        editor.ContextMenuItemCallback = OnContextMenuItem;
        editor.WindowFocusChangedCallback = OnWindowFocusChanged;
        if (Element is TextBox element)
        {
            _bridge.Attach(element);
            _adapter = TextBoxAndroidPlatform.Of(element);
            _adapter?.Attach(this);
            MapTextCore(element);
        }
    }

    /// <inheritdoc />
    protected override void DisconnectHandler(TextBoxView platformView)
    {
        _bridge.Detach();
        _adapter?.Attach(null);
        _adapter = null;
        var editor = platformView.Editor;
        editor.AfterTextChanged -= OnAfterTextChanged;
        editor.BeforeTextChanged -= OnBeforeTextChanged;
        editor.FocusChange -= OnFocusChange;
        SetFocusClaimPending(editor, false);
        editor.EditorAction -= OnEditorAction;
        editor.Touch -= OnTouch;
        platformView.Field.Touch -= OnTouch;
        editor.SelectionChangedCallback = null;
        editor.ContextMenuItemCallback = null;
        editor.WindowFocusChangedCallback = null;
        _foregroundWatcher.Clear();
        _backgroundWatcher.Clear();
        _borderWatcher.Clear();
        if (editor.HasFocus)
        {
            HideSoftInput(editor);
        }

        base.DisconnectHandler(platformView);
    }

    private void ApplyEditing(TextBox element)
    {
        // AP1.9 FIX: re-configuring the editor re-sets its text (SetSingleLine / SetTransformationMethod call
        // TextView.setText(getText())), and TextView.setText runs the input filters with an EMPTY destination - the
        // active read-only filter then replaced the whole text with "" and the handler handed that to Core. It
        // showed once Core-injected taps reached native TextBoxes (the C0 hit-test fix, pin 1.0.268.12): a tap on a
        // read-only box re-mapped its editing and cleared it. The handler's own re-configuration is its own write.
        var suspended = _readOnlyFilter.Suspended;
        var writing = _writingText;
        _readOnlyFilter.Suspended = true;
        _writingText = true;
        try
        {
            ApplyEditingCore(element);
        }
        finally
        {
            _readOnlyFilter.Suspended = suspended;
            _writingText = writing;
        }
    }

    private void ApplyEditingCore(TextBox element)
    {
        var editor = EditText;
        var multiLine = IsMultiLine(element);
        var start = editor.SelectionStart;
        var end = editor.SelectionEnd;
        editor.SetSingleLine(!multiLine);
        if (multiLine)
        {
            editor.SetMaxLines(int.MaxValue);
            editor.SetHorizontallyScrolling(element.TextWrapping == TextWrapping.NoWrap);
        }

        editor.SetRawInputType(InputType(element));
        ApplyTransformation(element);
        var single = !element.AcceptsReturn;
        var action = InputScopeMapping.FirstName(element.InputScope) == InputScopeNameValue.Search ? AImeAction.Search : AImeAction.Done;
        editor.ImeOptions = (single ? action : AImeAction.None) | (AImeAction)AImeFlags.NoExtractUi;

        _readOnlyFilter.Active = element.IsReadOnly;
        editor.SetFilters(element.MaxLength > 0
            ? new AInputFilter[] { _readOnlyFilter, new ALengthFilter(element.MaxLength) }
            : new AInputFilter[] { _readOnlyFilter });
        // The soft keyboard is shown by this handler for pointer focus only (WinUI shows no touch keyboard for
        // programmatic or keyboard focus, e.g. a page's initial focus).
        editor.ShowSoftInputOnFocus = false;
        editor.SetCursorVisible(!element.IsReadOnly);
        ApplyEndIcon(element);

        // The end-icon extras of the re-keyed TextControl family (presentation policy, AP5) follow the icon mode.
        Policy.ThemeKeyAppliers.TextControl(this, element);
        var length = editor.Length();
        editor.SetSelection(Math.Clamp(Math.Min(start, end), 0, length), Math.Clamp(Math.Max(start, end), 0, length));
        element.InvalidateMeasure();
    }

    private void Recolor()
    {
        if (Element is not TextBox element || PlatformView is not { } view)
        {
            return;
        }

        _foregroundWatcher.Watch(element.Foreground);
        _backgroundWatcher.Watch(element.Background);
        _borderWatcher.Watch(element.BorderBrush);
        var black = unchecked((int)0xFF000000);
        view.Editor.SetTextColor(Focusable(element, "TextControlForeground", element.Foreground, black));
        view.Editor.SetHintTextColor(Focusable(element, "TextControlPlaceholderForeground", element.PlaceholderForeground, unchecked((int)0xFF808080)));
        view.Field.SetBoxBackgroundColorStateList(Focusable(element, "TextControlBackground", element.Background, 0));
        view.Field.SetBoxStrokeColorStateList(Focusable(element, "TextControlBorderBrush", element.BorderBrush, unchecked((int)0xFF808080)));
        view.Header.SetTextColor(StateColors.FromKeys(element, "TextControlHeaderForeground", black).ToColorStateList());
        if (ThemeResources.ColorOf(element.SelectionHighlightColor) is { } highlight)
        {
            view.Editor.SetHighlightColor(new global::Android.Graphics.Color(highlight));
        }

        if (ThemeResources.ColorOf(element.Foreground) is { } foreground)
        {
            view.Field.SetEndIconTintList(StateColors.Single(foreground));
        }

        // The re-keyed TextControl family's extras (presentation policy, AP5): end-icon button, focus line.
        Policy.ThemeKeyAppliers.TextControl(this, element);
    }

    /// <summary>
    /// The colour list of a TextControl key family (Normal, PointerOver, Focused, Disabled); an app brush
    /// that differs from the key's value wins in every state but Disabled.
    /// </summary>
    private static AColorStateList Focusable(Control element, string key, Brush brush, int fallback)
    {
        var normal = ThemeResources.FindColor(element, key) ?? fallback;
        var pointerOver = ThemeResources.FindColor(element, key + "PointerOver") ?? normal;
        var focused = ThemeResources.FindColor(element, key + "Focused") ?? normal;
        var disabled = ThemeResources.FindColor(element, key + "Disabled") ?? normal;
        if (ThemeResources.ColorOf(brush) is { } actual && actual != normal)
        {
            normal = pointerOver = focused = actual;
        }

        var enabled = AResource.Attribute.StateEnabled;
        return new AColorStateList(
            new[] { new[] { -enabled }, new[] { AResource.Attribute.StateFocused }, new[] { AResource.Attribute.StateHovered }, Array.Empty<int>() },
            new[] { disabled, focused, pointerOver, normal });
    }

    private void OnBeforeTextChanged(object sender, global::Android.Text.TextChangedEventArgs e)
    {
        if (!_writingText)
        {
            _nativeEditing = true;
        }
    }

    private void OnAfterTextChanged(object sender, global::Android.Text.AfterTextChangedEventArgs e)
    {
        if (_writingText || Element is not TextBox element || EditText is not { } editor)
        {
            _nativeEditing = false;
            return;
        }

        try
        {
            var text = editor.Text ?? string.Empty;
            var effective = ApplyToCore(element, text, SelectionStart, SelectionLength);
            _nativeEditing = false;
            if (!string.Equals(effective, text, StringComparison.Ordinal))
            {
                SetTextFromCore(effective);
            }
        }
        finally
        {
            _nativeEditing = false;
        }

        OnEditorTextChanged();
    }

    private void OnSelectionChanged(int start, int end)
    {
        if (_selecting || _writingText || _nativeEditing || Element is not TextBox element)
        {
            return;
        }

        var from = Math.Min(start, end);
        var length = Math.Abs(end - start);
        var text = element.Text ?? string.Empty;
        if (from + length > text.Length)
        {
            return;
        }

        if (element.SelectionStart != from || element.SelectionLength != length)
        {
            _selecting = true;
            try
            {
                element.Select(from, length);
            }
            finally
            {
                _selecting = false;
            }
        }
    }

    /// <summary>
    /// [AP10-G] Gives the editor the Android focus Core gave the box, so that a hardware keyboard's keys go into it
    /// (<see cref="NativeFocusClaim"/>). Android refuses the focus of a view that has no size yet - the editor of a page
    /// that has just arrived by Frame.Navigate, whose first focus Core gives while the page's views are still 0 x 0 - so a
    /// refused request is repeated after the editor's next layout. No soft keyboard here (AP9-4: a finger asks for it).
    /// </summary>
    private void ClaimNativeFocus(AView editor, FocusState state)
    {
        if (NativeFocusClaim.OnCoreFocus(state, editor.HasFocus) != NativeFocusClaimAction.RequestNow)
        {
            SetFocusClaimPending(editor, false);
            return;
        }

        var granted = editor.RequestFocus() && editor.HasFocus;
        SetFocusClaimPending(editor, NativeFocusClaim.AfterRequest(granted) == NativeFocusClaimAction.RetryAfterLayout);
    }

    /// <summary>
    /// Arms or disarms the retry. The layout listener is attached only while a retry is pending: an editor that never
    /// needs one keeps no listener (a permanent one measurably moved a TextBox frame of the AndroidSoftInput group by a
    /// sub-pixel).
    /// </summary>
    private void SetFocusClaimPending(AView editor, bool pending)
    {
        if (pending == _focusClaimPending)
        {
            return;
        }

        _focusClaimPending = pending;
        if (pending)
        {
            editor.LayoutChange += OnEditorLayoutChange;
        }
        else
        {
            editor.LayoutChange -= OnEditorLayoutChange;
        }
    }

    private void OnEditorLayoutChange(object sender, AView.LayoutChangeEventArgs e)
    {
        if (!_focusClaimPending || EditText is not { } editor || Element is not TextBox element)
        {
            return;
        }

        if (NativeFocusClaim.OnLayout(_focusClaimPending, element.FocusState, editor.HasFocus) != NativeFocusClaimAction.RequestNow)
        {
            SetFocusClaimPending(editor, false);
            return;
        }

        // Asked after the layout pass that gave the editor its size (a focus change inside the pass is deferred by Android).
        editor.Post(() =>
        {
            if (_focusClaimPending && ReferenceEquals(EditText, editor) && Element is TextBox box)
            {
                ClaimNativeFocus(editor, box.FocusState);
            }
        });
    }

    private void OnWindowFocusChanged(bool hasWindowFocus)
    {
        // The window gaining focus may raise the IME for the focused editor on its own: keep it down unless
        // the focus came from a pointer (WinUI shows the touch keyboard for touch focus only).
        if (hasWindowFocus && EditText is { HasFocus: true } editor
            && Element is TextBox { FocusState: FocusState.Keyboard or FocusState.Programmatic })
        {
            editor.PostDelayed(() => HideSoftInput(editor), 100);
        }
    }

    private bool OnContextMenuItem(int id)
    {
        if ((id == AResource.Id.Paste || id == AResource.Id.PasteAsPlainText) && Element is TextBox element)
        {
            return element.RaisePasteFromPlatform();
        }

        return false;
    }

    private void OnFocusChange(object sender, AView.FocusChangeEventArgs e)
    {
        if (e.HasFocus && Element is TextBox { FocusState: FocusState.Unfocused } element && EditText is { } editor)
        {
            if (_bridge.IsTouchActive)
            {
                // A finger put the caret here: Core's focus follows (touch focus).
                element.Focus(FocusState.Pointer);
            }
            else
            {
                // Android gave the editor focus on its own (the window's first focusable editor): Core owns
                // focus, so the editor gives it back.
                editor.Post(() =>
                {
                    if (Element is TextBox { FocusState: FocusState.Unfocused } && editor.HasFocus)
                    {
                        HideSoftInput(editor);
                        ParkFocus(editor);
                    }
                });
            }
        }

        OnEditorTextChanged();
    }

    private void OnEditorAction(object sender, global::Android.Widget.TextView.EditorActionEventArgs e)
    {
        e.Handled = false;
        if (e.ActionId == AImeAction.ImeNull || Element?.XamlRoot is not { } root)
        {
            return;
        }

        // The soft keyboard's action key is the Enter key of WinUI apps (KeyDown checks VirtualKey.Enter).
        if (XamlRootMap.GetHostForRoot(root) is AndroidXamlRootHost { KeyboardSource: { } keyboard })
        {
            var now = ASystemClock.UptimeMillis();
            using var down = new AKeyEvent(now, now, AKeyEventActions.Down, AKeycode.Enter, 0);
            using var up = new AKeyEvent(now, now, AKeyEventActions.Up, AKeycode.Enter, 0);
            var handled = keyboard.OnNativeKeyEvent(down);
            keyboard.OnNativeKeyEvent(up);
            e.Handled = handled;
        }
    }

    private void OnTouch(object sender, AView.TouchEventArgs e)
    {
        e.Handled = false;
        _bridge.OnNativeTouch(sender as AView, e.Event);
        if (e.Event?.ActionMasked == global::Android.Views.MotionEventActions.Up && EditText is { } editor && Element is TextBox { IsReadOnly: false })
        {
            // A finger on the box asks for the soft keyboard, whether or not the box already had the focus.
            editor.Post(() =>
            {
                if (editor.HasFocus)
                {
                    ShowSoftInput(editor);
                }
            });
        }
    }

    private static void ShowSoftInput(AView editor)
    {
        if (editor.Context?.GetSystemService(AContext.InputMethodService) is AInputMethodManager manager)
        {
            manager.ShowSoftInput(editor, global::Android.Views.InputMethods.ShowFlags.Implicit);
        }
    }

    private static void HideSoftInput(AView editor)
    {
        if (editor.Context?.GetSystemService(AContext.InputMethodService) is AInputMethodManager manager)
        {
            manager.HideSoftInputFromWindow(editor.WindowToken, global::Android.Views.InputMethods.HideSoftInputFlags.None);
        }
    }

    /// <summary>
    /// Takes native focus away from the editor without Android handing it to the first other editor of
    /// the window (what View.clearFocus does in touch mode): the window's root element view takes it.
    /// </summary>
    private static void ParkFocus(AView editor)
    {
        AView root = null;
        for (var parent = editor.Parent; parent is AView view; parent = view.Parent)
        {
            if (view is CodeBrixViewGroup)
            {
                root = view;
            }
        }

        if (root != null)
        {
            // Local workaround (COORDINATOR CHANGE requested): the root view group should be focusable in touch mode.
            root.Focusable = true;
            root.FocusableInTouchMode = true;

            // Out of touch mode Android would draw its default focus highlight (translucent grey) over the whole window
            // view group while it holds the parked focus (AP7-B TerminalView fix, FIXLIST).
            root.DefaultFocusHighlightEnabled = false;
            if (root.RequestFocus())
            {
                return;
            }
        }

        editor.ClearFocus();
    }

    /// <summary>
    /// Refuses every edit while <see cref="Active"/> (IsReadOnly) - the editor stays focusable and
    /// selectable - except the text the handler itself writes (<see cref="Suspended"/>).
    /// </summary>
    private sealed class ReadOnlyFilter : Java.Lang.Object, AInputFilter
    {
        internal bool Active { get; set; }

        internal bool Suspended { get; set; }

        public ICharSequence FilterFormatted(ICharSequence source, int start, int end, ASpanned dest, int dstart, int dend) =>
            Active && !Suspended ? dest?.SubSequenceFormatted(dstart, dend) : null;
    }
}
