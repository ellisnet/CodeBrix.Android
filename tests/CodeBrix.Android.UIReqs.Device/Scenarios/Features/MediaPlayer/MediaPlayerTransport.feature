@needs-libvlc
Feature: The transport controls a media element brings with it
	A media element carries a whole row of chrome of its own: a play/pause button that knows
	which of the two it is, a slider that shows how far through the clip playback has got, and a
	readout of the elapsed time. None of it is wired up by an application - it is what the element
	is for - so what a person confirms is that the built-in chrome and the engine behind it agree
	with each other.
	The chrome slides and fades into place and reports nothing when it has arrived, so a scenario
	that is going to compare two frames of it waits for it to stop moving first. Its three-second
	auto-hide is switched off for every scenario in this suite: a timer that repaints is never
	left running across a comparison.
	Deliberately out of scope: hover, display scale (pinned at 1.0), the volume flyout, the
	full-window button and the buttons the default template leaves collapsed.

Scenario: Tapping the built-in play button starts playback and swaps its glyph
	Given the application shows a MediaPlayerElement named "player" 800 by 450
	And the clip "twocolour" is given to "player"
	And the media of "player" opens within 8000 milliseconds
	And the region of "ControlPanelGrid" has stopped changing
	And the frame is captured as "before the tap"
	When "PlayPauseButton" is tapped
	And the playback state of "player" is "Playing" within 4000 milliseconds
	And the frame is captured as "after the tap"
	Then the built-in play button of "player" shows "Pause"
	And the region of "PlayPauseSymbol" in frame "after the tap" differs from frame "before the tap"

Scenario: Playing moves the built-in progress slider along its track
	Given the application shows a MediaPlayerElement named "player" 800 by 450
	And the clip "twocolour" is given to "player"
	And the media of "player" opens within 8000 milliseconds
	And the region of "ControlPanelGrid" has stopped changing
	When the Slider thumb of "ProgressSlider" is captured as "at the start"
	And "player" is played
	And the position of "player" passes 1.0 seconds within 6000 milliseconds
	And the Slider thumb of "ProgressSlider" is captured as "a second in"
	Then the Slider thumb of "ProgressSlider" had ink in "at the start"
	And the Slider thumb of "ProgressSlider" moved right from "at the start" to "a second in" by at least 100 pixels
	And the position of "player" is between 0.9 and 2.1 seconds

Scenario: The elapsed-time readout follows playback
	Given the application shows a MediaPlayerElement named "player" 800 by 450
	And the clip "twocolour" is given to "player"
	And the media of "player" opens within 8000 milliseconds
	And the region of "ControlPanelGrid" has stopped changing
	And the frame is captured as "at the start"
	When "player" is played
	And the position of "player" passes 1.0 seconds within 6000 milliseconds
	And the frame is captured as "a second in"
	Then the Text of "TimeElapsedElement" is "0:00:01"
	And the region of "TimeElapsedElement" in frame "a second in" differs from frame "at the start"
