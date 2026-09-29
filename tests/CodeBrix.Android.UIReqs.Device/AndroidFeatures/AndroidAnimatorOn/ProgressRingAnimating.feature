Feature: Native indicators with the system animations on
	Android-only (not a copy of a CodeBrix.Platform feature). The runner starts this group with the system animator duration
	scale at 1 and restores it to 0 afterwards; every other group runs with it at 0 for deterministic frames. Native Material
	indicators honour that scale (Jeremy 2026-09-26, Option A: like UISettings.AnimationsEnabled), so the copied claim "An
	active ProgressRing is animating" (Lottie/LottieProgressRing, re-homed in tests/CodeBrix.Android.UIReqs/uireqs-rehomed.txt)
	runs here, and so does the colours claim (a still native indicator hides its track). No frame of this group is compared across runs: an animation is caught at a timing-dependent phase
	(the group is informational in build/test-scripts/uireqs-frame-compare.informational).

Scenario: An active ProgressRing is animating, not standing still
	Given the system animations are on
	And the application shows a ProgressRing named "busy" with:
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

Scenario: A spinning ProgressRing is drawn in the colours the control was given
	With the system animations off a native indeterminate indicator draws a still, FULL ring of its indicator colour over
	the track, so the copied claim (Lottie/LottieProgressRing, re-homed) is checked here, where the ring spins and its
	track - the Background (the handler's theme overlay keeps it visible while spinning) - shows beside the moving arc.
	Given the system animations are on
	And the application shows a ProgressRing named "busy" with:
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
