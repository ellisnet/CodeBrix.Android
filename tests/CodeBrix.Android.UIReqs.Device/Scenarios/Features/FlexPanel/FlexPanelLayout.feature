@needs-flexpanel
Feature: FlexPanel lines and sizes
	A FlexPanel must lay its children out along the axis its Direction names, size each one from
	the space it is given and the values it carries - Basis, Grow, Shrink - start a new line when
	Wrap allows it and the line is full, inset every line by its Padding, and give a collapsed
	child no space at all.

Scenario: A Row FlexPanel lays its children out left to right
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
		| Width      | 120   |
		| Height     | 80    |
		| Background | Blue  |
	When the frame is captured
	Then "first" is 200 by 80 device pixels
	And "second" is 120 by 80 device pixels
	And "first" sits directly left of "second"
	And "first" is aligned to the left of "flex"
	And the region of "first" is uniformly "Red"
	And the region of "second" is uniformly "Blue"

Scenario: A Column FlexPanel stacks its children top to bottom
	Given the application shows a FlexPanel named "flex" with:
		| Property  | Value  |
		| Width     | 800    |
		| Height    | 400    |
		| Direction | Column |
	And the layout "flex" holds a Border named "first" with:
		| Property   | Value |
		| Width      | 200   |
		| Height     | 80    |
		| Background | Red   |
	And the layout "flex" holds a Border named "second" with:
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

Scenario: RowReverse runs the same axis backwards
	Given the application shows a FlexPanel named "flex" with:
		| Property  | Value      |
		| Width     | 800        |
		| Height    | 400        |
		| Direction | RowReverse |
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
	Then "second" sits directly left of "first"
	And "first" is aligned to the right of "flex"
	And the region of "first" is uniformly "Red"
	And the region of "second" is uniformly "Blue"

Scenario: Grow gives a child the free space of the line
	Given the application shows a FlexPanel named "flex" with:
		| Property  | Value |
		| Width     | 800   |
		| Height    | 400   |
		| Direction | Row   |
	And the layout "flex" holds a Border named "fixed" with:
		| Property   | Value |
		| Width      | 200   |
		| Height     | 80    |
		| Background | Red   |
	And the layout "flex" holds a Border named "grower" with:
		| Property   | Value |
		| Height     | 80    |
		| Background | Blue  |
	And the child "grower" of "flex" has Grow "1"
	When the frame is captured
	Then "fixed" is 200 by 80 device pixels
	And "grower" is 600 by 80 device pixels
	And "fixed" sits directly left of "grower"
	And "grower" is aligned to the right of "flex"
	And the region of "grower" is uniformly "Blue"

Scenario: A Basis of 25 percent is a quarter of the main axis
	Given the application shows a FlexPanel named "flex" with:
		| Property  | Value |
		| Width     | 800   |
		| Height    | 400   |
		| Direction | Row   |
	And the layout "flex" holds a Border named "quarter" with:
		| Property   | Value |
		| Height     | 80    |
		| Background | Red   |
	And the child "quarter" of "flex" has Basis "25%"
	When the frame is captured
	Then "quarter" is 200 by 80 device pixels
	And the region of "quarter" is uniformly "Red"

Scenario: A Shrink of zero keeps a child at its basis while the line overflows
	Given the application shows a FlexPanel named "flex" with:
		| Property  | Value  |
		| Width     | 800    |
		| Height    | 400    |
		| Direction | Row    |
		| Wrap      | NoWrap |
	And the layout "flex" holds a Border named "stubborn" with:
		| Property   | Value |
		| Height     | 80    |
		| Background | Red   |
	And the child "stubborn" of "flex" has Basis "500"
	And the child "stubborn" of "flex" has Shrink "0"
	And the layout "flex" holds a Border named "shrinker" with:
		| Property   | Value |
		| Height     | 80    |
		| Background | Blue  |
	And the child "shrinker" of "flex" has Basis "500"
	When the frame is captured
	Then "stubborn" is 500 by 80 device pixels
	And "shrinker" is 300 by 80 device pixels
	And "stubborn" sits directly left of "shrinker"
	And the region of "stubborn" is uniformly "Red"
	And the region of "shrinker" is uniformly "Blue"

Scenario: Wrap starts a new line when the line is full
	Given the application shows a FlexPanel named "flex" with:
		| Property  | Value |
		| Width     | 800   |
		| Height    | 200   |
		| Direction | Row   |
		| Wrap      | Wrap  |
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
	Then "first" sits directly left of "second"
	And "first" sits directly above "third"
	And "third" is aligned to the left of "flex"
	And the region of "third" is uniformly "Green"

Scenario: Padding insets the children, and a child's own Padding insets its content
	Given the application shows a FlexPanel named "flex" with:
		| Property  | Value |
		| Width     | 800   |
		| Height    | 400   |
		| Direction | Row   |
		| Padding   | 40    |
	And the layout "flex" holds a Border named "first" with:
		| Property   | Value |
		| Width      | 200   |
		| Height     | 80    |
		| Background | Red   |
		| Padding    | 20    |
	And the layout "first" holds a Border named "inner" with:
		| Property   | Value |
		| Background | Blue  |
	When the frame is captured
	Then "first" is 200 by 80 device pixels
	And the top left of "first" is 40, 40 inside "flex"
	And the corner pixels of "flex" are the panel background
	And "inner" is 160 by 40 device pixels
	And "inner" is inset 20 pixels inside "first"
	And the corner pixels of "first" are "Red"
	And the region of "inner" is uniformly "Blue"

Scenario: A Collapsed child takes no space at all
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
	And the layout "flex" holds a Border named "hidden" with:
		| Property   | Value |
		| Width      | 200   |
		| Height     | 80    |
		| Background | Lime  |
	And the layout "flex" holds a Border named "last" with:
		| Property   | Value |
		| Width      | 200   |
		| Height     | 80    |
		| Background | Blue  |
	When the frame is captured as "all three"
	Then the region of "hidden" is uniformly "Lime"
	When the Visibility of "hidden" is set to "Collapsed"
	And the frame is captured
	Then "first" sits directly left of "last"
	And the region of "flex" does not contain "Lime"
	And the region of "last" is uniformly "Blue"
