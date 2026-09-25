// Technique derived from the upstream open-source XAML platform of CodeBrix.Platform (see THIRD-PARTY-NOTICES.txt, item 2),
// src/{U}.UI/UI/Xaml/Controls/DatePicker/NativeDatePickerFlyout.Android.cs, src/{U}.UI/UI/Xaml/Controls/TimePicker/
// NativeTimePickerFlyout.Android.cs and src/{U}.UI.Runtime.Skia.Android/UI/Xaml/Controls/{DatePicker,TimePicker}/
// AndroidSkia{Date,Time}PickerProvider.cs @ tag 6.6.166 (a picker flyout whose Open shows the platform dialog; the
// providers Core asks for). Rewritten over Material Components' MaterialDatePicker / MaterialTimePicker.
// Licensed under the Apache License, Version 2.0. See THIRD-PARTY-NOTICES.txt.

using System;
using CodeBrix.Android.UI.Hosting;
using CodeBrix.Android.UI.Overlay;
using CodeBrix.Android.UI.Policy;
using CodeBrix.Platform.Foundation.Extensibility;
using Microsoft.UI.Xaml.Controls;
using AMaterialDatePicker = Google.Android.Material.DatePicker.MaterialDatePicker;
using AMaterialTimePicker = Google.Android.Material.TimePicker.MaterialTimePicker;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>
/// The Material pickers of DatePicker and TimePicker (plan 2.3 row ISkiaNativeDatePickerProviderExtension /
/// ISkiaNativeTimePickerProviderExtension; adaptive table row 10): Core's DatePicker and TimePicker ask a registered
/// provider for their picker flyout when UseNativeStyle is on (its default on Android); these providers return
/// flyouts whose Open shows a MaterialDatePicker (calendar) / MaterialTimePicker (clock) dialog - text / keyboard
/// input first in the Expanded size class or with a fine pointer (adaptive table row 10) - and report the choice
/// back through Core's DatePicked / TimePicked path (the control's Date / Time, DateChanged / TimeChanged). The
/// control itself keeps its Fluent template (the date / time button).
/// </summary>
internal static class MaterialPickers
{
    private static readonly object _gate = new();
    private static bool _registered;

    /// <summary>Registers the two providers with Core (once).</summary>
    internal static void EnsureRegistered()
    {
        lock (_gate)
        {
            if (_registered)
            {
                return;
            }

            ApiExtensibility.Register(typeof(ISkiaNativeDatePickerProviderExtension), _ => new DateProvider());
            ApiExtensibility.Register(typeof(ISkiaNativeTimePickerProviderExtension), _ => new TimeProvider());
            _registered = true;
        }
    }

    /// <summary>True when the pickers open in their text / keyboard input mode (Expanded window or a fine pointer).</summary>
    internal static bool TextInputFirst()
    {
        try
        {
            var activity = ActivityRegistry.Current;
            if (activity == null)
            {
                return false;
            }

            var current = WindowSizeClassMonitor.Current(activity);
            return current.Width == WindowWidthClass.Expanded || current.FinePointer;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Hides the soft keyboard once a picker dialog is gone (its text / keyboard input mode raises the keyboard for the
    /// dialog's own fields; when the dialog goes the keyboard would otherwise stay up over the app). Nothing is hidden
    /// while a text field of the app has the focus.
    /// </summary>
    internal static void HideKeyboardAfterDialog()
    {
        var activity = ActivityRegistry.Current;
        var decor = activity?.Window?.DecorView;
        if (decor == null)
        {
            return;
        }

        void Hide()
        {
            if (activity.IsFinishing || activity.CurrentFocus is global::Android.Widget.EditText { IsFocused: true })
            {
                return;
            }

            if (decor.WindowToken is { } token
                && activity.GetSystemService(global::Android.Content.Context.InputMethodService) is global::Android.Views.InputMethods.InputMethodManager ime)
            {
                ime.HideSoftInputFromWindow(token, global::Android.Views.InputMethods.HideSoftInputFlags.None);
            }

            AndroidX.Core.View.WindowCompat.GetInsetsController(activity.Window, decor)?.Hide(AndroidX.Core.View.WindowInsetsCompat.Type.Ime());
        }

        // Once the dialog's window has gone (the activity's window is the keyboard's target again), and once more a moment
        // later: the window manager hands the keyboard back asynchronously.
        decor.Post(Hide);
        decor.PostDelayed(Hide, 300);
    }

    private sealed class DateProvider : ISkiaNativeDatePickerProviderExtension
    {
        public DatePickerFlyout CreateNativeDatePickerFlyout() => new MaterialDatePickerFlyout();
    }

    private sealed class TimeProvider : ISkiaNativeTimePickerProviderExtension
    {
        public TimePickerFlyout CreateNativeTimePickerFlyout() => new MaterialTimePickerFlyout();
    }
}

/// <summary>A DatePickerFlyout shown as a Material date picker dialog.</summary>
internal sealed class MaterialDatePickerFlyout : DatePickerFlyout
{
    private AMaterialDatePicker _dialog;
    private bool _dismissingFromCore;

    /// <summary>The dialog while it is shown (tests and diagnostics).</summary>
    internal AMaterialDatePicker Dialog => _dialog;

    /// <summary>Raised when a dialog was shown (tests and diagnostics).</summary>
    internal static event EventHandler<MaterialDatePickerFlyout> Shown;

    /// <inheritdoc />
    protected internal override void Open()
    {
        var activity = ActivityRegistry.Current;
        if (activity == null)
        {
            base.Open();
            return;
        }

        var date = Date;
        if (date == DatePicker.NullDateSentinelValue || date.Year < 1601)
        {
            date = DateTimeOffset.Now;
        }

        var constraints = new Google.Android.Material.DatePicker.CalendarConstraints.Builder()
            .SetStart(UtcDayMillis(MinYear))
            .SetEnd(UtcDayMillis(MaxYear))
            .SetOpenAt(UtcDayMillis(date))
            .Build();
        _dialog = AMaterialDatePicker.Builder.DatePicker()
            .SetSelection(Java.Lang.Long.ValueOf(UtcDayMillis(date)))
            .SetCalendarConstraints(constraints)
            .SetInputMode(MaterialPickers.TextInputFirst() ? AMaterialDatePicker.InputModeText : AMaterialDatePicker.InputModeCalendar)
            .Build();
        _dialog.AddOnPositiveButtonClickListener(new PositiveListener(this));
        _dialog.AddOnDismissListener(new DismissListener(OnDismissed));
        _dialog.Show(activity.SupportFragmentManager, "CodeBrix.DatePicker");
        AddToOpenFlyouts();
        Shown?.Invoke(null, this);
    }

    /// <inheritdoc />
    private protected override void OnClosed()
    {
        _dismissingFromCore = true;
        try
        {
            if (_dialog is { } dialog && dialog.IsAdded)
            {
                dialog.DismissAllowingStateLoss();
            }
        }
        finally
        {
            _dismissingFromCore = false;
        }

        base.OnClosed();
    }

    /// <summary>The chosen day (UTC midnight milliseconds, the Material picker's selection) becomes Date.</summary>
    /// <param name="utcMillis">The selection.</param>
    internal void Pick(long utcMillis)
    {
        var day = DateTime.SpecifyKind(DateTimeOffset.FromUnixTimeMilliseconds(utcMillis).UtcDateTime.Date, DateTimeKind.Unspecified);
        var old = Date;
        var chosen = new DateTimeOffset(day + old.TimeOfDay, old.Offset);
        Date = chosen;
        _datePicked?.Invoke(this, new DatePickedEventArgs(chosen, old));
    }

    private static long UtcDayMillis(DateTimeOffset date) =>
        new DateTimeOffset(date.Year, date.Month, date.Day, 0, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();

    private void OnDismissed()
    {
        _dialog = null;
        MaterialPickers.HideKeyboardAfterDialog();
        if (!_dismissingFromCore)
        {
            Hide(canCancel: false);
            RemoveFromOpenFlyouts();
        }
    }

    private sealed class PositiveListener : Java.Lang.Object, Google.Android.Material.DatePicker.IMaterialPickerOnPositiveButtonClickListener
    {
        private readonly WeakReference<MaterialDatePickerFlyout> _owner;

        internal PositiveListener(MaterialDatePickerFlyout owner) => _owner = new WeakReference<MaterialDatePickerFlyout>(owner);

        public void OnPositiveButtonClick(Java.Lang.Object selection)
        {
            if (_owner.TryGetTarget(out var owner) && selection is Java.Lang.Long millis)
            {
                owner.Pick(millis.LongValue());
            }
        }
    }
}

/// <summary>A TimePickerFlyout shown as a Material time picker dialog (ClockIdentifier -> 12/24-hour clock).</summary>
internal sealed class MaterialTimePickerFlyout : TimePickerFlyout
{
    private AMaterialTimePicker _dialog;
    private bool _dismissingFromCore;

    /// <summary>The dialog while it is shown (tests and diagnostics).</summary>
    internal AMaterialTimePicker Dialog => _dialog;

    /// <summary>Raised when a dialog was shown (tests and diagnostics).</summary>
    internal static event EventHandler<MaterialTimePickerFlyout> Shown;

    /// <inheritdoc />
    protected internal override void Open()
    {
        var activity = ActivityRegistry.Current;
        if (activity == null)
        {
            base.Open();
            return;
        }

        var time = Time;
        if (time.Ticks < 0 || time >= TimeSpan.FromDays(1))
        {
            time = DateTime.Now.TimeOfDay;
        }

        var clock24 = string.Equals(ClockIdentifier, "24HourClock", StringComparison.OrdinalIgnoreCase);
        _dialog = new AMaterialTimePicker.Builder()
            .SetTimeFormat(clock24 ? Google.Android.Material.TimePicker.TimeFormat.Clock24h : Google.Android.Material.TimePicker.TimeFormat.Clock12h)
            .SetHour(time.Hours)
            .SetMinute(time.Minutes)
            .SetInputMode(MaterialPickers.TextInputFirst() ? AMaterialTimePicker.InputModeKeyboard : AMaterialTimePicker.InputModeClock)
            .Build();
        _dialog.AddOnPositiveButtonClickListener(new PositiveListener(this));
        _dialog.AddOnDismissListener(new DismissListener(OnDismissed));
        _dialog.Show(activity.SupportFragmentManager, "CodeBrix.TimePicker");
        AddToOpenFlyouts();
        Shown?.Invoke(null, this);
    }

    /// <inheritdoc />
    private protected override void OnClosed()
    {
        _dismissingFromCore = true;
        try
        {
            if (_dialog is { } dialog && dialog.IsAdded)
            {
                dialog.DismissAllowingStateLoss();
            }
        }
        finally
        {
            _dismissingFromCore = false;
        }

        base.OnClosed();
    }

    /// <summary>The chosen hour and minute become Time (rounded to MinuteIncrement as the Fluent picker does).</summary>
    /// <param name="hour">The hour (0-23).</param>
    /// <param name="minute">The minute.</param>
    internal void Pick(int hour, int minute)
    {
        var old = Time;
        var increment = Math.Max(1, MinuteIncrement);
        var rounded = (int)(Math.Round(minute / (double)increment) * increment);
        var chosen = new TimeSpan(hour, 0, 0) + TimeSpan.FromMinutes(rounded);
        if (chosen >= TimeSpan.FromDays(1))
        {
            chosen -= TimeSpan.FromDays(1);
        }

        Time = chosen;
        OnTimePicked(new TimePickedEventArgs(old, chosen));
    }

    private void OnDismissed()
    {
        _dialog = null;
        MaterialPickers.HideKeyboardAfterDialog();
        if (!_dismissingFromCore)
        {
            Hide(canCancel: false);
            RemoveFromOpenFlyouts();
        }
    }

    private sealed class PositiveListener : Java.Lang.Object, global::Android.Views.View.IOnClickListener
    {
        private readonly WeakReference<MaterialTimePickerFlyout> _owner;

        internal PositiveListener(MaterialTimePickerFlyout owner) => _owner = new WeakReference<MaterialTimePickerFlyout>(owner);

        public void OnClick(global::Android.Views.View view)
        {
            if (_owner.TryGetTarget(out var owner) && owner.Dialog is { } dialog)
            {
                owner.Pick(dialog.Hour, dialog.Minute);
            }
        }
    }
}

/// <summary>A dialog dismiss listener calling back into managed code.</summary>
internal sealed class DismissListener : Java.Lang.Object, global::Android.Content.IDialogInterfaceOnDismissListener
{
    private readonly Action _dismissed;

    /// <summary>Creates the listener.</summary>
    /// <param name="dismissed">Called when the dialog is dismissed.</param>
    internal DismissListener(Action dismissed) => _dismissed = dismissed;

    /// <inheritdoc />
    public void OnDismiss(global::Android.Content.IDialogInterface dialog) => _dismissed?.Invoke();
}
