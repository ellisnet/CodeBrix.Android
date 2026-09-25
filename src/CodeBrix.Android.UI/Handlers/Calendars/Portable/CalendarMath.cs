using System;
using Microsoft.UI.Xaml.Controls;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>
/// AP10-A: the pure part of the native calendars (tsv rows CalendarView, CalendarDatePicker): the day a native
/// calendar works in (UTC-midnight milliseconds, what android.widget.CalendarView and MaterialDatePicker use), the
/// Core date a picked day becomes (the day with the old value's time of day and offset, as Core's own pickers keep
/// them), the Android first day of the week, and whether a CalendarView can be a native one.
/// </summary>
internal static class CalendarMath
{
    /// <summary>A day as UTC-midnight milliseconds.</summary>
    /// <param name="date">The date (its calendar day counts, not its instant).</param>
    /// <returns>The milliseconds.</returns>
    internal static long DayMillis(DateTimeOffset date) =>
        new DateTimeOffset(date.Year, date.Month, date.Day, 0, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();

    /// <summary>The Core date of a picked day.</summary>
    /// <param name="year">The year.</param>
    /// <param name="month">The month (1-12).</param>
    /// <param name="day">The day.</param>
    /// <param name="previous">The value it replaces (its time of day and offset are kept), or null.</param>
    /// <returns>The date.</returns>
    internal static DateTimeOffset Picked(int year, int month, int day, DateTimeOffset? previous)
    {
        var offset = previous?.Offset ?? DateTimeOffset.Now.Offset;
        var time = previous?.TimeOfDay ?? TimeSpan.Zero;
        return new DateTimeOffset(new DateTime(year, month, day) + time, offset);
    }

    /// <summary>The Core date of a day picked as UTC-midnight milliseconds.</summary>
    /// <param name="utcMillis">The selection.</param>
    /// <param name="previous">The value it replaces, or null.</param>
    /// <returns>The date.</returns>
    internal static DateTimeOffset PickedMillis(long utcMillis, DateTimeOffset? previous)
    {
        var day = DateTimeOffset.FromUnixTimeMilliseconds(utcMillis).UtcDateTime;
        return Picked(day.Year, day.Month, day.Day, previous);
    }

    /// <summary>java.util.Calendar's day number (Sunday = 1) of a WinRT day of the week.</summary>
    /// <param name="day">The day (Sunday = 0 ... Saturday = 6).</param>
    /// <returns>The Android number.</returns>
    internal static int AndroidFirstDay(Windows.Globalization.DayOfWeek day) => ((int)day % 7) + 1;

    /// <summary>
    /// True when android.widget.CalendarView can show a CalendarView: a month view with single (or no) selection.
    /// Multiple selection, the year / decade views keep the Fluent template (the Core-composed calendar).
    /// </summary>
    /// <param name="mode">SelectionMode.</param>
    /// <param name="display">DisplayMode.</param>
    /// <param name="reason">Why not, when false.</param>
    /// <returns>True for the native calendar.</returns>
    internal static bool CanShowNatively(CalendarViewSelectionMode mode, CalendarViewDisplayMode display, out string reason)
    {
        reason = mode == CalendarViewSelectionMode.Multiple ? "multiple selection (the native calendar selects one day)"
            : display != CalendarViewDisplayMode.Month ? "a year or decade view (the native calendar shows months)"
            : null;
        return reason == null;
    }
}
