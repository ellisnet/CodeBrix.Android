using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("CodeBrix.Android.UI.WebGpu.Tests")]

// The UIReqs scenario app's Android-only fences (AndroidFeatures/AndroidWebGpu) read the render engine, the paint surface
// and the registered handler.
[assembly: InternalsVisibleTo("CodeBrix.Android.UIReqs.Device")]
