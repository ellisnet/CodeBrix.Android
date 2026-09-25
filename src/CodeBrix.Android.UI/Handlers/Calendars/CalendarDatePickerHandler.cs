using System;
using CodeBrix.Android.UI.Hosting;
using CodeBrix.Android.UI.Policy;
using CodeBrix.Platform.UI.Contracts;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;
using Windows.Globalization.DateTimeFormatting;
using AMaterialDatePicker = Google.Android.Material.DatePicker.MaterialDatePicker;
using ATextInputEditText = Google.Android.Material.TextField.TextInputEditText;
using ATextInputLayout = Google.Android.Material.TextField.TextInputLayout;
using ALinearLayoutParams = global::Android.Widget.LinearLayout.LayoutParams;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>
/// AP10-A: the handler of CalendarDatePicker (tsv row CalendarDatePicker: "TextInputLayout + MaterialDatePicker").
/// An outlined Material text field (read-only) shows the Date formatted by DateFormat (Core's formatter; the short
/// date when empty) or the PlaceholderText, with Header as its label and Description as its helper text and a
/// calendar end icon; a finger on it opens a MaterialDatePicker (calendar mode; text input first in an Expanded
/// window or with a fine pointer, adaptive table row 10) bounded by MinDate / MaxDate and opened at the Date; OK sets
/// Date (Core raises DateChanged). IsCalendarOpen follows the dialog both ways, and the dialog's show / dismiss raise
/// Opened / Closed through the seam's entry points (CalendarDatePicker.RaiseOpenedFromPlatform /
/// RaiseClosedFromPlatform, pin 1.0.268.12). Not raised: CalendarViewDayItemChanging (NotImplemented in the Platform).
/// </summary>
internal sealed class CalendarDatePickerHandler : ViewHandler<CalendarDatePicker, ATextInputLayout>
{
    /// <summary>CalendarDatePicker's mapper.</summary>
    public static readonly PropertyMapper<CalendarDatePicker, CalendarDatePickerHandler> Mapper = new(ViewMappers.ViewMapper)
    {
        [CalendarDatePicker.DateProperty] = MapText,
        [CalendarDatePicker.DateFormatProperty] = MapText,
        [CalendarDatePicker.PlaceholderTextProperty] = MapText,
        [CalendarDatePicker.HeaderProperty] = MapText,
        [CalendarDatePicker.DescriptionProperty] = MapText,
        [CalendarDatePicker.IsCalendarOpenProperty] = MapIsCalendarOpen,
        [Control.IsEnabledProperty] = MapEnabled,
    };

    private ATextInputEditText _editor;
    private AMaterialDatePicker _dialog;
    private bool _syncing;

    /// <summary>Creates the handler.</summary>
    public CalendarDatePickerHandler()
        : base(Mapper)
    {
    }

    /// <summary>Raised when a dialog was shown (tests and diagnostics).</summary>
    internal static event EventHandler<CalendarDatePickerHandler> DialogShown;

    /// <inheritdoc />
    public override ElementHandlerCapabilities Capabilities =>
        ElementHandlerCapabilities.OwnsVisuals | ElementHandlerCapabilities.MeasuresNatively | ElementHandlerCapabilities.OwnsInput;

    /// <summary>The Material date picker while it is shown.</summary>
    internal AMaterialDatePicker Dialog => _dialog;

    /// <summary>The field's text.</summary>
    internal string Text => _editor?.Text ?? string.Empty;

    /// <summary>The native handler, or the templated fallback for a re-templated picker.</summary>
    /// <param name="element">The CalendarDatePicker.</param>
    /// <returns>The handler.</returns>
    internal static IAndroidElementHandler Create(UIElement element) =>
        element is CalendarDatePicker picker && NativeControlPolicy.IsNative(picker, typeof(CalendarDatePicker), Array.Empty<string>(), out _)
            ? new CalendarDatePickerHandler()
            : new TemplatedFallbackHandler();

    /// <summary>Maps Date / DateFormat / PlaceholderText / Header / Description.</summary>
    public static void MapText(CalendarDatePickerHandler handler, CalendarDatePicker element)
    {
        var layout = handler.PlatformView;
        var header = element.Header?.ToString();
        layout.Hint = string.IsNullOrEmpty(header) ? element.PlaceholderText : header;
        layout.PlaceholderText = string.IsNullOrEmpty(header) ? null : element.PlaceholderText;
        layout.HelperText = element.Description?.ToString();
        layout.HelperTextEnabled = element.Description != null;
        if (handler._editor != null)
        {
            handler._editor.Text = Format(element);
        }

        element.InvalidateMeasure();
    }

    /// <summary>Maps IsCalendarOpen: true opens the Material date picker, false closes it.</summary>
    public static void MapIsCalendarOpen(CalendarDatePickerHandler handler, CalendarDatePicker element)
    {
        if (handler._syncing)
        {
            return;
        }

        if (element.IsCalendarOpen && handler._dialog == null)
        {
            handler.PlatformView.Post(handler.Open);
        }
        else if (!element.IsCalendarOpen && handler._dialog is { IsAdded: true } dialog)
        {
            dialog.DismissAllowingStateLoss();
        }
    }

    /// <summary>Maps IsEnabled.</summary>
    public static void MapEnabled(CalendarDatePickerHandler handler, CalendarDatePicker element)
    {
        handler.PlatformView.Enabled = element.IsEnabled;
        if (handler._editor != null)
        {
            handler._editor.Enabled = element.IsEnabled;
        }
    }

    /// <summary>The text the field shows for the picker's Date (empty without a Date).</summary>
    /// <param name="picker">The picker.</param>
    /// <returns>The text.</returns>
    internal static string Format(CalendarDatePicker picker)
    {
        if (picker.Date is not { } date)
        {
            return string.Empty;
        }

        try
        {
            var formatter = string.IsNullOrEmpty(picker.DateFormat) ? DateTimeFormatter.ShortDate : new DateTimeFormatter(picker.DateFormat);
            return formatter.Format(date);
        }
        catch (Exception)
        {
            return date.ToString("d", System.Globalization.CultureInfo.CurrentCulture);
        }
    }

    /// <summary>A day chosen in the dialog (UTC-midnight milliseconds) becomes Date, as the dialog's OK does.</summary>
    /// <param name="utcMillis">The selection.</param>
    internal void Pick(long utcMillis)
    {
        if (Element is CalendarDatePicker picker)
        {
            picker.Date = CalendarMath.PickedMillis(utcMillis, picker.Date);
        }
    }

    /// <summary>Opens the Material date picker (a finger on the field does the same).</summary>
    internal void Open()
    {
        if (_dialog != null || Element is not CalendarDatePicker picker || !picker.IsEnabled || ActivityRegistry.Current is not { } activity)
        {
            return;
        }

        var date = picker.Date ?? DateTimeOffset.Now;
        var constraints = new Google.Android.Material.DatePicker.CalendarConstraints.Builder()
            .SetStart(CalendarMath.DayMillis(picker.MinDate))
            .SetEnd(CalendarMath.DayMillis(picker.MaxDate))
            .SetOpenAt(CalendarMath.DayMillis(date))
            .Build();
        var builder = AMaterialDatePicker.Builder.DatePicker()
            .SetCalendarConstraints(constraints)
            .SetInputMode(MaterialPickers.TextInputFirst() ? AMaterialDatePicker.InputModeText : AMaterialDatePicker.InputModeCalendar);
        if (picker.Date is { } current)
        {
            builder.SetSelection(Java.Lang.Long.ValueOf(CalendarMath.DayMillis(current)));
        }

        if (picker.Header?.ToString() is { Length: > 0 } title)
        {
            builder.SetTitleText(title);
        }

        _dialog = builder.Build();
        _dialog.AddOnPositiveButtonClickListener(new PositiveListener(this));
        _dialog.AddOnDismissListener(new DismissListener(OnDismissed));
        _dialog.Show(activity.SupportFragmentManager, "CodeBrix.CalendarDatePicker");
        SetOpen(true);
        DialogShown?.Invoke(null, this);
    }

    /// <inheritdoc />
    public override Size Measure(Size availableSize) => ViewHandlerExtensions.GetDesiredSizeFromView(PlatformView, availableSize, Density);

    /// <inheritdoc />
    protected override ATextInputLayout CreatePlatformView()
    {
        var context = MaterialWidgets.Material3(Context);
        var style = MaterialWidgets.AttrId(context, "textInputOutlinedStyle");
        var layout = style != 0 ? new ATextInputLayout(context, null, style) : new ATextInputLayout(context);
        _editor = new ATextInputEditText(layout.Context)
        {
            Focusable = false,
            FocusableInTouchMode = false,
            Clickable = true,
            LongClickable = false,
        };
        _editor.SetCursorVisible(false);
        _editor.SetSingleLine(true);
        layout.AddView(_editor, new ALinearLayoutParams(ALinearLayoutParams.MatchParent, ALinearLayoutParams.WrapContent));
        layout.EndIconMode = ATextInputLayout.EndIconCustom;
        var color = PagingWidgets.Role(context, "colorOnSurfaceVariant", unchecked((int)0xFF49454F));
        layout.EndIconDrawable = IconDrawables.Create(new FontIconSource { Glyph = "" }, context, Density, color, 18);
        layout.EndIconContentDescription = "Open calendar";
        return layout;
    }

    /// <inheritdoc />
    protected override void ConnectHandler(ATextInputLayout platformView)
    {
        base.ConnectHandler(platformView);
        _editor.Click += OnFieldClick;
        platformView.SetEndIconOnClickListener(new ClickListener(Open));
    }

    /// <inheritdoc />
    protected override void DisconnectHandler(ATextInputLayout platformView)
    {
        _editor.Click -= OnFieldClick;
        platformView.SetEndIconOnClickListener(null);
        if (_dialog is { IsAdded: true } dialog)
        {
            dialog.DismissAllowingStateLoss();
        }

        _dialog = null;
        base.DisconnectHandler(platformView);
    }

    private void OnFieldClick(object sender, EventArgs e) => Open();

    private void OnDismissed()
    {
        _dialog = null;
        MaterialPickers.HideKeyboardAfterDialog();
        SetOpen(false);
    }

    private void SetOpen(bool open)
    {
        if (Element is not CalendarDatePicker picker)
        {
            return;
        }

        // The entry points set IsCalendarOpen and raise Opened / Closed (the flyout's path on the Skia heads).
        _syncing = true;
        try
        {
            if (open)
            {
                picker.RaiseOpenedFromPlatform();
            }
            else
            {
                picker.RaiseClosedFromPlatform();
            }
        }
        finally
        {
            _syncing = false;
        }
    }

    private sealed class PositiveListener : Java.Lang.Object, Google.Android.Material.DatePicker.IMaterialPickerOnPositiveButtonClickListener
    {
        private readonly WeakReference<CalendarDatePickerHandler> _owner;

        internal PositiveListener(CalendarDatePickerHandler owner) => _owner = new WeakReference<CalendarDatePickerHandler>(owner);

        public void OnPositiveButtonClick(Java.Lang.Object selection)
        {
            if (_owner.TryGetTarget(out var owner) && selection is Java.Lang.Long millis)
            {
                owner.Pick(millis.LongValue());
            }
        }
    }

    private sealed class ClickListener : Java.Lang.Object, global::Android.Views.View.IOnClickListener
    {
        private readonly Action _action;

        internal ClickListener(Action action) => _action = action;

        public void OnClick(global::Android.Views.View view) => _action();
    }
}
