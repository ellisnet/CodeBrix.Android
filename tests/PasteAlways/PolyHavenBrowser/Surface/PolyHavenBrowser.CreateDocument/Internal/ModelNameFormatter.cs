namespace PolyHavenBrowser.CreateDocument.Internal;

/// <summary>Display-name helpers for the sheet's persuasive copy.</summary>
internal static class ModelNameFormatter
{
    /// <summary>
    /// Strips trailing whitespace-separated all-digit tokens from a model's display name, so
    /// the pull quote reads naturally: <c>Marble Bust 1</c> → <c>Marble Bust</c>,
    /// <c>Camera 01</c> → <c>Camera</c>. Trailing digit tokens are removed repeatedly, but a
    /// name consisting only of digits is returned unchanged rather than stripped to nothing.
    /// </summary>
    public static string StripTrailingNumbers(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) { return string.Empty; }

        var stripped = name.Trim();
        while (true)
        {
            var lastSpace = stripped.LastIndexOf(' ');
            if (lastSpace <= 0) { break; }

            var lastToken = stripped[(lastSpace + 1)..];
            if (lastToken.Length == 0 || !lastToken.All(char.IsAsciiDigit)) { break; }

            stripped = stripped[..lastSpace].TrimEnd();
        }

        return stripped;
    }
}
