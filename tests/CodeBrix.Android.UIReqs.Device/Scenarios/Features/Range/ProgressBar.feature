Feature: ProgressBar
	A ProgressBar's whole job is to say how far along something is: the part of its track that
	is painted must be exactly as long as its Value says, both as the tree lays it out and as it
	reaches the panel. A bar that has no value to report animates instead.

Scenario: A ProgressBar paints as much of its track as its Value says
	Given the application shows a ProgressBar named "loading" with:
		| Property | Value |
		| Width    | 400   |
		| Value    | 40    |
	When the frame is captured
	Then the ProgressBar "loading" fills 40 percent of its width, within 2 pixels
	And the ProgressBar "loading" is painted 40 percent of the way across in "Accent", within 2 pixels

Scenario: Raising a ProgressBar's Value paints more of its track
	Given the application shows a ProgressBar named "loading" with:
		| Property | Value |
		| Width    | 400   |
		| Value    | 20    |
	When the frame is captured as "early"
	Then the ProgressBar "loading" is painted 20 percent of the way across in "Accent", within 2 pixels
	When the Value of "loading" is set to "80"
	And the frame is captured as "late"
	Then the ProgressBar "loading" is painted 80 percent of the way across in "Accent", within 2 pixels
	And the region of "loading" in frame "late" differs from frame "early"

Scenario: A ProgressBar at its Minimum paints none of its track
	Given the application shows a ProgressBar named "loading" with:
		| Property | Value |
		| Width    | 400   |
		| Value    | 0     |
	When the frame is captured
	Then the ProgressBar "loading" fills 0 percent of its width, within 2 pixels
	And the region of "loading" does not contain "Accent"

Scenario: An indeterminate ProgressBar paints an indicator of its own
	Given the application shows a ProgressBar named "loading" with:
		| Property        | Value |
		| Width           | 400   |
		| IsIndeterminate | True  |
	When the frame is captured
	Then the ProgressBar "loading" paints an indicator in "Accent" within 1500 milliseconds

Scenario: An indeterminate ProgressBar keeps repainting its track
	Given the application shows a ProgressBar named "loading" with:
		| Property        | Value |
		| Width           | 400   |
		| IsIndeterminate | True  |
	When the frame is captured as "first"
	And the ProgressBar "loading" is left running for 700 milliseconds
	And the frame is captured as "second"
	Then the region of "loading" in frame "second" differs from frame "first"

Scenario: A paused ProgressBar that goes away leaves the accent colour as it was
	Given the application shows a ProgressBar named "paused" with:
		| Property   | Value |
		| Width      | 400   |
		| Value      | 40    |
		| ShowPaused | True  |
	And the panel is left alone for 500 milliseconds
	And the frame is captured as "paused"
	Then the region of "paused" in frame "paused" does not contain "Accent"
	Given the application shows a ProgressBar named "loading" with:
		| Property | Value |
		| Width    | 400   |
		| Value    | 40    |
	When the frame is captured
	Then the ProgressBar "loading" is painted 40 percent of the way across in "Accent", within 2 pixels
