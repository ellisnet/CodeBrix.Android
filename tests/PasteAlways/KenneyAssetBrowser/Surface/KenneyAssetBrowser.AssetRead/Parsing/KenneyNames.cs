using System;
using System.Globalization;
using System.Linq;

namespace KenneyAssetBrowser.AssetRead.Parsing;

/// <summary>
/// Turns Kenney bundle file names and license headers into display names.
/// </summary>
public static class KenneyNames
{
    /// <summary>
    /// Prettifies a bundle zip file name: <c>kenney_brick-kit.zip</c> becomes <c>Brick Kit</c>.
    /// </summary>
    /// <param name="zipFileName">The bundle file name or full path.</param>
    /// <returns>A title-cased display name, or an empty string for empty input.</returns>
    public static string PrettifyBundleFileName(string zipFileName)
    {
        if (string.IsNullOrWhiteSpace(zipFileName)) { return string.Empty; }

        var name = zipFileName.Replace('\\', '/');
        var lastSlash = name.LastIndexOf('/');
        if (lastSlash >= 0) { name = name.Substring(lastSlash + 1); }

        if (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            name = name.Substring(0, name.Length - 4);
        }

        if (name.StartsWith("kenney_", StringComparison.OrdinalIgnoreCase))
        {
            name = name.Substring("kenney_".Length);
        }

        var words = name.Split(['-', '_', ' '], StringSplitOptions.RemoveEmptyEntries)
            .Select(word => char.ToUpper(word[0], CultureInfo.InvariantCulture) + word.Substring(1));
        return string.Join(" ", words);
    }

    /// <summary>
    /// Attempts to read the bundle title and version from the first content line of a Kenney
    /// <c>License.txt</c> file, which looks like <c>Brick Kit (1.0)</c>.
    /// </summary>
    /// <param name="licenseText">The full text of the license file.</param>
    /// <param name="title">The bundle title, or <c>null</c> when not found.</param>
    /// <param name="version">The version inside the parentheses, or <c>null</c> when absent.</param>
    /// <returns><c>true</c> when a plausible title line was found.</returns>
    public static bool TryParseLicenseTitle(string licenseText, out string title, out string version)
    {
        title = null;
        version = null;
        if (string.IsNullOrWhiteSpace(licenseText)) { return false; }

        var firstLine = licenseText
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .FirstOrDefault(line => line.Length > 0);
        if (firstLine == null) { return false; }

        //A title line is short prose, not one of the license body sentences
        if (firstLine.Length > 80 || firstLine.Contains("http", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var openParen = firstLine.LastIndexOf('(');
        if (openParen > 0 && firstLine.EndsWith(")", StringComparison.Ordinal))
        {
            title = firstLine.Substring(0, openParen).Trim();
            version = firstLine.Substring(openParen + 1, firstLine.Length - openParen - 2).Trim();
        }
        else
        {
            title = firstLine;
        }

        return title.Length > 0;
    }
}
