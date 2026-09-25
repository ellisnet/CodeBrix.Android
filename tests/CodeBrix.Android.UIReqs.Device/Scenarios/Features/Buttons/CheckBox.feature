Feature: CheckBox
	A CheckBox says what it is by what is drawn inside its little box: nothing at all when it is
	clear, the theme's accent fill and a glyph when it is checked, and a glyph of its own when a
	three-state box is neither one nor the other.

Scenario: Tapping a CheckBox checks it and draws a glyph in its box
	Given the application shows a CheckBox named "agree" with:
		| Property | Value |
		| Content  | Agree |
	When the frame is captured
	Then the toggle "agree" is not checked
	And the box of the CheckBox "agree" shows no glyph
	And the box of the CheckBox "agree" is not filled with "Accent"
	When "agree" is tapped
	And the frame is captured
	Then the toggle "agree" is checked
	And the box of the CheckBox "agree" is filled with "Accent"
	And the box of the CheckBox "agree" shows a glyph

Scenario: Tapping a checked CheckBox clears it and takes the glyph away
	Given the application shows a CheckBox named "agree" with:
		| Property  | Value |
		| Content   | Agree |
		| IsChecked | True  |
	When the frame is captured
	Then the box of the CheckBox "agree" shows a glyph
	When "agree" is tapped
	And the frame is captured
	Then the toggle "agree" is not checked
	And the box of the CheckBox "agree" shows no glyph
	And the box of the CheckBox "agree" is not filled with "Accent"

Scenario: A CheckBox reports each change once
	Given the application shows a CheckBox named "agree" with:
		| Property | Value |
		| Content  | Agree |
	When "agree" is tapped
	And "agree" is tapped
	Then the Checked of "agree" was raised 1 times
	And the Unchecked of "agree" was raised 1 times

Scenario: A three-state CheckBox goes clear, checked, indeterminate and back
	Given the application shows a CheckBox named "agree" with:
		| Property     | Value |
		| Content      | Agree |
		| IsThreeState | True  |
	Then the toggle "agree" is not checked
	When "agree" is tapped
	Then the toggle "agree" is checked
	When "agree" is tapped
	Then the toggle "agree" is indeterminate
	When "agree" is tapped
	Then the toggle "agree" is not checked

Scenario: An indeterminate CheckBox draws a glyph of its own in its box
	Given the application shows a CheckBox named "agree" with:
		| Property     | Value         |
		| Content      | Agree         |
		| IsThreeState | True          |
		| IsChecked    | Indeterminate |
	When the frame is captured as "indeterminate"
	Then the toggle "agree" is indeterminate
	And the box of the CheckBox "agree" is filled with "Accent"
	And the box of the CheckBox "agree" shows a glyph
	When the IsChecked of "agree" is set to "False"
	And the frame is captured as "clear"
	Then the box of the CheckBox "agree" shows no glyph
	And the region of "NormalRectangle" in frame "indeterminate" differs from frame "clear"

Scenario: A disabled CheckBox ignores a tap
	Given the application shows a CheckBox named "agree" with:
		| Property  | Value |
		| Content   | Agree |
		| IsEnabled | False |
	When "agree" is tapped
	And the frame is captured
	Then the toggle "agree" is not checked
	And the box of the CheckBox "agree" is not filled with "Accent"
