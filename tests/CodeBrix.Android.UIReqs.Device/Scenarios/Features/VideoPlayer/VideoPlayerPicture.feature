@needs-videoplayer
Feature: VideoPlayer picture
	A VideoPlayer must put the clip's picture on the panel: the first frame as soon as a source is
	opened, a NEW picture as the clip advances, and the picture fitted into the space it was given
	the way Stretch says - letterboxed inside it, cropped to cover it, or drawn at its own pixel
	size on a black ground.

	Every clip here is two seconds of one flat colour changing to another (red for the first
	second, blue for the second) in an uncompressed track, so that what the scenarios claim is
	what a person sees on the frame.

	Deliberately out of scope: hover (this is a touch and keyboard panel), display scale (pinned
	at 1.0), the effect chain and layers (a grade on the processor path needs an opt-in the
	requirements here do not turn on), captions and chapters (data rather than picture).

Scenario: A clip that is merely opened shows its first picture
	Given the application shows a Grid named "cell" 640 by 480
	And the layout "cell" holds a VideoPlayer named "player"
	When the clip "twocolour_raw_videoonly.mkv" is opened in "player"
	And "player" has presented a frame within 8000 milliseconds
	Then the DurationSeconds of "player" is about 2.0
	And the MediaOpened of "player" was raised at least once
	And the video "player" is not playing
	And the region of "player" shows at least 99 percent "#FF0000" within 3000 milliseconds

Scenario: A playing VideoPlayer changes what it shows as the clip advances
	Given the application shows a Grid named "cell" 640 by 480
	And the layout "cell" holds a VideoPlayer named "player"
	And the clip "twocolour_raw_videoonly.mkv" is opened in "player"
	And "player" has presented a frame within 8000 milliseconds
	And the region of "player" shows at least 99 percent "#FF0000" within 8000 milliseconds
	And the frame is captured as "at-the-start"
	When the video "player" is played
	Then the region of "player" shows at least 99 percent "#0000FF" within 5000 milliseconds
	And the region of "player" in frame "at-the-start" is uniformly "#FF0000"
	And the video "player" is playing

Scenario: A portrait clip is letterboxed inside a landscape cell
	Given the application shows a Grid named "cell" 640 by 480
	And the layout "cell" holds a VideoPlayer named "player" with:
		| Property | Value   |
		| Stretch  | Uniform |
	And the clip "portrait_raw.mkv" is opened in "player"
	When "player" has presented a frame within 8000 milliseconds
	And "player" is showing a picture within 8000 milliseconds
	And the frame is captured
	Then "player" is 640 by 480 device pixels
	And the leftmost 130 pixels of "player" are uniformly "Black"
	And the rightmost 130 pixels of "player" are uniformly "Black"
	And the 340 by 460 block at 150, 10 inside "player" is uniformly "#FF0000"
	And the region of "player" contains at least 50 percent "#FF0000"
	And the region of "player" contains at least 40 percent "Black"

Scenario: UniformToFill covers the whole cell and crops instead of letterboxing
	Given the application shows a Grid named "cell" 640 by 480
	And the layout "cell" holds a VideoPlayer named "player" with:
		| Property | Value         |
		| Stretch  | UniformToFill |
	And the clip "portrait_raw.mkv" is opened in "player"
	When "player" has presented a frame within 8000 milliseconds
	Then the region of "player" shows at least 99 percent "#FF0000" within 3000 milliseconds
	And the region of "player" does not contain "Black"

Scenario: Stretch None draws the clip at its own pixel size
	Given the application shows a Grid named "cell" 640 by 480
	And the layout "cell" holds a VideoPlayer named "player" with:
		| Property | Value |
		| Stretch  | None  |
	And the clip "twocolour_raw_videoonly.mkv" is opened in "player"
	When "player" has presented a frame within 8000 milliseconds
	And "player" is showing a picture within 8000 milliseconds
	And the frame is captured
	Then the 124 by 92 block at 258, 194 inside "player" is uniformly "#FF0000"
	And the 200 by 100 block at 20, 20 inside "player" is uniformly "Black"
	And the region of "player" contains at most 6 percent "#FF0000"
