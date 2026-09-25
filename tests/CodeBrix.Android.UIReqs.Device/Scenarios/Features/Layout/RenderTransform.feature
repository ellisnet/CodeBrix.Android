Feature: RenderTransform
	A RenderTransform must move, grow and turn what an element draws without changing where the
	layout put it - which is why every expectation here is about the ink inside a container
	that did not move.

Scenario: A TranslateTransform moves the ink by exactly its offset
	Given the application shows a Border named "slot" 400 by 300
	And the layout "slot" holds a Border named "box" with:
		| Property   | Value |
		| Width      | 100   |
		| Height     | 60    |
		| Background | Red   |
	When the frame is captured as "before"
	Then the region of "slot" has ink
	When the RenderTransform of "box" is set to "Translate 80,40"
	And the frame is captured as "after"
	Then the ink of "slot" moved by 80, 40 pixels between frames "before" and "after"
	And the region of "box" is uniformly "Red"

Scenario: A ScaleTransform grows the ink by exactly its factors
	Given the application shows a Border named "slot" 400 by 300
	And the layout "slot" holds a Border named "box" with:
		| Property              | Value   |
		| Width                 | 100     |
		| Height                | 60      |
		| Background            | Red     |
		| RenderTransformOrigin | 0.5,0.5 |
	When the frame is captured as "before"
	When the RenderTransform of "box" is set to "Scale 2,1.5"
	And the frame is captured as "after"
	Then the ink of "slot" scaled by 2.0 and 1.5 between frames "before" and "after"
	And the region of "box" is uniformly "Red"

Scenario: A RotateTransform turns the ink and empties the corners of its box
	Given the application shows a Border named "slot" 400 by 300
	And the layout "slot" holds a Border named "box" with:
		| Property              | Value   |
		| Width                 | 100     |
		| Height                | 60      |
		| Background            | Red     |
		| RenderTransformOrigin | 0.5,0.5 |
	When the frame is captured as "before"
	When the RenderTransform of "box" is set to "Rotate 30"
	And the frame is captured as "after"
	Then the ink of "slot" scaled by 1.17 and 1.70 between frames "before" and "after"
	And the corner pixels of "box" are the panel background

Scenario: Removing the RenderTransform puts the ink back where it was
	Given the application shows a Border named "slot" 400 by 300
	And the layout "slot" holds a Border named "box" with:
		| Property   | Value |
		| Width      | 100   |
		| Height     | 60    |
		| Background | Red   |
	When the frame is captured as "before"
	When the RenderTransform of "box" is set to "Translate 80,40"
	And the frame is captured as "moved"
	Then the ink of "slot" moved by 80, 40 pixels between frames "before" and "moved"
	When the RenderTransform of "box" is set to "None"
	And the frame is captured as "restored"
	Then the ink of "slot" moved by 0, 0 pixels between frames "before" and "restored"
	And the region of "slot" in frame "restored" is unchanged from frame "before"
