Feature: RadioButton
	RadioButtons that share a group name are a choice of one: selecting any of them clears the
	one that was selected before, and a button in another group is none of their business.

Scenario: Tapping a RadioButton selects it and repaints it
	Given the application shows a RadioButton group with:
		| Name  | Group   | Content |
		| red   | colours | Red     |
		| blue  | colours | Blue    |
	When the frame is captured as "none"
	Then the toggle "red" is not checked
	When "red" is tapped
	And the frame is captured as "red chosen"
	Then the toggle "red" is checked
	And the Checked of "red" was raised 1 times
	And the region of "red" in frame "red chosen" differs from frame "none"

Scenario: Selecting a RadioButton clears the other one in its group
	Given the application shows a RadioButton group with:
		| Name  | Group   | Content |
		| red   | colours | Red     |
		| blue  | colours | Blue    |
	When "red" is tapped
	And the frame is captured as "red chosen"
	Then the toggle "red" is checked
	When "blue" is tapped
	And the frame is captured as "blue chosen"
	Then the toggle "blue" is checked
	And the toggle "red" is not checked
	And the Unchecked of "red" was raised 1 times
	And the region of "red" in frame "blue chosen" differs from frame "red chosen"

Scenario: A RadioButton in another group is left alone
	Given the application shows a RadioButton group with:
		| Name  | Group   | Content |
		| red   | colours | Red     |
		| blue  | colours | Blue    |
		| small | sizes   | Small   |
	When "red" is tapped
	And "small" is tapped
	Then the toggle "small" is checked
	And the toggle "red" is checked
	And the Unchecked of "red" was raised 0 times
