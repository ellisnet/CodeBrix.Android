using System;
using System.Globalization;

namespace CodeBrix.Android.UI.Portable;

/// <summary>
/// Word selection over a string (double-tap / Ctrl+arrow semantics): a word is a run of
/// letters, digits and connector punctuation; any other run of characters (white space or
/// punctuation) is its own "word".
/// </summary>
internal static class WordBoundaries
{
    /// <summary>
    /// Returns the word that contains <paramref name="index"/>. When the index is on a
    /// boundary between two runs, <paramref name="right"/> selects the run to its right
    /// (otherwise the run to its left).
    /// </summary>
    internal static (int Start, int Length) GetWordAt(string text, int index, bool right)
    {
        if (string.IsNullOrEmpty(text))
        {
            return (0, 0);
        }

        index = Math.Clamp(index, 0, text.Length);
        int probe;
        if (index == text.Length)
        {
            probe = text.Length - 1;
        }
        else if (index == 0)
        {
            probe = 0;
        }
        else if (Kind(text[index - 1]) != Kind(text[index]))
        {
            probe = right ? index : index - 1;
        }
        else
        {
            probe = index;
        }

        var kind = Kind(text[probe]);
        var start = probe;
        while (start > 0 && Kind(text[start - 1]) == kind)
        {
            start--;
        }

        var end = probe + 1;
        while (end < text.Length && Kind(text[end]) == kind)
        {
            end++;
        }

        return (start, end - start);
    }

    private static int Kind(char c)
    {
        if (char.IsLetterOrDigit(c) || c == '_' || char.GetUnicodeCategory(c) == UnicodeCategory.ConnectorPunctuation)
        {
            return 0;
        }

        return char.IsWhiteSpace(c) ? 1 : 2;
    }
}
