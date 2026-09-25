using System.Text;
using System.Text.RegularExpressions;

namespace WikipediaPublisher.RenderArticle.Internal;

/// <summary>
/// Filters article text down to the character ranges covered by the embedded book
/// fonts (EB Garamond and Source Sans 3: Latin, Latin Extended, Greek, Cyrillic and
/// common punctuation). Characters outside those ranges — for example Cuneiform,
/// CJK or Arabic glyphs quoted inline in an article — would otherwise render as
/// "tofu" boxes in the PDF, which ruins a printed page.
/// </summary>
internal static class GlyphFilter
{
    /// <summary>
    /// Removes characters the embedded fonts cannot render and tidies up what
    /// remains (collapsed whitespace, no empty bracket pairs). Returns the cleaned
    /// text; <paramref name="removedCount"/> reports how many characters were dropped.
    /// </summary>
    public static string Sanitize(string text, out int removedCount)
    {
        removedCount = 0;
        if (string.IsNullOrEmpty(text)) { return text ?? ""; }

        StringBuilder builder = null;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (IsSupported(c))
            {
                builder?.Append(c);
                continue;
            }

            if (builder is null)
            {
                builder = new StringBuilder(text.Length);
                builder.Append(text, 0, i);
            }

            removedCount++;
            if (char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                i++; //Skip the low half of the surrogate pair as well
            }
        }

        if (builder is null) { return text; } //Nothing removed

        var cleaned = builder.ToString();

        //Tidy the holes left behind: empty bracket pairs, doubled spaces,
        //  stray space before closing punctuation
        cleaned = Regex.Replace(cleaned, @"[\(\[«“‘]\s*[\)\]»”’]", "");
        cleaned = Regex.Replace(cleaned, @"\s+([,;.:!?)\]])", "$1");
        cleaned = Regex.Replace(cleaned, @"[ \t]{2,}", " ");

        return cleaned;
    }

    private static bool IsSupported(char c)
    {
        int code = c;

        return code switch
        {
            0x0009 or 0x000A or 0x000D => true,   //Tab, newline
            >= 0x0020 and <= 0x007E => true,      //ASCII
            >= 0x00A0 and <= 0x024F => true,      //Latin-1, Latin Extended A/B
            >= 0x0250 and <= 0x02FF => true,      //IPA and spacing modifiers (partial coverage)
            >= 0x0370 and <= 0x03FF => true,      //Greek
            >= 0x0400 and <= 0x04FF => true,      //Cyrillic
            >= 0x1E00 and <= 0x1EFF => true,      //Latin Extended Additional (incl. Vietnamese)
            >= 0x1F00 and <= 0x1FFF => true,      //Greek Extended
            >= 0x2000 and <= 0x206F => true,      //General punctuation (dashes, quotes, …)
            >= 0x20A0 and <= 0x20BF => true,      //Currency symbols
            0x2113 or 0x2122 or 0x2126 => true,   //ℓ, ™, Ω
            >= 0x2150 and <= 0x218F => true,      //Number forms (fractions, Roman numerals)
            _ => false
        };
    }
}
