@needs-commandbar
Feature: A tool bar lays its items out
	A tool bar must run its items along itself in the order it was given them, leave its stated gap
	between two of them, draw a hairline where a divider was asked for and where two groups meet,
	give a filling spacer everything that is left over, and move the trailing items it cannot fit
	into a flyout behind a chevron - the same item instances, not copies of them.

	Deliberately out of scope here. There is no pointer that hovers on this panel, so the bar's
	hover colours cannot be reached at all; the display scale is pinned at 1.0, so the hairline's
	scale story belongs to the add-in's own unit suite; and the press-and-hold mode of a drop-down
	button is left to that suite too, because its delay has no completion signal to wait on.

Scenario: A bar lays its buttons out along itself in the order it was given them
	Given the application shows a ToolBar named "bar" with:
		| Property | Value |
		| Width    | 400   |
	And the bar "bar" holds:
		| Kind       | Name | Icon | IconTint |
		| ToolButton | new  | dot  | Navy     |
		| ToolButton | open | dot  | Navy     |
	And the icons of "bar" are loaded
	When the frame is captured
	Then "new" is 36 by 32 device pixels
	And "open" is 36 by 32 device pixels
	And there is a gap of 4 pixels between "new" and "open"
	And the top left of "new" is 4, 4 inside "bar"
	And the region of "bar" contains "#F3F3F3"
	And the region of "new" contains "Navy"
	And the region of "open" contains "Navy"

Scenario: A separator draws a one-device-pixel line in the separator colour
	Given the application shows a ToolBar named "bar" with:
		| Property | Value |
		| Width    | 400   |
	And the bar "bar" holds:
		| Kind             | Name | Icon | IconTint |
		| ToolButton       | new  | dot  | Navy     |
		| ToolBarSeparator | sep  |      |          |
		| ToolButton       | open | dot  | Navy     |
	And the icons of "bar" are loaded
	When the frame is captured
	Then "sep" is 1 by 24 device pixels
	And the 1 by 24 block at 0, 0 inside "sep" is uniformly "#D0D0D0"
	And there is a gap of 12 pixels between "new" and "sep"
	And there is a gap of 12 pixels between "sep" and "open"

Scenario: Two adjacent groups get a separator between them without one being written
	Given the application shows a ToolBar named "bar" with:
		| Property | Value |
		| Width    | 400   |
	And the bar "bar" holds:
		| Kind         | Name  |
		| ToolBarGroup | files |
		| ToolBarGroup | edits |
	And the group "files" holds:
		| Kind       | Name | Icon | IconTint |
		| ToolButton | new  | dot  | Navy     |
	And the group "edits" holds:
		| Kind       | Name | Icon | IconTint |
		| ToolButton | cut  | dot  | Navy     |
	And the icons of "bar" are loaded
	When the separator between the groups of "bar" is named "auto"
	And the frame is captured
	Then "auto" is 1 by 24 device pixels
	And the 1 by 24 block at 0, 0 inside "auto" is uniformly "#D0D0D0"
	And there is a gap of 12 pixels between "new" and "auto"
	And there is a gap of 12 pixels between "auto" and "cut"

Scenario: A filling spacer pushes what follows to the far end of the bar
	Given the application shows a ToolBar named "bar" with:
		| Property | Value |
		| Width    | 400   |
	And the bar "bar" holds:
		| Kind          | Name | Icon | IconTint | Fill |
		| ToolButton    | new  | dot  | Navy     |      |
		| ToolBarSpacer | gap  |      |          | True |
		| ToolButton    | zoom | dot  | Navy     |      |
	And the icons of "bar" are loaded
	When the frame is captured
	Then "gap" is 312 by 32 device pixels
	And the region of "gap" is uniformly "#F3F3F3"
	And there is a gap of 320 pixels between "new" and "zoom"
	And the top left of "zoom" is 360, 4 inside "bar"

Scenario: A bar too narrow for its items moves the trailing ones behind a chevron
	Given the application shows a ToolBar named "bar" with:
		| Property | Value |
		| Width    | 160   |
	And the bar "bar" holds:
		| Kind       | Name  | Icon | IconTint |
		| ToolButton | new   | dot  | Navy     |
		| ToolButton | open  | dot  | Navy     |
		| ToolButton | save  | dot  | Navy     |
		| ToolButton | print | dot  | Navy     |
		| ToolButton | score | dot  | Red      |
	And the icons of "bar" are loaded
	When the chevron of "bar" is named "chevron"
	And the frame is captured
	Then the ToolBar "bar" has overflow items
	And the overflow of "bar" holds "score"
	And the region of "bar" does not contain "Red"
	And "chevron" is 28 by 32 device pixels
	# The chevron is a hairline path, so its darkest pixel is the antialiased form of the theme's
	# #1B1B1B button foreground rather than that value itself; the claim names what is on the frame.
	And the region of "chevron" contains at least 1.5 percent "#2B2B2B"

Scenario: Tapping the chevron opens the overflow flyout holding the same items
	Given the application shows a ToolBar named "bar" with:
		| Property | Value |
		| Width    | 160   |
	And the bar "bar" holds:
		| Kind       | Name  | Icon | IconTint |
		| ToolButton | new   | dot  | Navy     |
		| ToolButton | open  | dot  | Navy     |
		| ToolButton | save  | dot  | Navy     |
		| ToolButton | print | dot  | Navy     |
		| ToolButton | score | dot  | Red      |
	And the icons of "bar" are loaded
	And the chevron of "bar" is named "chevron"
	When "chevron" is tapped
	Then the overflow flyout of "bar" is showing
	When the frame is captured
	Then the open popup has ink
	And the overflow of "bar" holds "score"
	And the overflow of "bar" holds "print"
