@needs-lottie
Feature: Playing a Lottie animation
	An AnimatedVisualPlayer holding a Lottie source loads a Bodymovin document, reports how long
	it lasts, and draws it. Playing advances the frame from a stopwatch, so what a person sees
	changes while it plays and stands still when it does not; a segment that is not looped ends
	by itself and leaves its last frame on the panel, a looped one goes on until it is stopped.
	The animation these scenarios play is one red square that crosses a hundred-pixel composition
	in exactly one second, so where the square is says which frame is showing and whether two
	frames were taken at different moments.
	Deliberately out of scope: hover (the panel is touch and keyboard), display scale (pinned at
	1.0), any document that would have to be fetched over a network, and any claim that a
	PARTICULAR frame is showing after a given time - the frame comes from a stopwatch, so an
	elapsed-time claim here says only that two frames differ or that they do not.

Scenario: A Lottie document loads into a player, which then knows how long it lasts
	Given the application shows an AnimatedVisualPlayer named "anim" with:
		| Property | Value |
		| Source   | pulse |
	When the animation of "anim" is loaded
	And the frame is captured
	Then the animation of "anim" has loaded
	And the animation of "anim" lasts 1000 milliseconds
	And "anim" is 240 by 240 device pixels
	And the region of "anim" has ink
	And the region of "anim" contains "Red"

Scenario: A player set to autoplay is playing as soon as its animation has loaded
	Given the application shows an AnimatedVisualPlayer named "anim" with:
		| Property | Value |
		| AutoPlay | True  |
		| Source   | pulse |
	When the animation of "anim" is loaded
	And the frame is captured
	Then the player "anim" is playing
	And the region of "anim" has ink

Scenario: An animation that is playing changes what is on the panel
	Given the application shows an AnimatedVisualPlayer named "anim" with:
		| Property | Value |
		| AutoPlay | True  |
		| Source   | pulse |
	And the animation of "anim" is loaded
	When the frame is captured as "start"
	And "anim" is left running for 400 milliseconds
	And the frame is captured as "later"
	Then the player "anim" is playing
	And the region of "anim" in frame "later" differs from frame "start"

Scenario: A player that is not set to autoplay shows its first frame and stands still
	Given the application shows an AnimatedVisualPlayer named "anim" with:
		| Property | Value |
		| Source   | pulse |
	And the animation of "anim" is loaded
	When the frame is captured as "first"
	And "anim" is left running for 400 milliseconds
	And the frame is captured as "second"
	Then the player "anim" is not playing
	And the region of "anim" has ink
	And the leftmost 40 pixels of "anim" have ink
	And the rightmost 40 pixels of "anim" are blank
	And the region of "anim" in frame "second" is unchanged from frame "first"

Scenario: Pausing freezes the animation and resuming sets it going again
	Given the application shows an AnimatedVisualPlayer named "anim" with:
		| Property | Value |
		| AutoPlay | True  |
		| Source   | pulse |
	And the animation of "anim" is loaded
	When "anim" is paused
	And the frame is captured as "paused"
	And "anim" is left running for 300 milliseconds
	And the frame is captured as "still"
	Then the player "anim" is not playing
	And the region of "anim" in frame "still" is unchanged from frame "paused"
	When "anim" is resumed
	And the frame is captured as "resumed"
	And "anim" is left running for 300 milliseconds
	And the frame is captured as "moving"
	Then the player "anim" is playing
	And the region of "anim" in frame "moving" differs from frame "resumed"

Scenario: A segment that is not looped ends by itself and leaves its last frame showing
	Given the application shows an AnimatedVisualPlayer named "anim" with:
		| Property | Value |
		| Source   | pulse |
	And the animation of "anim" is loaded
	When "anim" plays from 0.0 to 0.5
	Then the player "anim" stops playing within 1500 milliseconds
	And the region of "anim" has ink
	And the leftmost 40 pixels of "anim" are blank
	And the rightmost 40 pixels of "anim" are blank
	And the 60 by 60 block at 90, 90 inside "anim" is uniformly "Red"
	# The last two blocks are the fence of a fixed defect, and they are narrow on purpose. Half a
	# second into a one-second composition the square is centred: at this size it covers exactly
	# the 72 pixels from 84 to 156 across the player, so its left edge is in the first block and
	# the panel beside its right edge is in the second. The animation used to come to rest on
	# whichever frame the tick that crossed the end of the segment had reached instead, which was
	# always further on and moved with the load on the machine; one pixel of that shows up here.
	And the 10 by 60 block at 84, 90 inside "anim" is uniformly "Red"
	And the 10 by 60 block at 156, 90 inside "anim" is blank

Scenario: A looped animation goes on past its own duration until it is stopped
	Given the application shows an AnimatedVisualPlayer named "anim" with:
		| Property | Value |
		| Source   | pulse |
	And the animation of "anim" is loaded
	When "anim" plays from 0.0 to 1.0 looped
	And "anim" is left running for 1200 milliseconds
	And the frame is captured as "past the end"
	And "anim" is left running for 300 milliseconds
	And the frame is captured as "still going"
	Then the player "anim" is playing
	And the region of "anim" has ink
	And the region of "anim" in frame "still going" differs from frame "past the end"
	When "anim" is stopped
	And the frame is captured
	Then the player "anim" is not playing
	And the region of "anim" has ink

Scenario: A faster playback rate reaches the end of a segment sooner
	Given the application shows a StackPanel named "board" with:
		| Property    | Value      |
		| Width       | 520        |
		| Height      | 240        |
		| Orientation | Horizontal |
	And the layout "board" holds a AnimatedVisualPlayer named "fast" with:
		| Property     | Value |
		| PlaybackRate | 4     |
		| Source       | pulse |
	And the layout "board" holds a AnimatedVisualPlayer named "slow" with:
		| Property     | Value |
		| PlaybackRate | 1     |
		| Source       | pulse |
	And the animation of "fast" is loaded
	And the animation of "slow" is loaded
	When "fast" plays from 0.0 to 1.0
	And "slow" plays from 0.0 to 1.0
	Then the player "fast" stops playing within 800 milliseconds
	And the player "slow" is playing
	And the region of "fast" has ink
	And the region of "slow" has ink
