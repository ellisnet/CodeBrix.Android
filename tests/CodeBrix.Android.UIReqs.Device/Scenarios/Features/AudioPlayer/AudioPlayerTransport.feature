@needs-audioplayer
Feature: An audio player and the scrubber that follows it
	An AudioPlayer renders nothing and takes no space: what a person sees of it is the controls
	that are bound to it. So every requirement here is stated twice over - once about what the
	player reports, and once about what a Slider bound to its length and its timecode with no
	converter at all actually shows on the panel.
	Loading a file is synchronous, so a length is available the moment the source is set; playing,
	pausing, stopping, seeking and scrubbing are what the transport is. Timecodes are stated as
	bands, never as exact numbers, because a decoder's clock is not the test's clock.
	Every fixture in this suite is digital silence and every player is turned down to nothing, so
	a run makes no sound. Deliberately out of scope: how the audio sounds, sound effects, volume
	and mute (nothing is audible either way), streams, and every format beyond the two here.

Scenario: A loaded player reports the length of its file at once
	Given the application shows a StackPanel named "deck" 900 by 200
	And the layout "deck" holds an AudioPlayer named "player"
	When the Source of "player" is set to "silence2s.wav"
	Then the DurationSeconds of "player" is about 2.0
	And "player" is not playing
	And the PositionSeconds of "player" is less than 0.05
	And the MediaFailed of "player" was never raised

Scenario: A Slider bound to a player takes the length of the file as its Maximum
	Given the application shows a StackPanel named "deck" 900 by 200
	And the layout "deck" holds an AudioPlayer named "player"
	And a Slider named "scrubber" is bound to "player"
	When the Source of "player" is set to "silence_long.wav"
	And the frame is captured
	Then the DurationSeconds of "player" is about 10.0
	And the Maximum of "scrubber" is about 10.0
	And the Value of "scrubber" is 0
	And "scrubber" is 800 by 32 device pixels
	And the region of "scrubber" has ink

@needs-audio-device
Scenario: Playing a file moves the thumb of the Slider that follows it
	Given the application shows a StackPanel named "deck" 900 by 200
	And the layout "deck" holds an AudioPlayer named "player"
	And a Slider named "scrubber" is bound to "player"
	And the Source of "player" is set to "silence_long.wav"
	And the frame is captured
	When the Slider thumb of "scrubber" is captured as "at the start"
	And "player" starts playing
	And "player" plays past 1.5 seconds within 6000 milliseconds
	And the Slider thumb of "scrubber" is captured as "a moment later"
	Then "player" is playing
	And the Value of "scrubber" is more than 1.4
	And the Slider thumb of "scrubber" had ink in "at the start"
	And the Slider thumb of "scrubber" had ink in "a moment later"
	And the Slider thumb of "scrubber" moved right from "at the start" to "a moment later" by at least 60 pixels

@needs-audio-device
Scenario: Pausing a player leaves its scrubber exactly where it was
	Given the application shows a StackPanel named "deck" 900 by 200
	And the layout "deck" holds an AudioPlayer named "player"
	And a Slider named "scrubber" is bound to "player"
	And the Source of "player" is set to "silence_long.wav"
	And "player" starts playing
	And "player" plays past 1.0 seconds within 6000 milliseconds
	When "player" is paused
	And the frame is captured as "just paused"
	And the panel is left alone for 600 milliseconds
	And the frame is captured as "still paused"
	Then "player" is not playing
	And the PositionSeconds of "player" is more than 1.0
	And the region of "scrubber" in frame "still paused" is unchanged from frame "just paused"

@needs-audio-device
Scenario: Stopping a player rewinds it and empties the filled part of its scrubber
	Given the application shows a StackPanel named "deck" 900 by 200
	And the layout "deck" holds an AudioPlayer named "player"
	And a Slider named "scrubber" is bound to "player"
	And the Source of "player" is set to "silence_long.wav"
	And "player" starts playing
	And "player" plays past 2.5 seconds within 8000 milliseconds
	And the frame is captured as "part way through"
	Then the region of "HorizontalDecreaseRect" contains at least 90 percent "Accent"
	When "player" is stopped
	And the frame is captured as "stopped"
	Then "player" is not playing
	And the PositionSeconds of "player" is less than 0.05
	And the Value of "scrubber" is less than 0.05
	And the Slider "scrubber" fills 0 percent of its track, within 2 pixels
	And the region of "scrubber" in frame "stopped" differs from frame "part way through"

Scenario: Seeking jumps a player to a timecode at once
	Given the application shows a StackPanel named "deck" 900 by 200
	And the layout "deck" holds an AudioPlayer named "player"
	And a Slider named "scrubber" is bound to "player"
	And the Source of "player" is set to "silence_long.wav"
	When "player" seeks to 6.0 seconds
	And the frame is captured
	Then the PositionSeconds of "player" is more than 5.9
	And the PositionSeconds of "player" is less than 6.1
	And the Value of "scrubber" is more than 5.9
	And "player" is not playing
	And the Slider "scrubber" fills 60 percent of its track, within 12 pixels
	And the region of "HorizontalDecreaseRect" contains at least 90 percent "Accent"

@needs-audio-device
Scenario: Dragging the scrubber seeks the audio once the finger has lifted
	Given the application shows a StackPanel named "deck" 900 by 200
	And the layout "deck" holds an AudioPlayer named "player"
	And a Slider named "scrubber" is bound to "player"
	And the Source of "player" is set to "silence_long.wav"
	And the frame is captured
	When the Slider thumb of "scrubber" is dragged 300 pixels to the right
	And the panel is left alone for 400 milliseconds
	And "player" starts playing
	And "player" plays past 4.0 seconds within 2000 milliseconds
	And the frame is captured
	Then "player" is playing
	And the PositionSeconds of "player" is more than 4.0
	And the PositionSeconds of "player" is less than 5.0
	And the region of "HorizontalDecreaseRect" contains at least 90 percent "Accent"

@needs-audio-device
Scenario: A player that reaches the end of its file says so and stops
	Given the application shows a StackPanel named "deck" 900 by 200
	And the layout "deck" holds an AudioPlayer named "player"
	And a Slider named "scrubber" is bound to "player"
	And the Source of "player" is set to "silence2s.wav"
	And the frame is captured as "before playing"
	When "player" starts playing
	And the PlaybackEnded of "player" is raised within 8000 milliseconds
	And the frame is captured as "at the end"
	Then "player" is not playing
	And the MediaFailed of "player" was never raised
	And the region of "scrubber" in frame "at the end" differs from frame "before playing"

@needs-audio-device
Scenario: A looping player never says it ended and keeps its scrubber moving
	Given the application shows a StackPanel named "deck" 900 by 200
	And the layout "deck" holds an AudioPlayer named "player"
	And the IsLooping of "player" is set to "True"
	And a Slider named "scrubber" is bound to "player"
	And the Source of "player" is set to "silence2s.wav"
	And the frame is captured
	When "player" starts playing
	And "player" plays past 1.5 seconds within 6000 milliseconds
	And "player" returns to before 0.5 seconds within 6000 milliseconds
	And the Slider thumb of "scrubber" is captured as "just after the loop"
	And "player" plays past 1.0 seconds within 6000 milliseconds
	And the Slider thumb of "scrubber" is captured as "a moment later"
	Then "player" is playing
	And the PlaybackEnded of "player" was never raised
	And the Slider thumb of "scrubber" moved right from "just after the loop" to "a moment later" by at least 100 pixels

Scenario: A player told to load a file that is not there says so and offers nothing to scrub
	Given the application shows a StackPanel named "deck" 900 by 200
	And the layout "deck" holds an AudioPlayer named "player"
	And a Slider named "scrubber" is bound to "player"
	When the Source of "player" is set to "no-such-file.wav"
	And the frame is captured
	Then the MediaFailed of "player" was raised at least once
	And the failure message of "player" is not empty
	And the DurationSeconds of "player" is about 0.0
	And "player" is not playing
	And the Maximum of "scrubber" is about 0.0
	And the Value of "scrubber" is 0
	And the region of "scrubber" has ink

Scenario: A player with nothing loaded ignores a request to play
	Given the application shows a StackPanel named "deck" 900 by 200
	And the layout "deck" holds an AudioPlayer named "player"
	And a Slider named "scrubber" is bound to "player"
	And the frame is captured as "before"
	When "player" starts playing
	And the frame is captured as "after"
	Then "player" is not playing
	And the PositionSeconds of "player" is less than 0.05
	And the PlaybackEnded of "player" was never raised
	And the MediaFailed of "player" was never raised
	And the region of "scrubber" in frame "after" is unchanged from frame "before"

@needs-audio-device
Scenario: A compressed file loads and plays exactly as an uncompressed one does
	Given the application shows a StackPanel named "deck" 900 by 200
	And the layout "deck" holds an AudioPlayer named "player"
	And a Slider named "scrubber" is bound to "player"
	When the Source of "player" is set to "silence2s.ogg"
	Then the DurationSeconds of "player" is about 2.0
	And the Maximum of "scrubber" is about 2.0
	When "player" starts playing
	And "player" plays past 0.8 seconds within 6000 milliseconds
	And the Slider "scrubber" has caught up with "player"
	And the frame is captured
	Then "player" is playing
	And the MediaFailed of "player" was never raised
	And the region of "HorizontalDecreaseRect" contains at least 90 percent "Accent"
