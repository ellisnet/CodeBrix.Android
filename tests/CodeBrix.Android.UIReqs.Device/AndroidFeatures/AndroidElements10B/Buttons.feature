Feature: Buttons and icons elements
	Android-only (AP10-B). HyperlinkButton is a Material text button (AP3a). DropDownButton is the Button handler's
	Material button with a trailing chevron; its flyout is a native Material menu (AP4) where the app lets overlays be native
	(tag @native-overlays; the copied groups keep Core's flyouts). SplitButton and
	ToggleSplitButton are a Material 3 split button drawn over their Fluent template, whose two buttons keep Click,
	the flyout and the toggling. BitmapIcon, PathIcon and AnimatedIcon are composed by Core (an Image, a Path, the
	fallback icon) and shown natively; a monochrome BitmapIcon's ImageView is tinted with its Foreground.

Scenario: A HyperlinkButton is a Material text button and a real finger raises Click once
	Given the application shows the AP10B sample "hyperlink button" named "link"
	And the Click events of "link" are tallied
	Then "link" is shown by a native MaterialButton
	When a real finger taps the native "Docs" of "link"
	Then "link" raised Click 1 times in all

@native-overlays
Scenario: A DropDownButton is a Material button with a trailing chevron that opens its flyout as a Material menu
	Given the application shows the AP10B sample "drop down button" named "menu"
	And the Click events of "menu" are tallied
	When the frame is captured
	Then "menu" is shown by a native MaterialButton
	And the Material button of "menu" has a trailing chevron
	When a real finger taps the native "Sort" of "menu"
	Then "menu" raised Click 1 times in all
	And the flyout of "menu" is open
	And the flyout of "menu" is a native Material menu
	When the flyout of "menu" is hidden
	Then the flyout of "menu" is closed

Scenario: A SplitButton is a Material split button over its template buttons
	Given the application shows the AP10B sample "split button" named "split"
	When the frame is captured
	Then "split" is shown by a native MaterialSplitButton
	And the native overlay of "split" is shown
	And "split" shows the native text "Paste"
	And the halves of the split button "split" lie over its template buttons
	And the Core tree of "split" holds a Button

Scenario: A real finger on the leading half of a SplitButton raises Click and leaves the flyout closed
	Given the application shows the AP10B sample "split button" named "split"
	And the Click events of "split" are tallied
	When a real finger taps the leading half of the split button "split"
	Then "split" raised Click 1 times in all
	And the flyout of "split" is closed

@native-overlays
Scenario: A real finger on the chevron half of a SplitButton opens its flyout without a Click
	Given the application shows the AP10B sample "split button" named "split"
	And the Click events of "split" are tallied
	When a real finger taps the trailing half of the split button "split"
	Then the flyout of "split" is open
	And the flyout of "split" is a native Material menu
	And "split" raised Click 0 times in all
	When the flyout of "split" is hidden
	Then the flyout of "split" is closed

Scenario: A real finger on the leading half of a ToggleSplitButton checks it
	Given the application shows the AP10B sample "toggle split button" named "toggle"
	And the IsCheckedChanged events of "toggle" are tallied
	Then the leading half of the split button "toggle" is unchecked
	When a real finger taps the leading half of the split button "toggle"
	Then the Core property IsChecked of "toggle" is "True"
	And "toggle" raised IsCheckedChanged 1 times in all
	And the leading half of the split button "toggle" is checked

Scenario: A SplitButton whose content is an element keeps its template visible
	Given the application shows the AP10B sample "split button with an icon" named "split"
	Then the native overlay of "split" is hidden

Scenario: A BitmapIcon shows its picture natively, untinted unless monochrome
	Given the application shows the AP10B sample "bitmap icon" named "icon"
	When the frame is captured
	Then "icon" shows a native bitmap
	And the BitmapIcon "icon" is tinted no

Scenario: A monochrome BitmapIcon is tinted with its Foreground
	Given the application shows the AP10B sample "monochrome bitmap icon" named "icon"
	When the frame is captured
	Then "icon" shows a native bitmap
	And the BitmapIcon "icon" is tinted Blue
	When the Core property ShowAsMonochrome of "icon" becomes "False"
	Then the BitmapIcon "icon" is tinted no

Scenario: A PathIcon is a native shape view of its Data
	Given the application shows the AP10B sample "path icon" named "icon"
	Then "icon" is shown by a native ShapeView
	And the Core tree of "icon" holds a Path

Scenario: An AnimatedIcon without an animation source shows its fallback icon natively
	Given the application shows the AP10B sample "animated icon" named "icon"
	Then "icon" shows the glyph of the symbol Accept
	And the Core tree of "icon" holds a SymbolIcon
