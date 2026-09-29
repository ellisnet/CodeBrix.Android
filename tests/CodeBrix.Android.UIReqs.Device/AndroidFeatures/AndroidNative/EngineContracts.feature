Feature: Platform contracts of the engine pass
	Android-only (not a copy of a CodeBrix.Platform feature). Since the engine-pass build of CodeBrix.Platform the
	Android platform registers three more contracts: the text engine's font source (the TextLayout add-in; a family
	name is the app's default text font, never a system font), the system animation setting behind
	UISettings.AnimationsEnabled (the animator duration scale), and the device family's operating-system name.

Scenario: The text engine's font source gives a family name the app's default text font
	Then the text engine's font source is "FontSourceAndroidPlatform"
	And the text engine gives the family "Segoe UI" the app's default text font

Scenario: The text engine finds the device's ICU when it first lays text out
	AP7-B: TextLayout.Core's engine (HarfBuzz shaping, ICU bidi and line breaking) runs on the device; its first layout
	loads the device's ICU (the NDK-stable libicu.so on API 31+).
	When the text engine lays out "שלום world" at size 32
	Then the text engine has bound the device's ICU
	And the text engine's last layout reads right to left
	When the text engine lays out "The engine measures and paints every word of this line" at size 32 within 200 pixels
	Then the text engine's last layout has at least 2 lines

Scenario: UISettings.AnimationsEnabled follows the system animator duration scale
	Then UISettings.AnimationsEnabled is what the system animator duration scale says

Scenario: The device family names Android and the device form
	Then AnalyticsInfo.VersionInfo.DeviceFamily starts with "Android."

Scenario: A Lottie animation is drawn on the Android canvas supply
	AP7-B: the Lottie Core asks the platform for the element it draws each frame on (ILottieCanvasPlatform, loaded BY
	NAME from CodeBrix.Android.UI.Lottie); on Android that element is painted through the SkiaSharp.Views canvas-host
	factory and shown as a native Skia view.
	Given the application shows an AnimatedVisualPlayer named "anim" with:
		| Property | Value |
		| Source   | pulse |
	When the Lottie animation of "anim" has loaded
	And the frame is captured
	Then the Lottie canvas supply is "LottieCanvasAndroidPlatform"
	And the Lottie animation of "anim" is painted on the Android canvas supply
	And "anim" is shown by a native SkiaCanvasView
	And the region of "anim" contains "Red"

Scenario: A Lottie document named by an ms-appx URI loads from the app's assets
	AP7-B, passing since AP1.12: the Lottie source opens an ms-appx:/// document with
	StorageFile.GetFileFromApplicationUriAsync; since pin 1.0.270.342 Core reads the package through the
	IApplicationPackageFilesPlatform contract, which CodeBrix.Android implements over the APK's AssetManager. The asset is
	in this app's APK at its ms-appx path.
	Given the application shows an AnimatedVisualPlayer named "anim"
	When the Lottie source of "anim" is "ms-appx:///Assets/LottieFence/pulse.json"
	And the Lottie animation of "anim" has loaded
	And the frame is captured
	Then the region of "anim" contains "Red"

Scenario: The Lottie frame clock is the Android display's Choreographer
	AP1.12 (WPE1-13): CodeBrix.Android.UI.Lottie registers ILottieTickSourcePlatform, so the Lottie engine's frames are
	ticked by Choreographer frame callbacks instead of a dispatcher timer.
	Then the Lottie frame clock is "LottieTickSourceAndroidPlatform"

Scenario: CompositionTarget.Rendering ticks while subscribed and stops when not
	AP10-C (FIXLIST A:161): Android draws natively and has no render loop of its own, so its frame clock (Choreographer
	frame callbacks on the UI thread) raises CompositionTarget.Rendering once per display frame for as long as anything is
	subscribed - a Storyboard, a TeachingTip opening, app code - and asks for no frames once nothing is.
	When app code subscribes to CompositionTarget.Rendering
	Then CompositionTarget.Rendering is raised on display frames
	When app code unsubscribes from CompositionTarget.Rendering
	Then CompositionTarget.Rendering is no longer raised and the frame clock is idle
