using System;
using System.Collections.Generic;

namespace CodeBrix.Android.Portable;

/// <summary>
/// Normalizes the user's locale list (Android LocaleList, most preferred first) into the
/// BCP-47 language tags GlobalizationPreferences.Languages reports.
/// </summary>
internal static class LanguageTags
{
    /// <summary>The tag reported when the device reports no usable locale.</summary>
    internal const string Fallback = "en-US";

    /// <summary>
    /// Returns the normalized, de-duplicated tags in preference order. Underscores become
    /// hyphens ("en_US" -> "en-US"), the undetermined tag "und" and empty entries are
    /// dropped, and an empty result becomes <see cref="Fallback"/>.
    /// </summary>
    internal static IReadOnlyList<string> Normalize(IEnumerable<string> tags)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (tags != null)
        {
            foreach (var tag in tags)
            {
                var normalized = NormalizeOne(tag);
                if (normalized != null && seen.Add(normalized))
                {
                    result.Add(normalized);
                }
            }
        }

        if (result.Count == 0)
        {
            result.Add(Fallback);
        }

        return result;
    }

    /// <summary>Normalizes one tag, or returns null when the tag is empty or undetermined.</summary>
    internal static string NormalizeOne(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return null;
        }

        var normalized = tag.Trim().Replace('_', '-');
        if (normalized.Equals("und", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return normalized;
    }
}
