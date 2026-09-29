Feature: The PlotterView on Android
	Android-only (the Platform's PlotterView group, copied as the PlotterView group, covers the chart itself: painting,
	keys, the engine's touch bindings driven through the harness's injected pointers). This group fences what Android
	adds. The chart draws on the Android canvas supply of the add-in (a Canvas shown by a native Skia view), and REAL
	MotionEvents dispatched to the activity - one finger, two fingers, a mouse wheel - reach the chart through the
	framework's input router and the control's own pointer handlers, with the chart engine's DEFAULT touch binding
	(CodeBrix.Plotter's PanZoomTrackByTouch: one finger pans, two fingers pinch, a finger held still tracks).

Scenario: A Plotter draws through the Android canvas supply
	Given the application shows a Plotter named "plot" with the "Line" model and the default controller
	Then the drawing surface of the Plotter "plot" is the Android plotter canvas
	And the drawing surface of the Plotter "plot" has painted
	When the frame is captured
	Then the region of "plot" contains at least 0.5 percent "Red"

Scenario: A real finger dragged across the plot pans it with the default touch binding
	Given the application shows a Plotter named "plot" with the "Line" model and the default controller
	When the axes of the Plotter "plot" are remembered
	And a real finger drags 300 pixels to the left across the Plotter "plot"
	Then the x-axis of the Plotter "plot" has moved right, keeping its range

Scenario: Two real fingers spread apart zoom the plot in with the default touch binding
	Given the application shows a Plotter named "plot" with the "Line" model and the default controller
	When the axes of the Plotter "plot" are remembered
	And two real fingers spread 300 pixels apart across the Plotter "plot"
	Then the x-axis range of the Plotter "plot" is narrower than before

Scenario: A real finger held on the plot shows the tracker and lifting it hides it
	Given the application shows a Plotter named "plot" with the "Line" model and the default controller
	When a real finger is held on the centre of the plot area of the Plotter "plot"
	Then the Plotter "plot" shows its tracker
	When the real finger on the Plotter "plot" is lifted
	Then the Plotter "plot" shows no tracker

Scenario: A real mouse wheel over the plot zooms it in
	Given the application shows a Plotter named "plot" with the "Line" model and the default controller
	When the axes of the Plotter "plot" are remembered
	And the real mouse wheel turns one notch up over the centre of the Plotter "plot"
	Then the x-axis range of the Plotter "plot" is narrower than before
