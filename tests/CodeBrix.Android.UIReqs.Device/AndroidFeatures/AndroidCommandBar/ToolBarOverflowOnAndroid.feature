@needs-commandbar
Feature: The ToolBar overflow on Android
	Android-only (not a copy of a CodeBrix.Platform feature). A click on an item in a ToolBar's overflow flyout runs the item
	and closes the flyout, as a CommandBar closes its overflow menu after a command: the next finger tap reaches the page and
	the next Android back button takes the Frame back (an open flyout would take either as its light dismiss). Fingers are
	REAL MotionEvents dispatched to the activity.

Scenario: A finger on an overflow item runs it and closes the overflow, and the next finger tap reaches the bar
	Given the application shows a Frame that went from page "one" to a page holding a ToolBar named "bar" 160 wide with the tool buttons "new, open, save, print, score"
	And the chevron of the ToolBar "bar" is named "chevron"
	When a real finger taps "chevron"
	Then the overflow flyout of the ToolBar "bar" is open
	When a real finger taps "score"
	Then the tool button "score" was clicked 1 time
	And no flyout is open over the page
	When the frame is captured
	And a real finger taps "new"
	Then the tool button "new" was clicked 1 time

Scenario: After a finger on an overflow item the next Android back button takes the Frame back
	Given the application shows a Frame that went from page "one" to a page holding a ToolBar named "bar" 160 wide with the tool buttons "new, open, save, print, score"
	And the chevron of the ToolBar "bar" is named "chevron"
	When a real finger taps "chevron"
	Then the overflow flyout of the ToolBar "bar" is open
	When a real finger taps "score"
	Then the tool button "score" was clicked 1 time
	When the Android back button is pressed
	Then the Frame shows page "one"
