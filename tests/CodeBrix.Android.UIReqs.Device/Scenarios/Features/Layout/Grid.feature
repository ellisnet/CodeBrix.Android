Feature: Grid rows and columns
	A Grid must give every row and column exactly the size its definition asks for - a pixel
	length verbatim, a star length as its share of what is left, an auto length as much as its
	content needs - and it must place each child in the cell it was assigned, painting nothing
	outside that cell.

Scenario: Pixel rows are exactly as tall as they ask to be
	Given the application shows a Grid named "grid" with:
		| Property | Value   |
		| Width    | 600     |
		| Height   | 400     |
		| Rows     | 100,300 |
		| Columns  | *       |
	And the layout "grid" holds a Border named "top" with:
		| Property   | Value |
		| Background | Red   |
		| Row        | 0     |
	And the layout "grid" holds a Border named "bottom" with:
		| Property   | Value |
		| Background | Blue  |
		| Row        | 1     |
	When the frame is captured
	Then "top" is 600 by 100 device pixels
	And "bottom" is 600 by 300 device pixels
	And "top" sits directly above "bottom"
	And the region of "top" is uniformly "Red"
	And the region of "bottom" is uniformly "Blue"

Scenario: Star rows share the height in the proportion they ask for
	Given the application shows a Grid named "grid" with:
		| Property | Value |
		| Width    | 600   |
		| Height   | 400   |
		| Rows     | *,3*  |
		| Columns  | *     |
	And the layout "grid" holds a Border named "small" with:
		| Property   | Value |
		| Background | Red   |
		| Row        | 0     |
	And the layout "grid" holds a Border named "large" with:
		| Property   | Value |
		| Background | Blue  |
		| Row        | 1     |
	When the frame is captured
	Then "small" is 600 by 100 device pixels
	And "large" is 600 by 300 device pixels
	And the region of "small" is uniformly "Red"
	And the region of "large" is uniformly "Blue"

Scenario: An Auto row is exactly as tall as its content
	Given the application shows a Grid named "grid" with:
		| Property | Value  |
		| Width    | 600    |
		| Height   | 400    |
		| Rows     | Auto,* |
		| Columns  | *      |
	And the layout "grid" holds a Border named "header" with:
		| Property   | Value |
		| Background | Red   |
		| Height     | 120   |
		| Row        | 0     |
	And the layout "grid" holds a Border named "body" with:
		| Property   | Value |
		| Background | Blue  |
		| Row        | 1     |
	When the frame is captured
	Then "header" is 600 by 120 device pixels
	And "body" is 600 by 280 device pixels
	And "header" sits directly above "body"
	And the region of "header" is uniformly "Red"

Scenario: Pixel columns are exactly as wide as they ask to be
	Given the application shows a Grid named "grid" with:
		| Property | Value   |
		| Width    | 600     |
		| Height   | 400     |
		| Rows     | *       |
		| Columns  | 150,450 |
	And the layout "grid" holds a Border named "left" with:
		| Property   | Value |
		| Background | Red   |
		| Column     | 0     |
	And the layout "grid" holds a Border named "right" with:
		| Property   | Value |
		| Background | Blue  |
		| Column     | 1     |
	When the frame is captured
	Then "left" is 150 by 400 device pixels
	And "right" is 450 by 400 device pixels
	And "left" sits directly left of "right"
	And the region of "left" is uniformly "Red"
	And the region of "right" is uniformly "Blue"

Scenario: Star columns share the width in the proportion they ask for
	Given the application shows a Grid named "grid" with:
		| Property | Value |
		| Width    | 600   |
		| Height   | 400   |
		| Rows     | *     |
		| Columns  | *,2*  |
	And the layout "grid" holds a Border named "narrow" with:
		| Property   | Value |
		| Background | Red   |
		| Column     | 0     |
	And the layout "grid" holds a Border named "wide" with:
		| Property   | Value |
		| Background | Blue  |
		| Column     | 1     |
	When the frame is captured
	Then "narrow" is 200 by 400 device pixels
	And "wide" is 400 by 400 device pixels
	And the region of "narrow" is uniformly "Red"
	And the region of "wide" is uniformly "Blue"

Scenario: An Auto column is exactly as wide as its content
	Given the application shows a Grid named "grid" with:
		| Property | Value  |
		| Width    | 600    |
		| Height   | 400    |
		| Rows     | *      |
		| Columns  | Auto,* |
	And the layout "grid" holds a Border named "side" with:
		| Property   | Value |
		| Background | Red   |
		| Width      | 220   |
		| Column     | 0     |
	And the layout "grid" holds a Border named "rest" with:
		| Property   | Value |
		| Background | Blue  |
		| Column     | 1     |
	When the frame is captured
	Then "side" is 220 by 400 device pixels
	And "rest" is 380 by 400 device pixels
	And "side" sits directly left of "rest"

Scenario: A row and a column together place a cell
	Given the application shows a Grid named "grid" with:
		| Property | Value |
		| Width    | 600   |
		| Height   | 400   |
		| Rows     | *,*   |
		| Columns  | *,*   |
	And the layout "grid" holds a Border named "corner" with:
		| Property   | Value |
		| Background | Red   |
		| Row        | 1     |
		| Column     | 1     |
	When the frame is captured
	Then "corner" is 300 by 200 device pixels
	And the top left of "corner" is 300, 200 inside "grid"
	And the region of "corner" is uniformly "Red"

Scenario: A cell paints its own rectangle and nothing else
	Given the application shows a Grid named "grid" with:
		| Property | Value |
		| Width    | 600   |
		| Height   | 400   |
		| Rows     | *,*   |
		| Columns  | *,*   |
	And the layout "grid" holds a Border named "topLeft" with:
		| Property   | Value |
		| Background | Red   |
		| Row        | 0     |
		| Column     | 0     |
	And the layout "grid" holds a Border named "topRight" with:
		| Property   | Value |
		| Background | Blue  |
		| Row        | 0     |
		| Column     | 1     |
	And the layout "grid" holds a Border named "bottomLeft" with:
		| Property   | Value |
		| Background | Green |
		| Row        | 1     |
		| Column     | 0     |
	And the layout "grid" holds a Border named "bottomRight" with:
		| Property   | Value  |
		| Background | Yellow |
		| Row        | 1      |
		| Column     | 1      |
	When the frame is captured
	Then the region of "topLeft" is uniformly "Red"
	And the region of "topLeft" does not contain "Blue"
	And the region of "topRight" is uniformly "Blue"
	And the region of "bottomLeft" is uniformly "Green"
	And the region of "bottomRight" is uniformly "Yellow"
	And the left half of the region of "grid" contains at least 45 percent "Red"
