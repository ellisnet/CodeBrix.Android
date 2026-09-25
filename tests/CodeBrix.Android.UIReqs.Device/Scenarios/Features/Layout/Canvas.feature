Feature: Canvas placement
	A Canvas must put each child exactly where its Canvas.Left and Canvas.Top say, leave a
	child with neither in its top left corner, move a child when those values change, and let
	children overlap in the order they were added.

Scenario: Canvas.Left and Canvas.Top place a child at an exact offset
	Given the application shows a Canvas named "board" 600 by 400
	And the layout "board" holds a Border named "tile" with:
		| Property   | Value |
		| Width      | 120   |
		| Height     | 80    |
		| Background | Red   |
		| Left       | 150   |
		| Top        | 90    |
	When the frame is captured
	Then "tile" is 120 by 80 device pixels
	And the top left of "tile" is 150, 90 inside "board"
	And the region of "tile" is uniformly "Red"

Scenario: A Canvas child with no Left or Top sits in the top left corner
	Given the application shows a Canvas named "board" 600 by 400
	And the layout "board" holds a Border named "tile" with:
		| Property   | Value |
		| Width      | 120   |
		| Height     | 80    |
		| Background | Red   |
	When the frame is captured
	Then the top left of "tile" is 0, 0 inside "board"
	And the region of "tile" is uniformly "Red"

Scenario: Changing Canvas.Left moves the child across the Canvas
	Given the application shows a Canvas named "board" 600 by 400
	And the layout "board" holds a Border named "tile" with:
		| Property   | Value |
		| Width      | 120   |
		| Height     | 80    |
		| Background | Red   |
		| Left       | 100   |
		| Top        | 90    |
	When the frame is captured as "before"
	Then the top left of "tile" is 100, 90 inside "board"
	When the Left of "tile" is set to "340"
	And the frame is captured as "after"
	Then the top left of "tile" is 340, 90 inside "board"
	And the region of "board" in frame "after" differs from frame "before"
	And the region of "tile" is uniformly "Red"

Scenario: Canvas children overlap in the order they were added
	Given the application shows a Canvas named "board" 600 by 400
	And the layout "board" holds a Border named "under" with:
		| Property   | Value |
		| Width      | 200   |
		| Height     | 200   |
		| Background | Red   |
		| Left       | 100   |
		| Top        | 100   |
	And the layout "board" holds a Border named "over" with:
		| Property   | Value |
		| Width      | 200   |
		| Height     | 200   |
		| Background | Blue  |
		| Left       | 200   |
		| Top        | 150   |
	When the frame is captured
	Then the region of "over" is uniformly "Blue"
	And the left half of the region of "under" is uniformly "Red"
