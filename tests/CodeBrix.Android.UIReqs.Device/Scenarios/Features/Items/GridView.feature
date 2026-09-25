Feature: GridView
	A GridView is a ListView that lays its items out across the panel rather than down it: the
	second item sits beside the first, and a finger picks one out the same way.

Scenario: A GridView lays its items out side by side
	Given the application shows a GridView named "tiles" with:
		| Property   | Value                     |
		| Width      | 520                       |
		| Height     | 300                       |
		| ItemLabels | Alpha, Beta, Gamma, Delta |
	When the frame is captured
	Then the GridView "tiles" holds 4 items
	And item 2 of the GridView "tiles" is to the right of item 1
	And item 1 of the GridView "tiles" has ink
	And item 2 of the GridView "tiles" carries the text "Beta"

Scenario: Tapping a tile of a GridView selects it and paints it
	Given the application shows a GridView named "tiles" with:
		| Property   | Value                     |
		| Width      | 520                       |
		| Height     | 300                       |
		| ItemLabels | Alpha, Beta, Gamma, Delta |
	When the frame is captured as "unselected"
	And item 2 of the GridView "tiles" is tapped
	And the frame is captured as "selected"
	Then the SelectedIndex of the GridView "tiles" is 1
	And item 2 of the GridView "tiles" is selected
	And the SelectionChanged of the GridView "tiles" was raised 1 times
	And item 2 of the GridView "tiles" in frame "selected" differs from frame "unselected"
	And item 1 of the GridView "tiles" in frame "selected" is unchanged from frame "unselected"

Scenario: A GridView in multiple selection mode keeps every tile a finger picks
	Given the application shows a GridView named "tiles" with:
		| Property      | Value                     |
		| Width         | 520                       |
		| Height        | 300                       |
		| ItemLabels    | Alpha, Beta, Gamma, Delta |
		| SelectionMode | Multiple                  |
	When item 1 of the GridView "tiles" is tapped
	And item 2 of the GridView "tiles" is tapped
	Then the GridView "tiles" has 2 selected items
	And item 1 of the GridView "tiles" is selected
	And item 2 of the GridView "tiles" is selected

Scenario: A finger dragging a GridView upwards scrolls it
	Given the application shows a GridView named "tiles" with:
		| Property   | Value                                                                                                  |
		| Width      | 400                                                                                                    |
		| Height     | 140                                                                                                    |
		| ItemLabels | 01, 02, 03, 04, 05, 06, 07, 08, 09, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26 |
	When item 2 of the GridView "tiles" is captured as "before the drag"
	And the GridView "tiles" is dragged up by 70 pixels
	And item 2 of the GridView "tiles" is captured as "after the drag"
	Then the GridView "tiles" has scrolled down by at least 20 pixels
	And item 2 of the GridView "tiles" had ink in "before the drag"
	And item 2 of the GridView "tiles" moved up from "before the drag" to "after the drag" by at least 20 pixels
