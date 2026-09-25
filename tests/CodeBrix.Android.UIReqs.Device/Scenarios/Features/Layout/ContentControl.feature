Feature: A ContentControl measures its content with the width it has
	A ContentControl must offer its content the width it was given itself, not an unbounded one.
	Arrange is not enough: content that decides its own layout from the width it is offered - a
	panel that wraps, a tool bar that moves items into an overflow - has already made that
	decision by the time arrange says how wide the container really is, so a container that
	measures with an unbounded width silently disables every such decision and lets the content
	run off the edge.

Scenario: Content in a sized Grid cell is measured with the cell's width
	Given the application shows a Grid named "grid" with:
		| Property | Value  |
		| Width    | 600    |
		| Height   | 400    |
		| Rows     | Auto,* |
		| Columns  | *      |
	And the layout "grid" holds a MeasureProbe named "probe" with:
		| Property   | Value |
		| Row        | 0     |
		| Height     | 40    |
		| Background | Red   |
	When the frame is captured
	Then the measured width of "probe" is bounded
	And the measured width of "probe" is 600 device pixels

Scenario: Content inside a ContentControl in a sized Grid cell is measured with the cell's width
	Given the application shows a Grid named "grid" with:
		| Property | Value  |
		| Width    | 600    |
		| Height   | 400    |
		| Rows     | Auto,* |
		| Columns  | *      |
	And the layout "grid" holds a ContentControl named "host" with:
		| Property                   | Value   |
		| Row                        | 0       |
		| HorizontalContentAlignment | Stretch |
	And the layout "host" holds a MeasureProbe named "probe" with:
		| Property   | Value |
		| Height     | 40    |
		| Background | Red   |
	When the frame is captured
	Then the measured width of "probe" is bounded
	And the measured width of "probe" is 600 device pixels
	And "probe" is 600 by 40 device pixels

Scenario: Content inside a ContentControl that fills the panel is measured with the panel's width
	Given the application shows a Grid named "grid" with:
		| Property            | Value   |
		| HorizontalAlignment | Stretch |
		| VerticalAlignment   | Stretch |
		| Rows                | Auto,*  |
		| Columns             | *       |
	And the layout "grid" holds a ContentControl named "host" with:
		| Property                   | Value   |
		| Row                        | 0       |
		| HorizontalContentAlignment | Stretch |
	And the layout "host" holds a MeasureProbe named "probe" with:
		| Property   | Value |
		| Height     | 40    |
		| Background | Red   |
	When the frame is captured
	Then the measured width of "probe" is bounded
	And the measured width of "probe" is the width of the panel
