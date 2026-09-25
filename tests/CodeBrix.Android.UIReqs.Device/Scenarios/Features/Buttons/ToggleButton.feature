Feature: ToggleButton
	A ToggleButton remembers whether it is checked, says so on the panel by filling itself with
	the theme's accent colour, and reports each change once.

Scenario: Tapping a ToggleButton checks it and fills it with the accent colour
	Given the application shows a ToggleButton named "bold" with:
		| Property | Value |
		| Content  | Bold  |
		| Width    | 240   |
		| Height   | 80    |
	When the frame is captured as "unchecked"
	Then the toggle "bold" is not checked
	And the region of "bold" in frame "unchecked" does not contain "Accent"
	When "bold" is tapped
	And the frame is captured as "checked"
	Then the toggle "bold" is checked
	And the region of "bold" contains at least 50 percent "Accent"
	And the region of "bold" in frame "checked" differs from frame "unchecked"

Scenario: Tapping a checked ToggleButton clears it and restores its look
	Given the application shows a ToggleButton named "bold" with:
		| Property | Value |
		| Content  | Bold  |
		| Width    | 240   |
		| Height   | 80    |
	When the frame is captured as "first"
	And "bold" is tapped
	And "bold" is tapped
	And the frame is captured as "last"
	Then the toggle "bold" is not checked
	And the region of "bold" does not contain "Accent"
	And the region of "bold" in frame "last" is unchanged from frame "first"

Scenario: A ToggleButton reports each change once
	Given the application shows a ToggleButton named "bold" with:
		| Property | Value |
		| Content  | Bold  |
		| Width    | 240   |
		| Height   | 80    |
	When "bold" is tapped
	And "bold" is tapped
	Then the Checked of "bold" was raised 1 times
	And the Unchecked of "bold" was raised 1 times
	And the Click of "bold" was raised 2 times

Scenario: A disabled ToggleButton ignores a tap
	Given the application shows a ToggleButton named "bold" with:
		| Property  | Value |
		| Content   | Bold  |
		| Width     | 240   |
		| Height    | 80    |
		| IsEnabled | False |
	When "bold" is tapped
	Then the toggle "bold" is not checked
	And the Checked of "bold" was raised 0 times
