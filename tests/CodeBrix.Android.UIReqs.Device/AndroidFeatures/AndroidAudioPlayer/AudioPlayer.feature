Feature: The AudioPlayer on Android
	Android-only (the Platform's AudioPlayer group, copied as the AudioPlayer group, covers the elements themselves: the
	transport, the scrubber, the MIDI player's background load). This group fences what Android adds. The AudioPlayer
	Core's three platform contracts are the Android add-in's: every AudioPlayer element plays through an AudioFilePlayer of
	CodeBrix.Audio on CodeBrix.Audio.Android's backend (initialised by the add-in), sound effects are that backend's voices,
	and the root of ms-appx:/// audio is the folder the APK's assets are copied out to on first use. MidiPlayer - not in the
	Core - is the Android add-in's own port, and plays an SFZ instrument named by ms-appx:///, whose samples come out of
	the APK with it. Every fixture is digital silence and every player is turned down to nothing.

Scenario: An AudioPlayer element plays through the Android add-in's output
	Given an AudioPlayer named "player" is on the panel, turned down to nothing
	Then the Android audio backend is initialised
	And "player" plays through the Android add-in's AudioFilePlayer output
	And the root of ms-appx audio is the Android asset copy folder

Scenario: An ms-appx audio asset is copied out of the APK and plays
	Given an AudioPlayer named "player" is on the panel, turned down to nothing
	When the Source of the AudioPlayer "player" is "ms-appx:///Assets/AudioFence/silence2s.wav"
	Then the AudioPlayer "player" has a length of about 2 seconds and reported no failure
	And the asset "Assets/AudioFence/silence2s.wav" has been copied out of the APK
	When the AudioPlayer "player" plays
	Then the AudioPlayer "player" plays past 0.5 seconds within 6000 milliseconds

Scenario: A sound effect plays through the Android add-in's shared output
	Given an AudioPlayer named "player" is on the panel, turned down to nothing
	When a silent sound effect is played at volume 0
	Then the sound effect was accepted by the Android output

Scenario: A MidiPlayer is the Android add-in's own and plays an ms-appx SFZ instrument
	Given a MidiPlayer named "midi" is on the panel, turned down to nothing
	Then "midi" is a MidiPlayer of the Android AudioPlayer add-in
	When the MidiPlayer "midi" is given the instrument "ms-appx:///Assets/AudioFence/silent.sfz" and the generated sequence
	Then the MidiPlayer "midi" opens its media within 10000 milliseconds with an SFZ instrument
	And the asset folder "Assets/AudioFence" has been copied out of the APK
	When the MidiPlayer "midi" plays
	Then the MidiPlayer "midi" sounds voices within 5000 milliseconds
