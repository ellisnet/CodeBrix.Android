Feature: ProgressRing
	A ProgressRing must show nothing at all until it is made active, and must put something on
	the panel once it is.

	The ring's own artwork is an animated visual, and playing one needs the animation add-in
	that this core suite deliberately does not reference, so these scenarios say only what is
	true of the control itself: whether it paints its rectangle at all, and that IsActive is
	what decides.

Scenario: An inactive ProgressRing draws nothing
	Given the application shows a ProgressRing named "busy" with:
		| Property | Value |
		| Width    | 120   |
		| Height   | 120   |
		| IsActive | False |
	When the frame is captured
	Then the region of "busy" is blank

Scenario: Activating a ProgressRing puts something in its rectangle
	Given the application shows a ProgressRing named "busy" with:
		| Property | Value |
		| Width    | 120   |
		| Height   | 120   |
		| IsActive | False |
	When the frame is captured as "idle"
	Then the region of "busy" is blank
	When the IsActive of "busy" is set to "True"
	And the ProgressRing "busy" is left running for 800 milliseconds
	And the frame is captured as "busy"
	Then the region of "busy" has ink
	And the region of "busy" in frame "busy" differs from frame "idle"

Scenario: A ProgressRing that stops being active clears its rectangle again
	Given the application shows a ProgressRing named "busy" with:
		| Property | Value |
		| Width    | 120   |
		| Height   | 120   |
		| IsActive | True  |
	When the frame is captured
	Then the region of "busy" has ink
	When the IsActive of "busy" is set to "False"
	And the frame is captured
	Then the region of "busy" is blank
