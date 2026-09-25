@needs-skiasharpviews
Feature: SKXamlCanvas on the panel
	An SKXamlCanvas is an ordinary element of the tree as well as a Skia surface: it takes part
	in layout, it stops painting while it is collapsed and paints again when it is shown, the
	XAML children put inside it draw above its painting, a touch reaches it in the coordinates
	its handler draws in, and one canvas on the panel says nothing about another.

	These scenarios say nothing about hover: this is a touch and keyboard panel.

Scenario: A collapsed SKXamlCanvas paints nothing, and paints again when it is shown
	Given the application shows a SkiaCanvas named "sketch" with:
		| Property   | Value |
		| Width      | 400   |
		| Height     | 300   |
		| PaintColor | Red   |
	When the frame is captured as "shown"
	Then the region of "sketch" is uniformly "Red"
	When the Visibility of "sketch" is set to "Collapsed"
	And the frame is captured as "hidden"
	Then the panel is blank
	When the recorded events are forgotten
	And the PaintColor of "sketch" is set to "Blue"
	And "sketch" is invalidated
	And the frame is captured as "still hidden"
	Then the panel is blank
	And the PaintSurface of "sketch" was never raised
	When the Visibility of "sketch" is set to "Visible"
	And the frame is captured as "shown again"
	Then the PaintSurface of "sketch" was raised at least once
	And the region of "sketch" is uniformly "Blue"

Scenario: A child of an SKXamlCanvas draws above its painting
	Given the application shows a SkiaCanvas named "sketch" with:
		| Property   | Value |
		| Width      | 400   |
		| Height     | 300   |
		| PaintColor | Red   |
	And the layout "sketch" holds a Border named "label" with:
		| Property   | Value  |
		| Width      | 120    |
		| Height     | 60     |
		| Background | Yellow |
	When the frame is captured
	Then "label" is 120 by 60 device pixels
	And the top left of "label" is 0, 0 inside "sketch"
	And the region of "label" is uniformly "Yellow"
	And the bottom half of the region of "sketch" is uniformly "Red"

Scenario: A tap reaches an SKXamlCanvas in the coordinates its handler paints in
	Given the application shows a SkiaCanvas named "sketch" with:
		| Property   | Value |
		| Width      | 400   |
		| Height     | 300   |
		| PaintColor | Red   |
	When "sketch" is tapped
	Then the PointerPressed of "sketch" was raised at least once
	And the pointer of "sketch" was last pressed at 200, 150
	When the handler of "sketch" also draws a 40 by 40 square of "Blue" where the pointer was pressed
	And "sketch" is invalidated
	And the frame is captured
	Then the 20 by 20 block at 190, 140 inside "sketch" is uniformly "Blue"
	And the corner pixels of "sketch" are "Red"

Scenario: Two SKXamlCanvases paint independently of one another
	Given the application shows a Grid named "board" with:
		| Property | Value |
		| Width    | 800   |
		| Height   | 300   |
	And the layout "board" holds a SkiaCanvas named "left" with:
		| Property            | Value |
		| Width               | 400   |
		| Height              | 300   |
		| HorizontalAlignment | Left  |
		| PaintColor          | Red   |
	And the layout "board" holds a SkiaCanvas named "right" with:
		| Property            | Value |
		| Width               | 400   |
		| Height              | 300   |
		| HorizontalAlignment | Right |
		| PaintColor          | Blue  |
	When the frame is captured as "red and blue"
	Then "left" sits directly left of "right"
	And the region of "left" is uniformly "Red"
	And the region of "right" is uniformly "Blue"
	When the PaintColor of "left" is set to "Lime"
	And "left" is invalidated
	And the frame is captured as "lime and blue"
	Then the region of "left" is uniformly "Lime"
	And the region of "right" is uniformly "Blue"
	And the region of "right" in frame "lime and blue" is unchanged from frame "red and blue"
