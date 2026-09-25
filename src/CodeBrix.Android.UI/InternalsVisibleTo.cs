using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("CodeBrix.Android.UI.Tests")]
[assembly: InternalsVisibleTo("CodeBrix.Android.UIReqs.Device")]

// The in-repo sample app's on-device self-check (samples/HelloPaste) reads Overlay.PlatformOverlays.
[assembly: InternalsVisibleTo("HelloPaste")]

// The add-ins (AP7): each Android add-in assembly (named by its Core's grant) registers its handlers through
// CodeBrixHandlers and builds its views on the handler infrastructure, which is internal in v1 (D-O1).
[assembly: InternalsVisibleTo("CodeBrix.Android.SkiaSharp.Views")]
[assembly: InternalsVisibleTo("CodeBrix.Android.UI.Svg")]
[assembly: InternalsVisibleTo("CodeBrix.Android.WinUI.Graphics2DSK")]
[assembly: InternalsVisibleTo("CodeBrix.Android.UI.CommandBar")]
[assembly: InternalsVisibleTo("CodeBrix.Android.UI.FlexPanel")]
[assembly: InternalsVisibleTo("CodeBrix.Android.AppSettings")]
[assembly: InternalsVisibleTo("CodeBrix.Android.UI.WebView")]
[assembly: InternalsVisibleTo("CodeBrix.Android.UI.MediaPlayer")]
[assembly: InternalsVisibleTo("CodeBrix.Android.WinUI.Graphics3DGL")]
[assembly: InternalsVisibleTo("CodeBrix.Android.UI.Lottie")]
[assembly: InternalsVisibleTo("CodeBrix.Android.UI.TextLayout")]
[assembly: InternalsVisibleTo("CodeBrix.Android.UI.AdvancedTextEdit")]
[assembly: InternalsVisibleTo("CodeBrix.Android.UI.TerminalView")]
[assembly: InternalsVisibleTo("CodeBrix.Android.UI.PlotterView")]
[assembly: InternalsVisibleTo("CodeBrix.Android.UI.AudioPlayer")]
[assembly: InternalsVisibleTo("CodeBrix.Android.UI.VideoPlayer")]
