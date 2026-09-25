Feature: Android theme keys and the theme bridge
	Android-only (not a copy of a CodeBrix.Platform feature). The Fluent control keys an application re-keys
	(its lightweight styling) are honored on the native Material widgets, state by state, and follow the app when
	it re-points a brush's colour at run time. The theme bridge writes the Material 3 colour roles - baseline or
	dynamic - into the framework's own keys, never into a key the app defines, and puts the framework's Fluent
	colours back when it is switched off. A day/night flip reaches the app without its activity being recreated.

Scenario: The re-keyed Button brushes colour the native button in every state
	Given the application re-keys the brushes:
		| Key                         | Colour    |
		| ButtonBackground            | #FF123456 |
		| ButtonBackgroundPointerOver | #FF223344 |
		| ButtonBackgroundPressed     | #FF334455 |
		| ButtonBackgroundDisabled    | #FF445566 |
		| ButtonForeground            | #FFFFEEDD |
	And the application shows a native Button named "go"
	Then the native "background" colours of "go" are "#FF123456" at rest, "#FF223344" hovered, "#FF334455" pressed and "#FF445566" disabled
	And the native "text" colour of "go" at rest is "#FFFFEEDD"

Scenario: Re-pointing a re-keyed brush repaints the native widget live
	Given the application re-keys the brushes:
		| Key              | Colour    |
		| ButtonBackground | #FF123456 |
	And the application shows a native Button named "go"
	When the re-keyed brush "ButtonBackground" is re-pointed to "#FF00AA00"
	Then the native "background" colour of "go" at rest is "#FF00AA00"
	When the frame is captured
	Then the region of "go" contains "#FF00AA00"

Scenario: The re-keyed CheckBox brushes reach every state of the Material check box
	Given the application re-keys the brushes:
		| Key                                             | Colour    |
		| CheckBoxCheckBackgroundFillChecked              | #FF0A0B0C |
		| CheckBoxCheckBackgroundFillCheckedPointerOver   | #FF1A1B1C |
		| CheckBoxCheckBackgroundFillCheckedPressed       | #FF2A2B2C |
		| CheckBoxCheckBackgroundFillCheckedDisabled      | #FF3A3B3C |
		| CheckBoxCheckBackgroundStrokeUnchecked          | #FF4A4B4C |
		| CheckBoxCheckBackgroundStrokeUncheckedPointerOver | #FF5A5B5C |
		| CheckBoxCheckBackgroundStrokeUncheckedPressed   | #FF5B5C5D |
		| CheckBoxCheckBackgroundStrokeUncheckedDisabled  | #FF5C5D5E |
		| CheckBoxCheckGlyphForegroundChecked             | #FF6A6B6C |
		| CheckBoxForegroundChecked                       | #FF7A7B7C |
		| CheckBoxForegroundUnchecked                     | #FF8A8B8C |
	And the application shows a native CheckBox named "agree"
	Then the native "box" colours of "agree" are "#FF4A4B4C" at rest, "#FF5A5B5C" hovered, "#FF5B5C5D" pressed and "#FF5C5D5E" disabled
	And the native checked "box" colours of "agree" are "#FF0A0B0C" at rest, "#FF1A1B1C" hovered, "#FF2A2B2C" pressed and "#FF3A3B3C" disabled
	And the native "glyph" colour of "agree" at rest is "#FF6A6B6C"
	And the native "text" colour of "agree" at rest is "#FF8A8B8C"
	And the native checked "text" colour of "agree" at rest is "#FF7A7B7C"

Scenario: The re-keyed Slider brushes reach the Material slider's tracks and thumb
	Given the application re-keys the brushes:
		| Key                            | Colour    |
		| SliderTrackValueFill           | #FF101112 |
		| SliderTrackValueFillPointerOver | #FF202122 |
		| SliderTrackValueFillPressed    | #FF303132 |
		| SliderTrackValueFillDisabled   | #FF707172 |
		| SliderTrackFill                | #FF404142 |
		| SliderThumbBackground          | #FF505152 |
		| SliderThumbBackgroundPressed   | #FF606162 |
	And the application shows a native Slider named "level"
	Then the native "active track" colours of "level" are "#FF101112" at rest, "#FF202122" hovered, "#FF303132" pressed and "#FF707172" disabled
	And the native "inactive track" colour of "level" at rest is "#FF404142"
	And the native "thumb" colour of "level" at rest is "#FF505152"
	And the native "thumb" colour of "level" pressed is "#FF606162"

Scenario: The re-keyed TextControl brushes reach the text field's end icon and its focused stroke
	Given the application re-keys the brushes:
		| Key                                    | Colour    |
		| TextControlButtonForeground            | #FF112211 |
		| TextControlButtonForegroundPressed     | #FF223322 |
		| TextControlElevationBorderFocusedBrush | #FF334433 |
		| TextControlBackground                  | #FF445544 |
		| TextControlSelectionHighlightColor     | #FF556655 |
	And the application shows a native TextBox named "field"
	Then the native "end icon" colour of "field" at rest is "#FF112211"
	And the native "end icon" colour of "field" pressed is "#FF223322"
	And the native "stroke" colour of "field" focused is "#FF334433"
	And the native "box" colour of "field" at rest is "#FF445544"
	And the native "highlight" colour of "field" at rest is "#FF556655"

Scenario: The re-keyed ProgressBar brush colours the indicator
	Given the application re-keys the brushes:
		| Key                   | Colour    |
		| ProgressBarForeground | #FF0000CC |
	And the application shows a native ProgressBar named "loading"
	Then the native "indicator" colour of "loading" at rest is "#FF0000CC"

Scenario: The re-keyed ComboBox, ListViewItem and ScrollBar brushes reach the templates those controls keep
	Given the application re-keys the brushes:
		| Key                           | Colour    |
		| ComboBoxBackground            | #FF0C0D0E |
		| ListViewItemBackgroundSelected | #FF1C1D1E |
		| ScrollBarBackground           | #FF2C2D2E |
	And the application shows a native ComboBox named "choice"
	And the application also shows a native ListView named "list" with its first item selected
	And the application also shows a native ScrollViewer named "scroller"
	Then a part of "choice" paints with the re-keyed brush "ComboBoxBackground"
	And a part of "list" paints with the re-keyed brush "ListViewItemBackgroundSelected"
	And a part of "scroller" paints with the re-keyed brush "ScrollBarBackground"

@native-overlays
Scenario: The re-keyed ContentDialog brushes colour a Material dialog
	Given the application re-keys the brushes:
		| Key                     | Colour    |
		| ContentDialogBackground | #FF203040 |
		| ContentDialogForeground | #FFF0E0D0 |
		| ContentDialogSmokeFill  | #80000000 |
	And the application shows a text ContentDialog named "note" titled "Note" saying "Saved."
	Then the Material dialog's surface is "#FF203040"
	And the Material dialog's title and message are drawn in "#FFF0E0D0"
	And the Material dialog dims the window behind it by 50 percent

@material-policy
Scenario: The Material palette colours an unthemed app and a key the app sets wins
	Given the Material palette is on with dynamic colour off
	And the application re-keys the brushes:
		| Key                    | Colour    |
		| AccentButtonBackground | #FF804000 |
	And the application shows a native Button named "plain"
	And the application also shows an accent Button named "accent"
	Then the native "background" colour of "plain" at rest is the Material role "colorSecondaryContainer"
	And the native "background" colour of "accent" at rest is "#FF804000"
	And the key "AccentFillColorDefaultBrush" resolves to the Material role "colorPrimary"

@material-policy
Scenario: Dynamic colour can be switched off and on, and never reaches a key the app defines
	Given the Material palette is on with dynamic colour off
	And the application re-keys the brushes:
		| Key                         | Colour    |
		| AccentFillColorDefaultBrush | #FF336699 |
	Then the framework's "TextFillColorPrimaryBrush" is the baseline Material role "colorOnSurface"
	When dynamic colour is switched on
	Then the framework's "TextFillColorPrimaryBrush" is the Material role "colorOnSurface" of the palette in use
	And the key "AccentFillColorDefaultBrush" still resolves to "#FF336699"

Scenario: Switching the Material palette off puts the framework's Fluent colours back
	Given the framework's colour of "AccentFillColorDefaultBrush" is remembered
	When the Material palette is switched on
	Then the framework's colour of "AccentFillColorDefaultBrush" changed
	When the Material palette is switched off
	Then the framework's colour of "AccentFillColorDefaultBrush" is the remembered one

Scenario: A day/night flip reaches the application without its activity being recreated
	Given the application shows a Grid named "page" 600 by 400 with Background "Gray"
	And the running activity is remembered
	When the app's night mode is switched on
	Then the running activity is the remembered one
	And the activity's configuration is in night mode
	When the app's night mode follows the system again
	Then the running activity is the remembered one
