using System;
using System.Globalization;

namespace GitHubIssueFinder.GitHub;

/// <summary>
/// Turns a moment into the short phrase GitHub shows beside an issue, for example
/// "3 days ago". Plain text work with the clock passed in, so it is straightforward to test.
/// </summary>
public static class RelativeTime
{
    //GitHub counts a month as a rough thirtieth of the days and a year as 365 of them;
    //the phrase is an approximation and the exact date is shown elsewhere.
    private const int DaysPerWeek = 7;
    private const int DaysPerMonth = 30;
    private const int DaysPerYear = 365;

    /// <summary>
    /// Describes how long ago a moment was, in GitHub's wording.
    /// </summary>
    /// <param name="when">The moment being described.</param>
    /// <param name="now">The moment to measure from.</param>
    /// <returns>
    /// The phrase, for example "just now", "5 minutes ago", "yesterday", "last week",
    /// "3 months ago" or "2 years ago". A moment in the future reads "just now".
    /// </returns>
    public static string Describe(DateTimeOffset when, DateTimeOffset now)
    {
        var elapsed = now - when;
        if (elapsed < TimeSpan.Zero) { return "just now"; }
        if (elapsed.TotalSeconds < 60d) { return "just now"; }

        var minutes = (int)elapsed.TotalMinutes;
        if (minutes < 60) { return Phrase(minutes, "minute"); }

        var hours = (int)elapsed.TotalHours;
        if (hours < 24) { return Phrase(hours, "hour"); }

        var days = (int)elapsed.TotalDays;
        if (days == 1) { return "yesterday"; }
        if (days < DaysPerWeek) { return Phrase(days, "day"); }

        var weeks = days / DaysPerWeek;
        if (weeks <= 4) { return weeks == 1 ? "last week" : Phrase(weeks, "week"); }

        var months = days / DaysPerMonth;
        if (months <= 11) { return months == 1 ? "last month" : Phrase(months, "month"); }

        //A gap past eleven months but short of a full year still reads as a year.
        var years = Math.Max(1, days / DaysPerYear);
        return years == 1 ? "last year" : Phrase(years, "year");
    }

    private static string Phrase(int count, string unit) =>
        count == 1
            ? "1 " + unit + " ago"
            : count.ToString(CultureInfo.InvariantCulture) + " " + unit + "s ago";
}
