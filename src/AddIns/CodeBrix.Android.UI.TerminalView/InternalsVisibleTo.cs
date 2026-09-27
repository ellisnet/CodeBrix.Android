using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("CodeBrix.Android.UI.TerminalView.Tests")]

// The UIReqs scenario app's Android-only fences (AndroidFeatures/AndroidTerminal) read the registered canvas supply and
// its surfaces.
[assembly: InternalsVisibleTo("CodeBrix.Android.UIReqs.Device")]
