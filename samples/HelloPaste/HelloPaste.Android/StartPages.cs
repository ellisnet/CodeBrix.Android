using System;

namespace HelloPaste;

/// <summary>
/// The pages HelloPaste can start on. The launch intent's <c>page</c> extra selects one:
/// <c>adb shell am start -n com.codebrix.hellopaste/com.codebrix.hellopaste.MainActivity --es page pdf</c>.
/// </summary>
public static class StartPages
{
    /// <summary>The JustBetweenUs MainPage (the default).</summary>
    public const string JustBetweenUs = "jbu";

    /// <summary>The PdfSideBySide MainPage.</summary>
    public const string PdfSideBySide = "pdf";

    /// <summary>The start page the launch intent asked for (set by MainActivity before the XAML application starts).</summary>
    public static string Requested { get; set; } = JustBetweenUs;

    /// <summary>Returns the page type for a start-page key (unknown keys give the default).</summary>
    public static Type Resolve(string key) =>
        string.Equals(key, PdfSideBySide, StringComparison.OrdinalIgnoreCase)
            ? typeof(global::PdfSideBySide.Views.MainPage)
            : typeof(global::JustBetweenUs.Views.MainPage);

    /// <summary>Returns the key of the other page (used by the self-check's navigation round trip).</summary>
    public static string Other(string key) =>
        string.Equals(key, PdfSideBySide, StringComparison.OrdinalIgnoreCase) ? JustBetweenUs : PdfSideBySide;
}
