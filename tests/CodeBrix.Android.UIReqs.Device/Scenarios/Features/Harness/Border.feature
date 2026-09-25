Feature: Border fill
	A Border that is given a Background must fill its own rectangle with that colour, at the
	size the tree says it has - which is what proves that geometry read from the visual tree and
	appearance read from the pixels agree.

Scenario: A Border is painted its Background colour
	Given the application shows a Border named "box" 400 by 300 with Background "Red"
	When the frame is captured
	Then "box" is 400 by 300 device pixels
	And the region of "box" is uniformly "Red"
	And the region of "box" does not contain "Blue"
