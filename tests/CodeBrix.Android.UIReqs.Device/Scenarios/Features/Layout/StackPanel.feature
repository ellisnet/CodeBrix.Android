Feature: StackPanel stacking and spacing
	A StackPanel must lay its children out one after another along its Orientation, leave
	exactly its Spacing between them, and re-lay them out when the Orientation changes.

Scenario: A vertical StackPanel stacks its children top to bottom
	Given the application shows a StackPanel named "stack" with:
		| Property    | Value    |
		| Orientation | Vertical |
	And the layout "stack" holds a Border named "first" with:
		| Property   | Value |
		| Width      | 200   |
		| Height     | 80    |
		| Background | Red   |
	And the layout "stack" holds a Border named "second" with:
		| Property   | Value |
		| Width      | 200   |
		| Height     | 120   |
		| Background | Blue  |
	When the frame is captured
	Then "first" is 200 by 80 device pixels
	And "second" is 200 by 120 device pixels
	And "first" sits directly above "second"
	And the region of "first" is uniformly "Red"
	And the region of "second" is uniformly "Blue"

Scenario: A horizontal StackPanel stacks its children left to right
	Given the application shows a StackPanel named "stack" with:
		| Property    | Value      |
		| Orientation | Horizontal |
	And the layout "stack" holds a Border named "first" with:
		| Property   | Value |
		| Width      | 200   |
		| Height     | 80    |
		| Background | Red   |
	And the layout "stack" holds a Border named "second" with:
		| Property   | Value |
		| Width      | 120   |
		| Height     | 80    |
		| Background | Blue  |
	When the frame is captured
	Then "first" sits directly left of "second"
	And the region of "first" is uniformly "Red"
	And the region of "second" is uniformly "Blue"

Scenario: Spacing leaves a gap between vertically stacked children
	Given the application shows a StackPanel named "stack" with:
		| Property    | Value    |
		| Orientation | Vertical |
		| Spacing     | 40       |
	And the layout "stack" holds a Border named "first" with:
		| Property   | Value |
		| Width      | 200   |
		| Height     | 80    |
		| Background | Red   |
	And the layout "stack" holds a Border named "second" with:
		| Property   | Value |
		| Width      | 200   |
		| Height     | 120   |
		| Background | Blue  |
	When the frame is captured
	Then there is a gap of 40 pixels between "first" and "second"
	And the region of "stack" contains at least 15 percent "White"

Scenario: Spacing leaves a gap between horizontally stacked children
	Given the application shows a StackPanel named "stack" with:
		| Property    | Value      |
		| Orientation | Horizontal |
		| Spacing     | 60         |
	And the layout "stack" holds a Border named "first" with:
		| Property   | Value |
		| Width      | 200   |
		| Height     | 80    |
		| Background | Red   |
	And the layout "stack" holds a Border named "second" with:
		| Property   | Value |
		| Width      | 140   |
		| Height     | 80    |
		| Background | Blue  |
	When the frame is captured
	Then there is a gap of 60 pixels between "first" and "second"
	And the region of "stack" contains at least 12 percent "White"

Scenario: Changing the Orientation re-lays the children out
	Given the application shows a StackPanel named "stack" with:
		| Property    | Value    |
		| Orientation | Vertical |
	And the layout "stack" holds a Border named "first" with:
		| Property   | Value |
		| Width      | 200   |
		| Height     | 100   |
		| Background | Red   |
	And the layout "stack" holds a Border named "second" with:
		| Property   | Value |
		| Width      | 200   |
		| Height     | 100   |
		| Background | Blue  |
	When the frame is captured as "vertical"
	Then "first" sits directly above "second"
	When the Orientation of "stack" is set to "Horizontal"
	And the frame is captured as "horizontal"
	Then "first" sits directly left of "second"
	And the region of "first" is uniformly "Red"
	And the region of "second" is uniformly "Blue"
