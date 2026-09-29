using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("CodeBrix.Android.UI.VideoPlayer.Tests")]

// The UIReqs scenario app's Android-only fences (AndroidFeatures/AndroidVideoPlayer) read the registered platforms and the
// surface.
[assembly: InternalsVisibleTo("CodeBrix.Android.UIReqs.Device")]
