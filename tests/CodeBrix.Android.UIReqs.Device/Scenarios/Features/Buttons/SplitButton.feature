Feature: SplitButton
	A SplitButton is two buttons in one: the primary half does the thing, the drop-down half
	offers the choices. A finger on one half must not do the other half's job.

Scenario: Tapping the primary half of a SplitButton raises Click and leaves its flyout closed
	Given the application shows a SplitButton named "menu" with a flyout panel named "sheet" 300 by 200 painted "Lime"
	Then the flyout of "menu" is closed
	When the primary half of the SplitButton "menu" is tapped
	Then the Click of "menu" was raised once
	And the flyout of "menu" is closed

Scenario: Tapping the drop-down half of a SplitButton opens its flyout
	Given the application shows a SplitButton named "menu" with a flyout panel named "sheet" 300 by 200 painted "Lime"
	When the drop-down half of the SplitButton "menu" is tapped
	And the frame is captured
	Then the flyout of "menu" is open
	And the Click of "menu" was not raised
	And the region of "sheet" is uniformly "Lime"
