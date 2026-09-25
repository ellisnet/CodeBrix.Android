@needs-graphics2dsk
Feature: Graphics2DSK placement
	An SKCanvasElement is an ordinary element of the tree that happens to paint itself with
	SkiaSharp. Its drawing starts at its own top left wherever the layout puts it, it cannot
	reach outside the area it was given, and everything the tree does to an element - a container
	that clips it, an Opacity, a RenderTransform - happens to the drawing too. The canvas it is
	handed is not cleared first: whatever was already painted underneath is still there where the
	element paints nothing.

	Deliberately out of scope here: hover (this is a touch and keyboard panel), display scale
	(pinned at 1.0), and pointer input on the element - what a tap does with the drawing is the
	application's business, not the add-in's.

Scenario: A FillCanvas draws from its own top left, not the panel's
	Given the application shows a Canvas named "board" with:
		| Property | Value |
		| Width    | 900   |
		| Height   | 400   |
	And the layout "board" holds a FillCanvas named "painter" with:
		| Property | Value    |
		| Width    | 400      |
		| Height   | 300      |
		| Left     | 300      |
		| Top      | 50       |
		| Fill     | Red,Blue |
	When the frame is captured
	Then the top left of "painter" is 300, 50 inside "board"
	And the drawing area of "painter" was 400 by 300
	And the left half of the region of "painter" is uniformly "Red"
	And the right half of the region of "painter" is uniformly "Blue"
	And the corner pixels of "board" are the panel background

Scenario: A ClipProbeCanvas cannot draw outside the area it was given
	Given the application shows a Border named "board" with:
		| Property | Value |
		| Width    | 800   |
		| Height   | 600   |
	And the layout "board" holds a ClipProbeCanvas named "painter" with:
		| Property            | Value  |
		| Width               | 400    |
		| Height              | 300    |
		| HorizontalAlignment | Center |
		| VerticalAlignment   | Center |
		| Fill                | Lime   |
	When the frame is captured as "lime"
	Then "painter" is 400 by 300 device pixels
	And the region of "painter" is uniformly "Lime"
	And the region of "board" does not contain "Magenta"
	When the Fill of "painter" is set to "Blue"
	And "painter" is invalidated
	And the frame is captured as "blue"
	Then the region of "painter" is uniformly "Blue"
	And the region of "board" does not contain "Magenta"
	And nothing outside "painter" changed between frames "lime" and "blue"

Scenario: A FillCanvas is handed a canvas nobody cleared for it
	Given the application shows a Border named "backdrop" with:
		| Property   | Value  |
		| Width      | 400    |
		| Height     | 300    |
		| Background | Yellow |
	And the layout "backdrop" holds a FillCanvas named "painter" with:
		| Property | Value           |
		| Fill     | Red,Transparent |
	When the frame is captured
	Then "painter" is 400 by 300 device pixels
	And the left half of the region of "painter" is uniformly "Red"
	And the right half of the region of "painter" is uniformly "Yellow"

Scenario: A FillCanvas inside a ScrollViewer is clipped to what the ScrollViewer shows
	Given the application shows a Grid named "board" with:
		| Property | Value |
		| Width    | 400   |
		| Height   | 400   |
		| Rows     | 100,* |
	And the layout "board" holds a ScrollViewer named "window" with:
		| Property                    | Value  |
		| Row                         | 0      |
		| VerticalScrollBarVisibility | Hidden |
	And the layout "board" holds a Border named "below" with:
		| Property | Value |
		| Row      | 1     |
	And the layout "window" holds a FillCanvas named "painter" with:
		| Property | Value |
		| Width    | 400   |
		| Height   | 400   |
		| Fill     | Blue  |
	When the frame is captured
	Then "window" is 400 by 100 device pixels
	And "painter" is 400 by 400 device pixels
	And the region of "window" is uniformly "Blue"
	And the region of "below" is blank
	And the region of "below" does not contain "Blue"

Scenario: Opacity zero leaves nothing of the drawing on the panel
	Given the application shows a FillCanvas named "painter" with:
		| Property | Value |
		| Width    | 400   |
		| Height   | 300   |
		| Fill     | Blue  |
		| Opacity  | 0     |
	When the frame is captured
	Then "painter" is 400 by 300 device pixels
	And the region of "painter" is blank
	When the Opacity of "painter" is set to "1"
	And the frame is captured
	Then the region of "painter" is uniformly "Blue"

# This scenario found a framework defect on 2026-09-10: the drawing was painted at full strength
# however faint the element was asked to be, because the canvas visual never applied the opacity
# the compositor had worked out for it. It states the requirement the core suite already states
# for a Border ("Opacity composites an element over the panel behind it"), which is the same
# requirement here - an element that paints itself is still an element. Fixed at the source
# (the visual now draws the callback's output through a layer carrying the opacity); this
# scenario is the fence.
Scenario: Opacity fades the drawing as it fades any other element
	Given the application shows a FillCanvas named "painter" with:
		| Property | Value |
		| Width    | 400   |
		| Height   | 300   |
		| Fill     | Blue  |
	When the frame is captured as "opaque"
	Then the region of "painter" in frame "opaque" is uniformly "Blue"
	When the Opacity of "painter" is set to "0.5"
	And the frame is captured as "faded"
	Then the region of "painter" in frame "faded" differs from frame "opaque"
	And the region of "painter" in frame "faded" does not contain "Blue"
	And the region of "painter" is uniformly "#FF8080FF"

Scenario: A TranslateTransform moves the drawing
	Given the application shows a Border named "slot" 800 by 400
	And the layout "slot" holds a FillCanvas named "painter" with:
		| Property            | Value  |
		| Width               | 200    |
		| Height              | 100    |
		| HorizontalAlignment | Center |
		| VerticalAlignment   | Center |
		| Fill                | Red    |
	When the frame is captured as "still"
	Then the region of "slot" has ink
	And the region of "painter" is uniformly "Red"
	When the RenderTransform of "painter" is set to "Translate 200,0"
	And the frame is captured as "moved"
	Then the ink of "slot" moved by 200, 0 pixels between frames "still" and "moved"
	And the region of "painter" is uniformly "Red"
