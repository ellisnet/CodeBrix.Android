Feature: Pivot
	A Pivot is a set of sections behind one row of headers: it shows the section whose header
	is selected, and a tap on another header puts that section on the panel instead.

Scenario: A Pivot shows the section whose header is selected
	Given the application shows a Pivot named "sections" with sections:
		| Header | Color |
		| One    | Lime  |
		| Two    | Navy  |
	When the frame is captured
	Then the Pivot "sections" holds 2 items
	And the SelectedIndex of the Pivot "sections" is 0
	And the region of "sections" contains "Lime"
	And the region of "sections" does not contain "Navy"

Scenario: Tapping another header of a Pivot swaps the section it shows
	Given the application shows a Pivot named "sections" with sections:
		| Header | Color |
		| One    | Lime  |
		| Two    | Navy  |
	When the frame is captured as "first section"
	And header 2 of the Pivot "sections" is tapped
	And the frame is captured as "second section"
	Then the SelectedIndex of the Pivot "sections" is 1
	And the SelectionChanged of the Pivot "sections" was raised 1 times
	And the region of "sections" contains "Navy"
	And the region of "sections" does not contain "Lime"
	And the region of "sections" in frame "second section" differs from frame "first section"
