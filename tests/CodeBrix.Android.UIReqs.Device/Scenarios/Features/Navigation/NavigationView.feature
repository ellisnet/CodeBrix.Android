Feature: NavigationView
	A NavigationView is a menu beside the thing the menu chose. Tapping one of its items selects
	that item and puts its page on the panel, and the button above the menu opens and shuts the
	pane the items live in.

Scenario: Selecting a NavigationView item puts its page on the panel
	Given the application shows a NavigationView named "nav" 900 by 500 with items:
		| Name  | Label | Panel      | Colour |
		| alpha | Alpha | alphaPanel | Red    |
		| beta  | Beta  | betaPanel  | Blue   |
	When the NavigationView "nav" selects "alpha"
	And the frame is captured
	Then the NavigationView "nav" has selected "alpha"
	And the region of "alphaPanel" is uniformly "Red"

Scenario: Tapping a NavigationView item selects it and shows its page
	Given the application shows a NavigationView named "nav" 900 by 500 with items:
		| Name  | Label | Panel      | Colour |
		| alpha | Alpha | alphaPanel | Red    |
		| beta  | Beta  | betaPanel  | Blue   |
	When the item "beta" of the NavigationView "nav" is tapped
	And the frame is captured
	Then the NavigationView "nav" has selected "beta"
	And the region of "betaPanel" is uniformly "Blue"
	And the NavigationView "nav" reported a selection change

Scenario: Tapping a second NavigationView item replaces what the first one showed
	Given the application shows a NavigationView named "nav" 900 by 500 with items:
		| Name  | Label | Panel      | Colour |
		| alpha | Alpha | alphaPanel | Red    |
		| beta  | Beta  | betaPanel  | Blue   |
	And the NavigationView "nav" selects "alpha"
	When the frame is captured as "alpha"
	And the item "beta" of the NavigationView "nav" is tapped
	And the frame is captured as "beta"
	Then the NavigationView "nav" has selected "beta"
	And the region of "betaPanel" is uniformly "Blue"
	And the region of "betaPanel" in frame "beta" differs from frame "alpha"

Scenario: A new NavigationView has an open pane and has chosen nothing
	Given the application shows a NavigationView named "nav" 900 by 500 with items:
		| Name  | Label | Panel      | Colour |
		| alpha | Alpha | alphaPanel | Red    |
		| beta  | Beta  | betaPanel  | Blue   |
	Then the pane of "nav" is open
	And the NavigationView "nav" has selected nothing

Scenario: Tapping the pane toggle shuts the pane and gives its room to the page
	Given the application shows a NavigationView named "nav" 900 by 500 with items:
		| Name  | Label | Panel      | Colour |
		| alpha | Alpha | alphaPanel | Red    |
		| beta  | Beta  | betaPanel  | Blue   |
	And the NavigationView "nav" selects "alpha"
	When the page "alphaPanel" of the NavigationView "nav" is captured as "open"
	And the pane toggle of the NavigationView "nav" is tapped
	And the page "alphaPanel" of the NavigationView "nav" is captured as "shut"
	Then the pane of "nav" is shut
	And the NavigationView page "alphaPanel" was wider in "shut" than in "open"
	And where the NavigationView page "alphaPanel" is in "shut" looked different in "open"
	And the region of "alphaPanel" is uniformly "Red"
	When the pane toggle of the NavigationView "nav" is tapped
	And the page "alphaPanel" of the NavigationView "nav" is captured as "reopened"
	Then the pane of "nav" is open
	And the NavigationView page "alphaPanel" was wider in "shut" than in "reopened"
	And the region of "alphaPanel" is uniformly "Red"

Scenario: Shutting a NavigationView's pane leaves the page it chose on the panel
	Given the application shows a NavigationView named "nav" 900 by 500 with items:
		| Name  | Label | Panel      | Colour |
		| alpha | Alpha | alphaPanel | Red    |
		| beta  | Beta  | betaPanel  | Blue   |
	And the NavigationView "nav" selects "alpha"
	When the pane of "nav" is closed
	And the frame is captured
	Then the pane of "nav" is shut
	And the NavigationView "nav" has selected "alpha"
	And the region of "alphaPanel" is uniformly "Red"
