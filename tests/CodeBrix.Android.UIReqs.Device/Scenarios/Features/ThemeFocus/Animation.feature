Feature: Animation
	A Storyboard changes a property over time. A fade started on a panel's Opacity runs for as
	long as it was given, is visibly part-way through in the middle of that, and leaves the
	panel at the value it was aimed at once it has finished.

Scenario: A fade to nothing leaves the panel invisible when it has finished
	Given the application shows a Border named "box" 400 by 300 with Background "Red"
	When the frame is captured as "before"
	And the Opacity of "box" is animated from 1 to 0 over 400 milliseconds
	And the animation is left to finish
	And the frame is captured as "after"
	Then the Opacity of "box" is 0
	And the region of "box" is blank
	And the region of "box" in frame "before" is uniformly "Red"
	And the animation reported that it finished

Scenario: A fade is part-way through in the middle of its duration
	Given the application shows a Border named "box" 400 by 300 with Background "Red"
	When the frame is captured as "before"
	And the Opacity of "box" is animated from 1 to 0 over 900 milliseconds
	And the animation is left running for 450 milliseconds
	And the frame is captured as "midway"
	Then the region of "box" in frame "midway" differs from frame "before"
	And the region of "box" has ink
	When the animation is left to finish
	And the frame is captured as "after"
	Then the Opacity of "box" is 0
	And the region of "box" is blank

Scenario: A fade in makes a panel that was invisible appear
	Given the application shows a Border named "box" with:
		| Property   | Value |
		| Width      | 400   |
		| Height     | 300   |
		| Background | Blue  |
		| Opacity    | 0     |
	When the frame is captured as "before"
	Then the region of "box" is blank
	When the Opacity of "box" is animated from 0 to 1 over 400 milliseconds
	And the animation is left to finish
	And the frame is captured as "after"
	Then the Opacity of "box" is 1
	And the region of "box" is uniformly "Blue"
	And the region of "box" in frame "after" differs from frame "before"

Scenario: An animation leaves the property where it was aimed, not where it started
	Given the application shows a Border named "box" 400 by 300 with Background "Red"
	When the Opacity of "box" is animated from 1 to 0.5 over 300 milliseconds
	And the animation is left to finish
	And the frame is captured
	Then the Opacity of "box" is 0.5
	And the region of "box" has ink
	And the region of "box" does not contain "Red"
