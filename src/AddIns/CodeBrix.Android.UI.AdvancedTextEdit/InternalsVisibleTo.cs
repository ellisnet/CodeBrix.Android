using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("CodeBrix.Android.UI.AdvancedTextEdit.Tests")]

// The UIReqs scenario app's Android-only fences (AndroidFeatures/AndroidAdvancedTextEdit) read the registered canvas
// supply, its surfaces and the editor's text target.
[assembly: InternalsVisibleTo("CodeBrix.Android.UIReqs.Device")]
