Feature: Keyboard
	A control that has the keyboard can be worked without a finger: Enter and Space press the
	button the keyboard is on, a control that does not have the keyboard is left alone, and
	Escape takes back whatever was opened over the panel.

Scenario: Enter presses the Button the keyboard is on
	Given the application shows a Button named "go" with:
		| Property | Value |
		| Width    | 300   |
		| Height   | 120   |
		| Content  | Go    |
	And the keyboard focus is given to "go"
	When the key "Enter" is pressed
	Then the Click of "go" was raised once

Scenario: Space presses the Button the keyboard is on
	Given the application shows a Button named "go" with:
		| Property | Value |
		| Width    | 300   |
		| Height   | 120   |
		| Content  | Go    |
	And the keyboard focus is given to "go"
	When the key "Space" is pressed
	Then the Click of "go" was raised once

Scenario: A Button the keyboard is not on is left alone
	Given the application shows a StackPanel named "row" 600 by 200
	And the layout "row" holds a Button named "first" with:
		| Property | Value |
		| Content  | First |
	And the layout "row" holds a Button named "second" with:
		| Property | Value  |
		| Content  | Second |
	And the keyboard focus is given to "first"
	When the key "Enter" is pressed
	Then the Click of "first" was raised once
	And the Click of "second" was not raised

Scenario: Escape takes back an open flyout
	Given the application shows a DropDownButton named "menu" with a flyout panel named "panel" 240 by 160 painted "Red"
	When "menu" is tapped
	Then the flyout of "menu" is open
	And a popup is open
	When the key "Escape" is pressed
	Then the flyout of "menu" is closed
	And no popup is open
