Feature: The VideoPlayer on Android
	Android-only (the Platform's VideoPlayer group, copied as the VideoPlayer group, covers the element itself: the picture,
	the stretch modes, the transport, the render paths, the failures). This group fences what Android adds. The VideoPlayer
	element - not in the VideoPlayer Core - is the Android add-in's own port; its picture is the SkiaSharp.Views add-in's
	canvas (a native Skia view), composed on the Graphics3DGL add-in's EGL / OpenGL ES context when one can be made; its
	sound plays through CodeBrix.Audio's shared output on CodeBrix.Audio.Android's backend; and the root of ms-appx:///
	video is the folder the APK's assets are copied out to on first use. Every player is muted at volume zero.

Scenario: A VideoPlayer is the Android add-in's own and shows its picture on a native Skia view
	Given a VideoPlayer named "player" 640 by 480 is on the panel, muted
	When the video "twocolour_raw_videoonly.mkv" of the VideoPlayer group is opened in the VideoPlayer "player"
	Then the VideoPlayer "player" presents a frame within 8000 milliseconds
	And "player" is a VideoPlayer of the Android VideoPlayer add-in
	And the picture of the VideoPlayer "player" is on the Android Skia canvas view
	And the Android audio backend is initialised
	And the region of "player" shows at least 95 percent "#FF0000" within 8000 milliseconds

Scenario: An ms-appx video is copied out of the APK and plays
	Given a VideoPlayer named "player" 640 by 480 is on the panel, muted
	When the Source of the VideoPlayer "player" is "ms-appx:///Assets/VideoFence/twocolour_raw_videoonly.mkv"
	Then the VideoPlayer "player" presents a frame within 8000 milliseconds
	And the VideoPlayer "player" reported no failure
	And the video asset "Assets/VideoFence/twocolour_raw_videoonly.mkv" has been copied out of the APK

Scenario: A player left to choose its path composes on the Android graphics context when one can be made
	Given a VideoPlayer named "player" 640 by 480 is on the panel, muted, with the render path "GpuAuto"
	When the video "twocolour_raw_videoonly.mkv" of the VideoPlayer group is opened in the VideoPlayer "player"
	Then the VideoPlayer "player" presents a frame within 8000 milliseconds
	And the active render path of the VideoPlayer "player" is Gpu exactly when an Android graphics context can be made

Scenario: A clip with a soundtrack plays through the Android audio output
	Given a VideoPlayer named "player" 640 by 480 is on the panel, muted
	When the video "twocolour_raw.mkv" of the VideoPlayer group is opened in the VideoPlayer "player"
	And the VideoPlayer "player" plays
	Then the VideoPlayer "player" plays past 1.0 seconds within 8000 milliseconds
	And the VideoPlayer "player" reported no failure
