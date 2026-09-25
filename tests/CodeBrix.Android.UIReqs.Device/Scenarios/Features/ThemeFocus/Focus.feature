Feature: Focus
	The keyboard talks to exactly one control at a time, Tab moves it on to the next one, and the
	control it is talking to says so on the panel: a focus visual is drawn in the ring of pixels
	around the control, where nothing was drawn before.

Scenario: A control given the keyboard has it, and the others do not
	Given the application shows a StackPanel named "row" 600 by 200
	And the layout "row" holds a Button named "first" with:
		| Property | Value |
		| Content  | First |
	And the layout "row" holds a Button named "second" with:
		| Property | Value  |
		| Content  | Second |
	When the keyboard focus is given to "first"
	Then "first" has keyboard focus
	And "second" does not have keyboard focus

Scenario: Tab moves the keyboard on to the next control
	Given the application shows a StackPanel named "row" 600 by 200
	And the layout "row" holds a Button named "first" with:
		| Property | Value |
		| Content  | First |
	And the layout "row" holds a Button named "second" with:
		| Property | Value  |
		| Content  | Second |
	And the keyboard focus is given to "first"
	When the key "Tab" is pressed
	Then "second" has keyboard focus
	And "first" does not have keyboard focus

Scenario: A control that has the keyboard draws a focus visual around itself
	Given the application shows a Button named "go" with:
		| Property | Value |
		| Width    | 300   |
		| Height   | 120   |
		| Content  | Go    |
	When the frame is captured as "unfocused"
	And the keyboard focus is given to "go"
	And the frame is captured as "focused"
	Then "go" has keyboard focus
	And a focus visual is drawn around "go" in frame "focused" but not in frame "unfocused"

Scenario: The focus visual moves to the control Tab moved the keyboard to
	Given the application shows a StackPanel named "row" 600 by 260
	And the layout "row" holds a Button named "first" with:
		| Property | Value |
		| Content  | First |
		| Margin   | 20    |
	And the layout "row" holds a Button named "second" with:
		| Property | Value  |
		| Content  | Second |
		| Margin   | 20     |
	And the keyboard focus is given to "first"
	When the frame is captured as "onFirst"
	And the key "Tab" is pressed
	And the frame is captured as "onSecond"
	Then "second" has keyboard focus
	And a focus visual is drawn around "second" in frame "onSecond" but not in frame "onFirst"
	And a focus visual is drawn around "first" in frame "onFirst" but not in frame "onSecond"
