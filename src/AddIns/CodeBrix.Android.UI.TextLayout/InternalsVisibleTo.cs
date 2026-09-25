using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("CodeBrix.Android.UI.TextLayout.Tests")]

// The UIReqs scenario app's Android-only fences (AndroidFeatures/AndroidNative/EngineContracts.feature) read the
// registered font source through FontSourceProbe.
[assembly: InternalsVisibleTo("CodeBrix.Android.UIReqs.Device")]
