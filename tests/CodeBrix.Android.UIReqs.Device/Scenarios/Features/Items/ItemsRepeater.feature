Feature: ItemsRepeater
	An ItemsRepeater realises an element for every item of its source and lays them out. It
	adds nothing of its own - no selection, no container chrome - so what a scenario sees on
	the panel is exactly the elements the items are.

Scenario: An ItemsRepeater realises an element for every item of its source
	Given the application shows an ItemsRepeater named "feed" with:
		| Property   | Value              |
		| Width      | 200                |
		| Height     | 240                |
		| ItemColors | Lime, Navy, Orange |
	When the frame is captured
	Then the ItemsRepeater "feed" holds 3 items
	And the region of "feed" contains "Lime"
	And the region of "feed" contains "Navy"
	And the region of "feed" contains "Orange"

Scenario: An ItemsRepeater draws a label for every text item of its source
	Given the application shows an ItemsRepeater named "feed" with:
		| Property   | Value              |
		| Width      | 200                |
		| Height     | 240                |
		| ItemLabels | Alpha, Beta, Gamma |
	When the frame is captured
	Then the ItemsRepeater "feed" holds 3 items
	And the region of "feed" has ink
