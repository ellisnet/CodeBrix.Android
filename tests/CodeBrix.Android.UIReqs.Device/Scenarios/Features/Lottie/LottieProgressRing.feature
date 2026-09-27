@needs-lottie
Feature: The framework's ProgressRing, once the animation add-in is registered
	A ProgressRing draws itself with a Lottie animation it asks the extension registry for. With
	no animation add-in registered it has nothing to draw the ring with and paints a red warning
	line instead - which is what the core suite sees, and why the strongest thing the core suite
	can say about an active ring is that its rectangle is not empty.
	This assembly registers the add-in, so these two scenarios say what the core suite cannot:
	that the ring MOVES, and that it is drawn in the colour the control was given rather than in
	the warning line's red.
	Deliberately out of scope: hover, display scale (pinned at 1.0), the determinate ring (its
	value change is itself an animation, so a claim about it would be a timing claim) and the
	exact artwork, which belongs to the framework and not to this add-in.

Scenario: An active ProgressRing is animating, not standing still
	Given the application shows a ProgressRing named "busy" with:
		| Property | Value |
		| Width    | 120   |
		| Height   | 120   |
		| IsActive | True  |
	When the frame is captured as "first"
	And the ProgressRing "busy" is left running for 300 milliseconds
	And the frame is captured as "second"
	Then the region of "busy" has ink
	And the region of "busy" in frame "second" differs from frame "first"
	And the region of "busy" does not contain "Red"

Scenario: A ProgressRing is drawn in the colours the control was given
	Given the application shows a ProgressRing named "busy" with:
		| Property   | Value   |
		| Width      | 120     |
		| Height     | 120     |
		| IsActive   | True    |
		| Foreground | Lime    |
		| Background | Magenta |
	When the frame is captured
	Then the region of "busy" has ink
	And the region of "busy" contains at least 0.5 percent "Lime"
	And the region of "busy" contains "Magenta"
	And the region of "busy" does not contain "Red"
