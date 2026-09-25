Feature: ComboBox
	A ComboBox shows one item until it is asked for the rest: a tap opens a drop-down over the
	application, a tap on one of its items takes that item and puts the drop-down away again.

Scenario: A ComboBox shows the item it has selected and no drop-down
	Given the application shows a ComboBox named "picker" with:
		| Property      | Value              |
		| Width         | 260                |
		| ItemLabels    | Alpha, Beta, Gamma |
		| SelectedIndex | 0                  |
	When the frame is captured
	Then the ComboBox "picker" holds 3 items
	And the ComboBox "picker" is closed
	And no popup is open
	And the region of "picker" has ink

Scenario: Tapping a ComboBox opens its drop-down over the application
	Given the application shows a ComboBox named "picker" with:
		| Property   | Value              |
		| Width      | 260                |
		| ItemLabels | Alpha, Beta, Gamma |
	When the ComboBox "picker" is opened
	And the frame is captured
	Then the ComboBox "picker" is open
	And a popup is open
	And the open popup has ink
	And item 2 of the ComboBox "picker" carries the text "Beta"
	And item 2 of the ComboBox "picker" has ink

Scenario: Tapping an item of an open ComboBox selects it and closes the drop-down
	Given the application shows a ComboBox named "picker" with:
		| Property   | Value              |
		| Width      | 260                |
		| ItemLabels | Alpha, Beta, Gamma |
	When the ComboBox "picker" is opened
	And item 3 of the ComboBox "picker" is tapped
	Then the SelectedIndex of the ComboBox "picker" is 2
	And the SelectionChanged of the ComboBox "picker" was raised 1 times
	And the ComboBox "picker" is closed
	And no popup is open

Scenario: Tapping away from an open ComboBox closes it and leaves the selection alone
	Given the application shows a ComboBox named "picker" with:
		| Property      | Value              |
		| Width         | 260                |
		| ItemLabels    | Alpha, Beta, Gamma |
		| SelectedIndex | 0                  |
	When the ComboBox "picker" is opened
	And a point far from "picker" is tapped
	Then the ComboBox "picker" is closed
	And the SelectedIndex of the ComboBox "picker" is 0
	And no popup is open

Scenario: A disabled ComboBox does not open
	Given the application shows a ComboBox named "picker" with:
		| Property   | Value              |
		| Width      | 260                |
		| ItemLabels | Alpha, Beta, Gamma |
		| IsEnabled  | False              |
	When the frame is captured as "before"
	And the ComboBox "picker" is opened
	And the frame is captured as "after"
	Then the ComboBox "picker" is closed
	And no popup is open
	And the region of "picker" in frame "after" is unchanged from frame "before"
