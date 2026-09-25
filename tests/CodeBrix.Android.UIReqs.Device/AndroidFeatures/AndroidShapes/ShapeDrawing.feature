Feature: Native shape drawing
	Android-only (AP6; not a copy of a CodeBrix.Platform feature). Every Shape is one native view drawing the
	geometry Core built, the WinUI way: independent start and end caps, the Triangle cap, dash caps and the
	figure's own caps at dashed ends, a Fill brush mapped over the geometry's own bounds, a stroke that Stretch
	does not thicken, and a hit test on the fill and the stroke - not on the bounding box. The stage every shape
	sits in is 400 by 240 with nothing painted behind it.

Scenario: Every kind of shape is a native shape view
	Given the application shows one shape of every kind
	Then "rect" is shown by a native ShapeView
	And "oval" is shown by a native ShapeView
	And "rule" is shown by a native ShapeView
	And "poly" is shown by a native ShapeView
	And "zigzag" is shown by a native ShapeView
	And "figure" is shown by a native ShapeView

Scenario: A Line's start and end caps are drawn independently
	Given the stage shows a Red Line 40 thick from 40,120 to 340,120 with caps "Flat" and "Round"
	When the frame is captured
	Then the 10 by 10 block at 20, 115 inside "stage" is blank
	And the 6 by 6 block at 344, 117 inside "stage" is uniformly "Red"
	And the 4 by 4 block at 356, 101 inside "stage" is blank

Scenario: A Triangle cap comes to a point half the thickness past the end
	Given the stage shows a Red Line 40 thick from 40,120 to 340,120 with caps "Triangle" and "Triangle"
	When the frame is captured
	Then the 4 by 4 block at 348, 118 inside "stage" is uniformly "Red"
	And the 4 by 4 block at 352, 102 inside "stage" is blank
	And the 4 by 4 block at 24, 118 inside "stage" is uniformly "Red"

Scenario: A flat dash cap leaves the whole gap empty
	Given the stage shows a Red Line 20 thick from 40,120 to 360,120 dashed "2 2" with dash cap "Flat"
	When the frame is captured
	Then the 10 by 10 block at 55, 115 inside "stage" is uniformly "Red"
	And the 6 by 6 block at 82, 117 inside "stage" is blank
	And the 10 by 10 block at 95, 115 inside "stage" is blank

Scenario: A square dash cap reaches into the gap by half the thickness
	Given the stage shows a Red Line 20 thick from 40,120 to 360,120 dashed "2 2" with dash cap "Square"
	When the frame is captured
	Then the 6 by 6 block at 82, 117 inside "stage" is uniformly "Red"
	And the 6 by 6 block at 97, 117 inside "stage" is blank

Scenario: A dashed line keeps its own start cap at its start
	Given the stage shows a Red Line 20 thick from 60,120 to 360,120 dashed "2 2" with dash cap "Flat" and start cap "Round"
	When the frame is captured
	Then the 4 by 4 block at 52, 118 inside "stage" is uniformly "Red"
	And the 4 by 4 block at 40, 100 inside "stage" is blank

Scenario: A Polygon closes its figure and a Polyline leaves it open
	Given the stage shows a Lime Polygon beside a Blue Polyline of the same three-point shape
	When the frame is captured
	Then the 10 by 10 block at 95, 150 inside "stage" is uniformly "Lime"
	And the 10 by 10 block at 295, 195 inside "stage" is blank
	And the 4 by 4 block at 298, 58 inside "stage" is uniformly "Blue"

Scenario: A gradient Fill runs across the Path's own figure, not the whole shape
	Given the stage shows a Path filling the square 200,40 to 360,200 with a horizontal gradient from "Red" to "Blue"
	When the frame is captured
	Then the pixel at 203, 120 inside "stage" is nearer "Red" than "Blue"
	And the pixel at 356, 120 inside "stage" is nearer "Blue" than "Red"

Scenario: Stretch Fill scales the figure but not the stroke
	Given the stage shows a 10 by 10 square Path stretched to 300 by 150 with a 4 thick "Blue" stroke over a "Red" fill
	When the frame is captured
	Then the 10 by 10 block at 145, 70 inside "stage" is uniformly "Red"
	And the 4 by 20 block at 0, 60 inside "stage" is uniformly "Blue"
	And the 6 by 20 block at 6, 60 inside "stage" is uniformly "Red"

Scenario: Stretch None keeps the figure at its own size
	Given the stage shows a 50 by 50 square Path in a 300 by 150 shape with Stretch "None" and a "Red" fill
	When the frame is captured
	Then the 10 by 10 block at 20, 20 inside "stage" is uniformly "Red"
	And the 10 by 10 block at 100, 100 inside "stage" is blank

Scenario: A tap on an Ellipse's fill reaches the Ellipse
	Given the stage shows a Red Ellipse named "target" 200 by 200 at 100,20
	When the point 100, 100 inside "target" is tapped
	Then the Ellipse "target" was tapped 1 times

Scenario: A tap in the corner of an Ellipse's box does not reach the Ellipse
	Given the stage shows a Red Ellipse named "target" 200 by 200 at 100,20
	When the point 8, 8 inside "target" is tapped
	Then the Ellipse "target" was tapped 0 times

Scenario: A tap on a Line's stroke reaches the Line
	Given the stage shows a Red Line 40 thick from 40,120 to 340,120 with caps "Flat" and "Flat"
	When the point 190, 125 inside "stage" is tapped
	Then the Line "rule" was tapped 1 times

Scenario: A Viewbox scales its child to fit, all four quarters showing
	Given the application shows a 100 by 100 Viewbox named "box" holding a 200 by 200 grid of four colours
	When the frame is captured
	Then the 10 by 10 block at 20, 20 inside "box" is uniformly "Red"
	And the 10 by 10 block at 70, 20 inside "box" is uniformly "Lime"
	And the 10 by 10 block at 20, 70 inside "box" is uniformly "Blue"
	And the 10 by 10 block at 70, 70 inside "box" is uniformly "Yellow"

Scenario: A Border draws a different thickness on each side
	Given the application shows a Border named "frame" 200 by 100 with BorderThickness "4,10,20,40" in "Blue" over "Red"
	When the frame is captured
	Then the 2 by 10 block at 1, 40 inside "frame" is uniformly "Blue"
	And the 10 by 4 block at 90, 3 inside "frame" is uniformly "Blue"
	And the 10 by 10 block at 185, 30 inside "frame" is uniformly "Blue"
	And the 10 by 10 block at 90, 75 inside "frame" is uniformly "Blue"
	And the 10 by 10 block at 50, 30 inside "frame" is uniformly "Red"

Scenario: A Border rounds only the corners its CornerRadius names
	Given the application shows a Border named "frame" 200 by 100 with CornerRadius "40,0,0,0" in "Red"
	When the frame is captured
	Then the 4 by 4 block at 1, 1 inside "frame" is blank
	And the 4 by 4 block at 195, 1 inside "frame" is uniformly "Red"
	And the 4 by 4 block at 1, 95 inside "frame" is uniformly "Red"
