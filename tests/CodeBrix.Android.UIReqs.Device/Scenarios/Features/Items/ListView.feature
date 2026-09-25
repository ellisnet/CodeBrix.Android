Feature: ListView
	A ListView shows a collection one row at a time. A finger that lands on a row picks it out:
	the list says so, it paints the row it has picked, and a finger that drags the list moves
	the rows under it.

Scenario: A ListView draws a row for every item it holds
	Given the application shows a ListView named "list" with:
		| Property   | Value                     |
		| Width      | 320                       |
		| Height     | 300                       |
		| ItemLabels | Alpha, Beta, Gamma, Delta |
	When the frame is captured
	Then the ListView "list" holds 4 items
	And item 1 of the ListView "list" carries the text "Alpha"
	And item 4 of the ListView "list" carries the text "Delta"
	And item 1 of the ListView "list" has ink
	And item 4 of the ListView "list" has ink

Scenario: Tapping a row of a ListView selects it and paints it
	Given the application shows a ListView named "list" with:
		| Property   | Value                     |
		| Width      | 320                       |
		| Height     | 300                       |
		| ItemLabels | Alpha, Beta, Gamma, Delta |
	When the frame is captured as "unselected"
	And item 2 of the ListView "list" is tapped
	And the frame is captured as "selected"
	Then the SelectedIndex of the ListView "list" is 1
	And item 2 of the ListView "list" is selected
	And the SelectionChanged of the ListView "list" was raised 1 times
	And item 2 of the ListView "list" is filled with "Accent"
	And item 1 of the ListView "list" is not filled with "Accent"
	And item 2 of the ListView "list" looks different from item 1

Scenario: Tapping another row of a single-selection ListView moves the selection
	Given the application shows a ListView named "list" with:
		| Property   | Value                     |
		| Width      | 320                       |
		| Height     | 300                       |
		| ItemLabels | Alpha, Beta, Gamma, Delta |
	When item 2 of the ListView "list" is tapped
	And item 4 of the ListView "list" is tapped
	And the frame is captured
	Then the SelectedIndex of the ListView "list" is 3
	And the ListView "list" has 1 selected items
	And item 4 of the ListView "list" is selected
	And item 2 of the ListView "list" is not selected
	And item 4 of the ListView "list" is filled with "Accent"
	And item 2 of the ListView "list" is not filled with "Accent"
	And the SelectionChanged of the ListView "list" was raised 2 times

Scenario: A ListView in multiple selection mode keeps every row a finger picks
	Given the application shows a ListView named "list" with:
		| Property      | Value                     |
		| Width         | 320                       |
		| Height        | 300                       |
		| ItemLabels    | Alpha, Beta, Gamma, Delta |
		| SelectionMode | Multiple                  |
	When item 1 of the ListView "list" is tapped
	And item 3 of the ListView "list" is tapped
	And the frame is captured
	Then the ListView "list" has 2 selected items
	And item 1 of the ListView "list" is selected
	And item 3 of the ListView "list" is selected
	And item 2 of the ListView "list" is not selected

Scenario: A finger dragging a ListView upwards scrolls it
	Given the application shows a ListView named "list" with:
		| Property   | Value                                                          |
		| Width      | 320                                                            |
		| Height     | 240                                                            |
		| ItemLabels | 01, 02, 03, 04, 05, 06, 07, 08, 09, 10, 11, 12, 13, 14, 15, 16 |
	When item 3 of the ListView "list" is captured as "before the drag"
	And the ListView "list" is dragged up by 80 pixels
	And item 3 of the ListView "list" is captured as "after the drag"
	Then the ListView "list" has scrolled down by at least 30 pixels
	And item 3 of the ListView "list" had ink in "before the drag"
	And item 3 of the ListView "list" moved up from "before the drag" to "after the drag" by at least 30 pixels
	And where item 3 of the ListView "list" was in "before the drag" looks different in "after the drag"

Scenario: A ListView that has not been touched is still at the top
	Given the application shows a ListView named "list" with:
		| Property   | Value                                                          |
		| Width      | 320                                                            |
		| Height     | 240                                                            |
		| ItemLabels | 01, 02, 03, 04, 05, 06, 07, 08, 09, 10, 11, 12, 13, 14, 15, 16 |
	Then the ListView "list" has not scrolled

Scenario: A disabled ListView ignores a tap on a row
	Given the application shows a ListView named "list" with:
		| Property   | Value                     |
		| Width      | 320                       |
		| Height     | 300                       |
		| ItemLabels | Alpha, Beta, Gamma, Delta |
		| IsEnabled  | False                     |
	When the frame is captured as "before"
	And item 2 of the ListView "list" is tapped
	And the frame is captured as "after"
	Then the SelectedIndex of the ListView "list" is -1
	And the SelectionChanged of the ListView "list" was raised 0 times
	And the region of "list" in frame "after" is unchanged from frame "before"

Scenario: A ListView with no items draws nothing
	Given the application shows a ListView named "list" with:
		| Property | Value |
		| Width    | 320   |
		| Height   | 200   |
	When the frame is captured
	Then the ListView "list" holds 0 items
	And the region of "list" is blank
