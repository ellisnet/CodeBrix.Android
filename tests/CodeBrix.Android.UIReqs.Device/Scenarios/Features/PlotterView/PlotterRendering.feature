@needs-plotterview
Feature: Plotter rendering
	A Plotter must draw the plot it is given across the whole of the cell it is laid out in,
	show nothing but the panel behind it while it has no plot, draw each series in the colour
	that series was given, paint the plot's own background over its whole client area, render
	the plot's text through the application's fonts, and redraw when the data behind it changes
	and the plot is invalidated.

	Deliberately out of scope here: hovering (this is a touch and keyboard panel, and the chart
	never sees a mouse), display scale (pinned at 1.0), and the streaming charts an application
	drives from a timer - a repaint with no completion signal to wait on is covered instead by
	the mutate-and-invalidate scenario, which has one.

Scenario: A Plotter given a model draws a plot
	Given the font "ms-appx:///CodeBrix.Platform.Fonts.OpenSans/Fonts/OpenSans.ttf" is warm
	And the application shows a Grid named "cell" with:
		| Property | Value |
		| Width    | 900   |
		| Height   | 600   |
	And the layout "cell" holds a Plotter named "plot"
	When the frame is captured as "before the model"
	Then the region of "plot" is blank
	When the Model of "plot" is set to "Line"
	And the frame is captured as "with the model"
	Then "plot" is 900 by 600 device pixels
	And the plot area of "plot" has a positive size
	And the region of "plot" has ink
	And the region of "plot" in frame "with the model" differs from frame "before the model"
	And the plot "plot" reported no render error

Scenario: A Plotter with no model shows nothing but the panel behind it
	Given the font "ms-appx:///CodeBrix.Platform.Fonts.OpenSans/Fonts/OpenSans.ttf" is warm
	And the application shows a Grid named "cell" with:
		| Property | Value |
		| Width    | 900   |
		| Height   | 600   |
	And the layout "cell" holds a Plotter named "plot"
	When the frame is captured
	Then "plot" is 900 by 600 device pixels
	And the region of "plot" is blank
	And the plot "plot" reported no render error

Scenario: The plot fills the cell the control was given
	Given the font "ms-appx:///CodeBrix.Platform.Fonts.OpenSans/Fonts/OpenSans.ttf" is warm
	And the application shows a Grid named "cell" with:
		| Property | Value |
		| Width    | 900   |
		| Height   | 600   |
	And the layout "cell" holds a Plotter named "plot" with:
		| Property | Value |
		| Model    | Line  |
	When the frame is captured
	Then "plot" is 900 by 600 device pixels
	And "plot" is inset 0 pixels inside "cell"
	And the plot of "plot" fills its client area
	And the plot area of "plot" has a positive size
	And the plot "plot" reported no render error

Scenario: A series draws in the colour it was given
	Given the font "ms-appx:///CodeBrix.Platform.Fonts.OpenSans/Fonts/OpenSans.ttf" is warm
	And the application shows a Grid named "cell" with:
		| Property | Value |
		| Width    | 900   |
		| Height   | 600   |
	And the layout "cell" holds a Plotter named "plot" with:
		| Property | Value |
		| Model    | Line  |
	When the frame is captured
	Then the region of "plot" contains at least 0.5 percent "Red"
	And the plot "plot" reported no render error

Scenario: The model's Background paints the whole plot
	Given the font "ms-appx:///CodeBrix.Platform.Fonts.OpenSans/Fonts/OpenSans.ttf" is warm
	And the application shows a Grid named "cell" with:
		| Property | Value |
		| Width    | 900   |
		| Height   | 600   |
	And the layout "cell" holds a Plotter named "plot" with:
		| Property | Value          |
		| Model    | Line on Yellow |
	When the frame is captured
	Then the region of "plot" contains at least 60 percent "Yellow"
	And the corner pixels of "plot" are "Yellow"
	And the region of "plot" contains at least 0.5 percent "Red"
	And the plot "plot" reported no render error

Scenario: Chart text is drawn through the application's own font
	Given the font "ms-appx:///CodeBrix.Platform.Fonts.OpenSans/Fonts/OpenSans.ttf" is warm
	And the application shows a Grid named "cell" with:
		| Property | Value |
		| Width    | 900   |
		| Height   | 600   |
	And the layout "cell" holds a Plotter named "plot" with:
		| Property | Value            |
		| Model    | Line in Magenta  |
	When the frame is captured
	Then the title of "plot" has ink
	And the ink color of the title of "plot" is "Magenta"
	And the plot "plot" reported no render error

Scenario: Changing the data and invalidating the plot redraws it
	Given the font "ms-appx:///CodeBrix.Platform.Fonts.OpenSans/Fonts/OpenSans.ttf" is warm
	And the application shows a Grid named "cell" with:
		| Property | Value |
		| Width    | 900   |
		| Height   | 600   |
	And the layout "cell" holds a Plotter named "plot" with:
		| Property | Value        |
		| Model    | Growing Line |
	When the frame is captured as "before the point"
	And the axes of "plot" are remembered as "before the point"
	And a point is added beyond the data of "plot" and the plot is invalidated
	And the frame is captured as "after the point"
	Then the x-axis maximum of "plot" is greater than in "before the point"
	And the region of "plot" in frame "after the point" differs from frame "before the point"
	And the plot "plot" reported no render error

Scenario: A bar, a scatter and a pie plot each draw a picture of their own
	Given the font "ms-appx:///CodeBrix.Platform.Fonts.OpenSans/Fonts/OpenSans.ttf" is warm
	And the application shows a Grid named "cell" with:
		| Property | Value |
		| Width    | 900   |
		| Height   | 600   |
	And the layout "cell" holds a Plotter named "plot" with:
		| Property | Value |
		| Model    | Bar   |
	When the frame is captured as "bars"
	Then the region of "plot" has ink
	And the region of "plot" contains at least 5 percent "Blue"
	And the plot "plot" reported no render error
	When the Model of "plot" is set to "Scatter"
	And the frame is captured as "markers"
	Then the region of "plot" in frame "markers" differs from frame "bars"
	And the region of "plot" contains at least 0.05 percent "Blue"
	And the plot "plot" reported no render error
	When the Model of "plot" is set to "Pie"
	And the frame is captured as "slices"
	Then the region of "plot" in frame "slices" differs from frame "markers"
	And the region of "plot" contains at least 5 percent "Red"
	And the plot "plot" reported no render error
