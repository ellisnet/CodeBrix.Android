@needs-skiasharpviews
Feature: SKXamlCanvas painting
	An SKXamlCanvas must show on the panel exactly what its PaintSurface handler drew, on a
	surface the size of the element, in the element's own coordinates. It paints itself as soon
	as it is shown and whenever it is resized; after any other change it paints when - and only
	when - it is invalidated, and it hands the handler the same pixel buffer as last time, so a
	handler that does not clear draws on top of the frame before it.

	Display scale is fixed at 1.0 on this panel, so nothing here is stated about
	IgnorePixelScaling, Dpi or the difference between pixels and device-independent units;
	that coverage belongs to the add-in's own unit tests, which can vary the scale.

Scenario: An SKXamlCanvas paints what its handler drew
	Given the application shows a SkiaCanvas named "sketch" with:
		| Property   | Value |
		| Width      | 400   |
		| Height     | 300   |
		| PaintColor | Red   |
	When the frame is captured
	Then "sketch" is 400 by 300 device pixels
	And the region of "sketch" is uniformly "Red"
	And the corner pixels of "sketch" are "Red"

Scenario: An SKXamlCanvas paints itself as soon as it is shown
	Given the application shows a SkiaCanvas named "sketch" with:
		| Property   | Value |
		| Width      | 400   |
		| Height     | 300   |
		| PaintColor | Lime  |
	When the frame is captured
	Then the PaintSurface of "sketch" was raised at least once
	And the region of "sketch" has ink
	And the region of "sketch" is uniformly "Lime"

Scenario: Nothing but Invalidate repaints an SKXamlCanvas
	Given the application shows a SkiaCanvas named "sketch" with:
		| Property   | Value |
		| Width      | 400   |
		| Height     | 300   |
		| PaintColor | Red   |
	When the frame is captured as "red"
	Then the region of "sketch" is uniformly "Red"
	When the PaintColor of "sketch" is set to "Blue"
	And the frame is captured as "not yet"
	Then the region of "sketch" in frame "not yet" is uniformly "Red"
	When "sketch" is invalidated
	And the frame is captured as "blue"
	Then the region of "sketch" is uniformly "Blue"
	And the region of "sketch" in frame "blue" differs from frame "red"

Scenario: The drawing lands where the handler put it
	Given the application shows a SkiaCanvas named "sketch" with:
		| Property   | Value     |
		| Width      | 400       |
		| Height     | 300       |
		| PaintColor | Red, Blue |
	When the frame is captured
	Then the leftmost 190 pixels of "sketch" are uniformly "Red"
	And the rightmost 190 pixels of "sketch" are uniformly "Blue"
	And the region of "sketch" does not contain "White"

Scenario: A handler that does not clear the surface leaves the previous frame behind
	Given the application shows a SkiaCanvas named "sketch" with:
		| Property   | Value |
		| Width      | 400   |
		| Height     | 300   |
		| PaintColor | Red   |
	When the frame is captured as "red"
	Then the region of "sketch" is uniformly "Red"
	When the handler of "sketch" no longer clears the surface
	And the handler of "sketch" draws only a 100 by 100 square of "Blue" at 150, 100
	And "sketch" is invalidated
	And the frame is captured as "square on red"
	Then the 60 by 60 block at 170, 120 inside "sketch" is uniformly "Blue"
	And the corner pixels of "sketch" are "Red"
	And the region of "sketch" contains at least 80 percent "Red"

Scenario: The surface the handler paints on is the size of the element
	Given the application shows a SkiaCanvas named "sketch" with:
		| Property   | Value |
		| Width      | 400   |
		| Height     | 300   |
		| PaintColor | Red   |
	When the frame is captured
	Then the surface the handler of "sketch" painted was 400 by 300 pixels
	And the canvas size of "sketch" is 400 by 300 pixels
	And the region of "sketch" is uniformly "Red"
	And the corner pixels of "sketch" are "Red"

Scenario: Resizing an SKXamlCanvas paints it again at its new size
	Given the application shows a SkiaCanvas named "sketch" with:
		| Property   | Value |
		| Width      | 400   |
		| Height     | 300   |
		| PaintColor | Red   |
	When the frame is captured as "narrow"
	Then the surface the handler of "sketch" painted was 400 by 300 pixels
	When the Width of "sketch" is set to "600"
	And the frame is captured as "wide"
	Then "sketch" is 600 by 300 device pixels
	And the surface the handler of "sketch" painted was 600 by 300 pixels
	And the region of "sketch" is uniformly "Red"
	And the corner pixels of "sketch" are "Red"
	And the region of "sketch" in frame "wide" differs from frame "narrow"

Scenario: What the handler's own shader drew reaches the panel as it was drawn
	Given the application shows a SkiaCanvas named "sketch" with:
		| Property | Value |
		| Width    | 400   |
		| Height   | 300   |
	When the handler of "sketch" paints a horizontal gradient from "Red" to "Blue"
	And "sketch" is invalidated
	And the frame is captured
	Then the region of "sketch" runs as a horizontal gradient from "Red" to "Blue"
	And the region of "sketch" does not contain "White"
