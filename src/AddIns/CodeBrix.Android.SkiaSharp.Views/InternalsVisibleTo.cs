using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("CodeBrix.Android.SkiaSharp.Views.Tests")]

// The Android add-ins that draw on the Skia canvas (their handlers reuse this assembly's canvas views).
[assembly: InternalsVisibleTo("CodeBrix.Android.UI.Graphics2DSK")]
[assembly: InternalsVisibleTo("CodeBrix.Android.UI.Svg")]
[assembly: InternalsVisibleTo("CodeBrix.Android.UI.Lottie")]
[assembly: InternalsVisibleTo("CodeBrix.Android.UI.TextLayout")]
[assembly: InternalsVisibleTo("CodeBrix.Android.UI.AdvancedTextEdit")]
[assembly: InternalsVisibleTo("CodeBrix.Android.UI.TerminalView")]
[assembly: InternalsVisibleTo("CodeBrix.Android.UI.PlotterView")]
[assembly: InternalsVisibleTo("CodeBrix.Android.UI.VideoPlayer")]
[assembly: InternalsVisibleTo("CodeBrix.Android.UI.Graphics3DGL")]
