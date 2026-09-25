using System;
using System.Globalization;
using Windows.Foundation;

namespace CodeBrix.Android.UI.Handlers;

/// <summary>What an InfoBadge shows.</summary>
internal enum InfoBadgeKind
{
    /// <summary>A small dot (no value, no icon).</summary>
    Dot,

    /// <summary>A number.</summary>
    Value,

    /// <summary>An icon.</summary>
    Icon,
}

/// <summary>
/// AP10-B: the platform-free rules of the native status controls (InfoBadge, PersonPicture, RatingControl, InfoBar):
/// kept here so they are unit-tested on the host.
/// </summary>
internal static class StatusMath
{
    /// <summary>The Material 3 small badge (a dot), in DIPs.</summary>
    internal const double DotSize = 6;

    /// <summary>The Material 3 large badge height (a number or an icon), in DIPs.</summary>
    internal const double BadgeHeight = 16;

    /// <summary>The horizontal padding of a number badge, in DIPs.</summary>
    internal const double BadgePadding = 4;

    /// <summary>What an InfoBadge shows: a number when Value is 0 or more, else an icon when it has one, else a dot.</summary>
    /// <param name="value">InfoBadge.Value (-1 = none).</param>
    /// <param name="hasIcon">True when IconSource is set.</param>
    /// <returns>The kind.</returns>
    internal static InfoBadgeKind BadgeKind(int value, bool hasIcon) =>
        value >= 0 ? InfoBadgeKind.Value : hasIcon ? InfoBadgeKind.Icon : InfoBadgeKind.Dot;

    /// <summary>The text of a number badge (Material: more than 999 shows "999+").</summary>
    /// <param name="value">The value.</param>
    /// <returns>The text.</returns>
    internal static string BadgeText(int value) =>
        value > 999 ? "999+" : Math.Max(0, value).ToString(CultureInfo.InvariantCulture);

    /// <summary>The size a badge asks for, in DIPs.</summary>
    /// <param name="kind">What it shows.</param>
    /// <param name="textWidth">The width of its text in DIPs (a number badge).</param>
    /// <returns>The size.</returns>
    internal static Size BadgeSize(InfoBadgeKind kind, double textWidth) => kind switch
    {
        InfoBadgeKind.Dot => new Size(DotSize, DotSize),
        InfoBadgeKind.Icon => new Size(BadgeHeight, BadgeHeight),
        _ => new Size(Math.Max(BadgeHeight, Math.Ceiling(textWidth) + (2 * BadgePadding)), BadgeHeight),
    };

    /// <summary>
    /// The initials a PersonPicture shows (WinUI rules): Initials when set, else the first letters of the first and
    /// last words of DisplayName (a single word gives one letter), upper case; empty when there is neither.
    /// </summary>
    /// <param name="initials">PersonPicture.Initials.</param>
    /// <param name="displayName">PersonPicture.DisplayName.</param>
    /// <returns>The initials.</returns>
    internal static string Initials(string initials, string displayName)
    {
        if (!string.IsNullOrWhiteSpace(initials))
        {
            return initials.Trim().ToUpper(CultureInfo.CurrentCulture);
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            return string.Empty;
        }

        var words = displayName.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        var first = FirstLetter(words[0]);
        var last = words.Length > 1 ? FirstLetter(words[^1]) : string.Empty;
        return (first + last).ToUpper(CultureInfo.CurrentCulture);
    }

    /// <summary>The initials' font size for a picture of the given diameter (WinUI: 42 % of it).</summary>
    /// <param name="diameter">The diameter in DIPs.</param>
    /// <returns>The font size in DIPs.</returns>
    internal static double InitialsFontSize(double diameter) => Math.Max(1, Math.Round(diameter * 0.42, 1));

    /// <summary>The text of a PersonPicture badge: BadgeText, else BadgeNumber (0 = none, more than 99 = "99+").</summary>
    /// <param name="badgeText">PersonPicture.BadgeText.</param>
    /// <param name="badgeNumber">PersonPicture.BadgeNumber.</param>
    /// <returns>The text, or null for no text badge.</returns>
    internal static string PictureBadgeText(string badgeText, int badgeNumber)
    {
        if (!string.IsNullOrEmpty(badgeText))
        {
            return badgeText;
        }

        return badgeNumber <= 0 ? null : badgeNumber > 99 ? "99+" : badgeNumber.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// The number of stars a native rating bar shows as filled for a RatingControl value (-1 = unset shows the
    /// placeholder value, or nothing), clamped to the stars there are.
    /// </summary>
    /// <param name="value">RatingControl.Value.</param>
    /// <param name="placeholder">RatingControl.PlaceholderValue.</param>
    /// <param name="maxRating">RatingControl.MaxRating.</param>
    /// <returns>The rating to show.</returns>
    internal static float ShownRating(double value, double placeholder, int maxRating)
    {
        var shown = value >= 0 ? value : placeholder >= 0 ? placeholder : 0;
        return (float)Math.Clamp(shown, 0, Math.Max(1, maxRating));
    }

    /// <summary>
    /// The value a finger's rating gives a RatingControl: the whole number of stars (at least one), or -1 when the
    /// finger chose the current value again and IsClearEnabled allows clearing.
    /// </summary>
    /// <param name="rating">The native rating.</param>
    /// <param name="current">RatingControl.Value.</param>
    /// <param name="isClearEnabled">RatingControl.IsClearEnabled.</param>
    /// <returns>The new value.</returns>
    internal static double ValueFromRating(float rating, double current, bool isClearEnabled)
    {
        var stars = Math.Max(1, (int)Math.Ceiling(rating - 0.001));
        return isClearEnabled && Math.Abs(stars - current) < 0.001 ? -1 : stars;
    }

    private static string FirstLetter(string word)
    {
        foreach (var c in word)
        {
            if (char.IsLetterOrDigit(c))
            {
                return c.ToString();
            }
        }

        return string.Empty;
    }
}
