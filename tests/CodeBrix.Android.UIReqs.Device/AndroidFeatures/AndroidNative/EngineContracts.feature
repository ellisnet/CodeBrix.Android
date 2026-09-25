Feature: Platform contracts of the engine pass
	Android-only (not a copy of a CodeBrix.Platform feature). Since the engine-pass build of CodeBrix.Platform the
	Android platform registers three more contracts: the text engine's font source (the TextLayout add-in; a family
	name is the app's default text font, never a system font), the system animation setting behind
	UISettings.AnimationsEnabled (the animator duration scale), and the device family's operating-system name.

Scenario: The text engine's font source gives a family name the app's default text font
	Then the text engine's font source is "FontSourceAndroidPlatform"
	And the text engine gives the family "Segoe UI" the app's default text font

Scenario: UISettings.AnimationsEnabled follows the system animator duration scale
	Then UISettings.AnimationsEnabled is what the system animator duration scale says

Scenario: The device family names Android and the device form
	Then AnalyticsInfo.VersionInfo.DeviceFamily starts with "Android."
