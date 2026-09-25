@needs-libvlc
Feature: Playing a clip in a media element
	The element hands its source to a native playback engine and paints the frames the engine
	decodes into the picture area. What a person can confirm by eye is that the clip they asked
	for is the clip on the panel, and that it is moving: every clip these scenarios play is red
	for its first second and blue for its second, so which second of a two-second clip is showing
	is a colour, and "it advanced" is a colour that changed.
	Every claim about the panel carries the budget it is allowed, because the colour change is a
	decode-time fact and the picture is a paint-time one, and nothing in between says when they
	have met. Every claim about where playback has got to is a BAND, because a decoder's position
	is not an exact number.
	Nothing is heard: every clip is digital silence AND the player is muted with its volume at
	zero. The scenarios that need no sound track play the clip that has none, so that a scenario
	about pictures has one fewer moving part; the run-to-the-end scenario deliberately plays the
	clip that DOES carry a silent audio track, so the path that opens an output device is
	exercised too.
	Deliberately out of scope: hover, display scale (pinned at 1.0), playback rate, playlists and
	full-window mode.

Scenario: Opening a clip reports how long it is and that it has a picture
	Given the application shows a MediaPlayerElement named "player" 800 by 450
	When the clip "twocolour" is given to "player"
	And the media of "player" opens within 8000 milliseconds
	And the frame is captured
	Then the MediaOpened of "player" was raised at least once
	And the media of "player" is about 2.0 seconds long
	And the media of "player" has a picture
	And the NaturalVideoDimensionChanged of "player" was raised at least once
	And the video surface of "player" is showing
	And the region of "player" has ink within 8000 milliseconds

Scenario: A clip that has only been opened already shows its first picture
	Given the application shows a MediaPlayerElement named "player" with:
		| Property                    | Value |
		| Width                       | 800   |
		| Height                      | 450   |
		| AreTransportControlsEnabled | false |
	And the clip "twocolour" is given to "player"
	And the media of "player" opens within 8000 milliseconds
	Then the region of "player" shows at least 50 percent "#FF0000" within 8000 milliseconds

Scenario: A playing clip changes what it shows as it advances
	Given the application shows a MediaPlayerElement named "player" with:
		| Property                    | Value |
		| Width                       | 800   |
		| Height                      | 450   |
		| AreTransportControlsEnabled | false |
	And the clip "twocolour" is given to "player"
	And the media of "player" opens within 8000 milliseconds
	And the region of "player" shows at least 50 percent "#FF0000" within 8000 milliseconds
	When "player" is played
	And the playback state of "player" is "Playing" within 4000 milliseconds
	Then the region of "player" shows at least 50 percent "#0000FF" within 5000 milliseconds
	And the PositionChanged of "player" was raised at least once

Scenario: Moving the position moves the picture to that timecode
	Given the application shows a MediaPlayerElement named "player" with:
		| Property                    | Value |
		| Width                       | 800   |
		| Height                      | 450   |
		| AreTransportControlsEnabled | false |
	And the clip "twocolour" is given to "player"
	And the media of "player" opens within 8000 milliseconds
	And the region of "player" shows at least 50 percent "#FF0000" within 8000 milliseconds
	When the position of "player" is moved to 1.5 seconds
	Then the region of "player" shows at least 50 percent "#0000FF" within 5000 milliseconds
	And the position of "player" is between 1.3 and 2.0 seconds

Scenario: A clip that runs to its end says so
	Given the application shows a MediaPlayerElement named "player" 800 by 450
	And the clip "twocolour-with-sound" is given to "player"
	And the media of "player" opens within 8000 milliseconds
	And the region of "ControlPanelGrid" has stopped changing
	When "player" is played
	And the media of "player" ends within 10000 milliseconds
	And the frame is captured
	Then the MediaEnded of "player" was raised at least once
	And the region of "ControlPanelGrid" has ink

Scenario: A source that names no file reports a failure a person can read
	# FOUND AND NOW FENCES: an element with nothing to show drew NOTHING at all, instead of the
	# black rectangle its own default style asks for. The style sets Background="Black" on the
	# element, but the template bound that brush only to the video presenter inside it, and the
	# presenter stays collapsed until a source arrives - so the Background of a MediaPlayerElement
	# had no effect until it was playing something, and an application that put one on a light
	# page got a hole in the page. The template's LayoutRoot now paints the element's Background
	# (Generic.xaml and mergedstyles.xaml, MEASURED 2026-09-10), and the claim below is the fence:
	# it goes red the moment that brush stops reaching the element's own root again.
	Given the application shows a MediaPlayerElement named "player" with:
		| Property                    | Value |
		| Width                       | 800   |
		| Height                      | 450   |
		| AreTransportControlsEnabled | false |
	When the clip "missing" is given to "player"
	And the media of "player" fails within 8000 milliseconds
	And the frame is captured
	Then the failure reported by "player" names "no-such-clip.mp4"
	And the MediaFailed of "player" was raised at least once
	And the MediaOpened of "player" was never raised
	And the region of "player" is uniformly "Black"
