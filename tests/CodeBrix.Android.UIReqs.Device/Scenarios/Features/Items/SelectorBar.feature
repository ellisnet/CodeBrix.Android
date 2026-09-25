Feature: SelectorBar
	A SelectorBar is a row of choices and nothing else: it carries no content of its own, so
	the requirement is that a tap moves its selection and tells the application, which is what
	puts the matching page on the panel.

Scenario: A SelectorBar starts on the choice the application selected
	Given the application shows a SelectorBar named "bar" with sections:
		| Header | Color |
		| One    | Lime  |
		| Two    | Navy  |
	When the frame is captured
	Then the SelectorBar "bar" holds 2 items
	And the SelectorBar "bar" has selected "One"
	And the region of "bar content" is uniformly "Lime"

Scenario: Tapping another choice of a SelectorBar swaps the page the application shows
	Given the application shows a SelectorBar named "bar" with sections:
		| Header | Color |
		| One    | Lime  |
		| Two    | Navy  |
	When the frame is captured as "first page"
	And header 2 of the SelectorBar "bar" is tapped
	And the frame is captured as "second page"
	Then the SelectorBar "bar" has selected "Two"
	And the SelectionChanged of the SelectorBar "bar" was raised 1 times
	And the region of "bar content" is uniformly "Navy"
	And the region of "bar content" in frame "second page" differs from frame "first page"
