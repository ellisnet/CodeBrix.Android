using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("CodeBrix.Android.UI.AudioPlayer.Tests")]

// The UIReqs scenario app's Android-only fences (AndroidFeatures/AndroidAudioPlayer) read the registered platforms.
[assembly: InternalsVisibleTo("CodeBrix.Android.UIReqs.Device")]

