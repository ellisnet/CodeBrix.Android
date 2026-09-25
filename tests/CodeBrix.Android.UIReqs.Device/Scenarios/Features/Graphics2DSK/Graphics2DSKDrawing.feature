@needs-graphics2dsk
Feature: Graphics2DSK drawing
	An SKCanvasElement draws with SkiaSharp straight onto the frame the panel is showing: it is
	handed a canvas whose origin is its own top left and an area that is the size the layout gave
	it, and whatever it paints there is what a person sees. It paints as soon as it is shown,
	without being asked; it paints again when it is invalidated, and what it paints then is the
	state it has now, never that state added to the one before.

	Deliberately out of scope here: hover (this is a touch and keyboard panel), display scale
	(pinned at 1.0), and the animation idiom in which an element invalidates itself from inside
	its own drawing - an element that does that is dirty forever, and no frame the harness asks
	for would ever arrive. Every element in these scenarios is repainted by the scenario, never
	by itself.

Scenario: The drawing area is the size the layout gave the element
	Given the application shows a FillCanvas named "painter" with:
		| Property | Value |
		| Width    | 500   |
		| Height   | 200   |
		| Fill     | Blue  |
	When the frame is captured
	Then "painter" is 500 by 200 device pixels
	And the drawing area of "painter" was 500 by 200
	And the region of "painter" is uniformly "Blue"
	And the corner pixels of "painter" are "Blue"

Scenario: A FillCanvas draws exactly what it painted
	Given the application shows a FillCanvas named "painter" with:
		| Property | Value    |
		| Width    | 400      |
		| Height   | 300      |
		| Fill     | Red,Blue |
	When the frame is captured
	Then "painter" is 400 by 300 device pixels
	And the left half of the region of "painter" is uniformly "Red"
	And the right half of the region of "painter" is uniformly "Blue"

Scenario: A FillCanvas draws itself as soon as it is shown
	Given the application shows a FillCanvas named "painter" with:
		| Property | Value |
		| Width    | 400   |
		| Height   | 300   |
		| Fill     | Blue  |
	When the frame is captured
	Then the render count of "painter" is at least 1
	And the region of "painter" has ink
	And the region of "painter" is uniformly "Blue"

Scenario: Invalidating a FillCanvas draws it again with the state it has now
	Given the application shows a FillCanvas named "painter" with:
		| Property | Value |
		| Width    | 400   |
		| Height   | 300   |
		| Fill     | Blue  |
	When the frame is captured as "blue"
	Then the region of "painter" in frame "blue" is uniformly "Blue"
	When the Fill of "painter" is set to "Red"
	And "painter" is invalidated
	And the frame is captured as "red"
	Then the render count of "painter" has grown
	And the region of "painter" is uniformly "Red"
	And the region of "painter" in frame "red" differs from frame "blue"

Scenario: Resizing a FillCanvas draws it again at the new size
	Given the application shows a FillCanvas named "painter" with:
		| Property | Value |
		| Width    | 400   |
		| Height   | 300   |
		| Fill     | Blue  |
	When the frame is captured as "narrow"
	Then the drawing area of "painter" was 400 by 300
	When the Width of "painter" is set to "600"
	And the frame is captured as "wide"
	Then "painter" is 600 by 300 device pixels
	And the drawing area of "painter" was 600 by 300
	And the region of "painter" is uniformly "Blue"
	And the corner pixels of "painter" are "Blue"

Scenario: Two FillCanvas elements draw independently in the same frame
	Given the application shows a StackPanel named "board" with:
		| Property    | Value      |
		| Width       | 800        |
		| Height      | 300        |
		| Orientation | Horizontal |
	And the layout "board" holds a FillCanvas named "left" with:
		| Property | Value |
		| Width    | 400   |
		| Height   | 300   |
		| Fill     | Red   |
	And the layout "board" holds a FillCanvas named "right" with:
		| Property | Value |
		| Width    | 400   |
		| Height   | 300   |
		| Fill     | Blue  |
	When the frame is captured
	Then "left" sits directly left of "right"
	And the region of "left" is uniformly "Red"
	And the region of "right" is uniformly "Blue"
	When the Fill of "left" is set to "Lime"
	And "left" is invalidated
	And the frame is captured
	Then the region of "left" is uniformly "Lime"
	And the region of "right" is uniformly "Blue"
