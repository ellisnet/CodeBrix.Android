Feature: TeachingTip
	A TeachingTip says something about the application over the application: it draws nothing
	until it is opened, and what it draws while it is open is a panel of its own beside the
	page rather than inside it.

Scenario: A TeachingTip that is not open draws nothing
	Given the application shows a TeachingTip named "tip" with:
		| Property | Value                  |
		| Title    | Try this               |
		| Subtitle | A tip about the button |
	When the frame is captured
	Then the TeachingTip "tip" is closed
	And no popup is open
	And the panel is blank

Scenario: Opening a TeachingTip puts it over the application
	Given the application shows a TeachingTip named "tip" with:
		| Property | Value                  |
		| Title    | Try this               |
		| Subtitle | A tip about the button |
	When the TeachingTip "tip" is opened
	And the frame is captured
	Then the TeachingTip "tip" is open
	And the TeachingTip "tip" carries the title "Try this"
	And a popup is open
	And the open popup has ink

Scenario: Closing a TeachingTip takes it off the panel again
	Given the application shows a TeachingTip named "tip" with:
		| Property | Value                  |
		| Title    | Try this               |
		| Subtitle | A tip about the button |
	When the TeachingTip "tip" is opened
	Then a popup is open
	When the TeachingTip "tip" is dismissed
	And the frame is captured
	Then the TeachingTip "tip" is closed
	And no popup is open
	And the panel is blank
