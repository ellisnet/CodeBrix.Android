Feature: What a media element shows before anything plays
	A media element is a black box with a picture in it and a row of transport controls under
	that. Long before a clip is playing, three of those parts are already a requirement: the
	built-in controls draw themselves when they are asked for and stay away when they are not,
	the poster picture stands in for a media that is not there, and an element with neither a
	source nor a poster shows an empty box rather than something left over.
	Nothing here needs the playback engine, which is why nothing here is tagged for it: these
	are requirements about the element's own chrome, and they hold on a machine that could not
	play a clip at all.
	Deliberately out of scope: hover (the panel is touch and keyboard), display scale (pinned at
	1.0), full-window mode, and the chrome's auto-hide - a timer that repaints is switched off
	for every scenario in this suite.

Scenario: An element with transport controls draws its control panel
	Given the application shows a MediaPlayerElement named "player" 800 by 450
	And the region of "ControlPanelGrid" has stopped changing
	Then the region of "ControlPanelGrid" has ink
	And the region of "PlayPauseButton" has ink
	And the region of "ProgressSlider" has ink
	And the built-in play button of "player" shows "Play"

Scenario: Turning the transport controls off draws none of them
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
	When the frame is captured
	Then "player" is 800 by 450 device pixels
	And the region of "player" is uniformly "Black"

Scenario: An element with no source shows its poster instead
	Given the application shows a MediaPlayerElement named "player" with:
		| Property                    | Value   |
		| Width                       | 800     |
		| Height                      | 450     |
		| Stretch                     | Fill    |
		| AreTransportControlsEnabled | false   |
		| PosterColour                | #2266DD |
	When the frame is captured
	Then the region of "player" is uniformly "#2266DD"
	And the region of "PosterImage" is uniformly "#2266DD"

Scenario: An element with no source and no poster shows an empty picture area
	# FOUND AND NOW FENCES: an element with nothing to show drew NOTHING at all, instead of the
	# black rectangle its own default style asks for. The style sets Background="Black" on the
	# element, but the template bound that brush only to the video presenter inside it, and the
	# presenter stays collapsed until a source arrives - so the Background of a MediaPlayerElement
	# had no effect until it was playing something, and an application that put one on a light
	# page got a hole in the page. The template's LayoutRoot now paints the element's Background
	# (Generic.xaml and mergedstyles.xaml, MEASURED 2026-09-10), and the claim below is the fence:
	# it goes red the moment that brush stops reaching the element's own root again.
	Given the application shows a MediaPlayerElement named "player" 800 by 450
	And the region of "ControlPanelGrid" has stopped changing
	Then the topmost 300 pixels of "player" are uniformly "Black"
	And the region of "ControlPanelGrid" has ink
