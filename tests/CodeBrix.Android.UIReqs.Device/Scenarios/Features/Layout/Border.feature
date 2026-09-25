Feature: Border painting
	A Border must fill itself with its Background, draw its own frame in its BorderBrush at the
	BorderThickness it was given, and - when it is given a CornerRadius - leave its corners
	unpainted, so the panel still shows through them.

Scenario: A Border paints its Background inside its border
	Given the application shows a Border named "box" with:
		| Property        | Value |
		| Width           | 400   |
		| Height          | 300   |
		| Background      | Red   |
		| BorderBrush     | Blue  |
		| BorderThickness | 20    |
	When the frame is captured
	Then "box" is 400 by 300 device pixels
	And the region of "box" contains at least 60 percent "Red"
	And the region of "box" contains at least 15 percent "Blue"
	And the corner pixels of "box" are "Blue"

Scenario: A Border with no BorderThickness draws no border at all
	Given the application shows a Border named "box" with:
		| Property        | Value |
		| Width           | 400   |
		| Height          | 300   |
		| Background      | Red   |
		| BorderBrush     | Blue  |
		| BorderThickness | 0     |
	When the frame is captured
	Then the region of "box" is uniformly "Red"
	And the region of "box" does not contain "Blue"
	And the corner pixels of "box" are "Red"

Scenario: A thicker BorderThickness paints more of the Border
	Given the application shows a Border named "box" with:
		| Property        | Value |
		| Width           | 400   |
		| Height          | 300   |
		| Background      | Red   |
		| BorderBrush     | Blue  |
		| BorderThickness | 10    |
	When the frame is captured as "thin"
	Then the region of "box" contains at least 5 percent "Blue"
	When the BorderThickness of "box" is set to "40"
	And the frame is captured as "thick"
	Then the region of "box" contains at least 35 percent "Blue"
	And the region of "box" in frame "thick" differs from frame "thin"

Scenario: A CornerRadius leaves the corner pixels showing the panel
	Given the application shows a Border named "box" with:
		| Property     | Value |
		| Width        | 400   |
		| Height       | 300   |
		| Background   | Red   |
		| CornerRadius | 60    |
	When the frame is captured
	Then "box" is 400 by 300 device pixels
	And the corner pixels of "box" are the panel background
	And the region of "box" contains at least 80 percent "Red"

Scenario: A Border with no CornerRadius paints its corners
	Given the application shows a Border named "box" with:
		| Property   | Value |
		| Width      | 400   |
		| Height     | 300   |
		| Background | Red   |
	When the frame is captured
	Then the corner pixels of "box" are "Red"
	And the region of "box" is uniformly "Red"

Scenario: A Border with no Background lets the panel show through
	Given the application shows a Border named "box" with:
		| Property | Value |
		| Width    | 400   |
		| Height   | 300   |
	When the frame is captured
	Then "box" is 400 by 300 device pixels
	And the region of "box" is blank
