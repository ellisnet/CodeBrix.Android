Feature: Button tap
	A Button must react to a finger: tapping the middle of it raises Click, exactly once, and
	only when it is actually tapped.

Scenario: Tapping a Button raises its Click
	Given the application shows a Button named "go" with:
		| Property | Value |
		| Content  | Go    |
		| Width    | 240   |
		| Height   | 80    |
	When the frame is captured
	Then the region of "go" has ink
	And the Click of "go" was not raised
	When "go" is tapped
	Then the Click of "go" was raised once
