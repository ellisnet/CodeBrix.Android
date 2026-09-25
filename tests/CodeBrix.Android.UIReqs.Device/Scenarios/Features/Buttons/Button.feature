Feature: Button
	A Button is the simplest thing a finger can do on this panel, so its requirements are the
	strictest: it counts every tap, it acts when the finger is lifted rather than when it lands,
	it ignores a finger that never touched it, and when it is disabled it neither acts nor looks
	the way it did.

Scenario: A Button raises Click once for every tap
	Given the application shows a Button named "go" with:
		| Property | Value |
		| Content  | Go    |
		| Width    | 240   |
		| Height   | 80    |
	When "go" is tapped
	And "go" is tapped
	Then the Click of "go" was raised 2 times

Scenario: A Button acts when the finger is lifted, not when it lands
	Given the application shows a Button named "go" with:
		| Property | Value |
		| Content  | Go    |
		| Width    | 240   |
		| Height   | 80    |
	When a finger is pressed on "go"
	Then the Click of "go" was not raised
	When the finger on "go" is lifted
	Then the Click of "go" was raised once

Scenario: A tap that lands away from a Button leaves its Click unraised
	Given the application shows a Button named "go" with:
		| Property | Value |
		| Content  | Go    |
		| Width    | 240   |
		| Height   | 80    |
	When a point far from "go" is tapped
	Then the Click of "go" was not raised

Scenario: A disabled Button ignores a tap and does not look like an enabled one
	Given the application shows a Button named "go" with:
		| Property | Value        |
		| Content  | Save changes |
		| Width    | 500          |
		| Height   | 140          |
		| FontSize | 48           |
	When the frame is captured as "enabled"
	Then the button "go" is enabled
	And the region of "go" has ink
	When the IsEnabled of "go" is set to "False"
	And the frame is captured as "disabled"
	Then the button "go" is disabled
	And the region of "go" in frame "disabled" differs from frame "enabled"
	When "go" is tapped
	Then the Click of "go" was not raised

Scenario: A Button that is enabled again takes taps once more
	Given the application shows a Button named "go" with:
		| Property  | Value |
		| Content   | Go    |
		| Width     | 240   |
		| Height    | 80    |
		| IsEnabled | False |
	When "go" is tapped
	Then the Click of "go" was not raised
	When the IsEnabled of "go" is set to "True"
	And "go" is tapped
	Then the button "go" is enabled
	And the Click of "go" was raised once
