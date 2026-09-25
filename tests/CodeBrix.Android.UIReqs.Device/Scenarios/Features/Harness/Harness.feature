Feature: Harness
	The harness itself has requirements: the application fills the panel it was given, a
	scenario starts on an empty panel, the reset between scenarios really removes what the last
	scenario showed - including a popup, which is hosted beside the root rather than inside it -
	and a scenario tagged for the other panel is skipped rather than silently absent.

Scenario: The root fills the panel
	Given the application shows a Border named "box" 400 by 300 with Background "Red"
	When the frame is captured
	Then the root fills the panel
	And the frame is the size of the panel

Scenario: A scenario starts on an empty panel
	Given no scenario content is present
	When the frame is captured
	Then the panel is blank

Scenario: The reset between scenarios removes what a scenario showed
	Given the application shows a Border named "leftover" 400 by 300 with Background "Red"
	When the frame is captured
	Then the region of "leftover" is uniformly "Red"
	When the scenario reset runs
	And the frame is captured
	Then no element named "leftover" exists
	And the panel is blank

Scenario: A popup left open by a scenario is closed by the reset
	Given the application shows a DropDownButton named "menu" with a flyout panel named "sheet" 300 by 200 painted "Lime"
	When the frame is captured as "before the popup"
	And "menu" is tapped
	Then the flyout of "menu" is open
	And a popup is open
	When the popups the scenario left open are closed
	Then no popup is open
	And the flyout of "menu" is closed
	When the frame is captured as "after the popup"
	Then nothing outside "menu" changed between frames "after the popup" and "before the popup"
	When the scenario reset runs
	Then no scenario content is present
	Given the application shows a Button named "next" 240 by 80 with Background "Red"
	When "next" is tapped
	Then the Click of "next" was raised once

Scenario: The colour the harness reports for a shade is its most common exact colour
	Given pixels of one colour shade: 1 of "#FF0909" first, then 20 of "#FF0000"
	Then the harness reports their colour as exactly "#FF0000"
	And the harness reports that colour for 21 of them

@landscape-only
Scenario: The landscape panel is wider than it is tall
	Then the panel is wider than it is tall
	And the panel orientation is "Landscape"

@portrait-only
Scenario: The portrait panel is taller than it is wide
	Then the panel is taller than it is wide
	And the panel orientation is "Portrait"
