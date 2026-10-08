namespace CodeBrix.Android.UI.Portable;

/// <summary>
/// The CodeBrix.Android add-in assembly names, exactly as the add-in Cores grant them InternalsVisibleTo
/// (plan 4.2 / AP7): the names the start-up add-in loader (Android/AddInLoader) loads by name. The order is
/// the dependency order - the canvas add-in before the add-ins that draw on it.
/// </summary>
internal static class AddInAssemblyNames
{
    /// <summary>Every Android add-in assembly name.</summary>
    internal static readonly string[] All =
    [
        "CodeBrix.Android.SkiaSharp.Views",
        "CodeBrix.Android.UI.Graphics2DSK",
        "CodeBrix.Android.UI.Svg",
        "CodeBrix.Android.UI.Lottie",
        "CodeBrix.Android.UI.TextLayout",
        "CodeBrix.Android.UI.AdvancedTextEdit",
        "CodeBrix.Android.UI.TerminalView",
        "CodeBrix.Android.UI.PlotterView",
        "CodeBrix.Android.UI.CommandBar",
        "CodeBrix.Android.UI.FlexPanel",
        "CodeBrix.Android.AppSettings",
        "CodeBrix.Android.UI.AudioPlayer",
        "CodeBrix.Android.UI.VideoPlayer",
        "CodeBrix.Android.UI.MediaPlayer",
        "CodeBrix.Android.UI.WebView",
        "CodeBrix.Android.UI.Graphics3DGL",
    ];
}
