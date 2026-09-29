@needs-plotterview
Feature: Plotter interaction
	A Plotter must let a person move around the chart: the plus key zooms the axes in, the Home
	key puts them back, an arrow key slides the view along without changing how much of it is
	shown, a finger dragged across the plot pans it, two fingers spread apart zoom it, a finger
	held on it reports the nearest data point when touch is bound to the tracker, and the zoom
	rectangle an application asks for is painted while it is wanted and gone afterwards.

	Deliberately out of scope here: everything that needs a mouse. This is a touch and keyboard
	panel, the control asks whether a pointer is a finger before anything else, and so the
	left-click tracker, the middle-drag zoom rectangle, the wheel zoom and the double-click
	reset are unreachable by design rather than untested. What a touch panel reaches instead is
	what these scenarios state: the touch bindings, the keys, and the view's own methods.

Scenario: The plus key zooms the axes in
	Given the font "ms-appx:///CodeBrix.Platform.Fonts.OpenSans/Fonts/OpenSans.ttf" is warm
	And the application shows a Grid named "cell" with:
		| Property | Value |
		| Width    | 900   |
		| Height   | 600   |
	And the layout "cell" holds a Plotter named "plot" with:
		| Property | Value |
		| Model    | Line  |
	And the keyboard focus is given to "plot"
	When the frame is captured as "before the key"
	And the axes of "plot" are remembered as "before the key"
	And the key "Add" is pressed
	And the frame is captured as "after the key"
	Then the x-axis range of "plot" is narrower than in "before the key"
	And the region of "plot" in frame "after the key" differs from frame "before the key"
	And the plot "plot" reported no render error

Scenario: The Home key puts the axes back where they started
	Given the font "ms-appx:///CodeBrix.Platform.Fonts.OpenSans/Fonts/OpenSans.ttf" is warm
	And the application shows a Grid named "cell" with:
		| Property | Value |
		| Width    | 900   |
		| Height   | 600   |
	And the layout "cell" holds a Plotter named "plot" with:
		| Property | Value |
		| Model    | Line  |
	And the keyboard focus is given to "plot"
	When the frame is captured as "before the zoom"
	And the axes of "plot" are remembered as "initial"
	And the key "Add" is pressed
	And the key "Add" is pressed
	And the frame is captured as "zoomed"
	Then the x-axis range of "plot" is narrower than in "initial"
	When the key "Home" is pressed
	And the frame is captured as "after the reset"
	Then the axes of "plot" are back to their initial range
	And the region of "plot" in frame "after the reset" is unchanged from frame "before the zoom"
	And the plot "plot" reported no render error

Scenario: The left arrow key slides the view along without changing the zoom
	Given the font "ms-appx:///CodeBrix.Platform.Fonts.OpenSans/Fonts/OpenSans.ttf" is warm
	And the application shows a Grid named "cell" with:
		| Property | Value |
		| Width    | 900   |
		| Height   | 600   |
	And the layout "cell" holds a Plotter named "plot" with:
		| Property | Value |
		| Model    | Line  |
	And the keyboard focus is given to "plot"
	When the frame is captured as "before the key"
	And the axes of "plot" are remembered as "before the key"
	And the key "Left" is pressed
	And the frame is captured as "after the key"
	Then the x-axis of "plot" has shifted from "before the key"
	And the x-axis range of "plot" is as wide as in "before the key"
	And the region of "plot" in frame "after the key" differs from frame "before the key"
	And the plot "plot" reported no render error

Scenario: A finger dragged across the plot pans it
	Given the font "ms-appx:///CodeBrix.Platform.Fonts.OpenSans/Fonts/OpenSans.ttf" is warm
	And the application shows a Grid named "cell" with:
		| Property | Value |
		| Width    | 900   |
		| Height   | 600   |
	And the layout "cell" holds a Plotter named "plot" with:
		| Property   | Value     |
		| Model      | Line      |
		| Controller | touch-pan |
	When the frame is captured as "before the drag"
	And the axes of "plot" are remembered as "before the drag"
	And a finger drags 200, 0 pixels across the plot area of "plot"
	And the frame is captured as "after the drag"
	Then the x-axis of "plot" has shifted from "before the drag"
	And the x-axis range of "plot" is as wide as in "before the drag"
	And the region of "plot" in frame "after the drag" differs from frame "before the drag"
	And the plot "plot" reported no render error

Scenario: Two fingers spread apart zoom the plot in
	Given the font "ms-appx:///CodeBrix.Platform.Fonts.OpenSans/Fonts/OpenSans.ttf" is warm
	And the application shows a Grid named "cell" with:
		| Property | Value |
		| Width    | 900   |
		| Height   | 600   |
	And the layout "cell" holds a Plotter named "plot" with:
		| Property   | Value     |
		| Model      | Line      |
		| Controller | touch-pan |
	When the frame is captured as "before the pinch"
	And the axes of "plot" are remembered as "before the pinch"
	And two fingers spread 240 pixels apart across the plot area of "plot"
	And the frame is captured as "after the pinch"
	Then the x-axis range of "plot" is narrower than in "before the pinch"
	And the region of "plot" in frame "after the pinch" differs from frame "before the pinch"
	And the plot "plot" reported no render error

Scenario: A finger held on the plot shows the tracker when touch is bound to it
	Given the font "ms-appx:///CodeBrix.Platform.Fonts.OpenSans/Fonts/OpenSans.ttf" is warm
	And the application shows a Grid named "cell" with:
		| Property | Value |
		| Width    | 900   |
		| Height   | 600   |
	And the layout "cell" holds a Plotter named "plot" with:
		| Property          | Value         |
		| Model             | Line          |
		| Controller        | touch-tracker |
		| TrackerBackground | Magenta       |
	When the frame is captured as "before the touch"
	Then the region of "plot" does not contain "Magenta"
	When a finger is put down at the centre of the plot area of "plot"
	And the frame is captured as "with the tracker"
	Then the region of "plot" contains at least 0.2 percent "Magenta"
	When that finger is lifted
	And the frame is captured as "after the touch"
	Then the region of "plot" in frame "after the touch" does not contain "Magenta"
	And the plot "plot" reported no render error

Scenario: The zoom rectangle is painted while it is wanted and gone afterwards
	Given the font "ms-appx:///CodeBrix.Platform.Fonts.OpenSans/Fonts/OpenSans.ttf" is warm
	And the application shows a Grid named "cell" with:
		| Property | Value |
		| Width    | 900   |
		| Height   | 600   |
	And the layout "cell" holds a Plotter named "plot" with:
		| Property          | Value |
		| Model             | Line  |
		| ZoomRectangleFill | Cyan  |
	When the frame is captured as "before the rectangle"
	Then the region of "plot" does not contain "Cyan"
	When a zoom rectangle 400 by 300 is shown at the centre of the plot area of "plot"
	And the frame is captured as "with the rectangle"
	Then the zoom rectangle area of "plot" is uniformly "Cyan"
	When the zoom rectangle of "plot" is hidden
	And the frame is captured as "after the rectangle"
	Then the region of "plot" in frame "after the rectangle" does not contain "Cyan"
	And the region of "plot" in frame "after the rectangle" is unchanged from frame "before the rectangle"
	And the plot "plot" reported no render error
