using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("CodeBrix.Android.UI.Tests")]
[assembly: InternalsVisibleTo("CodeBrix.Android.UIReqs.Device")]

// AP7-B: the TriPaneView handler's portable plan is tested host-free against the Toolkit Core's engine, which grants its
// internals only to the Toolkit names.
[assembly: InternalsVisibleTo("CodeBrix.Android.UI.Toolkit.Tests")]

// AP7-B: the soft keyboard's keystrokes (Portable/TextInput) are fed host-free through the TerminalView Core's own key
// mapping and encoder, which grants its internals only to the TerminalView names.
[assembly: InternalsVisibleTo("CodeBrix.Android.UI.TerminalView.Tests")]

// The add-ins (AP7): each Android add-in assembly (named by its Core's grant) registers its handlers through
// CodeBrixHandlers and builds its views on the handler infrastructure, which is internal in v1 (D-O1).
[assembly: InternalsVisibleTo("CodeBrix.Android.SkiaSharp.Views")]
[assembly: InternalsVisibleTo("CodeBrix.Android.UI.Svg")]
[assembly: InternalsVisibleTo("CodeBrix.Android.UI.Graphics2DSK")]
[assembly: InternalsVisibleTo("CodeBrix.Android.UI.CommandBar")]
[assembly: InternalsVisibleTo("CodeBrix.Android.UI.FlexPanel")]
[assembly: InternalsVisibleTo("CodeBrix.Android.AppSettings")]
[assembly: InternalsVisibleTo("CodeBrix.Android.UI.WebView")]
[assembly: InternalsVisibleTo("CodeBrix.Android.UI.MediaPlayer")]
[assembly: InternalsVisibleTo("CodeBrix.Android.UI.Graphics3DGL")]
[assembly: InternalsVisibleTo("CodeBrix.Android.UI.Lottie")]
[assembly: InternalsVisibleTo("CodeBrix.Android.UI.TextLayout")]
[assembly: InternalsVisibleTo("CodeBrix.Android.UI.AdvancedTextEdit")]
[assembly: InternalsVisibleTo("CodeBrix.Android.UI.TerminalView")]
[assembly: InternalsVisibleTo("CodeBrix.Android.UI.PlotterView")]
[assembly: InternalsVisibleTo("CodeBrix.Android.UI.AudioPlayer")]
[assembly: InternalsVisibleTo("CodeBrix.Android.UI.VideoPlayer")]
