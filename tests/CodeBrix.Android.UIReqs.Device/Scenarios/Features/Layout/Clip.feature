Feature: Clip
	A Clip must hide everything an element draws outside the clip rectangle, without changing
	the size the layout gave the element.

Scenario: A Clip hides the part of an element outside it
	Given the application shows a Border named "box" with:
		| Property   | Value       |
		| Width      | 400         |
		| Height     | 200         |
		| Background | Red         |
		| Clip       | 0,0,200,200 |
	When the frame is captured
	Then "box" is 400 by 200 device pixels
	And the left half of the region of "box" is uniformly "Red"
	And the right half of the region of "box" is blank

Scenario: Removing the Clip shows the whole element again
	Given the application shows a Border named "box" with:
		| Property   | Value       |
		| Width      | 400         |
		| Height     | 200         |
		| Background | Red         |
		| Clip       | 0,0,200,200 |
	When the frame is captured as "clipped"
	Then the right half of the region of "box" is blank
	When the Clip of "box" is set to "None"
	And the frame is captured as "whole"
	Then the region of "box" is uniformly "Red"
	And the region of "box" in frame "whole" differs from frame "clipped"

Scenario: A Clip does not change what the layout gave the element
	Given the application shows a Border named "slot" 400 by 300
	And the layout "slot" holds a Border named "box" with:
		| Property   | Value          |
		| Background | Red            |
		| Clip       | 100,80,200,140 |
	When the frame is captured
	Then "box" is 400 by 300 device pixels
	And the region of "box" has ink
	And the corner pixels of "box" are the panel background
