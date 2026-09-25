// Technique from .NET MAUI, src/Core/src/Handlers/SearchBar/SearchBarHandler2.Android.cs (a Material text
// field whose IME action submits the query) and src/Controls/src/Core/Compatibility/Handlers/Shell/Android/
// ShellSearchView.cs (an AutoCompleteTextView with a suggestion adapter; choosing a suggestion fills the text
// and submits) @ 828569a864. Copyright (c) .NET Foundation and Contributors. Licensed under the MIT License.
// See THIRD-PARTY-NOTICES.txt.

using System;
using System.Collections.Generic;
using CodeBrix.Android.UI.Android;
using CodeBrix.Platform.UI.Contracts;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;
using Windows.Foundation.Collections;
using AArrayAdapter = global::Android.Widget.ArrayAdapter;
using AComplexUnitType = global::Android.Util.ComplexUnitType;
using AContext = global::Android.Content.Context;
using AFilter = global::Android.Widget.Filter;
using AImeAction = global::Android.Views.InputMethods.ImeAction;
using AInputTypes = global::Android.Text.InputTypes;
using ATextInputLayout = Google.Android.Material.TextField.TextInputLayout;
using ATextView = global::Android.Widget.TextView;
using AView = global::Android.Views.View;
using AViewStates = global::Android.Views.ViewStates;
using ICharSequence = Java.Lang.ICharSequence;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>
/// The AutoSuggestBox handler (plan 3 row AutoSuggestBox): a Material text field with a suggestion list (an
/// AutoCompleteTextView whose adapter never filters - the app computes the suggestions in TextChanged and
/// sets ItemsSource, as WinUI apps do). Typing -> <c>SetTextFromPlatform(text, UserInput)</c> (TextChanged
/// with the UserInput reason); choosing a suggestion -> <c>ChoseItem</c> (SuggestionChosen, the text from
/// TextMemberPath) then <c>SubmitQueryFromPlatform(item)</c> (QuerySubmitted with ChosenSuggestion); the IME
/// action, the Enter key or the QueryIcon (end icon) -> <c>SubmitQueryFromPlatform(null)</c> (QuerySubmitted
/// with the QueryText). The suggestion list opens while there are suggestions and the box has the focus.
/// </summary>
internal sealed class AutoSuggestBoxHandler : ViewHandler<AutoSuggestBox, ComboBoxView>
{
    /// <summary>AutoSuggestBox's mapper.</summary>
    public static readonly PropertyMapper<AutoSuggestBox, AutoSuggestBoxHandler> Mapper = new(ViewMappers.ViewMapper)
    {
        [AutoSuggestBox.TextProperty] = MapText,
        [ItemsControl.ItemsSourceProperty] = MapSuggestions,
        [ItemsControl.DisplayMemberPathProperty] = MapSuggestions,
        [ItemsControl.ItemTemplateProperty] = MapSuggestions,
        [AutoSuggestBox.PlaceholderTextProperty] = MapPlaceholder,
        [AutoSuggestBox.HeaderProperty] = MapHeader,
        [AutoSuggestBox.QueryIconProperty] = MapQueryIcon,
        [AutoSuggestBox.IsSuggestionListOpenProperty] = MapIsSuggestionListOpen,
        [Control.IsEnabledProperty] = MapIsEnabled,
        [Control.ForegroundProperty] = MapColors,
        [Control.BackgroundProperty] = MapColors,
        [Control.FontFamilyProperty] = MapFont,
        [Control.FontSizeProperty] = MapFont,
    };

    private readonly CorePointerBridge _bridge;
    private readonly List<object> _items = new();
    private SuggestionAdapter _adapter;
    private IObservableVector<object> _watched;
    private bool _updating;
    private long _lastSubmit;
    private Microsoft.UI.Xaml.Input.KeyEventHandler _keyDown;

    /// <summary>Creates the handler.</summary>
    public AutoSuggestBoxHandler()
        : base(Mapper)
    {
        _bridge = new CorePointerBridge(() => NativeView);
    }

    /// <inheritdoc />
    public override ElementHandlerCapabilities Capabilities =>
        ElementHandlerCapabilities.OwnsVisuals | ElementHandlerCapabilities.MeasuresNatively | ElementHandlerCapabilities.OwnsInput;

    /// <summary>The AutoSuggestBox handler, or the templated fallback for one with a template of its own.</summary>
    internal static IAndroidElementHandler Create(UIElement element) =>
        element is AutoSuggestBox box && NativeControlPolicy.IsNative(box, typeof(AutoSuggestBox), new[] { "DefaultAutoSuggestBoxStyle" }, out _)
            ? new AutoSuggestBoxHandler()
            : new TemplatedFallbackHandler();

    /// <summary>Maps Text (Core's writes: a chosen suggestion, the app).</summary>
    public static void MapText(AutoSuggestBoxHandler handler, AutoSuggestBox element)
    {
        var editor = handler.PlatformView.Editor;
        var text = element.Text ?? string.Empty;
        if (!string.Equals(editor.Text, text, StringComparison.Ordinal))
        {
            handler._updating = true;
            try
            {
                editor.SetText(text, false);
                editor.SetSelection(editor.Length());
            }
            finally
            {
                handler._updating = false;
            }
        }
    }

    /// <summary>Maps ItemsSource (the suggestions) and their display.</summary>
    public static void MapSuggestions(AutoSuggestBoxHandler handler, AutoSuggestBox element)
    {
        handler.Watch(element);
        handler.Rebuild(element);
    }

    /// <summary>Maps PlaceholderText.</summary>
    public static void MapPlaceholder(AutoSuggestBoxHandler handler, AutoSuggestBox element) =>
        handler.PlatformView.Editor.Hint = element.PlaceholderText ?? string.Empty;

    /// <summary>Maps Header.</summary>
    public static void MapHeader(AutoSuggestBoxHandler handler, AutoSuggestBox element)
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

    /// <summary>Maps QueryIcon (the end icon, which submits the query).</summary>
    public static void MapQueryIcon(AutoSuggestBoxHandler handler, AutoSuggestBox element)
    {
        var field = handler.PlatformView.Field;
        var color = ThemeResources.ColorOf(element.Foreground) ?? unchecked((int)0xFF000000);
        if (element.QueryIcon is { } icon && IconDrawables.Create(icon, field.Context, handler.Density, color, 16) is { } drawable)
        {
            field.EndIconMode = ATextInputLayout.EndIconCustom;
            field.EndIconDrawable = drawable;
            field.SetEndIconOnClickListener(new QueryClick(handler));
        }
        else
        {
            field.EndIconMode = ATextInputLayout.EndIconNone;
        }
    }

    /// <summary>Maps IsSuggestionListOpen.</summary>
    public static void MapIsSuggestionListOpen(AutoSuggestBoxHandler handler, AutoSuggestBox element)
    {
        var editor = handler.PlatformView.Editor;
        if (element.IsSuggestionListOpen && handler._items.Count > 0 && !editor.IsPopupShowing && editor.WindowToken != null)
        {
            editor.ShowDropDown();
        }
        else if (!element.IsSuggestionListOpen && editor.IsPopupShowing)
        {
            editor.DismissDropDown();
        }
    }

    /// <summary>Maps IsEnabled.</summary>
    public static void MapIsEnabled(AutoSuggestBoxHandler handler, AutoSuggestBox element)
    {
        var view = handler.PlatformView;
        view.Enabled = element.IsEnabled;
        view.Field.Enabled = element.IsEnabled;
        view.Editor.Enabled = element.IsEnabled;
    }

    /// <summary>Maps the colours (the TextControl keys).</summary>
    public static void MapColors(AutoSuggestBoxHandler handler, AutoSuggestBox element)
    {
        var view = handler.PlatformView;
        var foreground = ThemeResources.ColorOf(element.Foreground) ?? ThemeResources.FindColor(element, "TextControlForeground") ?? unchecked((int)0xFF000000);
        view.Editor.SetTextColor(StateColors.Single(foreground));
        var background = ThemeResources.ColorOf(element.Background) ?? ThemeResources.FindColor(element, "TextControlBackground") ?? 0;
        view.Field.SetBoxBackgroundColorStateList(StateColors.Single(background));
        view.Field.SetBoxStrokeColorStateList(StateColors.Single(ThemeResources.FindColor(element, "TextControlBorderBrush") ?? unchecked((int)0xFF808080)));
    }

    /// <summary>Maps the font.</summary>
    public static void MapFont(AutoSuggestBoxHandler handler, AutoSuggestBox element)
    {
        var view = handler.PlatformView;
        if (AndroidPlatformBootstrap.Fonts?.Resolve(element.FontFamily, element.FontWeight, element.FontStyle, element.FontStretch) is { } typeface)
        {
            view.Editor.Typeface = typeface;
        }

        view.Editor.SetTextSize(AComplexUnitType.Px, (float)(element.FontSize * handler.Density));
        element.InvalidateMeasure();
    }

    /// <inheritdoc />
    public override Size Measure(Size availableSize) =>
        ViewHandlerExtensions.GetDesiredSizeFromView(NativeView, availableSize, Density);

    /// <inheritdoc />
    protected override ComboBoxView CreatePlatformView()
    {
        var view = new ComboBoxView(MaterialWidgets.Material3(Context)) { HeaderGap = MaterialWidgets.Px(8, Density) };
        view.Field.EndIconMode = ATextInputLayout.EndIconNone;
        view.Editor.InputType = AInputTypes.ClassText;
        view.Editor.ImeOptions = AImeAction.Search;
        view.Editor.Threshold = 1;
        _adapter = new SuggestionAdapter(view.Editor.Context);
        view.Editor.Adapter = _adapter;
        return view;
    }

    /// <inheritdoc />
    protected override void ConnectHandler(ComboBoxView platformView)
    {
        base.ConnectHandler(platformView);
        platformView.Editor.AfterTextChanged += OnAfterTextChanged;
        platformView.Editor.ItemClick += OnItemClick;
        platformView.Editor.EditorAction += OnEditorAction;
        platformView.Editor.FocusChange += OnFocusChange;
        platformView.Editor.Touch += OnTouch;
        platformView.Field.Touch += OnTouch;
        if (Element != null)
        {
            _bridge.Attach(Element);
            _keyDown = OnCoreKeyDown;
            Element.AddHandler(UIElement.KeyDownEvent, _keyDown, true);
        }
    }

    /// <inheritdoc />
    protected override void DisconnectHandler(ComboBoxView platformView)
    {
        _bridge.Detach();
        if (Element != null && _keyDown != null)
        {
            Element.RemoveHandler(UIElement.KeyDownEvent, _keyDown);
        }
        Watch(null);
        platformView.Editor.AfterTextChanged -= OnAfterTextChanged;
        platformView.Editor.ItemClick -= OnItemClick;
        platformView.Editor.EditorAction -= OnEditorAction;
        platformView.Editor.FocusChange -= OnFocusChange;
        platformView.Editor.Touch -= OnTouch;
        platformView.Field.Touch -= OnTouch;
        base.DisconnectHandler(platformView);
    }

    private void Watch(AutoSuggestBox element)
    {
        if (_watched != null)
        {
            _watched.VectorChanged -= OnItemsChanged;
        }

        _watched = element?.Items;
        if (_watched != null)
        {
            _watched.VectorChanged += OnItemsChanged;
        }
    }

    private void OnItemsChanged(IObservableVector<object> sender, IVectorChangedEventArgs e)
    {
        if (Element is AutoSuggestBox element && PlatformView != null)
        {
            Rebuild(element);
        }
    }

    private void Rebuild(AutoSuggestBox element)
    {
        _items.Clear();
        var texts = new List<string>();
        foreach (var item in element.Items)
        {
            _items.Add(item);
            texts.Add(ComboBoxHandler.TextOf(element, item));
        }

        _adapter?.Set(texts);
        var editor = PlatformView.Editor;
        if (_items.Count > 0 && editor.HasFocus && editor.WindowToken != null)
        {
            editor.ShowDropDown();
        }
        else if (_items.Count == 0 && editor.IsPopupShowing)
        {
            editor.DismissDropDown();
        }
    }

    private void OnAfterTextChanged(object sender, global::Android.Text.AfterTextChangedEventArgs e)
    {
        if (_updating || Element is not AutoSuggestBox element)
        {
            return;
        }

        var text = PlatformView.Editor.Text ?? string.Empty;
        if (!string.Equals(element.Text, text, StringComparison.Ordinal))
        {
            element.SetTextFromPlatform(text, AutoSuggestionBoxTextChangeReason.UserInput);
        }
    }

    private void OnItemClick(object sender, global::Android.Widget.AdapterView.ItemClickEventArgs e)
    {
        if (Element is not AutoSuggestBox element || e.Position < 0 || e.Position >= _items.Count)
        {
            return;
        }

        var item = _items[e.Position];
        _updating = true;
        try
        {
            element.ChoseItem(item);
        }
        finally
        {
            _updating = false;
        }

        MapText(this, element);
        element.SubmitQueryFromPlatform(item);
    }

    private void OnEditorAction(object sender, ATextView.EditorActionEventArgs e)
    {
        e.Handled = true;
        Submit();
    }

    private void Submit()
    {
        // The Enter key can arrive twice (Core's KeyDown and the editor's action for the same key).
        var now = global::Android.OS.SystemClock.UptimeMillis();
        if (now - _lastSubmit < 300)
        {
            return;
        }

        _lastSubmit = now;
        if (Element is AutoSuggestBox element)
        {
            PlatformView.Editor.DismissDropDown();
            element.SubmitQueryFromPlatform(null);
        }
    }

    private void OnCoreKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            Submit();
            e.Handled = true;
        }
    }

    private void OnFocusChange(object sender, AView.FocusChangeEventArgs e)
    {
        if (e.HasFocus && _bridge.IsTouchActive && Element is AutoSuggestBox { FocusState: FocusState.Unfocused } element)
        {
            element.Focus(FocusState.Pointer);
        }
    }

    private void OnTouch(object sender, AView.TouchEventArgs e)
    {
        e.Handled = false;
        _bridge.OnNativeTouch(sender as AView, e.Event);
    }

    private sealed class QueryClick : Java.Lang.Object, AView.IOnClickListener
    {
        private readonly WeakReference<AutoSuggestBoxHandler> _handler;

        internal QueryClick(AutoSuggestBoxHandler handler) => _handler = new WeakReference<AutoSuggestBoxHandler>(handler);

        public void OnClick(AView v)
        {
            if (_handler.TryGetTarget(out var handler))
            {
                handler.Submit();
            }
        }
    }

    /// <summary>The suggestion rows: the app decides what is suggested, so the adapter never filters.</summary>
    private sealed class SuggestionAdapter : AArrayAdapter
    {
        private readonly PassThroughFilter _filter;

        internal SuggestionAdapter(AContext context)
            : base(context, global::Android.Resource.Layout.SimpleDropDownItem1Line)
        {
            _filter = new PassThroughFilter(this);
        }

        public override AFilter Filter => _filter;

        internal void Set(IList<string> texts)
        {
            SetNotifyOnChange(false);
            Clear();
            foreach (var text in texts)
            {
                Add(text);
            }

            NotifyDataSetChanged();
        }

        private sealed class PassThroughFilter : AFilter
        {
            private readonly SuggestionAdapter _adapter;

            internal PassThroughFilter(SuggestionAdapter adapter) => _adapter = adapter;

            protected override FilterResults PerformFiltering(ICharSequence constraint) =>
                new() { Count = _adapter.Count };

            protected override void PublishResults(ICharSequence constraint, FilterResults results) =>
                _adapter.NotifyDataSetChanged();
        }
    }
}
