using System;
using CodeBrix.Android.UI.Handlers;
using Microsoft.UI.Xaml.Controls;
using SilverAssertions;
using Xunit;

namespace CodeBrix.Android.UI.Tests.Handlers;

/// <summary>AP10-A: the pure part of the native calendars.</summary>
public class CalendarMathTests
{
    [Fact]
    public void DayMillis_is_the_UTC_midnight_of_the_calendar_day()
    {
        //Arrange
        var evening = new DateTimeOffset(2026, 9, 24, 23, 30, 0, TimeSpan.FromHours(-7));

        //Act
        var millis = CalendarMath.DayMillis(evening);

        //Assert
        millis.Should().Be(new DateTimeOffset(2026, 9, 24, 0, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds());
    }

    [Fact]
    public void A_picked_day_keeps_the_previous_time_of_day_and_offset()
    {
        //Arrange
        var previous = new DateTimeOffset(2026, 9, 24, 12, 15, 0, TimeSpan.FromHours(-7));

        //Act
        var picked = CalendarMath.PickedMillis(new DateTimeOffset(2026, 10, 5, 0, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds(), previous);

        //Assert
        picked.Should().Be(new DateTimeOffset(2026, 10, 5, 12, 15, 0, TimeSpan.FromHours(-7)));
    }

    [Theory]
    [InlineData(Windows.Globalization.DayOfWeek.Sunday, 1)]
    [InlineData(Windows.Globalization.DayOfWeek.Monday, 2)]
    [InlineData(Windows.Globalization.DayOfWeek.Saturday, 7)]
    public void The_first_day_of_the_week_is_numbered_as_java_util_Calendar_numbers_it(Windows.Globalization.DayOfWeek day, int expected)
    {
        //Act
        var android = CalendarMath.AndroidFirstDay(day);

        //Assert
        android.Should().Be(expected);
    }

    [Theory]
    [InlineData(CalendarViewSelectionMode.Single, CalendarViewDisplayMode.Month, true)]
    [InlineData(CalendarViewSelectionMode.None, CalendarViewDisplayMode.Month, true)]
    [InlineData(CalendarViewSelectionMode.Multiple, CalendarViewDisplayMode.Month, false)]
    [InlineData(CalendarViewSelectionMode.Single, CalendarViewDisplayMode.Year, false)]
    public void Only_a_single_selection_month_view_is_native(CalendarViewSelectionMode mode, CalendarViewDisplayMode display, bool expected)
    {
        //Act
        var native = CalendarMath.CanShowNatively(mode, display, out var reason);

        //Assert
        native.Should().Be(expected);
        (reason == null).Should().Be(expected);
    }
}
