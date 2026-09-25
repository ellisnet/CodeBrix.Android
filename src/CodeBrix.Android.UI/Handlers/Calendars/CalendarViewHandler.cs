using System;
using CodeBrix.Android.UI.Hosting;
using CodeBrix.Platform.UI.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;
using Windows.Foundation.Collections;
using ACalendarView = global::Android.Widget.CalendarView;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>
/// AP10-A: the handler of CalendarView (tsv rows CalendarView, CalendarPanel, CalendarViewBaseItem,
/// CalendarViewDayItem: "android.widget.CalendarView (single selection) / Core-composed fallback for multi-select").
/// A month view with Single or None selection is the platform's calendar (its month grid replaces CalendarPanel and
/// the day items): MinDate / MaxDate bound it, FirstDayOfWeek orders its weeks, the first SelectedDate is its
/// selected day; a day a finger picks becomes the one SelectedDate (Core raises SelectedDatesChanged) - unless the
/// SelectionMode is None, where picking only moves the view. Multiple selection and the year / decade views keep the
/// Fluent template (<see cref="CalendarMath.CanShowNatively"/>). Not mapped (no native form): per-day density bars and
/// CalendarViewDayItemChanging, blackout days, IsGroupLabelVisible, the Fluent colours.
/// </summary>
internal sealed class CalendarViewHandler : ViewHandler<CalendarView, ACalendarView>
{
    /// <summary>CalendarView's mapper.</summary>
    public static readonly PropertyMapper<CalendarView, CalendarViewHandler> Mapper = new(ViewMappers.ViewMapper)
    {
        [CalendarView.MinDateProperty] = MapRange,
        [CalendarView.MaxDateProperty] = MapRange,
        [CalendarView.FirstDayOfWeekProperty] = MapFirstDay,
        [CalendarView.SelectionModeProperty] = MapMode,
        [CalendarView.DisplayModeProperty] = MapMode,
        [Control.IsEnabledProperty] = MapEnabled,
    };

    private static readonly ILogger _log = HostLog.For("CodeBrix.Android.UI.Handlers.CalendarView");
    private IObservableVector<DateTimeOffset> _selected;
    private bool _updating;

    /// <summary>Creates the handler.</summary>
    public CalendarViewHandler()
        : base(Mapper)
    {
    }

    /// <inheritdoc />
    public override ElementHandlerCapabilities Capabilities =>
        ElementHandlerCapabilities.OwnsVisuals | ElementHandlerCapabilities.MeasuresNatively | ElementHandlerCapabilities.OwnsInput;

    /// <summary>The native calendar, or the templated fallback when it cannot show the CalendarView.</summary>
    /// <param name="element">The CalendarView.</param>
    /// <returns>The handler.</returns>
    internal static IAndroidElementHandler Create(UIElement element)
    {
        if (element is not CalendarView calendar || !NativeControlPolicy.IsNative(calendar, typeof(CalendarView), Array.Empty<string>(), out _))
        {
            return new TemplatedFallbackHandler();
        }

        if (!CalendarMath.CanShowNatively(calendar.SelectionMode, calendar.DisplayMode, out var reason))
        {
            _log.LogInformation("CalendarView {Name} keeps its Fluent template: {Reason}.", calendar.Name, reason);
            return new TemplatedFallbackHandler();
        }

        return new CalendarViewHandler();
    }

    /// <summary>Maps MinDate / MaxDate.</summary>
    public static void MapRange(CalendarViewHandler handler, CalendarView element)
    {
        var view = handler.PlatformView;
        var min = LocalDayMillis(element.MinDate);
        var max = LocalDayMillis(element.MaxDate);
        if (min <= max)
        {
            view.MinDate = min;
            view.MaxDate = max;
        }

        handler.ShowSelection();
    }

    /// <summary>Maps FirstDayOfWeek.</summary>
    public static void MapFirstDay(CalendarViewHandler handler, CalendarView element) =>
        handler.PlatformView.FirstDayOfWeek = CalendarMath.AndroidFirstDay(element.FirstDayOfWeek);

    /// <summary>Maps SelectionMode / DisplayMode (a mode the native calendar cannot show is logged: the handler kind is chosen when the element connects).</summary>
    public static void MapMode(CalendarViewHandler handler, CalendarView element)
    {
        if (!CalendarMath.CanShowNatively(element.SelectionMode, element.DisplayMode, out var reason))
        {
            _log.LogWarning("CalendarView {Name}: {Reason} is not shown by the native calendar it already is.", element.Name, reason);
        }
    }

    /// <summary>Maps IsEnabled.</summary>
    public static void MapEnabled(CalendarViewHandler handler, CalendarView element) => handler.PlatformView.Enabled = element.IsEnabled;

    /// <summary>Picks a day as a finger does on the native grid.</summary>
    /// <param name="year">The year.</param>
    /// <param name="month">The month (1-12).</param>
    /// <param name="day">The day.</param>
    internal void PickDay(int year, int month, int day)
    {
        if (Element is not CalendarView calendar || _updating)
        {
            return;
        }

        if (calendar.SelectionMode == CalendarViewSelectionMode.None)
        {
            return;
        }

        var dates = calendar.SelectedDates;
        var previous = dates.Count > 0 ? dates[0] : (DateTimeOffset?)null;
        var picked = CalendarMath.Picked(year, month, day, previous);
        if (previous is { } p && p.Date == picked.Date && dates.Count == 1)
        {
            return;
        }

        _updating = true;
        try
        {
            dates.Clear();
            dates.Add(picked);
        }
        finally
        {
            _updating = false;
        }
    }

    /// <inheritdoc />
    public override Size Measure(Size availableSize) => ViewHandlerExtensions.GetDesiredSizeFromView(PlatformView, availableSize, Density);

    /// <inheritdoc />
    protected override ACalendarView CreatePlatformView() => new(MaterialWidgets.Material3(Context));

    /// <inheritdoc />
    protected override void ConnectHandler(ACalendarView platformView)
    {
        base.ConnectHandler(platformView);
        platformView.DateChange += OnDateChange;
    }

    /// <inheritdoc />
    protected override void OnConnected()
    {
        base.OnConnected();
        if (Element is CalendarView calendar && calendar.SelectedDates is IObservableVector<DateTimeOffset> selected)
        {
            _selected = selected;
            selected.VectorChanged += OnSelectedDatesChanged;
        }

        ShowSelection();
    }

    /// <inheritdoc />
    protected override void DisconnectHandler(ACalendarView platformView)
    {
        platformView.DateChange -= OnDateChange;
        if (_selected != null)
        {
            _selected.VectorChanged -= OnSelectedDatesChanged;
            _selected = null;
        }

        base.DisconnectHandler(platformView);
    }

    private void OnSelectedDatesChanged(IObservableVector<DateTimeOffset> sender, IVectorChangedEventArgs e)
    {
        if (!_updating)
        {
            ShowSelection();
        }
    }

    private void ShowSelection()
    {
        if (Element is not CalendarView calendar || PlatformView is not { } view || calendar.SelectedDates.Count == 0)
        {
            return;
        }

        var millis = LocalDayMillis(calendar.SelectedDates[0]);
        if (millis >= view.MinDate && millis <= view.MaxDate && view.Date != millis)
        {
            _updating = true;
            try
            {
                view.SetDate(millis, false, true);
            }
            finally
            {
                _updating = false;
            }
        }
    }

    // android.widget.CalendarView works in the device's time zone: a day is its local midnight.
    private static long LocalDayMillis(DateTimeOffset date)
    {
        var calendar = Java.Util.Calendar.Instance;
        calendar.Clear();
        calendar.Set(date.Year, date.Month - 1, date.Day, 0, 0, 0);
        return calendar.TimeInMillis;
    }

    private void OnDateChange(object sender, ACalendarView.DateChangeEventArgs e) => PickDay(e.Year, e.Month + 1, e.DayOfMonth);
}
