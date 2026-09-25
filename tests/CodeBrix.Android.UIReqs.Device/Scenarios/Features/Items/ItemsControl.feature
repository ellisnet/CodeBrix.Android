Feature: ItemsControl
	An ItemsControl is the plainest of the collection controls: it draws one element for every
	item it is given, in the order it was given them, and it has no notion of a selection at
	all - a tap on one of its items changes nothing.

Scenario: An ItemsControl draws one element for every item it holds
	Given the application shows an ItemsControl named "stack" with:
		| Property   | Value             |
		| Width      | 200               |
		| Height     | 240               |
		| ItemColors | Lime, Navy, Orange |
	When the frame is captured
	Then the ItemsControl "stack" holds 3 items
	And the region of "stack" contains "Lime"
	And the region of "stack" contains "Navy"
	And the region of "stack" contains "Orange"

Scenario: An ItemsControl draws the text of the items it is given
	Given the application shows an ItemsControl named "stack" with:
		| Property   | Value              |
		| Width      | 200                |
		| Height     | 240                |
		| ItemLabels | Alpha, Beta, Gamma |
	When the frame is captured
	Then the ItemsControl "stack" holds 3 items
	And the region of "stack" has ink

Scenario: Tapping an item of an ItemsControl changes nothing
	Given the application shows an ItemsControl named "stack" with:
		| Property   | Value              |
		| Width      | 200                |
		| Height     | 240                |
		| ItemColors | Lime, Navy, Orange |
	When the frame is captured as "before"
	And "stack" is tapped
	And the frame is captured as "after"
	Then the region of "stack" in frame "after" is unchanged from frame "before"
