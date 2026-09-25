Feature: Flyout
	A Flyout is a panel an application shows over itself, attached to the element it belongs
	to. It stays until something dismisses it: a tap anywhere else takes it away, and so does
	the application asking it to go.

Scenario: Showing a Flyout puts its content over the application
	Given the application shows a Border named "anchor" 300 by 200 with Background "Silver"
	And a Flyout named "sheet" is attached to "anchor" with a panel named "page" 280 by 180 painted "Lime"
	When the frame is captured as "before"
	Then the flyout "sheet" is closed
	And no popup is open
	When the flyout "sheet" is shown at "anchor"
	And the frame is captured as "showing"
	Then the flyout "sheet" is open
	And a popup is open
	And the region of "page" is uniformly "Lime"

Scenario: Tapping away from an open Flyout dismisses it and leaves no trace
	Given the application shows a Border named "anchor" 300 by 200 with Background "Silver"
	And a Flyout named "sheet" is attached to "anchor" with a panel named "page" 280 by 180 painted "Lime"
	When the frame is captured as "before"
	And the flyout "sheet" is shown at "anchor"
	Then the flyout "sheet" is open
	When a point far from "anchor" is tapped
	And the frame is captured as "dismissed"
	Then the flyout "sheet" is closed
	And no popup is open
	And nothing outside "anchor" changed between frames "dismissed" and "before"

Scenario: An application can take its own Flyout away
	Given the application shows a Border named "anchor" 300 by 200 with Background "Silver"
	And a Flyout named "sheet" is attached to "anchor" with a panel named "page" 280 by 180 painted "Lime"
	When the flyout "sheet" is shown at "anchor"
	Then the flyout "sheet" is open
	When the flyout "sheet" is hidden
	And the frame is captured
	Then the flyout "sheet" is closed
	And no popup is open

Scenario: A Flyout's presenter covers whatever it is shown over
	Given a Flyout named "sheet" with an empty panel 400 by 300 is attached to a light and dark backdrop named "page"
	When the flyout "sheet" is shown at "page"
	And the frame is captured
	Then the flyout "sheet" is open
	And the open flyout hides what is behind it
