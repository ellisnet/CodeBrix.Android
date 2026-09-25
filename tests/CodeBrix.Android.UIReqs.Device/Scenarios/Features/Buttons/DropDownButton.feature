Feature: DropDownButton
	A DropDownButton exists to open its flyout: a tap shows the flyout's content on the panel,
	and a tap somewhere else takes it away again.

Scenario: Tapping a DropDownButton opens its flyout
	Given the application shows a DropDownButton named "menu" with a flyout panel named "sheet" 300 by 200 painted "Lime"
	Then the flyout of "menu" is closed
	When "menu" is tapped
	And the frame is captured
	Then the flyout of "menu" is open
	And the Click of "menu" was raised once
	And the region of "sheet" is uniformly "Lime"

Scenario: Tapping away from an open flyout dismisses it and leaves no trace
	Given the application shows a DropDownButton named "menu" with a flyout panel named "sheet" 300 by 200 painted "Lime"
	When the frame is captured as "closed"
	And "menu" is tapped
	Then the flyout of "menu" is open
	When a point far from "menu" is tapped
	And the frame is captured as "dismissed"
	Then the flyout of "menu" is closed
	And nothing outside "menu" changed between frames "dismissed" and "closed"
