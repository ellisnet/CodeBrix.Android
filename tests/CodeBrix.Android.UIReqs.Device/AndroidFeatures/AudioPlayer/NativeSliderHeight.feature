@needs-audioplayer
Feature: The native Slider of an audio player on Android
	Android-only, kept in the AudioPlayer group's folder so the copied AudioPlayer steps bind (an add-in group's steps bind only
	the features of its folder). The copied AudioPlayerTransport scenario "A Slider bound to a player takes the length of the
	file as its Maximum" is re-homed here (uireqs-rehomed.txt): the Slider is the native Material slider, whose height is
	Material's 48-dp minimum touch target where the Fluent Slider is 32 - an accepted native difference (Jeremy 2026-10-17).

Scenario: A native Slider bound to a player takes the length of the file as its Maximum, at the Material touch-target height
	Given the application shows a StackPanel named "deck" 900 by 200
	And the layout "deck" holds an AudioPlayer named "player"
	And a Slider named "scrubber" is bound to "player"
	When the Source of "player" is set to "silence_long.wav"
	And the frame is captured
	Then the DurationSeconds of "player" is about 10.0
	And the Maximum of "scrubber" is about 10.0
	And the Value of "scrubber" is 0
	And "scrubber" is 800 by 48 device pixels
	And the region of "scrubber" has ink
