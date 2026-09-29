@needs-audioplayer
Feature: A MIDI player and the controls that follow it
	A MidiPlayer renders a sequence through an instrument, and unlike an audio file that instrument
	takes long enough to read that the load runs in the background: the player says it is loading
	the moment it has both halves, and says it is ready by raising MediaOpened. So a page binds a
	ProgressRing to IsLoading and a Slider to the same two properties an audio player exposes, and
	both work with no converter and no code - which is the whole of what this add-in claims.
	The ring's own artwork is an animated visual, and playing one needs the animation add-in that
	this suite deliberately does not reference, so the pixels are asked only whether the ring's
	rectangle is empty; that the binding turned the ring on and off again is recorded as it happened,
	because an instrument that loads in a couple of milliseconds is gone again before a frame of the
	whole panel can be taken, and a scenario that waited for one would be waiting on a race.
	The instrument here is a one-region SFZ whose only sample is two seconds of digital silence,
	and the sequence is written by the test run itself, so a run makes no sound and downloads
	nothing. Deliberately out of scope: SoundFont and Decent Sampler instruments, presets, MIDI
	Polyphonic Expression, tempo changes, sending messages alongside the sequence, and everything
	the instrument reported about opcodes it could not honour.

Scenario: A MIDI player loads its instrument in the background and says when it is ready
	Given the application shows a StackPanel named "deck" 900 by 260
	And the layout "deck" holds a MidiPlayer named "midi"
	And a ProgressRing named "spinner" is bound to the IsLoading of "midi"
	And a Slider named "scrubber" is bound to "midi"
	And the frame is captured as "before the load"
	Then the region of "spinner" is blank
	When the Instrument of "midi" is set to "silent.sfz"
	And the Source of "midi" is set to "tiny.mid"
	Then the LoadingStarted of "midi" was raised at least once
	And the SpinnerShown of "spinner" was raised at least once
	And the MediaOpened of "midi" is raised within 10000 milliseconds
	And "midi" is not loading
	And the InstrumentKind of "midi" is "Sfz"
	And the MediaFailed of "midi" was never raised
	And the DurationSeconds of "midi" is more than 30.0
	And the Maximum of "scrubber" is more than 30.0
	When the frame is captured as "ready"
	Then the SpinnerHidden of "spinner" was raised at least once
	And the region of "spinner" is blank
	And the region of "spinner" in frame "ready" is unchanged from frame "before the load"

@needs-audio-device
Scenario: A playing MIDI player sounds voices and moves the musical clock on
	Given the application shows a StackPanel named "deck" 900 by 260
	And the layout "deck" holds a MidiPlayer named "midi"
	And a Slider named "scrubber" is bound to "midi"
	And the Instrument of "midi" is set to "silent.sfz"
	And the Source of "midi" is set to "tiny.mid"
	And the MediaOpened of "midi" is raised within 10000 milliseconds
	When "midi" starts playing
	And "midi" plays past 3.0 seconds within 8000 milliseconds
	And the frame is captured
	Then "midi" is playing
	And the ActiveVoiceCount of "midi" is more than 0 within 3000 milliseconds
	And the BeatPosition of "midi" rises within 3000 milliseconds
	And the Value of "scrubber" is more than 3.0
	And the region of "HorizontalDecreaseRect" contains at least 90 percent "Accent"
