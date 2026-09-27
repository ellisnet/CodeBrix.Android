using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("CodeBrix.Android.UI.Lottie.Tests")]

// The UIReqs scenario app's Android-only fence (AndroidFeatures/AndroidNative/EngineContracts.feature) reads the
// registered canvas supply and its render surfaces.
[assembly: InternalsVisibleTo("CodeBrix.Android.UIReqs.Device")]
