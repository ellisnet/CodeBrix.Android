@needs-libvlc
Feature: How a media element fits a picture into its box
	The picture a clip decodes to is almost never the shape of the box it is shown in, and the
	element's Stretch says what to do about that: Uniform keeps the picture's own proportions and
	fills the rest of the box with black, UniformToFill covers the box and lets the picture's
	edges go outside it. A clip with sound and no picture has nothing to fit at all, and the
	element shows its poster instead of an empty video surface.
	The clips are taller than they are wide on purpose: in a box that is wider than it is tall,
	the difference between the two Stretch modes is bars a person can see.
	Deliberately out of scope: hover, display scale (pinned at 1.0), Stretch None and Fill (the
	two modes with no bars to reason about), and full-window mode.

Scenario: Uniform fits a taller-than-wide clip inside the box and fills the rest with black
	Given the application shows a MediaPlayerElement named "player" with:
		| Property                    | Value   |
		| Width                       | 800     |
		| Height                      | 450     |
		| Stretch                     | Uniform |
		| AreTransportControlsEnabled | false   |
	And the clip "portrait" is given to "player"
	And the media of "player" opens within 8000 milliseconds
	Then the region of "player" shows at least 30 percent "#FF0000" within 8000 milliseconds
	And the leftmost 200 pixels of "player" are uniformly "Black"
	And the rightmost 200 pixels of "player" are uniformly "Black"
	And the 100 by 100 block at 350, 175 inside "player" is uniformly "#FF0000"

Scenario: UniformToFill covers the whole box and leaves no bars
	Given the application shows a MediaPlayerElement named "player" with:
		| Property                    | Value         |
		| Width                       | 800           |
		| Height                      | 450           |
		| Stretch                     | UniformToFill |
		| AreTransportControlsEnabled | false         |
	And the clip "portrait" is given to "player"
	And the media of "player" opens within 8000 milliseconds
	Then the region of "player" shows at least 90 percent "#FF0000" within 8000 milliseconds
	And the region of "player" is uniformly "#FF0000"
	And the region of "player" does not contain "Black"

Scenario: A clip with sound and no picture keeps the video surface hidden and shows the poster
	Given the application shows a MediaPlayerElement named "player" with:
		| Property                    | Value   |
		| Width                       | 800     |
		| Height                      | 450     |
		| Stretch                     | Fill    |
		| AreTransportControlsEnabled | false   |
		| PosterColour                | #2266DD |
	And the clip "audio-only" is given to "player"
	And the media of "player" opens within 8000 milliseconds
	When the frame is captured
	Then the media of "player" has no picture
	And the video surface of "player" is hidden
	And the region of "PosterImage" is uniformly "#2266DD"
	And the region of "player" is uniformly "#2266DD"
