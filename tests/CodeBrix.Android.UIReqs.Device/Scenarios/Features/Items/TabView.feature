Feature: TabView
	A TabView is a strip of tabs over one page: the page belongs to the tab that is selected,
	and a tap on another tab puts that tab's page on the panel.

Scenario: A TabView shows the page of the tab that is selected
	Given the application shows a TabView named "tabs" with sections:
		| Header | Color |
		| One    | Lime  |
		| Two    | Navy  |
	When the frame is captured
	Then the TabView "tabs" holds 2 items
	And the SelectedIndex of the TabView "tabs" is 0
	And the region of "tabs" contains "Lime"
	And the region of "tabs" does not contain "Navy"

Scenario: Tapping another tab of a TabView swaps the page it shows
	Given the application shows a TabView named "tabs" with sections:
		| Header | Color |
		| One    | Lime  |
		| Two    | Navy  |
	When the frame is captured as "first page"
	And header 2 of the TabView "tabs" is tapped
	And the frame is captured as "second page"
	Then the SelectedIndex of the TabView "tabs" is 1
	And the SelectionChanged of the TabView "tabs" was raised 1 times
	And the region of "tabs" contains "Navy"
	And the region of "tabs" does not contain "Lime"
	And the region of "tabs" in frame "second page" differs from frame "first page"
