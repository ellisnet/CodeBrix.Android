@needs-videoplayer
Feature: VideoPlayer transport
	A VideoPlayer must do what the transport buttons of a player say: play, and the picture and the
	timecode move together; pause, and both stand still; seek, and the picture at that timecode is
	on the screen even with nothing playing; stop, and it is back at the first picture. A scrubber
	bound to it follows playback with no code behind it, the end of a clip is announced, and a
	looping player never reaches an end at all.

	Every timecode claim is a BAND rather than an exact number: a position is a moving value read
	at a moment of its own choosing. Every claim about the picture carries a budget, because the
	colour change at one second is a decode-time fact and a frame is a paint-time fact.

	Deliberately out of scope: hover, display scale, dragging the scrubber to seek (the debounced
	write-back the AudioPlayer requirements own), captions and chapters.

Scenario: Pausing a VideoPlayer freezes the picture where it was
	Given the application shows a Grid named "cell" 640 by 480
	And the layout "cell" holds a VideoPlayer named "player"
	And the clip "twocolour_raw_videoonly.mkv" is opened in "player"
	And the region of "player" shows at least 99 percent "#FF0000" within 8000 milliseconds
	And the video "player" is played
	And the region of "player" shows at least 99 percent "#0000FF" within 5000 milliseconds
	When the video "player" is paused
	And the position of "player" is remembered
	And the frame is captured as "when-it-was-paused"
	And the paused video "player" is left alone for 600 milliseconds
	And the frame is captured as "600-milliseconds-later"
	Then the video "player" is not playing
	And the PositionSeconds of "player" has not moved by more than 0.05 seconds
	And the region of "player" in frame "600-milliseconds-later" is unchanged from frame "when-it-was-paused"

Scenario: Seeking a VideoPlayer that is not playing puts that timecode on the screen
	Given the application shows a Grid named "cell" 640 by 480
	And the layout "cell" holds a VideoPlayer named "player"
	And the clip "twocolour_raw_videoonly.mkv" is opened in "player"
	And the region of "player" shows at least 99 percent "#FF0000" within 8000 milliseconds
	When the video "player" is sought to 1.5 seconds
	Then the region of "player" shows at least 99 percent "#0000FF" within 5000 milliseconds
	And the PositionSeconds of "player" is between 1.4 and 1.7
	And the video "player" is not playing

Scenario: Stop rewinds a VideoPlayer to its first picture
	Given the application shows a Grid named "cell" 640 by 480
	And the layout "cell" holds a VideoPlayer named "player"
	And the clip "twocolour_raw_videoonly.mkv" is opened in "player"
	And the region of "player" shows at least 99 percent "#FF0000" within 8000 milliseconds
	And the video "player" is played
	And the region of "player" shows at least 99 percent "#0000FF" within 5000 milliseconds
	When the video "player" is stopped
	Then the region of "player" shows at least 99 percent "#FF0000" within 5000 milliseconds
	And the PositionSeconds of "player" is less than 0.1
	And the video "player" is not playing

Scenario: A Slider bound to a VideoPlayer follows it along its track
	Given the application shows a StackPanel named "page" 820 by 560
	And the layout "page" holds a Grid named "cell" with:
		| Property | Value |
		| Width    | 640   |
		| Height   | 480   |
	And the layout "cell" holds a VideoPlayer named "player"
	And the clip "twocolour_raw_videoonly.mkv" is opened in "player"
	And the layout "page" holds a Slider named "scrubber" bound to "player"
	And the region of "player" shows at least 99 percent "#FF0000" within 8000 milliseconds
	When the Slider thumb of "scrubber" is captured as "at-the-start"
	And the video "player" is played
	And "player" has played past 1.0 seconds within 6000 milliseconds
	And the Slider thumb of "scrubber" is captured as "while-it-plays"
	Then the Slider thumb of "scrubber" had ink in "at-the-start"
	And the Slider thumb of "scrubber" moved right from "at-the-start" to "while-it-plays" by at least 100 pixels
	And the Value of "scrubber" is more than 0.9

Scenario: A VideoPlayer that reaches the end of its clip stops and says so
	Given the application shows a Grid named "cell" 640 by 480
	And the layout "cell" holds a VideoPlayer named "player"
	And the clip "twocolour_raw_videoonly.mkv" is opened in "player"
	And the region of "player" shows at least 99 percent "#FF0000" within 8000 milliseconds
	When the video "player" is played
	Then the PlaybackEnded of "player" is raised within 8000 milliseconds
	And the video "player" is not playing
	And the PositionSeconds of "player" is between 1.5 and 2.1

Scenario: A looping VideoPlayer returns to its first picture instead of ending
	Given the application shows a Grid named "cell" 640 by 480
	And the layout "cell" holds a VideoPlayer named "player" with:
		| Property  | Value |
		| IsLooping | true  |
	And the clip "twocolour_raw_videoonly.mkv" is opened in "player"
	And the region of "player" shows at least 99 percent "#FF0000" within 8000 milliseconds
	And the video "player" is played
	And the region of "player" shows at least 99 percent "#0000FF" within 5000 milliseconds
	When the region of "player" shows at least 99 percent "#FF0000" within 8000 milliseconds
	Then the video "player" is playing
	And the PlaybackEnded of "player" was never raised

@needs-audio-device
Scenario: A clip that carries a soundtrack plays its picture with the sound silenced
	Given the application shows a Grid named "cell" 640 by 480
	And the layout "cell" holds a VideoPlayer named "player"
	And the clip "twocolour_raw.mkv" is opened in "player"
	And the region of "player" shows at least 99 percent "#FF0000" within 8000 milliseconds
	When the video "player" is played
	Then the region of "player" shows at least 99 percent "#0000FF" within 5000 milliseconds
	And the soundtrack of "player" is silent
	And the DurationSeconds of "player" is about 2.0
