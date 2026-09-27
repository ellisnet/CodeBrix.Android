@needs-lottie
Feature: What a Lottie animation looks like on the panel
	Where the animation is drawn, how big it is drawn and what colour it is drawn in are the
	three things a person judges an animation player by, and none of them depends on timing:
	setting the progress puts an exact frame on the panel and leaves it there, Stretch decides
	how much of the player the composition fills, and a document whose shapes carry colour
	bindings is repainted in whatever colour the application gives it - before it has loaded or
	afterwards.
	Two documents are used. One is a red square that crosses a hundred-pixel composition, so
	which half of the player holds ink says which frame is showing. The other fills its whole
	composition with one bound colour, so its ink is the composition itself and both its size
	and its colour can be read straight off the panel.
	Deliberately out of scope: hover, display scale (pinned at 1.0), and the colour bindings the
	parser accepts but the source ignores - only "Color" is honoured.

Scenario: Setting the progress puts an exact frame of the animation on the panel
	Given the application shows an AnimatedVisualPlayer named "anim" with:
		| Property | Value |
		| Source   | pulse |
	And the animation of "anim" is loaded
	When the progress of "anim" is set to 0.0
	And the frame is captured as "start"
	Then the player "anim" is not playing
	And the region of "anim" contains "Red"
	And the leftmost 40 pixels of "anim" have ink
	And the rightmost 40 pixels of "anim" are blank
	When the progress of "anim" is set to 1.0
	And the frame is captured as "end"
	Then the region of "anim" contains "Red"
	And the rightmost 40 pixels of "anim" have ink
	And the leftmost 40 pixels of "anim" are blank
	And the region of "anim" in frame "end" differs from frame "start"

Scenario: Stretch decides how much of the player the animation is drawn across
	Given the application shows an AnimatedVisualPlayer named "anim" with:
		| Property   | Value          |
		| Stretch    | None           |
		| ThemeColor | Foreground=Red |
		| Source     | themed         |
	And the animation of "anim" is loaded
	When the frame is captured as "none"
	Then "anim" is 240 by 240 device pixels
	And the 60 by 60 block at 90, 90 inside "anim" is uniformly "Red"
	And the leftmost 40 pixels of "anim" are blank
	And the topmost 40 pixels of "anim" are blank
	When the Stretch of "anim" is set to "Uniform"
	And the frame is captured as "uniform"
	Then the 200 by 200 block at 20, 20 inside "anim" is uniformly "Red"
	And the leftmost 40 pixels of "anim" have ink
	And the topmost 40 pixels of "anim" have ink
	And the ink of "anim" in frame "uniform" is at least 2 times as tall as in frame "none"
	And the ink of "anim" in frame "uniform" is wider than in frame "none"

Scenario: An animation is painted in the colour it was given before it had loaded
	Given the application shows an AnimatedVisualPlayer named "anim" with:
		| Property   | Value          |
		| ThemeColor | Foreground=Red |
		| Source     | themed         |
	When the animation of "anim" is loaded
	And the frame is captured
	Then the region of "anim" is uniformly "Red"
	And the region of "anim" does not contain "Gray"

Scenario: Giving a loaded animation another colour repaints it
	Given the application shows an AnimatedVisualPlayer named "anim" with:
		| Property   | Value          |
		| ThemeColor | Foreground=Red |
		| Source     | themed         |
	And the animation of "anim" is loaded
	When the frame is captured as "red"
	Then the region of "anim" is uniformly "Red"
	When the ThemeColor of "anim" is set to "Foreground=Blue"
	And the frame is captured as "blue"
	Then the region of "anim" is uniformly "Blue"
	And the region of "anim" does not contain "Red"
	And the region of "anim" in frame "blue" differs from frame "red"
