Feature: Margin, Padding and Alignment
	Margin must hold an element away from the edges of its slot, Padding must hold a Border's
	child away from the Border's own edges, and an Alignment must push an element against the
	edge it names instead of stretching it.

Scenario: A Margin holds an element away from every edge of its slot
	Given the application shows a Border named "slot" 400 by 300
	And the layout "slot" holds a Border named "box" with:
		| Property   | Value |
		| Background | Red   |
		| Margin     | 40    |
	When the frame is captured
	Then "box" is 320 by 220 device pixels
	And "box" is inset 40 pixels inside "slot"
	And the region of "box" is uniformly "Red"

Scenario: A Margin can hold an element away by a different amount on each side
	Given the application shows a Border named "slot" 400 by 300
	And the layout "slot" holds a Border named "box" with:
		| Property   | Value       |
		| Background | Red         |
		| Margin     | 10,20,30,40 |
	When the frame is captured
	Then "box" is 360 by 240 device pixels
	And the top left of "box" is 10, 20 inside "slot"
	And the region of "box" is uniformly "Red"

Scenario: Padding holds a Border's child away from the Border's own edges
	Given the application shows a Border named "box" with:
		| Property   | Value |
		| Width      | 400   |
		| Height     | 300   |
		| Background | Blue  |
		| Padding    | 30    |
	And the layout "box" holds a Border named "inner" with:
		| Property   | Value |
		| Background | Red   |
	When the frame is captured
	Then "inner" is 340 by 240 device pixels
	And "inner" is inset 30 pixels inside "box"
	And the region of "inner" is uniformly "Red"
	And the corner pixels of "box" are "Blue"

Scenario: A left HorizontalAlignment pushes an element against the left edge
	Given the application shows a Border named "slot" 400 by 300
	And the layout "slot" holds a Border named "box" with:
		| Property            | Value |
		| Width               | 120   |
		| Background          | Red   |
		| HorizontalAlignment | Left  |
	When the frame is captured
	Then "box" is 120 by 300 device pixels
	And "box" is aligned to the left of "slot"
	And the left half of the region of "slot" has ink

Scenario: A right HorizontalAlignment pushes an element against the right edge
	Given the application shows a Border named "slot" 400 by 300
	And the layout "slot" holds a Border named "box" with:
		| Property            | Value |
		| Width               | 120   |
		| Background          | Red   |
		| HorizontalAlignment | Right |
	When the frame is captured
	Then "box" is aligned to the right of "slot"
	And the right half of the region of "slot" has ink
	And the left half of the region of "slot" is blank

Scenario: A top VerticalAlignment pushes an element against the top edge
	Given the application shows a Border named "slot" 400 by 300
	And the layout "slot" holds a Border named "box" with:
		| Property          | Value |
		| Height            | 100   |
		| Background        | Red   |
		| VerticalAlignment | Top   |
	When the frame is captured
	Then "box" is 400 by 100 device pixels
	And "box" is aligned to the top of "slot"
	And the bottom half of the region of "slot" is blank

Scenario: A centre alignment leaves the same room on both sides
	Given the application shows a Border named "slot" 400 by 300
	And the layout "slot" holds a Border named "box" with:
		| Property            | Value  |
		| Width               | 120    |
		| Height              | 100    |
		| Background          | Red    |
		| HorizontalAlignment | Center |
		| VerticalAlignment   | Center |
	When the frame is captured
	Then "box" is 120 by 100 device pixels
	And "box" is aligned to the centre of "slot"
	And the top left of "box" is 140, 100 inside "slot"
	And the region of "box" is uniformly "Red"
