@needs-flexpanel
Feature: FlexPanel alignment and distribution
	A FlexPanel must distribute the free space of a line the way its JustifyContent asks, align
	its children across that line the way its AlignItems asks, let one child override that with
	AlignSelf, arrange the children in the order their Order values ask for rather than the order
	they were added in, and distribute whole lines the way its AlignContent asks.

Scenario: SpaceBetween puts the first child at the start and the last at the end
	Given the application shows a FlexPanel named "flex" with:
		| Property       | Value        |
		| Width          | 800          |
		| Height         | 400          |
		| Direction      | Row          |
		| JustifyContent | SpaceBetween |
	And the layout "flex" holds a Border named "first" with:
		| Property   | Value |
		| Width      | 200   |
		| Height     | 80    |
		| Background | Red   |
	And the layout "flex" holds a Border named "last" with:
		| Property   | Value |
		| Width      | 200   |
		| Height     | 80    |
		| Background | Blue  |
	When the frame is captured
	Then "first" is aligned to the left of "flex"
	And "last" is aligned to the right of "flex"
	And there is a gap of 400 pixels between "first" and "last"
	And the region of "first" is uniformly "Red"
	And the region of "last" is uniformly "Blue"

Scenario: Center centres the children on the main axis
	Given the application shows a FlexPanel named "flex" with:
		| Property       | Value  |
		| Width          | 800    |
		| Height         | 400    |
		| Direction      | Row    |
		| JustifyContent | Center |
	And the layout "flex" holds a Border named "first" with:
		| Property   | Value |
		| Width      | 200   |
		| Height     | 80    |
		| Background | Red   |
	And the layout "flex" holds a Border named "second" with:
		| Property   | Value |
		| Width      | 200   |
		| Height     | 80    |
		| Background | Blue  |
	When the frame is captured
	Then the top left of "first" is 200, 0 inside "flex"
	And "first" sits directly left of "second"
	And the region of "first" is uniformly "Red"
	And the region of "second" is uniformly "Blue"

Scenario: AlignItems Stretch fills the line with a child that has no height of its own
	Given the application shows a FlexPanel named "flex" with:
		| Property   | Value   |
		| Width      | 800     |
		| Height     | 400     |
		| Direction  | Row     |
		| AlignItems | Stretch |
	And the layout "flex" holds a Border named "stretchy" with:
		| Property   | Value |
		| Width      | 120   |
		| Background | Red   |
	When the frame is captured
	Then "stretchy" is 120 by 400 device pixels
	And "stretchy" is aligned to the top of "flex"
	And "stretchy" is aligned to the bottom of "flex"
	And the region of "stretchy" is uniformly "Red"

Scenario: AlignSelf End overrides AlignItems for one child
	Given the application shows a FlexPanel named "flex" with:
		| Property   | Value |
		| Width      | 800   |
		| Height     | 400   |
		| Direction  | Row   |
		| AlignItems | Start |
	And the layout "flex" holds a Border named "first" with:
		| Property   | Value |
		| Width      | 200   |
		| Height     | 80    |
		| Background | Red   |
	And the layout "flex" holds a Border named "odd" with:
		| Property   | Value |
		| Width      | 200   |
		| Height     | 80    |
		| Background | Blue  |
	And the child "odd" of "flex" has AlignSelf "End"
	When the frame is captured
	Then "first" is aligned to the top of "flex"
	And "odd" is aligned to the bottom of "flex"
	And the region of "first" is uniformly "Red"
	And the region of "odd" is uniformly "Blue"

Scenario: An Order of minus one moves a child to the front of the line
	Given the application shows a FlexPanel named "flex" with:
		| Property  | Value |
		| Width     | 800   |
		| Height    | 400   |
		| Direction | Row   |
	And the layout "flex" holds a Border named "first" with:
		| Property   | Value |
		| Width      | 200   |
		| Height     | 80    |
		| Background | Red   |
	And the layout "flex" holds a Border named "second" with:
		| Property   | Value |
		| Width      | 200   |
		| Height     | 80    |
		| Background | Blue  |
	And the layout "flex" holds a Border named "third" with:
		| Property   | Value |
		| Width      | 200   |
		| Height     | 80    |
		| Background | Green |
	When the frame is captured as "in the order they were added"
	Then "first" is aligned to the left of "flex"
	When the child "third" of "flex" has Order "-1"
	And the frame is captured
	Then "third" is aligned to the left of "flex"
	And "third" sits directly left of "first"
	And "first" sits directly left of "second"
	And the region of "third" is uniformly "Green"

Scenario: AlignContent SpaceBetween distributes the wrapped lines on the cross axis
	Given the application shows a FlexPanel named "flex" with:
		| Property     | Value        |
		| Width        | 800          |
		| Height       | 400          |
		| Direction    | Row          |
		| Wrap         | Wrap         |
		| AlignContent | SpaceBetween |
	And the layout "flex" holds a Border named "first" with:
		| Property   | Value |
		| Width      | 300   |
		| Height     | 100   |
		| Background | Red   |
	And the layout "flex" holds a Border named "second" with:
		| Property   | Value |
		| Width      | 300   |
		| Height     | 100   |
		| Background | Blue  |
	And the layout "flex" holds a Border named "third" with:
		| Property   | Value |
		| Width      | 300   |
		| Height     | 100   |
		| Background | Green |
	When the frame is captured
	Then "first" is aligned to the top of "flex"
	And "third" is aligned to the bottom of "flex"
	And the top left of "third" is 0, 300 inside "flex"
	And the region of "third" is uniformly "Green"
