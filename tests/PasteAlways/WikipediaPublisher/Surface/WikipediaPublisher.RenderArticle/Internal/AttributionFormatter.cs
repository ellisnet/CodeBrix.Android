using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;

namespace WikipediaPublisher.RenderArticle.Internal;

/// <summary>
/// Turns a media file's Wikimedia "extmetadata" fields into a single short credit line
/// suitable for printing under an image — e.g. "Jane Doe · CC BY-SA 4.0",
/// "Public domain", or "Reproduced under Creative Commons". Returns an empty string when
/// no usable authorship or licensing information is present.
/// </summary>
internal static class AttributionFormatter
{
    //Keep the line short enough to sit on one or two lines at caption size
    private const int MaxLength = 170;

    //Values that carry no real authorship and read worse than saying nothing
    private static readonly HashSet<string> PlaceholderNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "unknown", "unknown author", "unknown artist", "author unknown",
            "anonymous", "not provided", "see source", "n/a", "na", "none"
        };

    public static string Format(IReadOnlyDictionary<string, string> extMetadata)
    {
        if (extMetadata is null || extMetadata.Count == 0) { return ""; }

        var artist = CleanName(Get(extMetadata, "Artist"));
        //An explicit "Attribution" credit is the author's preferred wording; fall back to it
        if (artist.Length == 0)
        {
            artist = CleanName(Get(extMetadata, "Attribution"));
        }

        var license = NormalizeLicense(Get(extMetadata, "LicenseShortName"));

        //Don't repeat a value that already appears as the author (e.g. "Public domain · Public domain")
        if (artist.Length > 0 && artist.Equals(license, StringComparison.OrdinalIgnoreCase))
        {
            license = "";
        }

        //Nothing to attribute and no license — better to print no line at all
        if (artist.Length == 0 && license.Length == 0)
        {
            return "";
        }

        string line;
        if (artist.Length > 0 && license.Length > 0)
        {
            line = $"{artist} · {license}";
        }
        else if (artist.Length > 0)
        {
            line = artist;
        }
        else
        {
            //Licence only: name the licence, but give the friendly phrase for bare CC licences
            line = license;
        }

        return Truncate(line, MaxLength);
    }

    private static string Get(IReadOnlyDictionary<string, string> metadata, string key) =>
        metadata.TryGetValue(key, out var value) ? value ?? "" : "";

    private static string CleanName(string raw)
    {
        var cleaned = CleanText(raw);
        //Some Artist fields list several creators separated by line breaks; keep the first
        if (cleaned.Contains(';'))
        {
            cleaned = cleaned.Split(';')[0].Trim();
        }
        cleaned = CollapseDoubledPhrase(cleaned);
        return PlaceholderNames.Contains(cleaned) ? "" : cleaned;
    }

    /// <summary>
    /// Collapses a value that is simply the same phrase repeated twice — Wikimedia templates
    /// sometimes emit a machine- and a human-readable copy side by side, giving values like
    /// "Unknown artist Unknown artist". Returns the input unchanged when it is not a clean double.
    /// </summary>
    private static string CollapseDoubledPhrase(string text)
    {
        if (text.Length == 0) { return text; }

        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length >= 2 && words.Length % 2 == 0)
        {
            var half = words.Length / 2;
            var firstHalf = string.Join(' ', words[..half]);
            var secondHalf = string.Join(' ', words[half..]);
            if (firstHalf.Equals(secondHalf, StringComparison.OrdinalIgnoreCase))
            {
                return firstHalf;
            }
        }
        return text;
    }

    /// <summary>
    /// Cleans a licence short name and rewrites codes that mean nothing to a general reader.
    /// "CC0" (and the "CCO" look-alike) is a public-domain dedication whose bare code is just
    /// jargon, so it is shown as the plain-language "Public domain" instead.
    /// </summary>
    private static string NormalizeLicense(string raw)
    {
        var cleaned = CleanText(raw);
        if (cleaned.Length == 0) { return ""; }

        var compact = cleaned.Replace(" ", "").Replace("-", "").ToUpperInvariant();
        if (compact.StartsWith("CC0", StringComparison.Ordinal)
            || compact.StartsWith("CCO", StringComparison.Ordinal))
        {
            return "Public domain";
        }

        return cleaned;
    }

    /// <summary>Strips HTML markup, decodes entities and collapses whitespace to a single line.</summary>
    private static string CleanText(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) { return ""; }

        var withoutTags = Regex.Replace(raw, "<[^>]+>", " ");
        var decoded = WebUtility.HtmlDecode(withoutTags);
        var collapsed = Regex.Replace(decoded, @"\s+", " ").Trim();

        //Drop a few noisy fragments that Wikimedia embeds in Artist/Attribution fields
        collapsed = collapsed.Replace("[edit]", "").Trim();
        return collapsed;
    }

    private static string Truncate(string text, int maxLength)
    {
        if (text.Length <= maxLength) { return text; }
        return text[..maxLength].TrimEnd(' ', ',', ';', '·', '-') + "…";
    }
}
