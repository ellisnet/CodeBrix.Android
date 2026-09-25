using System;

namespace KenneyAssetBrowser.Helpers;

/// <summary>
/// Small display-formatting helpers shared by the view models.
/// </summary>
public static class FormatHelper
{
    /// <summary>
    /// Formats a byte count for display: <c>712 B</c>, <c>26.1 KB</c>, <c>4.2 MB</c>.
    /// </summary>
    /// <param name="bytes">The byte count.</param>
    public static string FormatBytes(long bytes)
    {
        if (bytes < 1024) { return $"{bytes} B"; }
        if (bytes < 1024 * 1024) { return $"{bytes / 1024d:0.#} KB"; }
        return $"{bytes / (1024d * 1024d):0.#} MB";
    }

    /// <summary>
    /// Formats a count with its singular or plural noun: <c>1 model</c>, <c>296 models</c>.
    /// </summary>
    /// <param name="count">The count.</param>
    /// <param name="singular">The singular noun.</param>
    public static string FormatCount(int count, string singular) =>
        count == 1 ? $"1 {singular}" : $"{count:N0} {singular}s";
}
