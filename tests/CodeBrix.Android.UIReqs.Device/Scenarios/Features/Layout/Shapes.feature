Feature: Shape fill and stroke
	Rectangle, Ellipse, Line and Path must each fill their own geometry with their Fill and
	draw their outline in their Stroke - an Ellipse leaving its bounding box's corners empty
	and a Path drawing exactly the figure its data describes.

Scenario: A Rectangle is filled with its Fill
	Given the application shows a Rectangle named "box" with:
		| Property | Value |
		| Width    | 300   |
		| Height   | 200   |
		| Fill     | Red   |
	When the frame is captured
	Then "box" is 300 by 200 device pixels
	And the region of "box" is uniformly "Red"
	And the corner pixels of "box" are "Red"

Scenario: A Rectangle draws its outline in its Stroke
	Given the application shows a Rectangle named "box" with:
		| Property        | Value |
		| Width           | 300   |
		| Height          | 200   |
		| Fill            | Red   |
		| Stroke          | Blue  |
		| StrokeThickness | 20    |
	When the frame is captured
	Then the region of "box" contains at least 10 percent "Blue"
	And the region of "box" contains at least 40 percent "Red"
	And the corner pixels of "box" are "Blue"

Scenario: An Ellipse fills its middle and leaves its corners empty
	Given the application shows an Ellipse named "oval" with:
		| Property | Value |
		| Width    | 300   |
		| Height   | 200   |
		| Fill     | Red   |
	When the frame is captured
	Then "oval" is 300 by 200 device pixels
	And the corner pixels of "oval" are the panel background
	And the region of "oval" contains at least 60 percent "Red"
	And the top half of the region of "oval" contains at least 60 percent "Red"

Scenario: An Ellipse draws its outline in its Stroke
	Given the application shows an Ellipse named "oval" with:
		| Property        | Value |
		| Width           | 300   |
		| Height          | 200   |
		| Fill            | Red   |
		| Stroke          | Blue  |
		| StrokeThickness | 16    |
	When the frame is captured
	Then the region of "oval" contains at least 5 percent "Blue"
	And the region of "oval" contains at least 40 percent "Red"

Scenario: A Line draws ink in its Stroke colour
	Given the application shows a Line named "rule" with:
		| Property        | Value |
		| X1              | 0     |
		| Y1              | 0     |
		| X2              | 300   |
		| Y2              | 0     |
		| Stroke          | Red   |
		| StrokeThickness | 10    |
	When the frame is captured
	Then the region of "rule" has ink
	And the ink color of "rule" is "Red"

Scenario: A thicker StrokeThickness draws a thicker Line
	Given the application shows a Line named "rule" with:
		| Property        | Value |
		| X1              | 0     |
		| Y1              | 0     |
		| X2              | 300   |
		| Y2              | 0     |
		| Stroke          | Red   |
		| StrokeThickness | 6     |
	When the frame is captured as "thin"
	Then the region of "rule" has ink
	And the ink color of "rule" is "Red"
	When the StrokeThickness of "rule" is set to "20"
	And the frame is captured as "thick"
	Then the region of "rule" has ink
	And the ink color of "rule" is "Red"
	And the region of "rule" in frame "thick" holds more ink than in frame "thin"

Scenario: A Path draws exactly the figure its data describes
	Given the application shows a Path named "tri" with:
		| Property | Value                       |
		| Data     | M 0,0 L 200,0 L 100,150 Z   |
		| Fill     | Red                         |
	When the frame is captured
	Then the region of "tri" has ink
	And the ink color of "tri" is "Red"
	And the top half of the region of "tri" contains at least 60 percent "Red"
	And the bottom half of the region of "tri" contains at most 40 percent "Red"

Scenario: Changing a shape's Fill repaints it in the new colour
	Given the application shows a Rectangle named "box" with:
		| Property | Value |
		| Width    | 300   |
		| Height   | 200   |
		| Fill     | Red   |
	When the frame is captured as "red"
	Then the region of "box" is uniformly "Red"
	When the Fill of "box" is set to "Blue"
	And the frame is captured as "blue"
	Then the region of "box" is uniformly "Blue"
	And the region of "box" does not contain "Red"
	And the region of "box" in frame "blue" differs from frame "red"
