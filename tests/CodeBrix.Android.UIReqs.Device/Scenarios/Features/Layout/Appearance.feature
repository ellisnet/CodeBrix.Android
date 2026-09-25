Feature: Opacity and Visibility
	Opacity must make an element show through to the panel behind it, and nothing at all at
	zero; Visibility must take a Collapsed element off the panel AND out of the layout, and put
	it back when it becomes Visible again.

Scenario: Opacity composites an element over the panel behind it
	Given the application shows a Border named "box" with:
		| Property   | Value |
		| Width      | 400   |
		| Height     | 300   |
		| Background | Red   |
		| Opacity    | 0.5   |
	When the frame is captured
	Then the region of "box" is uniformly "#FFFF8080"
	And the region of "box" does not contain "Red"

Scenario: Opacity zero leaves nothing of an element on the panel
	Given the application shows a Border named "box" with:
		| Property   | Value |
		| Width      | 400   |
		| Height     | 300   |
		| Background | Red   |
		| Opacity    | 0     |
	When the frame is captured
	Then "box" is 400 by 300 device pixels
	And the region of "box" is blank

Scenario: Restoring the Opacity brings the colour back
	Given the application shows a Border named "box" with:
		| Property   | Value |
		| Width      | 400   |
		| Height     | 300   |
		| Background | Red   |
		| Opacity    | 0     |
	When the frame is captured as "invisible"
	Then the region of "box" is blank
	When the Opacity of "box" is set to "1"
	And the frame is captured as "visible"
	Then the region of "box" is uniformly "Red"
	And the region of "box" in frame "visible" differs from frame "invisible"

Scenario: A Collapsed element is not painted
	Given the application shows a Border named "slot" 400 by 300
	And the layout "slot" holds a Border named "box" with:
		| Property   | Value |
		| Background | Red   |
	When the frame is captured as "visible"
	Then the region of "slot" is uniformly "Red"
	When the Visibility of "box" is set to "Collapsed"
	And the frame is captured as "collapsed"
	Then "box" is 0 by 0 device pixels
	And the region of "slot" is blank
	And the region of "slot" in frame "collapsed" differs from frame "visible"

Scenario: A Collapsed element takes no room in the layout
	Given the application shows a StackPanel named "stack" with:
		| Property    | Value    |
		| Orientation | Vertical |
	And the layout "stack" holds a Border named "first" with:
		| Property   | Value |
		| Width      | 200   |
		| Height     | 100   |
		| Background | Red   |
	And the layout "stack" holds a Border named "middle" with:
		| Property   | Value |
		| Width      | 200   |
		| Height     | 100   |
		| Background | Green |
	And the layout "stack" holds a Border named "last" with:
		| Property   | Value |
		| Width      | 200   |
		| Height     | 100   |
		| Background | Blue  |
	When the frame is captured
	Then there is a gap of 100 pixels between "first" and "last"
	When the Visibility of "middle" is set to "Collapsed"
	And the frame is captured
	Then "first" sits directly above "last"
	And the region of "first" is uniformly "Red"
	And the region of "last" is uniformly "Blue"

Scenario: Making an element Visible again puts it back
	Given the application shows a Border named "slot" 400 by 300
	And the layout "slot" holds a Border named "box" with:
		| Property   | Value     |
		| Background | Red       |
		| Visibility | Collapsed |
	When the frame is captured as "collapsed"
	Then the region of "slot" is blank
	When the Visibility of "box" is set to "Visible"
	And the frame is captured as "visible"
	Then "box" is 400 by 300 device pixels
	And the region of "slot" is uniformly "Red"
	And the region of "slot" in frame "visible" differs from frame "collapsed"
